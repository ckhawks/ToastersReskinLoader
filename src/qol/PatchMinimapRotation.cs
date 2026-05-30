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

    [HarmonyPatch(typeof(UIMinimap), "Update")]
    private class PatchUIMinimapUpdate
    {
        private static void Postfix(UIMinimap __instance)
        {
            var cfg = QoLRunner.Instance?.Config;
            if (cfg == null) return;
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
        }
    }
}
