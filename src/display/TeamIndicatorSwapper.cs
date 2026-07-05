using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

using ToasterReskinLoader.swappers;

using ToasterReskinLoader.core;

namespace ToasterReskinLoader.display;

/// <summary>
/// Recolors the base game's HUD team-color bar (UIHUD.teamColorBar) to the reskin
/// profile's custom team color.
///
/// B1117 ships its own team-color bar in the HUD (colored via a USS team class in
/// UIHUD.SetTeam, gated by SettingsManager.ShowTeamColorBar), so instead of adding a
/// duplicate bar we tint the native one — but ONLY when custom team colors are
/// enabled for that team. When they aren't, we clear our inline override so the
/// game's own coloring shows through, and native owns the bar's visibility entirely.
/// </summary>
public static class TeamIndicatorSwapper
{
    private static readonly FieldInfo _teamColorBarField = typeof(UIHUD)
        .GetField("teamColorBar", BindingFlags.Instance | BindingFlags.NonPublic);

    // Tint the native bar with the custom team color, or clear our override so the
    // game's USS team class colors it. Only tints when custom colors are enabled.
    private static void Apply(VisualElement bar, PlayerTeam team)
    {
        if (bar == null) return;

        if ((team == PlayerTeam.Blue || team == PlayerTeam.Red) && TeamColorSwapper.IsEnabled(team))
        {
            var custom = TeamColorSwapper.GetOverrideColor(team);
            if (custom.HasValue)
            {
                bar.style.backgroundColor = new StyleColor(custom.Value);
                return;
            }
        }

        // Custom colors off (or none set) — drop our inline color so vanilla wins.
        bar.style.backgroundColor = StyleKeyword.Null;
    }

    private static UIHUD FindHud() => UnityEngine.Object.FindFirstObjectByType<UIHUD>();

    /// <summary>
    /// Re-applies the custom team color to the native bar for the local player's
    /// current team. Call when team colors change in the reskin menu.
    /// </summary>
    public static void Refresh()
    {
        try
        {
            var hud = FindHud();
            if (hud == null) return;
            var bar = _teamColorBarField?.GetValue(hud) as VisualElement;
            if (bar == null) return;
            var localTeam = PlayerManager.Instance?.GetLocalPlayer()?.GameState.Value.Team ?? PlayerTeam.None;
            Apply(bar, localTeam);
        }
        catch (Exception e) { Plugin.LogDebug($"TeamIndicator: Refresh error: {e.Message}"); }
    }

    /// <summary>
    /// Clears our inline override so the native bar returns to vanilla coloring.
    /// </summary>
    public static void Cleanup()
    {
        try
        {
            var hud = FindHud();
            if (hud == null) return;
            var bar = _teamColorBarField?.GetValue(hud) as VisualElement;
            if (bar != null) bar.style.backgroundColor = StyleKeyword.Null;
        }
        catch { }
    }

    // Native re-colors the bar via UIUtils.SetTeamClass on every RefreshTeamColorBar
    // (team change, ShowTeamColorBar toggle, HUD visibility). Re-apply our custom tint
    // right after so it survives those refreshes.
    [HarmonyPatch(typeof(UIHUD), nameof(UIHUD.SetTeam))]
    public static class SetTeam_ApplyCustomColor
    {
        [HarmonyPostfix]
        public static void Postfix(UIHUD __instance, PlayerTeam team)
        {
            try
            {
                var bar = _teamColorBarField?.GetValue(__instance) as VisualElement;
                Apply(bar, team);
            }
            catch (Exception e) { Plugin.LogDebug($"TeamIndicator: SetTeam postfix error: {e.Message}"); }
        }
    }
}
