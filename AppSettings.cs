using System;
using System.IO;
using System.Text.Json;
using TtyClient.TTY;

namespace TtyClient;

/// <summary>
/// User-tunable application settings, persisted as JSON. A single shared instance lives at
/// <see cref="App.Settings"/> for the app's lifetime; the internal constructor exists so tests
/// can point at their own temp file instead of the real %AppData% path.
/// </summary>
public sealed class AppSettings
{
    public int IdleHopsThreshold { get; private set; } = GoertzelDetector.DefaultIdleHopsThreshold;
    public bool AiImprovedTextEnabled { get; private set; } = true;

    public event Action? Changed;

    private readonly string _filePath;

    public AppSettings() : this(DefaultFilePath)
    {
    }

    internal AppSettings(string filePath)
    {
        _filePath = filePath;
        Load();
    }

    private static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TtyClient", "settings.json");

    private void Load()
    {
        try
        {
            var json = File.ReadAllText(_filePath);
            var data = JsonSerializer.Deserialize<SettingsData>(json);
            if (data is not null)
            {
                if (data.IdleHopsThreshold > 0)
                    IdleHopsThreshold = data.IdleHopsThreshold;
                if (data.AiImprovedTextEnabled.HasValue)
                    AiImprovedTextEnabled = data.AiImprovedTextEnabled.Value;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Missing or corrupt settings file: first run, or nothing worth recovering.
            // Keep the default already assigned by the property initializer.
        }
    }

    public void Save(int idleHopsThreshold, bool aiImprovedTextEnabled)
    {
        if (idleHopsThreshold <= 0)
            throw new ArgumentOutOfRangeException(nameof(idleHopsThreshold), "Must be a positive integer.");

        IdleHopsThreshold = idleHopsThreshold;
        AiImprovedTextEnabled = aiImprovedTextEnabled;

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(_filePath, JsonSerializer.Serialize(new SettingsData(idleHopsThreshold, aiImprovedTextEnabled)));
        Changed?.Invoke();
    }

    private record SettingsData(int IdleHopsThreshold, bool? AiImprovedTextEnabled);
}
