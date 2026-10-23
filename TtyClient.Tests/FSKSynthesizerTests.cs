using System.Text;
using TtyClient.TTY;

namespace TtyClient.Tests;

public class FSKSynthesizerTests
{
    // Samples per bit at 45.45 baud / 8000 Hz; must match FSKSynthesizer's own (private) value.
    private const int SamplesPerBit = 176;

    private static short[] ToSamples(byte[] pcm)
    {
        var samples = new short[pcm.Length / 2];
        for (int i = 0; i < samples.Length; i++)
            samples[i] = (short)(pcm[i * 2] | (pcm[i * 2 + 1] << 8));
        return samples;
    }

    private static List<bool> Decode(short[] samples)
    {
        var detector = new GoertzelDetector();
        var detections = new List<bool>();
        detector.BitDetected += detections.Add;
        detector.ProcessSamples(samples, samples.Length);
        return detections;
    }

    [Fact]
    public void Synthesize_EmptyList_ProducesOnly200msMarkIdle()
    {
        var synth = new FSKSynthesizer();

        var bytes = synth.Synthesize(new List<byte>());

        Assert.Equal(3200, bytes.Length); // 8000 Hz * 0.2 s * 2 bytes/sample
        var detections = Decode(ToSamples(bytes));
        Assert.NotEmpty(detections);
        Assert.All(detections, Assert.True); // MARK
    }

    [Fact]
    public void Synthesize_SingleCharacter_StartBitIsSpaceAndStopBitIsMark()
    {
        const byte code = 5; // 'S'
        var synth = new FSKSynthesizer();

        var bytes = synth.Synthesize(new List<byte> { code });
        var samples = ToSamples(bytes);

        var startSegment = samples.Take(SamplesPerBit).ToArray();
        Assert.All(Decode(startSegment), Assert.False); // SPACE

        var stopOffset = SamplesPerBit + 5 * SamplesPerBit;
        var stopLength = (int)(SamplesPerBit * 1.5);
        var stopSegment = samples.Skip(stopOffset).Take(stopLength).ToArray();
        Assert.All(Decode(stopSegment), Assert.True); // MARK
    }

    [Theory]
    [InlineData("SOS")]
    [InlineData("HELLO WORLD")]
    [InlineData("TEST 123")]
    [InlineData("")]
    public void Synthesize_RoundTrip_DecodesBackToOriginalText(string text)
    {
        var codes = BaudotEncoder.Encode(text);
        var synth = new FSKSynthesizer();
        var samples = ToSamples(synth.Synthesize(codes));

        var goertzel = new GoertzelDetector();
        var sync = new BitSynchronizer();
        var decoder = new BaudotDecoder();
        var sb = new StringBuilder();
        goertzel.BitDetected += sync.OnBit;
        sync.CharacterReceived += decoder.OnByte;
        decoder.CharacterDecoded += ch => sb.Append(ch);

        goertzel.ProcessSamples(samples, samples.Length);

        Assert.Equal(text.ToUpperInvariant(), sb.ToString());
    }

    [Fact]
    public void Synthesize_CodeWithBitsAboveFive_IsTruncatedToLowFiveBits()
    {
        var withHighBits = new FSKSynthesizer().Synthesize(new List<byte> { 0b1110_0011 });
        var lowFiveBitsOnly = new FSKSynthesizer().Synthesize(new List<byte> { 0b0000_0011 });

        Assert.Equal(lowFiveBitsOnly, withHighBits);
    }

    [Fact]
    public void Synthesize_Null_ThrowsNullReferenceException()
    {
        var synth = new FSKSynthesizer();

        Assert.Throws<NullReferenceException>(() => synth.Synthesize(null!));
    }
}
