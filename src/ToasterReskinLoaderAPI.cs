using System;
using UnityEngine;

namespace ToasterReskinLoader;

/// <summary>
/// Public API for other mods to read TRL settings.
/// Other mods can reference ToasterReskinLoader.dll and use this directly,
/// or use reflection for a soft/optional dependency.
/// </summary>
public static class ToasterReskinLoaderAPI
{
    private static readonly Color DefaultBlue = new Color(0.231f, 0.510f, 0.965f, 1f);
    private static readonly Color DefaultRed = new Color(0.820f, 0.200f, 0.200f, 1f);

    /// <summary>
    /// Fired whenever the user changes team color settings (enable/disable, color values).
    /// Subscribe to this to react to changes in real time.
    /// </summary>
    public static event Action OnTeamColorsChanged;

    /// <summary>Whether custom team colors are enabled by the user.</summary>
    public static bool TeamColorsEnabled =>
        ReskinProfileManager.currentProfile?.teamColorsEnabled ?? false;

    /// <summary>The user's custom blue team color, or the default if not customized.</summary>
    public static Color BlueTeamColor =>
        ReskinProfileManager.currentProfile?.blueTeamColor ?? DefaultBlue;

    /// <summary>The user's custom red team color, or the default if not customized.</summary>
    public static Color RedTeamColor =>
        ReskinProfileManager.currentProfile?.redTeamColor ?? DefaultRed;

    /// <summary>The user's custom blue team name, or null/empty if not set.</summary>
    public static string BlueTeamName =>
        ReskinProfileManager.currentProfile?.blueTeamName ?? "";

    /// <summary>The user's custom red team name, or null/empty if not set.</summary>
    public static string RedTeamName =>
        ReskinProfileManager.currentProfile?.redTeamName ?? "";

    /// <summary>Call this internally whenever team color settings change.</summary>
    internal static void NotifyTeamColorsChanged()
    {
        try
        {
            // Refresh TRL's own UI overrides
            swappers.TeamColorSwapper.RefreshAll();

            // Notify external mods
            OnTeamColorsChanged?.Invoke();
        }
        catch (Exception e)
        {
            Plugin.LogDebug($"ToasterReskinLoaderAPI.OnTeamColorsChanged handler error: {e.Message}");
        }
    }
}
