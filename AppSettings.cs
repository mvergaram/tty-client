using System;
using System.IO;
using System.Text.Json;
using TtyClient.AI;
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
    public bool AiImprovedTextEnabled { get; private set; } = false;
    public string AiUrl { get; private set; } = AiDefaults.Url;
    public string AiApiKey { get; private set; } = AiDefaults.ApiKey;
    public string AiProvider { get; private set; } = AiDefaults.Provider;
    public string AiModel { get; private set; } = AiDefaults.Model;

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
                if (!string.IsNullOrWhiteSpace(data.AiUrl))
                    AiUrl = data.AiUrl;
                if (!string.IsNullOrWhiteSpace(data.AiApiKey))
                    AiApiKey = data.AiApiKey;
                if (!string.IsNullOrWhiteSpace(data.AiProvider))
                    AiProvider = data.AiProvider;
                if (!string.IsNullOrWhiteSpace(data.AiModel))
                    AiModel = data.AiModel;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Missing or corrupt settings file: first run, or nothing worth recovering.
            // Keep the defaults already assigned by the property initializers.
        }
    }

    public void Save(int idleHopsThreshold, bool aiImprovedTextEnabled, string aiUrl, string aiApiKey, string aiProvider, string aiModel)
    {
        if (idleHopsThreshold <= 0)
            throw new ArgumentOutOfRangeException(nameof(idleHopsThreshold), "Must be a positive integer.");

        IdleHopsThreshold = idleHopsThreshold;
        AiImprovedTextEnabled = aiImprovedTextEnabled;
        AiUrl = string.IsNullOrWhiteSpace(aiUrl) ? AiDefaults.Url : aiUrl;
        AiApiKey = string.IsNullOrWhiteSpace(aiApiKey) ? AiDefaults.ApiKey : aiApiKey;
        AiProvider = string.IsNullOrWhiteSpace(aiProvider) ? AiDefaults.Provider : aiProvider;
        AiModel = string.IsNullOrWhiteSpace(aiModel) ? AiDefaults.Model : aiModel;

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(_filePath, JsonSerializer.Serialize(
            new SettingsData(IdleHopsThreshold, AiImprovedTextEnabled, AiUrl, AiApiKey, AiProvider, AiModel)));
        Changed?.Invoke();
    }

    private record SettingsData(
        int IdleHopsThreshold,
        bool? AiImprovedTextEnabled,
        string? AiUrl = null,
        string? AiApiKey = null,
        string? AiProvider = null,
        string? AiModel = null);
}
