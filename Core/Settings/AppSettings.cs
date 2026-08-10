using System.IO;
using System.Text.Json;

namespace OctoTask.Core.Settings;

public enum TrayDisplayMode
{
    Cpu,
    Ram
}

public class AppSettings
{
    public TrayDisplayMode TrayDisplayMode { get; set; } = TrayDisplayMode.Cpu;
    public bool MinimizeToTray { get; set; } = true;
    public int UpdateIntervalMs { get; set; } = 2000;

    private static string SettingsDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OctoTask");

    private static string SettingsPath => Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings != null)
                    return settings;
            }
        }
        catch
        {
            // Fall through to defaults
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            string json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch
            {
            // Silently fail — settings are non-critical
        }
    }
}
