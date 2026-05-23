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

    private static Color GetTeamColor(PlayerTeam team)
    {
        // Use the user's custom team color when custom team colors are enabled;
        // otherwise fall back to the game's vanilla team color so usernames stay
        // team-colored and match the rest of the UI when overrides are off.
        return TeamColorSwapper.GetOverrideColor(team) ?? TeamColorSwapper.GetDefaultTeamColor(team);
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
