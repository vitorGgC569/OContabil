using System.IO;
using System.Text.Json;

namespace OContabil.Services;

/// <summary>
/// Lightweight persistent settings using a local JSON file in %LocalAppData%\OContabil
/// </summary>
public static class AppSettings
{
    private static readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OContabil", "settings.json");

    private static SettingsData _data = Load();

    // ── Public Properties ─────────────────────────────────────────────────
    public static string GlinerModelName
    {
        get => _data.GlinerModelName;
        set { _data.GlinerModelName = value; Save(); }
    }

    public static double GlinerThreshold
    {
        get => _data.GlinerThreshold;
        set { _data.GlinerThreshold = value; Save(); }
    }

    // ── Persistence ───────────────────────────────────────────────────────
    private static SettingsData Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<SettingsData>(json) ?? new SettingsData();
            }
        }
        catch { /* Use defaults if anything fails */ }
        return new SettingsData();
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch { }
    }

    private class SettingsData
    {
        public string GlinerModelName { get; set; } = "fastino/gliner2-base-v1";
        public double GlinerThreshold { get; set; } = 0.80;
    }
}
