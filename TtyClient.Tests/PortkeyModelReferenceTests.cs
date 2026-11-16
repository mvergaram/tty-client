using TtyClient.AI;

namespace TtyClient.Tests;

public class PortkeyModelReferenceTests
{
    [Fact]
    public void Parse_ValidReference_SplitsProviderAndModel()
    {
        var (providerSlug, modelName) = PortkeyModelReference.Parse("@dsvertex/anthropic.claude-haiku-5-5");

        Assert.Equal("@dsvertex", providerSlug);
        Assert.Equal("anthropic.claude-haiku-5-5", modelName);
    }

    [Fact]
    public void Parse_ModelNameContainsSlashes_KeepsRemainderIntact()
    {
        var (providerSlug, modelName) = PortkeyModelReference.Parse("@azure-openai/deployments/gpt-5");

        Assert.Equal("@azure-openai", providerSlug);
        Assert.Equal("deployments/gpt-5", modelName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("dsvertex/anthropic.claude-haiku-5-5")] // missing leading '@'
    [InlineData("@dsvertex")]                            // missing '/'
    [InlineData("@dsvertex/")]                            // empty model name
    public void Parse_InvalidReference_Throws(string modelReference)
    {
        Assert.Throws<FormatException>(() => PortkeyModelReference.Parse(modelReference));
    }
}
