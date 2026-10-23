using TtyClient.TTY;

namespace TtyClient.Tests;

public class BaudotDecoderTests
{
    [Fact]
    public void OnByte_LtrsAndFigsShift_SwitchStateWithoutEmittingCharacter()
    {
        var decoder = new BaudotDecoder();
        var emitted = new List<char>();
        decoder.CharacterDecoded += ch => emitted.Add(ch);

        decoder.OnByte(BaudotTables.Ltrs);
        decoder.OnByte(BaudotTables.Figs);

        Assert.Empty(emitted);
    }

    [Fact]
    public void OnByte_SameCodeDecodesDifferently_DependingOnShift()
    {
        var decoder = new BaudotDecoder();
        var emitted = new List<char>();
        decoder.CharacterDecoded += ch => emitted.Add(ch);

        decoder.OnByte(1); // LTRS (default) -> 'E'
        decoder.OnByte(BaudotTables.Figs);
        decoder.OnByte(1); // FIGS -> '3'

        Assert.Equal(new[] { 'E', '3' }, emitted);
    }

    [Fact]
    public void OnByte_ControlCharacters_DecodeIdenticallyInBothShifts()
    {
        var decoder = new BaudotDecoder();
        var emitted = new List<char>();
        decoder.CharacterDecoded += ch => emitted.Add(ch);

        decoder.OnByte(BaudotTables.LineFeed);
        decoder.OnByte(BaudotTables.Space);
        decoder.OnByte(BaudotTables.CarriageReturn);
        decoder.OnByte(BaudotTables.Figs);
        decoder.OnByte(BaudotTables.LineFeed);
        decoder.OnByte(BaudotTables.Space);
        decoder.OnByte(BaudotTables.CarriageReturn);

        Assert.Equal(new[] { '\n', ' ', '\r', '\n', ' ', '\r' }, emitted);
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)9)]
    [InlineData((byte)11)]
    [InlineData((byte)13)]
    public void OnByte_UnassignedFigsCode_EmitsNothing(byte code)
    {
        var decoder = new BaudotDecoder();
        var emitted = new List<char>();
        decoder.CharacterDecoded += ch => emitted.Add(ch);

        decoder.OnByte(BaudotTables.Figs);
        decoder.OnByte(code);

        Assert.Empty(emitted);
    }

    [Fact]
    public void OnByte_BlankCode_EmitsNothingInEitherShift()
    {
        var decoder = new BaudotDecoder();
        var emitted = new List<char>();
        decoder.CharacterDecoded += ch => emitted.Add(ch);

        decoder.OnByte(0); // LTRS shift (default), blank code

        Assert.Empty(emitted);
    }

    [Fact]
    public void Reset_ReturnsToLettersShift()
    {
        var decoder = new BaudotDecoder();
        var emitted = new List<char>();
        decoder.CharacterDecoded += ch => emitted.Add(ch);

        decoder.OnByte(BaudotTables.Figs);
        decoder.OnByte(1); // '3'
        decoder.Reset();
        decoder.OnByte(1); // back to LTRS -> 'E'

        Assert.Equal(new[] { '3', 'E' }, emitted);
    }

    [Theory]
    [InlineData((byte)32)]
    [InlineData((byte)255)]
    public void OnByte_CodeOutOfRange_ThrowsIndexOutOfRangeException(byte code)
    {
        var decoder = new BaudotDecoder();

        Assert.Throws<IndexOutOfRangeException>(() => decoder.OnByte(code));
    }
}
