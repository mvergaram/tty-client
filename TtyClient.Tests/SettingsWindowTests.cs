namespace TtyClient.Tests;

public class SettingsWindowTests
{
    [Theory]
    [InlineData("182", true, 182)]
    [InlineData("0", false, 0)]
    [InlineData("", false, 0)]
    [InlineData("abc", false, 0)]
    [InlineData("99999999999", false, 0)]
    public void TryParsePositiveHops_VariousInputs_ReturnsExpectedResult(string text, bool expectedSuccess, int expectedHops)
    {
        var success = SettingsWindow.TryParsePositiveHops(text, out var hops);

        Assert.Equal(expectedSuccess, success);
        Assert.Equal(expectedHops, hops);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("")]
    [InlineData("abc")]
    public void FormatSecondsLabel_InvalidOrNonPositiveInput_ReturnsPlaceholder(string hopsText)
    {
        var label = SettingsWindow.FormatSecondsLabel(hopsText);

        Assert.Equal("~ ? sec.", label);
    }

    [Fact]
    public void FormatSecondsLabel_ValidHops_ReturnsFormattedSeconds()
    {
        var label = SettingsWindow.FormatSecondsLabel("182");

        Assert.Equal("~ 1.0 sec.", label);
    }

    [Theory]
    [InlineData("123", true)]
    [InlineData("", false)]
    [InlineData("1.5", false)]
    [InlineData("-1", false)]
    [InlineData("1a", false)]
    public void DigitsOnly_VariousInputs_MatchesOnlyPlainDigits(string text, bool expectedMatch)
    {
        var isMatch = SettingsWindow.DigitsOnly.IsMatch(text);

        Assert.Equal(expectedMatch, isMatch);
    }
}
