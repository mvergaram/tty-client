using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TtyClient.Audio;
using TtyClient.TTY;

namespace TtyClient.Views;

public partial class AudioFilesView : UserControl
{
    private readonly FSKSynthesizer _synth = new();

    public AudioFilesView()
    {
        InitializeComponent();
    }

    private void AppendTranscript(char ch)
    {
        if (ch == '\r') return;
        TranscriptText.Text += ch;
        TranscriptScroller.ScrollToBottom();
    }

    private void BtnSaveWav_Click(object sender, RoutedEventArgs e)
    {
        var text = MessageInput.Text.Trim();
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
        decoder.CharacterDecoded += ch => Dispatcher.Invoke(() => AppendTranscript(ch));

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
        TranscriptText.Text = "";
    }
}
