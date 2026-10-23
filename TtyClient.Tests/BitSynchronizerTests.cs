using TtyClient.TTY;

namespace TtyClient.Tests;

public class BitSynchronizerTests
{
    // Builds the quarter-bit tick sequence OnBit expects for one full frame: 1 START bit
    // (SPACE) + 5 data bits (LSB first) + 1.5 STOP bits (MARK), oversampled by 4 and sampled
    // at each bit's center tick (index 2 of every 4-tick group). Only 3 ticks are emitted for
    // the stop bit because BitSynchronizer returns to Idle as soon as it samples the stop
    // bit's center, without waiting out its remaining half.
    private static IEnumerable<bool> BuildFrameTicks(byte code, bool cleanStop = true)
    {
        for (int i = 0; i < 4; i++) yield return false; // start bit (SPACE)

        for (int bit = 0; bit < 5; bit++)
        {
            bool isMark = ((code >> bit) & 1) == 1;
            for (int i = 0; i < 4; i++) yield return isMark;
        }

        for (int i = 0; i < 3; i++) yield return cleanStop; // stop bit center
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)1)]
    [InlineData((byte)5)]
    [InlineData((byte)17)]
    [InlineData((byte)31)]
    public void OnBit_FullValidFrame_EmitsCorrectByte(byte code)
    {
        var sync = new BitSynchronizer();
        byte? received = null;
        sync.CharacterReceived += b => received = b;

        foreach (var tick in BuildFrameTicks(code))
            sync.OnBit(tick);

        Assert.Equal(code, received);
    }

    [Fact]
    public void OnBit_GlitchNotSustainedAtStartBitCenter_IsIgnored()
    {
        var sync = new BitSynchronizer();
        var received = new List<byte>();
        sync.CharacterReceived += b => received.Add(b);

        sync.OnBit(true);  // idle MARK
        sync.OnBit(true);  // idle MARK
        sync.OnBit(false); // falling edge candidate -> tick 0 of a new frame
        sync.OnBit(true);  // tick 1
        sync.OnBit(true);  // tick 2 (start-bit center): MARK here means false trigger, abort

        foreach (var tick in BuildFrameTicks(5))
            sync.OnBit(tick);

        Assert.Equal(new byte[] { 5 }, received);
    }

    [Theory]
    [InlineData((byte)3)]
    [InlineData((byte)30)]
    public void OnBit_CorruptStopBit_StillEmitsCharacter(byte code)
    {
        var sync = new BitSynchronizer();
        byte? received = null;
        sync.CharacterReceived += b => received = b;

        foreach (var tick in BuildFrameTicks(code, cleanStop: false))
            sync.OnBit(tick);

        Assert.Equal(code, received);
    }

    [Fact]
    public void OnBit_BackToBackFramesWithoutIdleGap_BothDecodeCorrectly()
    {
        var sync = new BitSynchronizer();
        var received = new List<byte>();
        sync.CharacterReceived += b => received.Add(b);

        foreach (var tick in BuildFrameTicks(5))
            sync.OnBit(tick);
        foreach (var tick in BuildFrameTicks(24))
            sync.OnBit(tick);

        Assert.Equal(new byte[] { 5, 24 }, received);
    }

    [Fact]
    public void Reset_MidFrame_DiscardsPartialFrameWithoutEmitting()
    {
        var sync = new BitSynchronizer();
        var received = new List<byte>();
        sync.CharacterReceived += b => received.Add(b);

        // Partial frame: start bit plus the first two data-bit groups, never completed.
        foreach (var tick in BuildFrameTicks(5).Take(12))
            sync.OnBit(tick);

        sync.Reset();

        foreach (var tick in BuildFrameTicks(17))
            sync.OnBit(tick);

        Assert.Equal(new byte[] { 17 }, received);
    }
}
