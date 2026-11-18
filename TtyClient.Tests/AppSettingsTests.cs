using TtyClient;
using TtyClient.AI;
using TtyClient.TTY;

namespace TtyClient.Tests;

public class AppSettingsTests
{
    private static string TempSettingsPath() =>
        Path.Combine(Path.GetTempPath(), $"tty-settings-{Guid.NewGuid()}.json");

    private static void SaveDefaults(AppSettings settings, int idleHopsThreshold, bool aiImprovedTextEnabled = false) =>
        settings.Save(idleHopsThreshold, aiImprovedTextEnabled, AiDefaults.Url, AiDefaults.ApiKey, AiDefaults.Provider, AiDefaults.Model, AiDefaults.MaxOutputTokens);

    [Fact]
    public void Save_PersistsAndReloadsIdleHopsThreshold()
    {
        var path = TempSettingsPath();
        try
        {
            var first = new AppSettings(path);
            SaveDefaults(first, 321);

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

            Assert.Throws<ArgumentOutOfRangeException>(() => SaveDefaults(settings, invalidValue));
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

            SaveDefaults(settings, 50);

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
            SaveDefaults(first, 321, aiImprovedTextEnabled: true);

            var second = new AppSettings(path);

            Assert.True(second.AiImprovedTextEnabled);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Constructor_MissingFile_DefaultsAiImprovedTextEnabledToFalse()
    {
        var path = TempSettingsPath(); // never written

        var settings = new AppSettings(path);

        Assert.False(settings.AiImprovedTextEnabled);
    }

    [Fact]
    public void Constructor_FileFromOlderVersionMissingAiFields_UsesDefaults()
    {
        var path = TempSettingsPath();
        try
        {
            File.WriteAllText(path, """{"IdleHopsThreshold":321}""");

            var settings = new AppSettings(path);

            Assert.Equal(321, settings.IdleHopsThreshold);
            Assert.False(settings.AiImprovedTextEnabled);
            Assert.Equal(AiDefaults.Url, settings.AiUrl);
            Assert.Equal(AiDefaults.ApiKey, settings.AiApiKey);
            Assert.Equal(AiDefaults.Provider, settings.AiProvider);
            Assert.Equal(AiDefaults.Model, settings.AiModel);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Constructor_MissingFile_UsesDefaultAiFields()
    {
        var path = TempSettingsPath(); // never written

        var settings = new AppSettings(path);

        Assert.Equal(AiDefaults.Url, settings.AiUrl);
        Assert.Equal(AiDefaults.ApiKey, settings.AiApiKey);
        Assert.Equal(AiDefaults.Provider, settings.AiProvider);
        Assert.Equal(AiDefaults.Model, settings.AiModel);
    }

    [Fact]
    public void Save_PersistsAndReloadsAiFields()
    {
        var path = TempSettingsPath();
        try
        {
            var first = new AppSettings(path);
            first.Save(321, true, "https://custom.example/v1", "custom-key", "custom-provider", "custom-model", 2500);

            var second = new AppSettings(path);

            Assert.Equal("https://custom.example/v1", second.AiUrl);
            Assert.Equal("custom-key", second.AiApiKey);
            Assert.Equal("custom-provider", second.AiProvider);
            Assert.Equal("custom-model", second.AiModel);
            Assert.Equal(2500, second.AiMaxOutputTokens);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Save_BlankAiFields_FallBackToDefaults(string blank)
    {
        var path = TempSettingsPath();
        try
        {
            var settings = new AppSettings(path);
            settings.Save(321, true, blank, blank, blank, blank, AiDefaults.MaxOutputTokens);

            Assert.Equal(AiDefaults.Url, settings.AiUrl);
            Assert.Equal(AiDefaults.ApiKey, settings.AiApiKey);
            Assert.Equal(AiDefaults.Provider, settings.AiProvider);
            Assert.Equal(AiDefaults.Model, settings.AiModel);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Constructor_MissingFile_UsesDefaultMaxOutputTokens()
    {
        var path = TempSettingsPath(); // never written

        var settings = new AppSettings(path);

        Assert.Equal(AiDefaults.MaxOutputTokens, settings.AiMaxOutputTokens);
    }

    [Fact]
    public void Constructor_FileFromOlderVersionMissingMaxOutputTokens_UsesDefault()
    {
        var path = TempSettingsPath();
        try
        {
            File.WriteAllText(path, """{"IdleHopsThreshold":321}""");

            var settings = new AppSettings(path);

            Assert.Equal(AiDefaults.MaxOutputTokens, settings.AiMaxOutputTokens);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Save_NonPositiveMaxOutputTokens_FallsBackToDefault(int invalidValue)
    {
        var path = TempSettingsPath();
        try
        {
            var settings = new AppSettings(path);
            settings.Save(321, true, AiDefaults.Url, AiDefaults.ApiKey, AiDefaults.Provider, AiDefaults.Model, invalidValue);

            Assert.Equal(AiDefaults.MaxOutputTokens, settings.AiMaxOutputTokens);
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
