using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ToasterReskinLoader;

/// <summary>
/// Handles one-time migration of settings from the standalone ToasterPuckFX mod
/// into the ToasterReskinLoader profile. If a user has PuckFX configured but
/// hasn't touched Puck FX settings in TRL yet, their old values are imported.
/// </summary>
public static class PuckFXMigrator
{
    private static string PuckFXConfigPath =>
        Path.Combine(PathManager.GameRootFolder, "config", "ToasterPuckFX.json");

    /// <summary>
    /// Attempts to migrate PuckFX settings into the current profile.
    /// Only migrates if:
    ///   1. The PuckFX config file exists
    ///   2. The current profile's Puck FX settings are all still at defaults
    /// </summary>
    public static void TryMigrate()
    {
        try
        {
            if (!File.Exists(PuckFXConfigPath))
            {
                Plugin.LogDebug("PuckFX config not found, no migration needed.");
                return;
            }

            if (!ProfileIsAtPuckFXDefaults())
            {
                Plugin.LogDebug("Puck FX settings already customized in profile, skipping migration.");
                return;
            }

            Plugin.Log($"Found PuckFX config at {PuckFXConfigPath}, migrating settings...");

            string json = File.ReadAllText(PuckFXConfigPath);
            var config = JObject.Parse(json);

            var profile = ReskinProfileManager.currentProfile;

            // Outline
            profile.puckFXOutlineColor = new Color(
                GetFloat(config, "PuckOutlineR", 1f),
                GetFloat(config, "PuckOutlineG", 1f),
                GetFloat(config, "PuckOutlineB", 1f),
                1f);
            profile.puckFXOutlineKernelSize = GetInt(config, "PuckOutlineKernelSize", 1);

            // Elevation indicator
            profile.puckFXElevationIndicatorColor = new Color(
                GetFloat(config, "ElevationIndicatorR", 0f),
                GetFloat(config, "ElevationIndicatorG", 0f),
                GetFloat(config, "ElevationIndicatorB", 0f),
                GetFloat(config, "ElevationIndicatorA", 1f));

            // Verticality line
            profile.puckFXVerticalityLineColor = new Color(
                GetFloat(config, "VerticalityLineR", 0f),
                GetFloat(config, "VerticalityLineG", 0f),
                GetFloat(config, "VerticalityLineB", 0f),
                GetFloat(config, "VerticalityLineA", 0.8f));
            profile.puckFXVerticalityLineStartAlpha = GetFloat(config, "VerticalityLineStartA", 0.5f);
            profile.puckFXVerticalityLineEndAlpha = GetFloat(config, "VerticalityLineEndA", 1f);

            // Trail
            profile.puckFXTrailEnabled = GetBool(config, "PuckTrailEnabled", false);
            profile.puckFXTrailColor = new Color(
                GetFloat(config, "PuckTrailColorR", 0f),
                GetFloat(config, "PuckTrailColorG", 0f),
                GetFloat(config, "PuckTrailColorB", 0f),
                1f);
            profile.puckFXTrailStartWidth = GetFloat(config, "PuckTrailStartWidth", 0.1f);
            profile.puckFXTrailEndWidth = GetFloat(config, "PuckTrailEndWidth", 0f);
            profile.puckFXTrailLifetime = GetFloat(config, "PuckTrailLifetimeSeconds", 0.6f);

            // Trail alpha was not configurable in PuckFX, keep defaults

            ReskinProfileManager.SaveProfile();
            Plugin.Log("PuckFX settings migrated successfully into Reskin Manager profile!");
        }
        catch (Exception e)
        {
            Plugin.LogError($"Failed to migrate PuckFX settings: {e.Message}");
        }
    }

    /// <summary>
    /// Checks whether the current profile's Puck FX values are all still at their defaults.
    /// If they are, it's safe to overwrite them with PuckFX values.
    /// </summary>
    private static bool ProfileIsAtPuckFXDefaults()
    {
        var profile = ReskinProfileManager.currentProfile;
        var defaults = new ReskinProfileManager.Profile();

        return profile.puckFXOutlineColor == defaults.puckFXOutlineColor
               && profile.puckFXOutlineKernelSize == defaults.puckFXOutlineKernelSize
               && profile.puckFXElevationIndicatorColor == defaults.puckFXElevationIndicatorColor
               && profile.puckFXVerticalityLineColor == defaults.puckFXVerticalityLineColor
               && Mathf.Approximately(profile.puckFXVerticalityLineStartAlpha, defaults.puckFXVerticalityLineStartAlpha)
               && Mathf.Approximately(profile.puckFXVerticalityLineEndAlpha, defaults.puckFXVerticalityLineEndAlpha)
               && profile.puckFXTrailEnabled == defaults.puckFXTrailEnabled
               && profile.puckFXTrailColor == defaults.puckFXTrailColor
               && Mathf.Approximately(profile.puckFXTrailStartWidth, defaults.puckFXTrailStartWidth)
               && Mathf.Approximately(profile.puckFXTrailEndWidth, defaults.puckFXTrailEndWidth)
               && Mathf.Approximately(profile.puckFXTrailLifetime, defaults.puckFXTrailLifetime);
    }

    private static float GetFloat(JObject obj, string key, float fallback)
    {
        var token = obj[key];
        return token != null ? token.Value<float>() : fallback;
    }

    private static int GetInt(JObject obj, string key, int fallback)
    {
        var token = obj[key];
        return token != null ? token.Value<int>() : fallback;
    }

    private static bool GetBool(JObject obj, string key, bool fallback)
    {
        var token = obj[key];
        return token != null ? token.Value<bool>() : fallback;
    }
}
