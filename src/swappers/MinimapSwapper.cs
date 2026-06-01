using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ToasterReskinLoader.qol;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.swappers;

/// <summary>
/// Customizes minimap appearance: player number text color (per-team),
/// puck icon color, player icon scale, and puck icon scale.
/// Minimap player element structure: Player > Body > Local, NumberLabel
/// Puck elements are stored in puckVisualElementMap.
/// </summary>
public static class MinimapSwapper
{
    // Minimap settings live in the QoL profile now (HUD). Non-null fallback so the per-frame
    // tinting code can't NRE before the QoL runner has bootstrapped.
    private static readonly QoLConfig _fallback = new QoLConfig();
    private static QoLConfig Cfg => QoLRunner.Instance?.Config ?? _fallback;

    private static readonly FieldInfo PlayerMapField = typeof(UIMinimap)
        .GetField("playerBodyVisualElementMap", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo PuckMapField = typeof(UIMinimap)
        .GetField("puckVisualElementMap", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo UpdateRateField = typeof(UIMinimap)
        .GetField("updateRate", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    /// Applies the configured refresh rate to the minimap's update loop.
    /// </summary>
    public static void ApplyRefreshRate()
    {
        try
        {
            var uiManager = MonoBehaviourSingleton<UIManager>.Instance;
            if (uiManager == null) return;
            var minimap = uiManager.Minimap;
            if (minimap == null) return;

            int rate = Cfg.minimapRefreshRate;
            UpdateRateField?.SetValue(minimap, rate);
        }
        catch (Exception e)
        {
            Plugin.LogDebug($"MinimapSwapper.ApplyRefreshRate error: {e.Message}");
        }
    }

    /// <summary>
    /// Re-applies all minimap customizations to currently visible elements.
    /// </summary>
    public static void RefreshAll()
    {
        try
        {
            var uiManager = MonoBehaviourSingleton<UIManager>.Instance;
            if (uiManager == null) return;
            var minimap = uiManager.Minimap;
            if (minimap == null) return;
            var profile = Cfg;
            if (profile == null) return;

            // Refresh player elements
            var playerMap = (Dictionary<PlayerBody, VisualElement>)PlayerMapField?.GetValue(minimap);
            if (playerMap != null)
            {
                foreach (var kvp in playerMap)
                {
                    if (!kvp.Key || !kvp.Key.Player) continue;
                    ApplyPlayerStyle(kvp.Value, kvp.Key.Player.Team, kvp.Key.Player.IsLocalPlayer, profile);
                }
            }

            // Refresh puck elements
            var puckMap = (Dictionary<Puck, VisualElement>)PuckMapField?.GetValue(minimap);
            if (puckMap != null)
            {
                foreach (var kvp in puckMap)
                    ApplyPuckStyle(kvp.Value, profile);
            }
        }
        catch (Exception e)
        {
            Plugin.LogDebug($"MinimapSwapper.RefreshAll error: {e.Message}");
        }
    }

    private static void ApplyPlayerStyle(VisualElement rootEl, PlayerTeam team, bool isLocalPlayer, QoLConfig profile)
    {
        if (rootEl == null) return;

        VisualElement playerEl = rootEl.Q("Player");
        if (playerEl == null) return;

        // Number text color
        var numberLabel = playerEl.Q<Label>("NumberLabel");
        if (numberLabel != null)
        {
            Color textColor = team == PlayerTeam.Red ? profile.redMinimapNumberColor : profile.blueMinimapNumberColor;
            numberLabel.style.color = textColor;
        }

        // Local player icon color override
        if (isLocalPlayer && profile.localPlayerMinimapIconEnabled)
        {
            VisualElement bodyEl = playerEl.Q("Body");
            if (bodyEl != null)
            {
                Color iconColor = team == PlayerTeam.Red
                    ? profile.redLocalPlayerMinimapIconColor
                    : profile.blueLocalPlayerMinimapIconColor;
                bodyEl.style.unityBackgroundImageTintColor = iconColor;
            }
        }

        // Player icon scale
        float scale = profile.minimapPlayerScale;
        if (Math.Abs(scale - 1f) > 0.001f)
            playerEl.style.scale = new StyleScale(new Scale(new Vector2(scale, scale)));
        else
            playerEl.style.scale = new StyleScale(new Scale(Vector2.one));
    }

    private static void ApplyPuckStyle(VisualElement puckEl, QoLConfig profile)
    {
        if (puckEl == null) return;

        // Puck scale
        float scale = profile.minimapPuckScale;
        if (Math.Abs(scale - 1f) > 0.001f)
            puckEl.style.scale = new StyleScale(new Scale(new Vector2(scale, scale)));
        else
            puckEl.style.scale = new StyleScale(new Scale(Vector2.one));

        // Puck color — try tinting the deepest child with a background image,
        // falling back to the root element itself.
        ApplyTintRecursive(puckEl, profile.minimapPuckColor);
    }

    private static bool _puckStructureLogged;

    private static void ApplyTintRecursive(VisualElement el, Color color)
    {
        if (!_puckStructureLogged)
        {
            LogElementTree(el, 0);
            _puckStructureLogged = true;
        }

        // Apply tint to this element and all children
        el.style.unityBackgroundImageTintColor = color;
        foreach (var child in el.Children())
            ApplyTintRecursive(child, color);
    }

    private static void LogElementTree(VisualElement el, int depth)
    {
        string indent = new string(' ', depth * 2);
        string bg = el.resolvedStyle.backgroundImage.texture != null ? " [has bg image]" : "";
        Plugin.Log($"[Minimap Puck] {indent}{el.GetType().Name} name='{el.name}' classes='{string.Join(",", el.GetClasses())}'{bg}");
        foreach (var child in el.Children())
            LogElementTree(child, depth + 1);
    }

    // ── Harmony Patches ─────────────────────────────────────────────────

    [HarmonyPatch(typeof(UIMinimap), nameof(UIMinimap.StylePlayer))]
    public static class MinimapStylePlayerPatch
    {
        [HarmonyPostfix]
        public static void Postfix(UIMinimap __instance, PlayerBody playerBody)
        {
            try
            {
                if (!playerBody || !playerBody.Player) return;
                var profile = Cfg;
                if (profile == null) return;

                var map = (Dictionary<PlayerBody, VisualElement>)PlayerMapField?.GetValue(__instance);
                if (map == null || !map.ContainsKey(playerBody)) return;

                ApplyPlayerStyle(map[playerBody], playerBody.Player.Team, playerBody.Player.IsLocalPlayer, profile);
            }
            catch (Exception e)
            {
                Plugin.LogDebug($"MinimapSwapper.StylePlayer error: {e.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(UIMinimap), nameof(UIMinimap.AddPuck))]
    public static class MinimapAddPuckPatch
    {
        [HarmonyPostfix]
        public static void Postfix(UIMinimap __instance, Puck puck)
        {
            try
            {
                if (!puck) return;
                var profile = Cfg;
                if (profile == null) return;

                var map = (Dictionary<Puck, VisualElement>)PuckMapField?.GetValue(__instance);
                if (map == null || !map.ContainsKey(puck)) return;

                ApplyPuckStyle(map[puck], profile);
            }
            catch (Exception e)
            {
                Plugin.LogDebug($"MinimapSwapper.AddPuck error: {e.Message}");
            }
        }
    }
}
