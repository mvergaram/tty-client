using TtyClient.AI;

namespace TtyClient.Tests;

public class AiConfigTests
{
    private static string TempConfigPath() =>
        Path.Combine(Path.GetTempPath(), $"tty-ai-{Guid.NewGuid()}.conf");

    [Fact]
    public void Load_MissingFile_UsesAllDefaults()
    {
        var path = TempConfigPath(); // never written

        var config = AiConfig.Load(path);

        Assert.Equal(AiConfig.DefaultApiKey, config.ApiKey);
        Assert.Equal(AiConfig.DefaultModel, config.Model);
        Assert.Equal(AiConfig.DefaultSystemPrompt, config.SystemPrompt);
    }

    [Fact]
    public void Load_CorruptFile_UsesAllDefaults()
    {
        var path = TempConfigPath();
        try
        {
            File.WriteAllText(path, "{ not valid json");

            var config = AiConfig.Load(path);

            Assert.Equal(AiConfig.DefaultApiKey, config.ApiKey);
            Assert.Equal(AiConfig.DefaultModel, config.Model);
            Assert.Equal(AiConfig.DefaultSystemPrompt, config.SystemPrompt);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_PartialFile_FillsOnlyMissingFieldsWithDefaults()
    {
        var path = TempConfigPath();
        try
        {
            File.WriteAllText(path, """{"ApiKey":"custom-key"}""");

            var config = AiConfig.Load(path);

            Assert.Equal("custom-key", config.ApiKey);
            Assert.Equal(AiConfig.DefaultModel, config.Model);
            Assert.Equal(AiConfig.DefaultSystemPrompt, config.SystemPrompt);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Load_BlankStringFields_FallBackToDefault(string blank)
    {
        var path = TempConfigPath();
        try
        {
            File.WriteAllText(path, $$"""{"ApiKey":"{{blank}}","Model":"{{blank}}","SystemPrompt":"{{blank}}"}""");

            var config = AiConfig.Load(path);

            Assert.Equal(AiConfig.DefaultApiKey, config.ApiKey);
            Assert.Equal(AiConfig.DefaultModel, config.Model);
            Assert.Equal(AiConfig.DefaultSystemPrompt, config.SystemPrompt);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_FullFile_UsesAllProvidedValues()
    {
        var path = TempConfigPath();
        try
        {
            File.WriteAllText(path, """{"ApiKey":"k","Model":"@provider/model","SystemPrompt":"prompt"}""");

            var config = AiConfig.Load(path);

            Assert.Equal("k", config.ApiKey);
            Assert.Equal("@provider/model", config.Model);
            Assert.Equal("prompt", config.SystemPrompt);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
