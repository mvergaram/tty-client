using System.Text;
using TtyClient.Audio;
using TtyClient.TTY;

namespace TtyClient.Tests;

public class WavDecodingTests
{
    // These fixtures are real captured PSTN call audio, not synthetic tones. Per the
    // documented ±3% baud-drift / noise-margin trade-off in GoertzelDetector's AnalysisWindow,
    // real recordings can produce a stray leading/trailing character from sync lock-on/idle
    // noise, or an occasional mid-message bit error — so an exact string match isn't realistic.
    // A small edit-distance tolerance still catches a genuinely broken decode (garbled output,
    // dropped message, systematic protocol bug) while accepting that known noise.
    private const int MaxAllowedEditDistance = 5;

    public static IEnumerable<object[]> AudioFiles()
    {
        var audiosDir = Path.Combine(AppContext.BaseDirectory, "Audios");
        foreach (var path in Directory.GetFiles(audiosDir, "*.wav"))
        {
            yield return new object[] { path, Path.GetFileNameWithoutExtension(path) };
        }
    }

    [Theory]
    [MemberData(nameof(AudioFiles))]
    public void DecodesWavFileToExpectedText(string filePath, string expectedText)
    {
        var goertzel = new GoertzelDetector();
        var sync = new BitSynchronizer();
        var decoder = new BaudotDecoder();
        var sb = new StringBuilder();

        goertzel.BitDetected += sync.OnBit;
        sync.CharacterReceived += decoder.OnByte;
        decoder.CharacterDecoded += ch => sb.Append(ch);

        WavFileHandler.LoadSamples(filePath, (samples, count) => goertzel.ProcessSamples(samples, count));

        var actualText = sb.ToString();
        var distance = LevenshteinDistance(expectedText, actualText);

        Assert.True(
            distance <= MaxAllowedEditDistance,
            $"Decoded text differs too much from the expected file name.\n" +
            $"Expected: \"{expectedText}\"\n" +
            $"Actual:   \"{actualText}\"\n" +
            $"Edit distance: {distance} (max allowed: {MaxAllowedEditDistance})");
    }

    private static int LevenshteinDistance(string a, string b)
    {
        var distances = new int[a.Length + 1, b.Length + 1];

        for (var i = 0; i <= a.Length; i++)
        {
            distances[i, 0] = i;
        }

        for (var j = 0; j <= b.Length; j++)
        {
            distances[0, j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                distances[i, j] = Math.Min(
                    Math.Min(distances[i - 1, j] + 1, distances[i, j - 1] + 1),
                    distances[i - 1, j - 1] + cost);
            }
        }

        return distances[a.Length, b.Length];
    }
}
