using TtyClient.Audio;
using TtyClient.TTY;

namespace TtyClient.Tests;

public class AudioResamplerTests
{
    private static byte[] ToFloatBytes(params float[] samples)
    {
        var bytes = new byte[samples.Length * sizeof(float)];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    [Fact]
    public void Resample_MonoAtNativeRate_ScalesFloatToInt16Range()
    {
        var resampler = new AudioResampler(inputSampleRate: 8000, inputChannels: 1);
        var input = ToFloatBytes(0.5f, -0.5f, 1.0f, -1.0f);

        var output = resampler.Resample(input, input.Length);

        Assert.Equal(new short[] { 16383, -16383, 32767, -32767 }, output);
    }

    [Fact]
    public void Resample_StereoInput_DownmixesChannelsByAveraging()
    {
        var resampler = new AudioResampler(inputSampleRate: 8000, inputChannels: 2);
        // Frame 0: L=1.0, R=0.0 -> avg 0.5. Frame 1: L=-1.0, R=1.0 -> avg 0.0.
        var input = ToFloatBytes(1.0f, 0.0f, -1.0f, 1.0f);

        var output = resampler.Resample(input, input.Length);

        Assert.Equal(new short[] { 16383, 0 }, output);
    }

    [Fact]
    public void Resample_FractionalRatio_LinearlyInterpolatesBetweenSamples()
    {
        // 12000 Hz -> 8000 Hz gives ratio 1.5, so every other output sample lands
        // exactly halfway between two input samples (frac = 0.5).
        var resampler = new AudioResampler(inputSampleRate: 12000, inputChannels: 1);
        var input = ToFloatBytes(0.0f, 1.0f, 0.0f, -1.0f, 0.0f, 1.0f);

        var output = resampler.Resample(input, input.Length);

        Assert.Equal(new short[] { 0, 16383, -32767, 16383 }, output);
    }

    [Fact]
    public void Resample_AmplitudeAboveUnity_ClampsToInt16Range()
    {
        var resampler = new AudioResampler(inputSampleRate: 8000, inputChannels: 1);
        var input = ToFloatBytes(2.0f, -2.0f);

        var output = resampler.Resample(input, input.Length);

        Assert.Equal(new short[] { short.MaxValue, short.MinValue }, output);
    }

    [Fact]
    public void Resample_EmptyInput_ReturnsEmptyArray()
    {
        var resampler = new AudioResampler(inputSampleRate: 8000, inputChannels: 1);

        var output = resampler.Resample(Array.Empty<byte>(), 0);

        Assert.Empty(output);
    }

    [Fact]
    public void Resample_StateCarriesAcrossCalls_StreamingMatchesSingleCall()
    {
        var singleCall = new AudioResampler(inputSampleRate: 12000, inputChannels: 1);
        var fullInput = ToFloatBytes(0.0f, 1.0f, 0.0f, -1.0f, 0.0f, 1.0f);
        var expected = singleCall.Resample(fullInput, fullInput.Length);

        var streamed = new AudioResampler(inputSampleRate: 12000, inputChannels: 1);
        var firstHalf = ToFloatBytes(0.0f, 1.0f, 0.0f);
        var secondHalf = ToFloatBytes(-1.0f, 0.0f, 1.0f);
        var part1 = streamed.Resample(firstHalf, firstHalf.Length);
        var part2 = streamed.Resample(secondHalf, secondHalf.Length);

        Assert.Equal(expected, part1.Concat(part2).ToArray());
    }

    [Fact]
    public void Resample_RealisticLoopbackRate_PreservesToneForGoertzelDetection()
    {
        // Typical WASAPI loopback format: 48 kHz stereo, downmixed and downsampled to the
        // decoder's 8 kHz mono. Both channels carry the same tone so the downmix stays clean.
        const int inputRate = 48000;
        const double freqHz = 1400.0;
        const int frameCount = 4800; // 100 ms
        double phaseIncrement = 2.0 * Math.PI * freqHz / inputRate;

        var stereoSamples = new float[frameCount * 2];
        double phase = 0;
        for (int i = 0; i < frameCount; i++)
        {
            float value = (float)Math.Sin(phase);
            stereoSamples[i * 2] = value;
            stereoSamples[i * 2 + 1] = value;
            phase += phaseIncrement;
        }

        var resampler = new AudioResampler(inputRate, inputChannels: 2);
        var input = ToFloatBytes(stereoSamples);
        var resampled = resampler.Resample(input, input.Length);

        var detector = new GoertzelDetector();
        var detections = new List<bool>();
        detector.BitDetected += detections.Add;
        detector.ProcessSamples(resampled, resampled.Length);

        Assert.NotEmpty(detections);
        Assert.All(detections, Assert.True); // MARK (1400 Hz)
    }
}
