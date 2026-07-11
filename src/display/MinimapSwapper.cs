using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ToasterReskinLoader.core;
using UnityEngine;
using UnityEngine.UIElements;

using ToasterReskinLoader.swappers;

namespace ToasterReskinLoader.display;

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
    private static readonly SettingsConfig _fallback = new SettingsConfig();
    private static SettingsConfig Cfg => Settings.Current ?? _fallback;

    private static readonly FieldInfo PlayerMapField = typeof(UIMinimap)
        .GetField("playerBodyVisualElementMap", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo PuckMapField = typeof(UIMinimap)
        .GetField("puckVisualElementMap", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo StickMapField = typeof(UIMinimap)
        .GetField("stickVisualElementMap", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo UpdateRateField = typeof(UIMinimap)
        .GetField("updateRate", BindingFlags.Instance | BindingFlags.NonPublic);

    // Peak scale the puck reaches at max height when elevation is reversed (grow-with-height).
    // Mirrors vanilla's 0.6 shrink floor with a comparable growth on the other side.
    private const float PuckElevationMaxScale = 1.75f;

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

            // Refresh player elements. B1117: playerBodyVisualElementMap is now a
            // (Root, Body) tuple dict; style the Root element.
            var playerMap = (Dictionary<PlayerBody, (VisualElement Root, VisualElement Body)>)PlayerMapField?.GetValue(minimap);
            if (playerMap != null)
            {
                foreach (var kvp in playerMap)
                {
                    if (!kvp.Key || !kvp.Key.Player) continue;
                    ApplyPlayerStyle(kvp.Value.Root, kvp.Key.Player.Team, kvp.Key.Player.IsLocalPlayer, profile);
                }
            }

            // Refresh puck elements
            var puckMap = (Dictionary<Puck, VisualElement>)PuckMapField?.GetValue(minimap);
            if (puckMap != null)
            {
                foreach (var kvp in puckMap)
                    ApplyPuckStyle(kvp.Value, profile);
            }

            // Refresh stick elements
            var stickMap = (Dictionary<Stick, VisualElement>)StickMapField?.GetValue(minimap);
            if (stickMap != null)
            {
                foreach (var kvp in stickMap)
                    ApplyStickStyle(kvp.Value, profile);
            }
        }
        catch (Exception e)
        {
            Plugin.LogDebug($"MinimapSwapper.RefreshAll error: {e.Message}");
        }
    }

    // Scales the stick icon. Vanilla rewrites the .minimapStick element's scale every frame for
    // elevation foreshortening (X only), so we scale its parent instead — vanilla only translates the
    // parent, never scales it, so our uniform scale composes on top and survives. The parent's origin
    // sits on the blade world position, so scaling keeps the icon anchored (still matches the world).
    private static void ApplyStickStyle(VisualElement stickEl, SettingsConfig profile)
    {
        VisualElement parent = stickEl?.parent;
        if (parent == null) return;

        float scale = profile.minimapStickScale;
        if (Math.Abs(scale - 1f) > 0.001f)
            parent.style.scale = new StyleScale(new Scale(new Vector2(scale, scale)));
        else
            parent.style.scale = new StyleScale(new Scale(Vector2.one));
    }

    private static void ApplyPlayerStyle(VisualElement rootEl, PlayerTeam team, bool isLocalPlayer, SettingsConfig profile)
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
            VisualElement bodyEl = rootEl.Q("Body");
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

    private static void ApplyPuckStyle(VisualElement puckEl, SettingsConfig profile)
    {
        if (puckEl == null) return;

        // Puck scale. B1117: UIMinimap.Update rewrites the root element's scale every frame
        // (elevation falloff, or Vector2.one), so scaling the root here is clobbered instantly.
        // Scale the inner .minimapPuck dot instead — the base game never touches it, so our
        // scale composes on top of the root's elevation scale.
        VisualElement dot = puckEl.Q(className: "minimapPuck") ?? puckEl;
        float scale = profile.minimapPuckScale;
        if (Math.Abs(scale - 1f) > 0.001f)
            dot.style.scale = new StyleScale(new Scale(new Vector2(scale, scale)));
        else
            dot.style.scale = new StyleScale(new Scale(Vector2.one));

        // Puck color. B1117: the dot is painter-drawn (UIMinimap.DrawPuckDot fills black), so it
        // has no background image to tint. Override generateVisualContent with our own painter that
        // fills with the configured color, then force a repaint so a settings change takes effect.
        dot.generateVisualContent = DrawPuckDotTinted;
        dot.MarkDirtyRepaint();
    }

    // Mirrors UIMinimap.DrawPuckDot (a single-radius arc stays round regardless of pixel rounding)
    // but fills with the configured minimap puck color instead of hard-coded black.
    private static void DrawPuckDotTinted(MeshGenerationContext context)
    {
        Rect rect = context.visualElement.contentRect;
        if (rect.width <= 0f || rect.height <= 0f)
            return;

        float radius = Mathf.Min(rect.width, rect.height) / 2f;

        Painter2D painter = context.painter2D;
        painter.fillColor = Cfg.minimapPuckColor;
        painter.BeginPath();
        painter.Arc(rect.center, radius, new Angle(0f, AngleUnit.Degree), new Angle(360f, AngleUnit.Degree));
        painter.Fill();
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

                var map = (Dictionary<PlayerBody, (VisualElement Root, VisualElement Body)>)PlayerMapField?.GetValue(__instance);
                if (map == null || !map.ContainsKey(playerBody)) return;

                ApplyPlayerStyle(map[playerBody].Root, playerBody.Player.Team, playerBody.Player.IsLocalPlayer, profile);
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

    [HarmonyPatch(typeof(UIMinimap), nameof(UIMinimap.AddStick))]
    public static class MinimapAddStickPatch
    {
        [HarmonyPostfix]
        public static void Postfix(UIMinimap __instance, Stick stick)
        {
            try
            {
                if (!stick) return;
                var profile = Cfg;
                if (profile == null) return;

                var map = (Dictionary<Stick, VisualElement>)StickMapField?.GetValue(__instance);
                if (map == null || !map.ContainsKey(stick)) return;

                ApplyStickStyle(map[stick], profile);
            }
            catch (Exception e)
            {
                Plugin.LogDebug($"MinimapSwapper.AddStick error: {e.Message}");
            }
        }
    }

    // Reworks the vanilla puck elevation indicator (shrink + fade as the puck rises). Vanilla rewrites
    // the puck root's scale/opacity every Update from height, so we override in a postfix: reverse the
    // size direction (grow with height) and/or drop the fade, per the user's settings. When both are at
    // their defaults (shrink + fade) we do nothing and let vanilla own it.
    [HarmonyPatch(typeof(UIMinimap), "Update")]
    public static class MinimapPuckElevationPatch
    {
        [HarmonyPostfix]
        public static void Postfix(UIMinimap __instance)
        {
            try
            {
                if (!SettingsManager.ShowMinimapPuckElevation) return;
                var profile = Cfg;
                if (profile == null) return;

                bool reverse = profile.minimapPuckElevationReverse;
                bool transparency = profile.minimapPuckElevationTransparency;
                if (!reverse && transparency) return; // matches vanilla — leave it alone

                var map = (Dictionary<Puck, VisualElement>)PuckMapField?.GetValue(__instance);
                if (map == null) return;

                foreach (var kvp in map)
                {
                    Puck puck = kvp.Key;
                    VisualElement root = kvp.Value;
                    if (!puck || root == null) continue;

                    float t = Mathf.Clamp01(
                        Mathf.Abs(puck.transform.position.y) / Constants.MINIMAP_PUCK_MAX_HEIGHT
                    );
                    float scale = reverse
                        ? Mathf.Lerp(1.0f, PuckElevationMaxScale, t)
                        : Mathf.Lerp(1.0f, Constants.MINIMAP_PUCK_MIN_SCALE, t);
                    float opacity = transparency ? Mathf.Lerp(1.0f, Constants.MINIMAP_PUCK_MIN_SCALE, t) : 1.0f;

                    root.style.scale = new StyleScale(new Scale(new Vector2(scale, scale)));
                    root.style.opacity = opacity;
                }
            }
            catch (Exception e)
            {
                Plugin.LogDebug($"MinimapSwapper.PuckElevation error: {e.Message}");
            }
        }
    }
}
