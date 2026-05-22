// PatchPlayerUsernameColors.cs
//
// Re-colors the floating world-space player username labels by team. StyleUsername
// is called by vanilla when a player is added; we postfix it to apply our color.
// Update is also postfixed (cheap) so a team swap or a setting toggle takes
// effect on the next minimap tick without waiting for a respawn.

using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ToasterReskinLoader.swappers;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.qol;

public static class PatchPlayerUsernameColors
{
    private static readonly FieldInfo _mapField = typeof(UIUsernames)
        .GetField("playerBodyVisualElementMap", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly MethodInfo _getOverrideColor = typeof(TeamColorSwapper)
        .GetMethod("GetOverrideColor", BindingFlags.Static | BindingFlags.NonPublic);

    private static Color GetTeamColor(PlayerTeam team)
    {
        // Try TRL's override first; fall back to the profile's default team color
        // so behavior matches the rest of the team-colored UI when overrides are off.
        var profile = ReskinProfileManager.currentProfile;
        if (_getOverrideColor != null)
        {
            var maybe = _getOverrideColor.Invoke(null, new object[] { team }) as Color?;
            if (maybe.HasValue) return maybe.Value;
        }
        if (profile != null)
        {
            if (team == PlayerTeam.Blue) return profile.blueTeamColor;
            if (team == PlayerTeam.Red)  return profile.redTeamColor;
        }
        return Color.white;
    }

    private static void ApplyTo(VisualElement playerVisualElement, PlayerBody playerBody)
    {
        if (playerVisualElement == null || playerBody == null || playerBody.Player == null) return;

        var label = playerVisualElement.Q<Label>("UsernameLabel");
        if (label == null) return;

        var cfg = QoLRunner.Instance?.Config;
        if (cfg == null || !cfg.enablePlayerUsernameTeamColors)
        {
            // Restore vanilla — clear our override.
            label.style.color = new StyleColor(StyleKeyword.Null);
            return;
        }

        var team = playerBody.Player.Team;
        if (team != PlayerTeam.Blue && team != PlayerTeam.Red)
        {
            label.style.color = new StyleColor(StyleKeyword.Null);
            return;
        }

        label.style.color = GetTeamColor(team);
    }

    [HarmonyPatch(typeof(UIUsernames), nameof(UIUsernames.StyleUsername))]
    private class PatchStyleUsername
    {
        private static void Postfix(UIUsernames __instance, PlayerBody playerBody)
        {
            if (_mapField == null) return;
            if (_mapField.GetValue(__instance) is not Dictionary<PlayerBody, VisualElement> map) return;
            if (!map.TryGetValue(playerBody, out var ve)) return;
            ApplyTo(ve, playerBody);
        }
    }

    [HarmonyPatch(typeof(UIUsernames), "Update")]
    private class PatchUpdate
    {
        private static void Postfix(UIUsernames __instance)
        {
            if (_mapField == null) return;
            if (_mapField.GetValue(__instance) is not Dictionary<PlayerBody, VisualElement> map) return;
            foreach (var kvp in map)
                if (kvp.Key != null) ApplyTo(kvp.Value, kvp.Key);
        }
    }
}
