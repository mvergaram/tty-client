using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using TtyClient.Audio;
using TtyClient.TTY;

namespace TtyClient;

public partial class MainWindow : Window
{
    private readonly AudioCapture _capture = new();
    private readonly GoertzelDetector _goertzel = new();
    private readonly BitSynchronizer _sync = new();
    private readonly BaudotDecoder _decoder = new();
    private readonly FSKSynthesizer _synth = new();
    private readonly VirtualMicInjector _injector = new();

    public MainWindow()
    {
        InitializeComponent();

        // Wire the TTY decode pipeline: audio → goertzel → bits → baudot → text
        _capture.SamplesAvailable += (samples, count) => _goertzel.ProcessSamples(samples, count);
        _goertzel.BitDetected += isMark => _sync.OnBit(isMark);
        _sync.CharacterReceived += code => _decoder.OnByte(code);
        _decoder.CharacterDecoded += ch => Dispatcher.Invoke(() => AppendReceived(ch));

        RefreshDevices();
        RefreshInputDevices();
    }

    private void AppendReceived(char ch)
    {
        if (ch == '\r') return;
        ReceivedText.Text += ch;
        ReceivedScroller.ScrollToBottom();
    }

    private void RefreshDevices() // sólo cable input (virtual)
    {
        var devices = _injector.GetVirtualDevices();
        DeviceCombo.Items.Clear();
        DeviceCombo.Items.Add("(ninguno / predeterminado)");
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

    private void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _sync.Reset();
            _decoder.Reset();
            var endpoint = InputDeviceCombo.SelectedItem as AudioEndpointInfo;
            _capture.Start(endpoint);

            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1)); // green
            StatusLabel.Text = "Escuchando";
            DeviceLabel.Text = _capture.DeviceName is null
                ? "—"
                : $"{_capture.DeviceName} · {(_capture.IsLoopback ? "loopback" : "micrófono")}";

            BtnStart.IsEnabled = false;
            BtnStop.IsEnabled = true;
            BtnSend.IsEnabled = true;
            InputDeviceCombo.IsEnabled = false;
            BtnRefreshInputDevices.IsEnabled = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al iniciar la captura de audio:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        _capture.Stop();
        StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8)); // red
        StatusLabel.Text = "Detenido";

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
        foreach (char c in $"TÚ: {text}")
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

    private void BtnSaveWav_Click(object sender, RoutedEventArgs e)
    {
        var text = SendInput.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Archivo WAV|*.wav",
            FileName = "tty_mensaje"
        };
        if (dlg.ShowDialog() != true) return;

        var codes = BaudotEncoder.Encode(text);
        var pcm = _synth.Synthesize(codes);
        WavFileHandler.SaveToWav(pcm, dlg.FileName);
        StatusLabel.Text = $"WAV guardado: {System.IO.Path.GetFileName(dlg.FileName)}";
    }

    private void BtnImportWav_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Archivos WAV|*.wav"
        };
        if (dlg.ShowDialog() != true) return;

        var goertzel = new GoertzelDetector();
        var sync = new BitSynchronizer();
        var decoder = new BaudotDecoder();
        goertzel.BitDetected += sync.OnBit;
        sync.CharacterReceived += decoder.OnByte;
        decoder.CharacterDecoded += ch => Dispatcher.Invoke(() => AppendReceived(ch));

        BtnImportWav.IsEnabled = false;
        StatusLabel.Text = "Procesando WAV…";

        Task.Run(() =>
        {
            try
            {
                WavFileHandler.LoadSamples(dlg.FileName, (s, c) => goertzel.ProcessSamples(s, c));
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => MessageBox.Show(
                    $"Error al procesar el archivo WAV:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error));
            }
            finally
            {
                Dispatcher.Invoke(() =>
                {
                    BtnImportWav.IsEnabled = true;
                    StatusLabel.Text = "Importación completada";
                });
            }
        });
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

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _capture.Dispose();
        _injector.Dispose();
    }
}
