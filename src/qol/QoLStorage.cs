// File-backed storage for the QoL feature surface.
//
// Two files under <gameRoot>/config/, prefixed with ToastersReskinLoader
// so they're trivially attributable when sitting next to other plugins'
// config files:
//   * ToastersReskinLoaderQoL.json          — toggles + filters + dev-console window state
//   * ToastersReskinLoaderServerPrefs.json  — savedServerPasswords + trustedServerMods
//
// Reskin profiles can be shared without leaking any of the QoL surface;
// per-server credentials live in their own file so they're obviously not
// part of the shareable visual profile.

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace ToasterReskinLoader.qol;

internal static class QoLStorage
{
    private static readonly string Dir =
        Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "config");

    internal static readonly string QoLPath         = Path.Combine(Dir, "ToastersReskinLoaderQoL.json");
    internal static readonly string ServerPrefsPath = Path.Combine(Dir, "ToastersReskinLoaderServerPrefs.json");

    public static QoLConfig Load()
    {
        try
        {
            Directory.CreateDirectory(Dir);

            var qol   = ReadJson<QoLProfile>(QoLPath)               ?? new QoLProfile();
            var prefs = ReadJson<ServerPrefsProfile>(ServerPrefsPath) ?? new ServerPrefsProfile();

            var cfg = qol.ToConfig();
            cfg.savedServerPasswords = prefs.SavedServerPasswords ?? new Dictionary<string, string>();
            cfg.trustedServerMods    = prefs.TrustedServerMods    ?? new Dictionary<string, string>();
            return cfg;
        }
        catch (Exception e)
        {
            Plugin.LogError($"[QoL] QoLStorage.Load failed: {e.Message}");
            return new QoLConfig();
        }
    }

    public static void Save(QoLConfig cfg)
    {
        if (cfg == null) return;
        try
        {
            Directory.CreateDirectory(Dir);

            var qol = new QoLProfile();
            qol.FromConfig(cfg);
            File.WriteAllText(QoLPath, JsonConvert.SerializeObject(qol, Formatting.Indented));

            var prefs = new ServerPrefsProfile
            {
                SavedServerPasswords = cfg.savedServerPasswords != null
                    ? new Dictionary<string, string>(cfg.savedServerPasswords)
                    : new Dictionary<string, string>(),
                TrustedServerMods = cfg.trustedServerMods != null
                    ? new Dictionary<string, string>(cfg.trustedServerMods)
                    : new Dictionary<string, string>(),
            };
            File.WriteAllText(ServerPrefsPath, JsonConvert.SerializeObject(prefs, Formatting.Indented));
        }
        catch (Exception e) { Plugin.LogError($"[QoL] QoLStorage.Save failed: {e.Message}"); }
    }

    private static T ReadJson<T>(string path) where T : class
    {
        if (!File.Exists(path)) return null;
        try
        {
            return JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Plugin.LogError($"[QoL] failed to read {path}: {e.Message}");
            return null;
        }
    }
}
