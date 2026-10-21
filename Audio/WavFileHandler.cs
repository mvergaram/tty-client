using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace TtyClient.Audio;

internal static class WavFileHandler
{
    private static readonly WaveFormat TtyFormat = new WaveFormat(8000, 16, 1);

    public static void SaveToWav(byte[] pcmBytes, string filePath)
    {
        using var writer = new WaveFileWriter(filePath, TtyFormat);
        writer.Write(pcmBytes, 0, pcmBytes.Length);
    }

    public static void LoadSamples(string filePath, Action<short[], int> onSamples)
    {
        using var reader = new AudioFileReader(filePath);
        ISampleProvider mono = reader.WaveFormat.Channels == 1
            ? (ISampleProvider)reader
            : new StereoToMonoSampleProvider(reader);
        var resampled = new WdlResamplingSampleProvider(mono, 8000);
        var wave16 = new SampleToWaveProvider16(resampled);

        var buf = new byte[320];
        var shorts = new short[160];
        int read;
        while ((read = wave16.Read(buf, 0, buf.Length)) > 0)
        {
            Buffer.BlockCopy(buf, 0, shorts, 0, read);
            onSamples(shorts, read / 2);
        }
    }
}
