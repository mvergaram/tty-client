using System;

namespace TtyClient.AI;

/// <summary>
/// Splits a combined Portkey model reference (e.g. <c>"@dsvertex/anthropic.claude-haiku-5-5"</c>)
/// into the provider slug used for the <c>x-portkey-provider</c> header (kept with its leading
/// <c>@</c>, matching Portkey's own C# SDK examples) and the model name passed to the chat client.
/// </summary>
public static class PortkeyModelReference
{
    public static (string ProviderSlug, string ModelName) Parse(string modelReference)
    {
        if (string.IsNullOrWhiteSpace(modelReference) || modelReference[0] != '@')
            throw new FormatException($"Expected a model reference like \"@provider/model\", got \"{modelReference}\".");

        int slashIdx = modelReference.IndexOf('/');
        if (slashIdx < 0 || slashIdx == modelReference.Length - 1)
            throw new FormatException($"Expected a model reference like \"@provider/model\", got \"{modelReference}\".");

        return (modelReference[..slashIdx], modelReference[(slashIdx + 1)..]);
    }
}
