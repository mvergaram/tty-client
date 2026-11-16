namespace TtyClient.AI;

/// <summary>
/// Default values for the AI text-improvement settings, used whenever a field is missing or
/// blank in <see cref="TtyClient.AppSettings"/> — same role as
/// <c>GoertzelDetector.DefaultIdleHopsThreshold</c> plays for the hops setting.
/// </summary>
public static class AiDefaults
{
    public const string Url = "https://api.portkey.ai/v1";
    public const string ApiKey = "";
    public const string Provider = "dsvertex";
    public const string Model = "anthropic.claude-haiku-5-5";

    public const string SystemPrompt = """
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
}
