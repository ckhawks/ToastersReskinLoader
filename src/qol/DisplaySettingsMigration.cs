// One-time migration of settings that used to live in the reskin profile but now live in the
// QoL profile (shadows, and — as later stages land — minimap, chat, gloss, team indicator).
//
// Reads the OLD values straight from reskinprofiles/ReskinProfile.json (raw JSON, since the
// typed loader no longer knows these keys) and seeds them into the QoL config the first time,
// so upgrading users don't lose their settings. Guarded by a flag in the QoL config; runs once.

using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ToasterReskinLoader.qol;

internal static class DisplaySettingsMigration
{
    public static void Run()
    {
        try
        {
            // Load the QoL config straight from disk — this runs before QoLRunner bootstraps so
            // it can read the reskin profile before anything re-saves (and strips) it.
            var cfg = QoLStorage.Load();
            if (cfg.displaySettingsMigrated) return;

            string path = Path.Combine(PathManager.GameRootFolder, "reskinprofiles", "ReskinProfile.json");
            if (File.Exists(path))
            {
                var j = JObject.Parse(File.ReadAllText(path));

                // Shadows
                ReadBool(j, "crispyShadowsEnabled", v => cfg.crispyShadowsEnabled = v);
                ReadInt(j, "shadowResolution", v => cfg.shadowResolution = v);
                ReadFloat(j, "shadowDistance", v => cfg.shadowDistance = v);
                ReadInt(j, "shadowCascadeCount", v => cfg.shadowCascadeCount = v);
                ReadBool(j, "shadowSoftShadows", v => cfg.shadowSoftShadows = v);

                // Gloss
                ReadBool(j, "glossRemoverEnabled", v => cfg.glossRemoverEnabled = v);
                ReadFloat(j, "glossSmoothness", v => cfg.glossSmoothness = v);
                ReadBool(j, "glossAffectSticks", v => cfg.glossAffectSticks = v);
                ReadBool(j, "glossAffectPlayers", v => cfg.glossAffectPlayers = v);
                ReadBool(j, "glossAffectPucks", v => cfg.glossAffectPucks = v);

                // Minimap
                ReadColor(j, "blueMinimapNumberColor", v => cfg.blueMinimapNumberColor = v);
                ReadColor(j, "redMinimapNumberColor", v => cfg.redMinimapNumberColor = v);
                ReadColor(j, "minimapPuckColor", v => cfg.minimapPuckColor = v);
                ReadFloat(j, "minimapPlayerScale", v => cfg.minimapPlayerScale = v);
                ReadFloat(j, "minimapPuckScale", v => cfg.minimapPuckScale = v);
                ReadInt(j, "minimapRefreshRate", v => cfg.minimapRefreshRate = v);
                ReadBool(j, "localPlayerMinimapIconEnabled", v => cfg.localPlayerMinimapIconEnabled = v);
                ReadColor(j, "blueLocalPlayerMinimapIconColor", v => cfg.blueLocalPlayerMinimapIconColor = v);
                ReadColor(j, "redLocalPlayerMinimapIconColor", v => cfg.redLocalPlayerMinimapIconColor = v);

                // Chat
                ReadFloat(j, "chatHeight", v => cfg.chatHeight = v);
                ReadBool(j, "chatBackground", v => cfg.chatBackground = v);
                ReadFloat(j, "quickChatX", v => cfg.quickChatX = v);
                ReadFloat(j, "quickChatY", v => cfg.quickChatY = v);
                ReadBool(j, "chatRenderAllEmojis", v => cfg.chatRenderAllEmojis = v);

                // Team indicator
                ReadBool(j, "teamIndicatorEnabled", v => cfg.teamIndicatorEnabled = v);

                Plugin.Log("[QoL] Migrated display settings from the reskin profile.");
            }

            cfg.displaySettingsMigrated = true;
            QoLStorage.Save(cfg);
        }
        catch (Exception e)
        {
            Plugin.LogError($"[QoL] Display settings migration failed: {e.Message}");
        }
    }

    private static void ReadBool(JObject j, string key, Action<bool> set)
    {
        if (j.TryGetValue(key, out var t) && t.Type != JTokenType.Null) set(t.Value<bool>());
    }

    private static void ReadInt(JObject j, string key, Action<int> set)
    {
        if (j.TryGetValue(key, out var t) && t.Type != JTokenType.Null) set(t.Value<int>());
    }

    private static void ReadFloat(JObject j, string key, Action<float> set)
    {
        if (j.TryGetValue(key, out var t) && t.Type != JTokenType.Null) set(t.Value<float>());
    }

    // Reskin-profile colors are stored as { r, g, b, a } objects.
    private static void ReadColor(JObject j, string key, Action<Color> set)
    {
        if (j.TryGetValue(key, out var t) && t is JObject o)
            set(new Color(
                o.Value<float?>("r") ?? 0f,
                o.Value<float?>("g") ?? 0f,
                o.Value<float?>("b") ?? 0f,
                o.Value<float?>("a") ?? 1f));
    }
}
