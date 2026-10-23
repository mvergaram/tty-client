using System.Text;
using NAudio.Wave;
using TtyClient.Audio;
using TtyClient.TTY;

namespace TtyClient.Tests;

public class WavFileHandlerTests
{
    [Fact]
    public void SaveToWav_ThenLoadSamples_RoundTripsThroughFullDecodePipeline()
    {
        const string text = "SOS SAVE SAVES";
        var codes = BaudotEncoder.Encode(text);
        var pcm = new FSKSynthesizer().Synthesize(codes);
        var path = Path.Combine(Path.GetTempPath(), $"tty-test-{Guid.NewGuid()}.wav");

        try
        {
            WavFileHandler.SaveToWav(pcm, path);

            var goertzel = new GoertzelDetector();
            var sync = new BitSynchronizer();
            var decoder = new BaudotDecoder();
            var sb = new StringBuilder();
            goertzel.BitDetected += sync.OnBit;
            sync.CharacterReceived += decoder.OnByte;
            decoder.CharacterDecoded += ch => sb.Append(ch);

            WavFileHandler.LoadSamples(path, (samples, count) => goertzel.ProcessSamples(samples, count));

            Assert.Equal(text, sb.ToString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SaveToWav_WritesFixedTtyWaveFormat()
    {
        var pcm = new byte[] { 0, 0, 0, 0 }; // two silent 16-bit samples
        var path = Path.Combine(Path.GetTempPath(), $"tty-test-{Guid.NewGuid()}.wav");

        try
        {
            WavFileHandler.SaveToWav(pcm, path);

            using var reader = new WaveFileReader(path);
            Assert.Equal(8000, reader.WaveFormat.SampleRate);
            Assert.Equal(16, reader.WaveFormat.BitsPerSample);
            Assert.Equal(1, reader.WaveFormat.Channels);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
