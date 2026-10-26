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

    // Contract change: silence used to resolve to a deterministic SPACE decision on every hop
    // (both Goertzel magnitudes are ~0, and 0 > 0 is false). That fabricated a byte stream out
    // of pure noise, which BitSynchronizer would happily frame into characters - including,
    // given enough real-world line noise, an occasional spurious FIGS/LTRS that corrupts
    // BaudotDecoder's shift state. Below NoiseFloor there's no real carrier, so no bit decision
    // is trustworthy; ProcessSamples now emits nothing instead of guessing.
    [Fact]
    public void ProcessSamples_BriefSilence_EmitsNoBitDetections()
    {
        var detector = new GoertzelDetector();
        var detections = new List<bool>();
        detector.BitDetected += detections.Add;

        var silence = new short[200];
        detector.ProcessSamples(silence, silence.Length);

        Assert.Empty(detections);
    }

    [Fact]
    public void ProcessSamples_SustainedSilenceAfterTone_RaisesLineIdle()
    {
        var detector = new GoertzelDetector();
        int idleCount = 0;
        detector.LineIdle += () => idleCount++;

        var tone = GenerateTone(1400.0, 2000);
        detector.ProcessSamples(tone, tone.Length);

        // Well over a second of silence: comfortably past the ~1s idle threshold.
        var silence = new short[16000];
        detector.ProcessSamples(silence, silence.Length);

        Assert.Equal(1, idleCount);
    }

    [Fact]
    public void ProcessSamples_BriefGapShorterThanThreshold_DoesNotRaiseLineIdle()
    {
        var detector = new GoertzelDetector();
        int idleCount = 0;
        detector.LineIdle += () => idleCount++;

        var tone = GenerateTone(1400.0, 2000);
        detector.ProcessSamples(tone, tone.Length);

        var briefGap = new short[2000]; // well under the ~1s idle threshold
        detector.ProcessSamples(briefGap, briefGap.Length);

        Assert.Equal(0, idleCount);
    }

    [Fact]
    public void ProcessSamples_ContinuousTone_NeverRaisesLineIdle()
    {
        var detector = new GoertzelDetector();
        int idleCount = 0;
        detector.LineIdle += () => idleCount++;

        var tone = GenerateTone(1400.0, 20000); // well over a second of continuous tone
        detector.ProcessSamples(tone, tone.Length);

        Assert.Equal(0, idleCount);
    }

    [Fact]
    public void ProcessSamples_CustomIdleHopsThreshold_RaisesLineIdleAtConfiguredCount()
    {
        const int customThreshold = 20; // far below the ~182-hop (~1s) default
        var detector = new GoertzelDetector(customThreshold);
        int idleCount = 0;
        detector.LineIdle += () => idleCount++;

        var tone = GenerateTone(1400.0, 2000);
        detector.ProcessSamples(tone, tone.Length);

        // Plenty of silence (hop = 44 samples, so ~113 hops available - comfortably more than
        // customThreshold once the handful of tone/silence straddling hops right after the
        // tone are discounted). A default-threshold detector would need ~182 hops (16000+
        // samples) to go idle, so firing this early proves the configured threshold - not the
        // default - is what actually triggered LineIdle.
        var silence = new short[5000];
        detector.ProcessSamples(silence, silence.Length);

        Assert.Equal(1, idleCount);
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
