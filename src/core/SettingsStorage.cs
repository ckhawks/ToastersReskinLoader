// File-backed storage for the settings surface.
//
// Two files under <gameRoot>/config/, prefixed with ToastersReskinLoader so
// they're trivially attributable next to other plugins' config files:
//   * ToastersReskinLoaderQoL.json          — SettingsConfig (toggles + filters + window state)
//   * ToastersReskinLoaderServerPrefs.json  — per-server credentials (passwords/trusted/fav/blocked)
//
// SettingsConfig is the on-disk shape directly (no SettingsProfile mirror).
// The four per-server dicts are [JsonIgnore]d on SettingsConfig and round-trip
// through ServerPrefsProfile here so credentials stay out of the shareable QoL file.
// Color fields use ColorJsonConverter to match the old SerializableColor shape.

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace ToasterReskinLoader.core;

internal static class SettingsStorage
{
    private static readonly string Dir =
        Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "config");

    internal static readonly string QoLPath         = Path.Combine(Dir, "ToastersReskinLoaderQoL.json");
    internal static readonly string ServerPrefsPath = Path.Combine(Dir, "ToastersReskinLoaderServerPrefs.json");

    private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
    {
        Formatting = Formatting.Indented,
        Converters = { new ColorJsonConverter() },
    };

    public static SettingsConfig Load()
    {
        try
        {
            Directory.CreateDirectory(Dir);

            var cfg   = ReadJson<SettingsConfig>(QoLPath)        ?? new SettingsConfig();
            var prefs = ReadJson<ServerPrefsProfile>(ServerPrefsPath) ?? new ServerPrefsProfile();

            cfg.savedServerPasswords = prefs.SavedServerPasswords ?? new Dictionary<string, string>();
            cfg.trustedServerMods    = prefs.TrustedServerMods    ?? new Dictionary<string, string>();
            cfg.favoriteServers      = prefs.FavoriteServers      ?? new Dictionary<string, string>();
            cfg.blockedServers       = prefs.BlockedServers       ?? new Dictionary<string, string>();
            return cfg;
        }
        catch (Exception e)
        {
            Plugin.LogError($"[QoL] SettingsStorage.Load failed: {e.Message}");
            return new SettingsConfig();
        }
    }

    public static void Save(SettingsConfig cfg)
    {
        if (cfg == null) return;
        try
        {
            Directory.CreateDirectory(Dir);

            File.WriteAllText(QoLPath, JsonConvert.SerializeObject(cfg, JsonSettings));

            var prefs = new ServerPrefsProfile
            {
                SavedServerPasswords = cfg.savedServerPasswords != null
                    ? new Dictionary<string, string>(cfg.savedServerPasswords)
                    : new Dictionary<string, string>(),
                TrustedServerMods = cfg.trustedServerMods != null
                    ? new Dictionary<string, string>(cfg.trustedServerMods)
                    : new Dictionary<string, string>(),
                FavoriteServers = cfg.favoriteServers != null
                    ? new Dictionary<string, string>(cfg.favoriteServers)
                    : new Dictionary<string, string>(),
                BlockedServers = cfg.blockedServers != null
                    ? new Dictionary<string, string>(cfg.blockedServers)
                    : new Dictionary<string, string>(),
            };
            File.WriteAllText(ServerPrefsPath, JsonConvert.SerializeObject(prefs, JsonSettings));
        }
        catch (Exception e) { Plugin.LogError($"[QoL] SettingsStorage.Save failed: {e.Message}"); }
    }

    private static T ReadJson<T>(string path) where T : class
    {
        if (!File.Exists(path)) return null;
        try
        {
            return JsonConvert.DeserializeObject<T>(File.ReadAllText(path), JsonSettings);
        }
        catch (Exception e)
        {
            Plugin.LogError($"[QoL] failed to read {path}: {e.Message}");
            return null;
        }
    }
}
