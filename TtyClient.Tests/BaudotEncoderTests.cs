using TtyClient.TTY;

namespace TtyClient.Tests;

public class BaudotEncoderTests
{
    [Fact]
    public void Encode_PrependsAndAppendsLtrs_ForEmptyString()
    {
        var result = BaudotEncoder.Encode("");

        Assert.Equal(new[] { BaudotTables.Ltrs, BaudotTables.Ltrs }, result);
    }

    [Fact]
    public void Encode_SimpleLetters_ProducesLtrsPrefixAndCodes()
    {
        var result = BaudotEncoder.Encode("SOS");

        Assert.Equal(new byte[] { BaudotTables.Ltrs, 5, 24, 5, BaudotTables.Ltrs }, result);
    }

    [Fact]
    public void Encode_Lowercase_MatchesUppercase()
    {
        Assert.Equal(BaudotEncoder.Encode("ABC"), BaudotEncoder.Encode("abc"));
    }

    [Fact]
    public void Encode_LetterDigitLetterDigit_MinimizesShifts()
    {
        var result = BaudotEncoder.Encode("A1B2");

        Assert.Equal(new byte[]
        {
            BaudotTables.Ltrs,
            3, BaudotTables.Figs, 23,
            BaudotTables.Ltrs, 25, BaudotTables.Figs, 19,
            BaudotTables.Ltrs,
        }, result);
    }

    [Fact]
    public void Encode_RepeatedFigures_DoesNotRepeatShift()
    {
        var result = BaudotEncoder.Encode("12");

        Assert.Equal(new byte[] { BaudotTables.Ltrs, BaudotTables.Figs, 23, 19, BaudotTables.Ltrs }, result);
    }

    [Fact]
    public void Encode_ControlCharacters_DoNotForceFigsShift()
    {
        var result = BaudotEncoder.Encode(" \r\n");

        Assert.Equal(new byte[] { BaudotTables.Ltrs, 4, 8, 2, BaudotTables.Ltrs }, result);
    }

    [Fact]
    public void Encode_UnsupportedCharacters_AreSilentlyDropped()
    {
        Assert.Equal(BaudotEncoder.Encode("AB"), BaudotEncoder.Encode("A@B"));
    }

    [Fact]
    public void Encode_OnlyUnsupportedCharacters_ProducesJustLtrsWrapper()
    {
        var result = BaudotEncoder.Encode("@@@");

        Assert.Equal(new[] { BaudotTables.Ltrs, BaudotTables.Ltrs }, result);
    }

    [Fact]
    public void Encode_Null_ThrowsNullReferenceException()
    {
        Assert.Throws<NullReferenceException>(() => BaudotEncoder.Encode(null!));
    }

    [Theory]
    [InlineData("mamá", "MAMA")]
    [InlineData("pinguino", "PINGUINO")]
    [InlineData("pingüino", "PINGUINO")]
    [InlineData("niño", "NINO")]
    [InlineData("café", "CAFE")]
    public void TryNormalize_StripsDiacriticsAndUppercases(string input, string expected)
    {
        Assert.True(BaudotEncoder.TryNormalize(input, out string normalized));
        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void TryNormalize_DropsUnsupportedCharacters()
    {
        Assert.True(BaudotEncoder.TryNormalize("A@B%C", out string normalized));
        Assert.Equal("ABC", normalized);
    }

    [Fact]
    public void TryNormalize_OnlyUnsupportedCharacters_ReturnsFalse()
    {
        Assert.False(BaudotEncoder.TryNormalize("@%", out string normalized));
        Assert.Equal(string.Empty, normalized);
    }

    [Fact]
    public void TryNormalize_EmptyString_ReturnsFalse()
    {
        Assert.False(BaudotEncoder.TryNormalize("", out string normalized));
        Assert.Equal(string.Empty, normalized);
    }
}
