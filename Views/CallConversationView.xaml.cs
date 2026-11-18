using System;
using System.ClientModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TtyClient.AI;
using TtyClient.Audio;
using TtyClient.TTY;

namespace TtyClient.Views;

public partial class CallConversationView : UserControl
{
    private static readonly Brush ReceivedBrush = FreezeBrush(0xA6, 0xE3, 0xA1); // matches the "RECEIVED" header
    private static readonly Brush SentBrush = FreezeBrush(0x89, 0xB4, 0xFA); // matches the "SEND" header
    private static readonly Brush AiBrush = FreezeBrush(0xCB, 0xA6, 0xF7); // AI-improved text
    private static readonly Brush AiErrorBrush = FreezeBrush(0xF3, 0x8B, 0xA8); // AI call failed

    private static Brush FreezeBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private readonly AudioCapture _capture = new();
    private GoertzelDetector _goertzel = new();
    private readonly BitSynchronizer _sync = new();
    private readonly BaudotDecoder _decoder = new();
    private readonly FSKSynthesizer _synth = new();
    private readonly VirtualMicInjector _injector = new();

    private PortkeyChatClient _aiClient = BuildAiClient();
    private readonly DispatcherTimer _aiDebounceTimer;
    private readonly StringBuilder _pendingReceivedMessage = new();
    private CancellationTokenSource? _callSessionCts;
    private CallTranscriptLogger? _transcriptLogger;

    private Run? _lastRun;

    public CallConversationView()
    {
        InitializeComponent();

        // Wire the TTY decode pipeline: audio → goertzel → bits → baudot → text. _goertzel
        // itself is (re)created in StartCapture() so it always picks up the current
        // App.Settings.IdleHopsThreshold; capturing it as a field (not a local) here means this
        // closure keeps reading whichever instance is current.
        _capture.SamplesAvailable += (samples, count) => _goertzel.ProcessSamples(samples, count);
        _sync.CharacterReceived += code => _decoder.OnByte(code);
        _decoder.CharacterDecoded += ch => Dispatcher.Invoke(() => AppendReceived(ch));

        _aiDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _aiDebounceTimer.Tick += AiDebounceTimer_Tick;

        UpdateAiBadgeVisibility();
        App.Settings.Changed += UpdateAiBadgeVisibility;
        App.Settings.Changed += () => _aiClient = BuildAiClient();

        RefreshDevices();
        RefreshInputDevices();
    }

    private static PortkeyChatClient BuildAiClient() => new(
        App.Settings.AiUrl, App.Settings.AiApiKey, App.Settings.AiProvider, App.Settings.AiModel,
        AiDefaults.SystemPrompt, App.Settings.AiMaxOutputTokens);

    private void UpdateAiBadgeVisibility() =>
        AiBadge.Visibility = App.Settings.AiImprovedTextEnabled ? Visibility.Visible : Visibility.Collapsed;

    private void AppendChar(char ch, Brush brush)
    {
        if (ch == '\r') return;

        if (ch == '\n')
        {
            // A Run's Text is verbatim — unlike TextBlock.Text, it never turns an
            // embedded '\n' into a visible line break, so this needs an explicit Inline.
            ReceivedParagraph.Inlines.Add(new LineBreak());
            _lastRun = null;
            ReceivedBox.ScrollToEnd();
            return;
        }

        if (_lastRun is null || _lastRun.Foreground != brush)
        {
            _lastRun = new Run { Foreground = brush };
            ReceivedParagraph.Inlines.Add(_lastRun);
        }
        _lastRun.Text += ch;
        ReceivedBox.ScrollToEnd();
    }

    private void AppendLine(string text, Brush brush)
    {
        AppendChar('\n', brush);
        foreach (char c in text) AppendChar(c, brush);
        AppendChar('\n', brush);
        AppendChar('\n', brush);
    }

    private void AppendReceived(char ch, bool isSent = false)
    {
        AppendChar(ch, isSent ? SentBrush : ReceivedBrush);

        if (!isSent)
        {
            if (ch != '\n' && ch != '\r') _pendingReceivedMessage.Append(ch);
            _aiDebounceTimer.Stop();
            _aiDebounceTimer.Start();
        }
    }

    private void AiDebounceTimer_Tick(object? sender, EventArgs e)
    {
        _aiDebounceTimer.Stop();
        if (_pendingReceivedMessage.Length == 0) return;

        string message = _pendingReceivedMessage.ToString();
        _pendingReceivedMessage.Clear();
        _transcriptLogger?.LogReceived(message);

        if (App.Settings.AiImprovedTextEnabled)
            _ = ImproveAndAppendAsync(message, _transcriptLogger, _callSessionCts?.Token ?? CancellationToken.None);
    }

    private async Task ImproveAndAppendAsync(string originalText, CallTranscriptLogger? logger, CancellationToken sessionToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(sessionToken);
        cts.CancelAfter(TimeSpan.FromSeconds(15));

        string? improved = null;
        string? error = null;
        try
        {
            improved = await _aiClient.ImproveTextAsync(originalText, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return; // call stopped or app closing mid-request; nothing to show, nothing to log
        }
        catch (ClientResultException ex)
        {
            error = $"status {ex.Status}: {ex.Message}";
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }

        if (improved is not null)
        {
            Dispatcher.Invoke(() => AppendLine($"AI: {improved}", AiBrush));
            logger?.LogImproved(originalText, improved);
        }
        else
        {
            error ??= "unknown error";
            Dispatcher.Invoke(() => AppendLine($"AI: (error) {error}", AiErrorBrush));
            logger?.LogError(originalText, error);
        }
    }

    private void RefreshDevices() // virtual cable (output) devices only
    {
        var devices = _injector.GetVirtualDevices();
        DeviceCombo.Items.Clear();
        DeviceCombo.Items.Add("(none / default)");
        foreach (var d in devices)
            DeviceCombo.Items.Add(d);
        DeviceCombo.SelectedIndex = 0;
    }

    private void RefreshInputDevices()
    {
        string? previousId = (InputDeviceCombo.SelectedItem as AudioEndpointInfo)?.Id;

        var devices = _capture.GetInputDevices();
        InputDeviceCombo.Items.Clear();
        foreach (var d in devices)
            InputDeviceCombo.Items.Add(d);

        int idx = previousId is null ? -1 : devices.FindIndex(d => d.Id == previousId);
        InputDeviceCombo.SelectedIndex = InputDeviceCombo.Items.Count == 0 ? -1 : (idx >= 0 ? idx : 0);
    }

    private void BtnListen_Click(object sender, RoutedEventArgs e)
    {
        if (_capture.IsCapturing) StopCapture();
        else StartCapture();
    }

    private void StartCapture()
    {
        try
        {
            _sync.Reset();
            _decoder.Reset();

            _goertzel = new GoertzelDetector(App.Settings.IdleHopsThreshold);
            _goertzel.BitDetected += isMark => _sync.OnBit(isMark);
            _goertzel.LineIdle += () => { _sync.Reset(); _decoder.Reset(); };

            _pendingReceivedMessage.Clear();
            _aiDebounceTimer.Stop();
            _callSessionCts?.Dispose();
            _callSessionCts = new CancellationTokenSource();
            _transcriptLogger?.Dispose();
            try
            {
                _transcriptLogger = CallTranscriptLogger.Open(DateTime.UtcNow);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _transcriptLogger = null; // don't block "Start listening" over a logging-only failure
            }

            var endpoint = InputDeviceCombo.SelectedItem as AudioEndpointInfo;
            _capture.Start(endpoint);

            BtnListen.Content = "Stop";
            BtnListen.Style = (Style)FindResource("StopButton");
            BtnSend.IsEnabled = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error starting audio capture:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void StopCapture()
    {
        _capture.Stop();

        _aiDebounceTimer.Stop();
        if (_pendingReceivedMessage.Length > 0)
        {
            _transcriptLogger?.LogReceived(_pendingReceivedMessage.ToString());
            _pendingReceivedMessage.Clear();
        }
        _callSessionCts?.Cancel();
        _transcriptLogger?.Dispose();
        _transcriptLogger = null;

        BtnListen.Content = "Start listening";
        BtnListen.Style = (Style)FindResource("SendButton");
        BtnSend.IsEnabled = false;
    }

    private void InputDeviceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Only hot-swap if we're already listening; otherwise the user still has to
        // press "Start listening" explicitly, same as picking a device for the first time.
        if (_capture.IsCapturing) StartCapture();
    }

    private void BtnSend_Click(object sender, RoutedEventArgs e) => SendText();

    private void SendInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            SendText();
        }
    }

    private void SendText()
    {
        string text = SendInput.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        // Append to received area with "GA" (Go Ahead) convention
        AppendReceived('\n', isSent: true);
        foreach (char c in $"YOU: {text}")
            AppendReceived(c, isSent: true);
        AppendReceived('\n', isSent: true);
        AppendReceived('\n', isSent: true);
        _transcriptLogger?.LogSent(text);

        var codes = BaudotEncoder.Encode(text);
        byte[] audio = _synth.Synthesize(codes);

        string? device = DeviceCombo.SelectedIndex > 0
            ? DeviceCombo.SelectedItem?.ToString()
            : null;

        _injector.Play(audio, device);
        SendInput.Clear();
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        ReceivedParagraph.Inlines.Clear();
        _lastRun = null;
    }

    /// <summary>
    /// Stops and disposes the live audio resources on a background thread, so
    /// MainWindow can await this method before closing the window without blocking
    /// the UI thread (WASAPI StopRecording/Dispose can take a while to respond).
    /// </summary>
    public async Task ShutdownAsync()
    {
        _aiDebounceTimer.Stop();
        _callSessionCts?.Cancel();
        _transcriptLogger?.Dispose();

        await Task.Run(() =>
        {
            _capture.Dispose();
            _injector.Dispose();
        });
    }
}
