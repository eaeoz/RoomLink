using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RoomLink;

/// <summary>
/// Persisted configuration: the four endpoint ids the user selected.
/// Stored as %APPDATA%\RoomLink\settings.json.
/// </summary>
public sealed class Settings
{
    public string? HeadsetMicId { get; set; }
    public string? HeadsetSpeakerId { get; set; }
    public string? LocalMicId { get; set; }
    public string? LocalSpeakerId { get; set; }

    public bool AutoStart { get; set; } = false;
    public float HeadsetToSpeakersGain { get; set; } = 1.0f;
    public float MicToHeadsetGain { get; set; } = 1.0f;

    [JsonIgnore]
    public bool IsComplete =>
        !string.IsNullOrEmpty(HeadsetMicId) &&
        !string.IsNullOrEmpty(HeadsetSpeakerId) &&
        !string.IsNullOrEmpty(LocalMicId) &&
        !string.IsNullOrEmpty(LocalSpeakerId);

    private static string Dir =>
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RoomLink");

    private static string Path => System.IO.Path.Combine(Dir, "settings.json");

    public static Settings Load()
    {
        try
        {
            if (System.IO.File.Exists(Path))
            {
                var s = JsonSerializer.Deserialize<Settings>(System.IO.File.ReadAllText(Path));
                if (s != null) return s;
            }
        }
        catch { /* corrupt settings: fall through to defaults */ }
        return new Settings();
    }

    public void Save()
    {
        System.IO.Directory.CreateDirectory(Dir);
        System.IO.File.WriteAllText(
            Path,
            JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
