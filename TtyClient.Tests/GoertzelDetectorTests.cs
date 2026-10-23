using TtyClient.TTY;

namespace TtyClient.Tests;

public class GoertzelDetectorTests
{
    private const double SampleRate = 8000.0;

    private static short[] GenerateTone(double freqHz, int count, double amplitude = 10000.0)
    {
        var samples = new short[count];
        double phase = 0;
        double increment = 2.0 * Math.PI * freqHz / SampleRate;
        for (int i = 0; i < count; i++)
        {
            samples[i] = (short)(amplitude * Math.Sin(phase));
            phase += increment;
        }
        return samples;
    }

    [Fact]
    public void ProcessSamples_PureMarkTone_DetectsMark()
    {
        var detector = new GoertzelDetector();
        var detections = new List<bool>();
        detector.BitDetected += detections.Add;

        var tone = GenerateTone(1400.0, 2000);
        detector.ProcessSamples(tone, tone.Length);

        Assert.NotEmpty(detections);
        Assert.All(detections, Assert.True);
    }

    [Fact]
    public void ProcessSamples_PureSpaceTone_DetectsSpace()
    {
        var detector = new GoertzelDetector();
        var detections = new List<bool>();
        detector.BitDetected += detections.Add;

        var tone = GenerateTone(1800.0, 2000);
        detector.ProcessSamples(tone, tone.Length);

        Assert.NotEmpty(detections);
        Assert.All(detections, Assert.False);
    }

    [Fact]
    public void ProcessSamples_Silence_ResolvesToSpace()
    {
        var detector = new GoertzelDetector();
        var detections = new List<bool>();
        detector.BitDetected += detections.Add;

        var silence = new short[200];
        detector.ProcessSamples(silence, silence.Length);

        Assert.NotEmpty(detections);
        Assert.All(detections, Assert.False);
    }

    [Fact]
    public void ProcessSamples_PartialWindowAtStreamStart_EmitsNoBitUntilWindowFills()
    {
        // AnalysisWindow is 80 samples and the hop is 44: the first hop boundary lands at
        // sample 44, while the window is still only 44 samples full, so it is skipped; the
        // window only reaches 80 (AnalysisWindow) samples in, and the next hop boundary -
        // the first one allowed to emit - lands at sample 88.
        var detector = new GoertzelDetector();
        var detections = new List<bool>();
        detector.BitDetected += detections.Add;

        var tone = GenerateTone(1400.0, 90);

        detector.ProcessSamples(tone, 80);
        Assert.Empty(detections);

        detector.ProcessSamples(tone.Skip(80).Take(8).ToArray(), 8);
        Assert.Single(detections);
    }

    [Fact]
    public void ProcessSamples_StreamedInSmallChunks_MatchesSingleCallResult()
    {
        var tone = GenerateTone(1400.0, 500);

        var singleCallDetector = new GoertzelDetector();
        var singleCallDetections = new List<bool>();
        singleCallDetector.BitDetected += singleCallDetections.Add;
        singleCallDetector.ProcessSamples(tone, tone.Length);

        var chunkedDetector = new GoertzelDetector();
        var chunkedDetections = new List<bool>();
        chunkedDetector.BitDetected += chunkedDetections.Add;
        foreach (var sample in tone)
            chunkedDetector.ProcessSamples(new[] { sample }, 1);

        Assert.Equal(singleCallDetections, chunkedDetections);
    }

    [Fact]
    public void ProcessSamples_CountGreaterThanArrayLength_ThrowsIndexOutOfRangeException()
    {
        var detector = new GoertzelDetector();

        Assert.Throws<IndexOutOfRangeException>(() => detector.ProcessSamples(new short[5], 10));
    }
}
