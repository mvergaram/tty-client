using System;
using System.IO;
using System.Text.Json;

namespace TtyClient.AI;

/// <summary>
/// AI text-improvement parameters, loaded from a plain JSON file (<c>ai.conf</c>) next to the
/// executable so an operator can edit it without touching the app's own settings.json. Any
/// missing or blank field falls back to its own default independently of the others.
/// </summary>
public sealed class AiConfig
{
    public const string DefaultApiKey = "asdfnlkj2134oiu_default_api_key";
    public const string DefaultModel = "@dsvertex/anthropic.claude-haiku-5-5";

    public const string DefaultSystemPrompt = """
        You are a correction assistant for TTY/TDD (Telecommunications Device for the Deaf) transcripts.
        The input text was decoded in real time from Baudot/FSK audio tones picked up over a phone line,
        and may contain errors from two specific sources: audio noise causing misread bits, and the
        transmitting device mixing up the FIGS/LTRS shift state (which can turn a letter into a digit or
        punctuation mark, or vice versa, mid-word).
        Make only light, conservative corrections for those specific error types. Do not rephrase, expand,
        summarize, translate, or add any commentary, explanation, or quotation marks around your answer.
        Preserve TTY conventions exactly as written, including turn-taking markers such as GA (Go Ahead)
        and SK (Stop Keying), abbreviations, and casing. If a word is ambiguous but plausible as written,
        leave it unchanged rather than guessing.
        Respond with only the corrected text and nothing else.
        """;

    public string ApiKey { get; }
    public string Model { get; }
    public string SystemPrompt { get; }

    private AiConfig(string apiKey, string model, string systemPrompt)
    {
        ApiKey = apiKey;
        Model = model;
        SystemPrompt = systemPrompt;
    }

    public static AiConfig Load() => Load(DefaultFilePath);

    internal static AiConfig Load(string filePath)
    {
        string apiKey = DefaultApiKey;
        string model = DefaultModel;
        string systemPrompt = DefaultSystemPrompt;

        try
        {
            var json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<AiConfigData>(json);
            if (data is not null)
            {
                if (!string.IsNullOrWhiteSpace(data.ApiKey)) apiKey = data.ApiKey;
                if (!string.IsNullOrWhiteSpace(data.Model)) model = data.Model;
                if (!string.IsNullOrWhiteSpace(data.SystemPrompt)) systemPrompt = data.SystemPrompt;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Missing or corrupt ai.conf: first run, or nothing worth recovering. Keep defaults.
        }

        return new AiConfig(apiKey, model, systemPrompt);
    }

    private static string DefaultFilePath => Path.Combine(AppContext.BaseDirectory, "ai.conf");

    private record AiConfigData(string? ApiKey, string? Model, string? SystemPrompt);
}
