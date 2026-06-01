// PatchMinimapRotation.cs
//
// Optional minimap rotation modes:
//   "off"           — vanilla, no rotation
//   "rotate90"      — fixed 90° turn (useful when most action is along the X axis)
//   "followPlayer"  — minimap continuously yaws so the local player's facing is "up"
//
// Hooked to UIMinimap.Update so the rotation is re-applied every minimap tick
// (only ~30Hz given the vanilla update accumulator). Per-tick is required for
// followPlayer; rotate90 could be one-shot but reusing the same hook keeps the
// behavior consistent (mode changes in the menu apply immediately, no restart).

using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ToasterReskinLoader.qol;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.qol;

public static class PatchMinimapRotation
{
    private static readonly FieldInfo _minimapField = typeof(UIMinimap)
        .GetField("minimap", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo _playerMapField = typeof(UIMinimap)
        .GetField("playerBodyVisualElementMap", BindingFlags.Instance | BindingFlags.NonPublic);

    // Drop references to UIMinimap instances destroyed by a scene change so the
    // tracking set doesn't accumulate stale entries.
    public static void ResetTracking() => PatchUIMinimapUpdate._rotated.Clear();

    [HarmonyPatch(typeof(UIMinimap), "Update")]
    private class PatchUIMinimapUpdate
    {
        // Instances we've rotated. While rotation is off and we haven't touched
        // an instance, skip the per-frame reflection + per-label UQuery. When
        // it's switched back to "off" we run one more pass (deg=0) to reset the
        // minimap + labels to identity, then drop the instance and go quiet.
        internal static readonly HashSet<UIMinimap> _rotated = new HashSet<UIMinimap>();

        private static void Postfix(UIMinimap __instance)
        {
            var cfg = QoLRunner.Instance?.Config;
            if (cfg == null) return;

            bool active = cfg.minimapRotationMode == "rotate90" ||
                          cfg.minimapRotationMode == "followPlayer";
            if (!active && !_rotated.Contains(__instance)) return;

            if (_minimapField == null) return;
            if (_minimapField.GetValue(__instance) is not VisualElement minimap) return;

            float deg;
            switch (cfg.minimapRotationMode)
            {
                case "rotate90":
                    deg = -90f;
                    break;
                case "followPlayer":
                {
                    var local = PlayerManager.Instance != null ? PlayerManager.Instance.GetLocalPlayer() : null;
                    var body = local != null ? local.PlayerBody : null;
                    if (body == null) { deg = 0f; break; }
                    // UIMinimap mirrors world positions for Red, so the on-map yaw
                    // is the player's world yaw, with +180° for Red. Rotate the
                    // minimap by the negation so that yaw points "up" on screen.
                    var yaw = body.transform.rotation.eulerAngles.y;
                    if (__instance.Team == PlayerTeam.Red) yaw += 180f;
                    deg = -yaw;
                    break;
                }
                default:
                    deg = 0f;
                    break;
            }

            minimap.style.rotate = new Rotate(new Angle(deg, AngleUnit.Degree));

            // Counter-rotate the player number labels so they stay upright. The
            // labels are descendants of the rotated minimap container, so without
            // this they pick up the parent rotation and read sideways.
            if (_playerMapField != null &&
                _playerMapField.GetValue(__instance) is IDictionary<PlayerBody, VisualElement> playerMap)
            {
                foreach (var kvp in playerMap)
                {
                    if (kvp.Value == null) continue;
                    var label = kvp.Value.Q<Label>("NumberLabel");
                    if (label != null)
                        label.style.rotate = new Rotate(new Angle(-deg, AngleUnit.Degree));
                }
            }

            if (active) _rotated.Add(__instance);
            else _rotated.Remove(__instance);
        }
    }
}
