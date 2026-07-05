using System.IO;
using System.Text.Json;

namespace ToasterReskinLoader;

public class ModSettings
{
    public bool DebugLoggingModeEnabled { get; set; } = false;
    public bool BigHeadsEnabled { get; set; } = false;
    public bool ShowPersonalization { get; set; } = true;
    public bool ShowOtherPlayersHats { get; set; } = true;
    public bool ShowNonNaturalSkinTones { get; set; } = true;
    public bool ShowLevelUpNotifications { get; set; } = true;

    static string ConfigurationFileName = $"{Plugin.MOD_NAME}.json";

    public static ModSettings Load()
    {
        var path = GetConfigPath();
        var dir = Path.GetDirectoryName(path);

        // Make sure config/ directory exists
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
            Plugin.Log($"Created missing /config directory");
        }
        
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<ModSettings>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return settings ?? new ModSettings();
            }
            catch (JsonException je)
            {
                Plugin.Log($"Corrupt config JSON, using defaults: {je.Message}");
                return new ModSettings();
            }
        }
        
        var defaults = new ModSettings();
        File.WriteAllText(path, JsonSerializer.Serialize(defaults, new JsonSerializerOptions { WriteIndented = true }));
        Plugin.Log($"Config file `{path}` did not exist, created with defaults.");
        return defaults;
    }

    public void Save()
    {
        var path = GetConfigPath();
        var dir  = Path.GetDirectoryName(path);

        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static string GetConfigPath()
    {
        // Use GameRootFolder (Application.dataPath/..) so the config lands next to the game
        // regardless of the process working directory, which can differ under Steam/launchers.
        // Every other config path in the mod (PuckFXMigrator, PresetStore, ...) resolves this way.
        string configPath = Path.Combine(PathManager.GameRootFolder, "config", ConfigurationFileName);
        return configPath;
    }
}