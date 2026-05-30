using System;
using HarmonyLib;

namespace ToasterReskinLoader;

// Vanilla bug (b323): UIManager.ShowPhaseViews() calls HideAllViews() on every
// phase transition, then only re-Shows a hard-coded subset (Chat, GameState,
// Announcements). Usernames isn't in that subset, so it stays hidden until
// the user toggles "Show Player Usernames" off and on. This postfix re-shows it
// whenever the setting is enabled.
public static class PatchUsernamesInit
{
    [HarmonyPatch(typeof(UIManager), "ShowPhaseViews")]
    class PatchShowPhaseViews
    {
        [HarmonyPostfix]
        static void Postfix(UIManager __instance)
        {
            try
            {
                if (!SettingsManager.ShowPlayerUsernames) return;
                if (__instance?.Usernames == null) return;

                __instance.Usernames.Show();
            }
            catch (Exception e)
            {
                Plugin.LogError($"PatchUsernamesInit failed: {e}");
            }
        }
    }
}
