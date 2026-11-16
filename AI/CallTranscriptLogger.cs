using System;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace TtyClient.AI;

/// <summary>
/// Writes one JSON object per line (JSON Lines) for every received/sent/AI event during a call,
/// to a file named from the call's UTC start time next to the executable. Using one line per
/// event (rather than rewriting a JSON array) keeps the log crash-safe without buffering.
/// </summary>
public sealed class CallTranscriptLogger : IDisposable
{
    private readonly StreamWriter _writer;

    private CallTranscriptLogger(StreamWriter writer) => _writer = writer;

    public static CallTranscriptLogger Open(DateTime callStartUtc) => Open(DefaultDirectory, callStartUtc);

    internal static CallTranscriptLogger Open(string directory, DateTime callStartUtc)
    {
        Directory.CreateDirectory(directory);

        string fileName = callStartUtc.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture) + "Z.txt";
        var stream = new FileStream(Path.Combine(directory, fileName), FileMode.Create, FileAccess.Write, FileShare.Read);
        return new CallTranscriptLogger(new StreamWriter(stream) { AutoFlush = true });
    }

    private static string DefaultDirectory => Path.Combine(AppContext.BaseDirectory, "transcription");

    public void LogReceived(string text) => Write(new TranscriptEntry("received", text, null, null));

    public void LogSent(string text) => Write(new TranscriptEntry("sent", text, null, null));

    public void LogImproved(string original, string improved) => Write(new TranscriptEntry("ai", original, improved, null));

    public void LogError(string original, string error) => Write(new TranscriptEntry("ai", original, null, error));

    private void Write(TranscriptEntry entry)
    {
        lock (_writer)
            _writer.WriteLine(JsonSerializer.Serialize(entry with { TimeUtc = DateTime.UtcNow }));
    }

    public void Dispose() => _writer.Dispose();

    private sealed record TranscriptEntry(string Direction, string? Original, string? Improved, string? Error)
    {
        public DateTime TimeUtc { get; init; }
    }
}
