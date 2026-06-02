// PatchMinimapRotation.cs
//
// Optional minimap rotation modes:
//   "off"           — vanilla, no rotation
//   "rotate90"      — fixed 90° turn (useful when most action is along the X axis)
//   "followPlayer"  — the map pans so the local player sits at the centre of the minimap
//                     window and yaws so the player's facing is "up"
//
// rotate90 turns the whole widget by rotating the `minimap` element (vanilla never
// touches its rotate, so there's no conflict). followPlayer instead transforms the
// inner map layers (background + content) so the widget stays where the player's
// minimap-position settings put it — vanilla's SetPosition owns minimap.translate /
// transformOrigin for screen placement, so we must not clobber them.
//
// Hooked to UIMinimap.Update so the transform is re-applied every minimap tick
// (only ~30Hz given the vanilla update accumulator). Per-tick is required since the
// player moves; mode changes in the menu apply immediately, no restart.

using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ToasterReskinLoader.core;
using UnityEngine;
using UnityEngine.UIElements;

using ToasterReskinLoader.swappers;

namespace ToasterReskinLoader.display;

public static class PatchMinimapRotation
{
    private static readonly FieldInfo _minimapField = typeof(UIMinimap)
        .GetField("minimap", BindingFlags.Instance | BindingFlags.NonPublic);

    // The three map layers that must pan/rotate together in followPlayer mode: the plain
    // backing, the rink lines/markings, and the icon container.
    private static readonly FieldInfo _backgroundField = typeof(UIMinimap)
        .GetField("background", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo _foregroundField = typeof(UIMinimap)
        .GetField("foreground", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo _contentField = typeof(UIMinimap)
        .GetField("content", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo _playerMapField = typeof(UIMinimap)
        .GetField("playerBodyVisualElementMap", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly Rotate _identityRotate = new Rotate(new Angle(0f, AngleUnit.Degree));

    // Drop references to UIMinimap instances destroyed by a scene change so the
    // tracking set doesn't accumulate stale entries.
    public static void ResetTracking() => PatchUIMinimapUpdate._rotated.Clear();

    [HarmonyPatch(typeof(UIMinimap), "Update")]
    private class PatchUIMinimapUpdate
    {
        // Instances we've transformed. While rotation is off and we haven't touched
        // an instance, skip the per-frame reflection. When switched back to "off" we
        // run one more pass to reset the layers/labels to identity, then drop it.
        internal static readonly HashSet<UIMinimap> _rotated = new HashSet<UIMinimap>();

        private static void Postfix(UIMinimap __instance)
        {
            var cfg = SettingsRunner.Instance?.Config;
            if (cfg == null) return;

            string mode = cfg.minimapRotationMode;
            bool active = mode == "rotate90" || mode == "followPlayer";
            if (!active && !_rotated.Contains(__instance)) return;

            if (_minimapField?.GetValue(__instance) is not VisualElement minimap) return;
            var content = _contentField?.GetValue(__instance) as VisualElement;
            var background = _backgroundField?.GetValue(__instance) as VisualElement;
            var foreground = _foregroundField?.GetValue(__instance) as VisualElement;

            if (mode == "followPlayer" &&
                TryComputeFollow(__instance, content, out float deg, out Translate translate, out TransformOrigin origin))
            {
                // Pan + rotate the map layers around the local player; leave `minimap`
                // alone so its screen-placement transform (SetPosition) is preserved.
                minimap.style.rotate = _identityRotate;
                minimap.style.overflow = Overflow.Hidden; // clip the map bleeding past the window
                ApplyLayerTransform(content, deg, translate, origin);
                ApplyLayerTransform(background, deg, translate, origin);
                ApplyLayerTransform(foreground, deg, translate, origin);
                CounterRotateLabels(__instance, deg);
                _rotated.Add(__instance);
                return;
            }

            if (mode == "rotate90")
            {
                // Whole-widget 90° turn applied to `minimap` (vanilla never sets its rotate).
                ResetLayerTransform(content);
                ResetLayerTransform(background);
                ResetLayerTransform(foreground);
                minimap.style.overflow = Overflow.Visible;
                minimap.style.rotate = new Rotate(new Angle(-90f, AngleUnit.Degree));
                CounterRotateLabels(__instance, -90f);
                _rotated.Add(__instance);
                return;
            }

            // "off", or followPlayer before the local player exists: reset anything we set.
            ResetLayerTransform(content);
            ResetLayerTransform(background);
            ResetLayerTransform(foreground);
            minimap.style.rotate = _identityRotate;
            minimap.style.overflow = Overflow.Visible;
            CounterRotateLabels(__instance, 0f);
            _rotated.Remove(__instance);
        }

        // Builds the transform that centres the local player and points their facing "up".
        // Mirrors UIMinimap.WorldPositionToMinimapPosition so the player's map point is
        // computed exactly as the game places the icon.
        private static bool TryComputeFollow(UIMinimap inst, VisualElement content,
            out float deg, out Translate translate, out TransformOrigin origin)
        {
            deg = 0f; translate = default; origin = default;
            if (content == null) return false;

            var local = PlayerManager.Instance != null ? PlayerManager.Instance.GetLocalPlayer() : null;
            var body = local != null ? local.PlayerBody : null;
            if (body == null) return false;

            float w = content.resolvedStyle.width;
            float h = content.resolvedStyle.height;
            if (float.IsNaN(w) || float.IsNaN(h) || w <= 0f || h <= 0f) return false;

            var bounds = inst.Bounds;
            if (bounds.size.x == 0f || bounds.size.z == 0f) return false;

            // UIMinimap mirrors world positions for Red, so do the same here.
            Vector3 pos = inst.Team == PlayerTeam.Blue ? body.transform.position : -body.transform.position;
            float yaw = body.transform.rotation.eulerAngles.y;
            if (inst.Team == PlayerTeam.Red) yaw += 180f;
            // +180 over the naive -yaw: with the map upright the local player's arrow points
            // "down", so we turn an extra half-turn to bring their facing to the top.
            deg = 180f - yaw;

            float normX = (pos.x + bounds.center.x) / bounds.size.x;
            float normZ = (pos.z + bounds.center.z) / bounds.size.z;
            float pixelX = w * normX;
            float pixelY = h * normZ;

            // The icon's base position is the layer centre, offset by (-pixelX, +pixelY)
            // (the translate the game applies). That offset point is where the player is.
            float px = w * 0.5f - pixelX;
            float py = h * 0.5f + pixelY;

            // Pivot the rotation on the player, then shift the player point to the window
            // centre: translate = centre - playerPoint = (pixelX, -pixelY).
            origin = new TransformOrigin(new Length(px), new Length(py));
            translate = new Translate(new Length(pixelX), new Length(-pixelY));
            return true;
        }

        private static void ApplyLayerTransform(VisualElement el, float deg, Translate translate, TransformOrigin origin)
        {
            if (el == null) return;
            el.style.transformOrigin = origin;
            el.style.translate = translate;
            el.style.rotate = new Rotate(new Angle(deg, AngleUnit.Degree));
        }

        private static void ResetLayerTransform(VisualElement el)
        {
            if (el == null) return;
            el.style.rotate = _identityRotate;
            el.style.translate = new Translate(new Length(0f), new Length(0f));
            el.style.transformOrigin = new TransformOrigin(Length.Percent(50f), Length.Percent(50f));
        }

        // Counter-rotate the player number labels so they stay upright regardless of the
        // map/widget rotation (they're descendants of the rotated element).
        private static void CounterRotateLabels(UIMinimap inst, float deg)
        {
            if (_playerMapField?.GetValue(inst) is not IDictionary<PlayerBody, VisualElement> playerMap) return;
            foreach (var kvp in playerMap)
            {
                if (kvp.Value == null) continue;
                var label = kvp.Value.Q<Label>("NumberLabel");
                if (label != null)
                    label.style.rotate = new Rotate(new Angle(-deg, AngleUnit.Degree));
            }
        }
    }
}
