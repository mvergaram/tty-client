using TtyClient;
using TtyClient.TTY;

namespace TtyClient.Tests;

public class AppSettingsTests
{
    private static string TempSettingsPath() =>
        Path.Combine(Path.GetTempPath(), $"tty-settings-{Guid.NewGuid()}.json");

    [Fact]
    public void Save_PersistsAndReloadsIdleHopsThreshold()
    {
        var path = TempSettingsPath();
        try
        {
            var first = new AppSettings(path);
            first.Save(321, true);

            var second = new AppSettings(path);

            Assert.Equal(321, second.IdleHopsThreshold);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Save_NonPositiveValue_ThrowsArgumentOutOfRangeException(int invalidValue)
    {
        var path = TempSettingsPath();
        try
        {
            var settings = new AppSettings(path);

            Assert.Throws<ArgumentOutOfRangeException>(() => settings.Save(invalidValue, true));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Save_RaisesChangedEvent()
    {
        var path = TempSettingsPath();
        try
        {
            var settings = new AppSettings(path);
            int changedCount = 0;
            settings.Changed += () => changedCount++;

            settings.Save(50, true);

            Assert.Equal(1, changedCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Save_PersistsAndReloadsAiImprovedTextEnabled()
    {
        var path = TempSettingsPath();
        try
        {
            var first = new AppSettings(path);
            first.Save(321, false);

            var second = new AppSettings(path);

            Assert.False(second.AiImprovedTextEnabled);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Constructor_MissingFile_DefaultsAiImprovedTextEnabledToTrue()
    {
        var path = TempSettingsPath(); // never written

        var settings = new AppSettings(path);

        Assert.True(settings.AiImprovedTextEnabled);
    }

    [Fact]
    public void Constructor_FileFromOlderVersionMissingAiField_DefaultsToTrue()
    {
        var path = TempSettingsPath();
        try
        {
            File.WriteAllText(path, """{"IdleHopsThreshold":321}""");

            var settings = new AppSettings(path);

            Assert.Equal(321, settings.IdleHopsThreshold);
            Assert.True(settings.AiImprovedTextEnabled);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Constructor_MissingFile_UsesDefaultIdleHopsThreshold()
    {
        var path = TempSettingsPath(); // never written

        var settings = new AppSettings(path);

        Assert.Equal(GoertzelDetector.DefaultIdleHopsThreshold, settings.IdleHopsThreshold);
    }

    [Fact]
    public void Constructor_CorruptFile_FallsBackToDefault()
    {
        var path = TempSettingsPath();
        try
        {
            File.WriteAllText(path, "{ not valid json");

            var settings = new AppSettings(path);

            Assert.Equal(GoertzelDetector.DefaultIdleHopsThreshold, settings.IdleHopsThreshold);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
