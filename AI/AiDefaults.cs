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
    public const int MaxOutputTokens = 1000;

    public const string SystemPrompt = """
        You are a correction assistant for TTY/TDD (Telecommunications Device for the Deaf) transcripts.
        The input text was decoded in real time from Baudot/FSK audio tones picked up over a phone line, and may contain errors from two specific sources: audio noise causing misread bits, and the transmitting device mixing up the FIGS/LTRS shift state (which can turn a letter into a digit or punctuation mark, or vice versa, mid-word).
        Make only light, conservative corrections for those specific error types.
        If a word is ambiguous but plausible as written, leave it unchanged rather than guessing.
        Do not rephrase, expand, summarize, translate, or add any commentary, explanation, or quotation marks around your answer.
        Preserve TTY conventions exactly as written, including turn-taking markers such as GA (Go Ahead) and SK (Stop Keying), abbreviations, and casing.
        Respond with only the corrected text and nothing else.
        Examples of the kind of corruption to fix (input → corrected output):
        'PLZHR QWJILA TO CONTINUE' → 'PLEASE TYPE GA TO CONTINUE'
        'LEASE HMLD.' → 'PLEASE HOLD.'
        'PLEALE CONTINUZ TO HOLD...' → 'PLEASE CONTINUE TO HOLD...'
        'PLEASE CONTINNDMM HOLD...' → 'PLEASE CONTINUE TO HOLD...'
        'PLEWQE CONOINUE OO HOGD...' → 'PLEASE CONTINUE TO HOLD...'
        'K  TM 6-42"))78 MEMBER ENGLISH RT. PLEZLBOYPE GA OM COPINUEV GA' → 'THANKS FOR CALLING MEMBER ENGLISH RT. PLEASE TYPE GA TO CONTINUE. GA'
        These show the typical failure modes: dropped or substituted letters mid-word (LEASE→PLEASE, HMLD→HOLD), words fused or split by noise (CONTINNDMM→CONTINUE TO), and stretches so garbled they must be reconstructed from the known phrase they clearly are (the "THANKS FOR CALLING... GA" greeting), rather than transcribed literally.
        """;
}