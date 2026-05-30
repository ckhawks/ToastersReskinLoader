using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace ToasterReskinLoader;

// Vanilla bugs (b323) around the HUD speed/units display:
//
// 1. UIHUDController only calls UIHUD.SetUnits() inside Event_OnUnitsChanged,
//    which fires when the user toggles the setting. At startup the saved value
//    is never applied, so the speed label keeps the UXML default ("KPH") even
//    when the saved unit is Imperial. Postfix on Event_Everyone_OnPlayerBodySpawned
//    (where vanilla also calls Show/SetStamina) applies the saved value each
//    time the local player spawns.
//
// 2. Event_OnUnitsChanged only calls SetUnits, never SetSpeed. UIHUD.SetSpeed
//    reads SettingsManager.Units live and converts properly, but it's only
//    invoked from Event_Everyone_OnPlayerBodySpeedChanged. If the user toggles
//    units while not actively moving (menu, paused, standing still), the label
//    updates but the displayed number stays in the old scale until the next
//    speed change event. We cache the last raw speed via a SetSpeed prefix and
//    replay it whenever units change, so the value refreshes instantly.
public static class PatchUnitsLabelInit
{
    private static float lastRawSpeed;
    private static bool hasLastSpeed;

    [HarmonyPatch(typeof(UIHUDController), "Event_Everyone_OnPlayerBodySpawned")]
    class PatchPlayerBodySpawned
    {
        [HarmonyPostfix]
        static void Postfix(UIHUDController __instance, Dictionary<string, object> message)
        {
            try
            {
                PlayerBody playerBody = (PlayerBody)message["playerBody"];
                if (!playerBody.Player.IsLocalPlayer) return;

                var field = typeof(UIHUDController).GetField(
                    "uiHud",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (field?.GetValue(__instance) is UIHUD ui)
                {
                    string label = SettingsManager.Units == Units.Imperial ? "MPH" : "KPH";
                    ui.SetUnits(label);
                }
            }
            catch (Exception e)
            {
                Plugin.LogError($"PatchUnitsLabelInit failed: {e}");
            }
        }
    }

    [HarmonyPatch(typeof(UIHUD), "SetSpeed")]
    class CacheLastSpeed
    {
        [HarmonyPrefix]
        static void Prefix(float value)
        {
            lastRawSpeed = value;
            hasLastSpeed = true;
        }
    }

    [HarmonyPatch(typeof(UIHUDController), "Event_OnUnitsChanged")]
    class RefreshSpeedOnUnitsChanged
    {
        [HarmonyPostfix]
        static void Postfix(UIHUDController __instance)
        {
            try
            {
                if (!hasLastSpeed) return;

                var field = typeof(UIHUDController).GetField(
                    "uiHud",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (field?.GetValue(__instance) is UIHUD ui)
                {
                    ui.SetSpeed(lastRawSpeed);
                }
            }
            catch (Exception e)
            {
                Plugin.LogError($"PatchUnitsLabelInit (units changed) failed: {e}");
            }
        }
    }
}
