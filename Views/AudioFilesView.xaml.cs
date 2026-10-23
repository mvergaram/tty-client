using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using TtyClient.Audio;
using TtyClient.TTY;

namespace TtyClient.Views;

public partial class AudioFilesView : UserControl
{
    private readonly FSKSynthesizer _synth = new();

    private Run? _lastRun;

    public AudioFilesView()
    {
        InitializeComponent();
    }

    private void AppendTranscript(char ch)
    {
        if (ch == '\r') return;

        if (ch == '\n')
        {
            // A Run's Text is verbatim — unlike TextBlock.Text, it never turns an
            // embedded '\n' into a visible line break, so this needs an explicit Inline.
            TranscriptParagraph.Inlines.Add(new LineBreak());
            _lastRun = null;
            TranscriptBox.ScrollToEnd();
            return;
        }

        if (_lastRun is null)
        {
            _lastRun = new Run();
            TranscriptParagraph.Inlines.Add(_lastRun);
        }
        _lastRun.Text += ch;
        TranscriptBox.ScrollToEnd();
    }

    private void BtnSaveWav_Click(object sender, RoutedEventArgs e)
    {
        var text = MessageInput.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "WAV file|*.wav",
            FileName = "tty_message"
        };
        if (dlg.ShowDialog() != true) return;

        var codes = BaudotEncoder.Encode(text);
        var pcm = _synth.Synthesize(codes);
        WavFileHandler.SaveToWav(pcm, dlg.FileName);
        StatusLabel.Text = $"WAV saved: {System.IO.Path.GetFileName(dlg.FileName)}";
    }

    private void BtnImportWav_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "WAV files|*.wav"
        };
        if (dlg.ShowDialog() != true) return;

        var goertzel = new GoertzelDetector();
        var sync = new BitSynchronizer();
        var decoder = new BaudotDecoder();
        goertzel.BitDetected += sync.OnBit;
        sync.CharacterReceived += decoder.OnByte;
        decoder.CharacterDecoded += ch => Dispatcher.Invoke(() => AppendTranscript(ch));

        BtnImportWav.IsEnabled = false;
        StatusLabel.Text = "Processing WAV…";

        Task.Run(() =>
        {
            try
            {
                WavFileHandler.LoadSamples(dlg.FileName, (s, c) => goertzel.ProcessSamples(s, c));
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => MessageBox.Show(
                    $"Error processing the WAV file:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error));
            }
            finally
            {
                Dispatcher.Invoke(() =>
                {
                    BtnImportWav.IsEnabled = true;
                    StatusLabel.Text = "Import completed";
                });
            }
        });
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        TranscriptParagraph.Inlines.Clear();
        _lastRun = null;
    }
}
