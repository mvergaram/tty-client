using System.Text.Json;
using TtyClient.AI;

namespace TtyClient.Tests;

public class CallTranscriptLoggerTests
{
    private static string TempDirectory() =>
        Path.Combine(Path.GetTempPath(), $"tty-transcription-{Guid.NewGuid()}");

    [Fact]
    public void Open_CreatesFileNamedFromUtcCallStartTime()
    {
        var directory = TempDirectory();
        try
        {
            var callStartUtc = new DateTime(2026, 3, 5, 14, 23, 5, DateTimeKind.Utc);

            using var logger = CallTranscriptLogger.Open(directory, callStartUtc);

            Assert.True(File.Exists(Path.Combine(directory, "2026-03-05_14-23-05Z.txt")));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void LogReceived_And_LogSent_WriteOneJsonLinePerEntry()
    {
        var directory = TempDirectory();
        try
        {
            var callStartUtc = DateTime.UtcNow;
            string filePath;
            using (var logger = CallTranscriptLogger.Open(directory, callStartUtc))
            {
                filePath = Directory.GetFiles(directory).Single();
                logger.LogReceived("HELLO");
                logger.LogSent("YOU: HI");
            }

            var lines = File.ReadAllLines(filePath);
            Assert.Equal(2, lines.Length);

            var first = JsonDocument.Parse(lines[0]).RootElement;
            Assert.Equal("received", first.GetProperty("Direction").GetString());
            Assert.Equal("HELLO", first.GetProperty("Original").GetString());

            var second = JsonDocument.Parse(lines[1]).RootElement;
            Assert.Equal("sent", second.GetProperty("Direction").GetString());
            Assert.Equal("YOU: HI", second.GetProperty("Original").GetString());
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void LogImproved_WritesOriginalAndImprovedFields()
    {
        var directory = TempDirectory();
        try
        {
            string filePath;
            using (var logger = CallTranscriptLogger.Open(directory, DateTime.UtcNow))
            {
                filePath = Directory.GetFiles(directory).Single();
                logger.LogImproved("HELO", "HELLO");
            }

            var entry = JsonDocument.Parse(File.ReadAllLines(filePath).Single()).RootElement;
            Assert.Equal("ai", entry.GetProperty("Direction").GetString());
            Assert.Equal("HELO", entry.GetProperty("Original").GetString());
            Assert.Equal("HELLO", entry.GetProperty("Improved").GetString());
            Assert.Equal(JsonValueKind.Null, entry.GetProperty("Error").ValueKind);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void LogError_WritesErrorFieldAndNullImproved()
    {
        var directory = TempDirectory();
        try
        {
            string filePath;
            using (var logger = CallTranscriptLogger.Open(directory, DateTime.UtcNow))
            {
                filePath = Directory.GetFiles(directory).Single();
                logger.LogError("HELO", "timeout");
            }

            var entry = JsonDocument.Parse(File.ReadAllLines(filePath).Single()).RootElement;
            Assert.Equal("ai", entry.GetProperty("Direction").GetString());
            Assert.Equal("HELO", entry.GetProperty("Original").GetString());
            Assert.Equal(JsonValueKind.Null, entry.GetProperty("Improved").ValueKind);
            Assert.Equal("timeout", entry.GetProperty("Error").GetString());
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
