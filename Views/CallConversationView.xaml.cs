using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TtyClient.Audio;
using TtyClient.TTY;

namespace TtyClient.Views;

public partial class CallConversationView : UserControl
{
    private readonly AudioCapture _capture = new();
    private readonly GoertzelDetector _goertzel = new();
    private readonly BitSynchronizer _sync = new();
    private readonly BaudotDecoder _decoder = new();
    private readonly FSKSynthesizer _synth = new();
    private readonly VirtualMicInjector _injector = new();

    // Naming is intentionally inverted relative to the VB-CABLE device's own name:
    // VB-CABLE's "Output" device is this app's audio INPUT (we read from it), and
    // VB-CABLE's "Input" device is this app's audio OUTPUT (we write to it).
    // Prefix-only match: VB-CABLE appends a letter before the closing paren for
    // multi-instance installs, e.g. "CABLE Output (VB-Audio Virtual Cable A)".
    private const string VbCableInputDeviceNamePrefix = "CABLE Output (VB-Audio Virtual Cable";
    private const string VbCableOutputDeviceNamePrefix = "CABLE Input (VB-Audio Virtual Cable";

    private const string VbCableMissingWarning =
        "For call conversation feature best results, install VB-CABLE Virtual Audio Device application from https://vb-audio.com/Cable/";

    public CallConversationView()
    {
        InitializeComponent();

        // Wire the TTY decode pipeline: audio → goertzel → bits → baudot → text
        _capture.SamplesAvailable += (samples, count) => _goertzel.ProcessSamples(samples, count);
        _goertzel.BitDetected += isMark => _sync.OnBit(isMark);
        _sync.CharacterReceived += code => _decoder.OnByte(code);
        _decoder.CharacterDecoded += ch => Dispatcher.Invoke(() => AppendReceived(ch));

        RefreshDevices();
        RefreshInputDevices();
        EnsureVirtualCableDefaults();
    }

    private void AppendReceived(char ch)
    {
        if (ch == '\r') return;
        ReceivedText.Text += ch;
        ReceivedScroller.ScrollToBottom();
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

    /// <summary>
    /// Checks whether the VB-CABLE virtual audio devices are installed and, for whichever
    /// one(s) are found, selects them as the default choice in their combo box. Runs once
    /// at startup (not on manual refresh) so it never overrides a later user selection.
    /// Shows a single warning if either required device is missing; the app keeps running.
    /// </summary>
    private void EnsureVirtualCableDefaults()
    {
        var inputDevice = InputDeviceCombo.Items.OfType<AudioEndpointInfo>()
            .FirstOrDefault(d => d.FriendlyName.StartsWith(VbCableInputDeviceNamePrefix, StringComparison.OrdinalIgnoreCase));
        if (inputDevice is not null)
            InputDeviceCombo.SelectedItem = inputDevice;

        var outputDevice = DeviceCombo.Items.OfType<string>()
            .FirstOrDefault(d => d.StartsWith(VbCableOutputDeviceNamePrefix, StringComparison.OrdinalIgnoreCase));
        if (outputDevice is not null)
            DeviceCombo.SelectedItem = outputDevice;

        if (inputDevice is null || outputDevice is null)
        {
            MessageBox.Show(VbCableMissingWarning, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _sync.Reset();
            _decoder.Reset();
            var endpoint = InputDeviceCombo.SelectedItem as AudioEndpointInfo;
            _capture.Start(endpoint);

            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1)); // green
            StatusLabel.Text = "Listening";
            DeviceLabel.Text = _capture.DeviceName is null
                ? "—"
                : $"{_capture.DeviceName} · {(_capture.IsLoopback ? "loopback" : "microphone")}";

            BtnStart.IsEnabled = false;
            BtnStop.IsEnabled = true;
            BtnSend.IsEnabled = true;
            InputDeviceCombo.IsEnabled = false;
            BtnRefreshInputDevices.IsEnabled = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error starting audio capture:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        _capture.Stop();
        StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8)); // red
        StatusLabel.Text = "Stopped";

        BtnStart.IsEnabled = true;
        BtnStop.IsEnabled = false;
        BtnSend.IsEnabled = false;
        InputDeviceCombo.IsEnabled = true;
        BtnRefreshInputDevices.IsEnabled = true;
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
        AppendReceived('\n');
        foreach (char c in $"YOU: {text}")
            AppendReceived(c);
        AppendReceived('\n');

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
        ReceivedText.Text = "";
    }

    private void BtnRefreshDevices_Click(object sender, RoutedEventArgs e)
    {
        RefreshDevices();
    }

    private void BtnRefreshInputDevices_Click(object sender, RoutedEventArgs e)
    {
        RefreshInputDevices();
    }

    /// <summary>
    /// Stops and disposes the live audio resources on a background thread, so
    /// MainWindow can await this method before closing the window without blocking
    /// the UI thread (WASAPI StopRecording/Dispose can take a while to respond).
    /// </summary>
    public async Task ShutdownAsync()
    {
        await Task.Run(() =>
        {
            _capture.Dispose();
            _injector.Dispose();
        });
    }
}
