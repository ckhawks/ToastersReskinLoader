// GoalFlashSuppression — optionally skip the full-screen team-colored flash the
// game shows when a goal is scored.
//
// Vanilla `UIOverlayManagerController.Event_Everyone_OnGoalScored` fires on the
// goal event and calls `uiOverlay.FlashScreen(...)`, tinting the whole screen
// with the scoring team's color for ~0.8s. Some players find it jarring.
//
// The goal SLOW-MOTION is a completely separate, server-side effect
// (StandardGameMode.Server_StartGoalSlowMotion → GameManager.Server_StartSlowMotion)
// and is deliberately left alone — this only suppresses the client-side flash.
//
// A prefix that returns false skips the original handler entirely when the
// user has opted in, so no flash element is ever created or tweened. Gated on
// the `disableGoalScoredFlash` toggle; default off leaves vanilla untouched.

using System.Reflection;
using HarmonyLib;

using ToasterReskinLoader.core;

namespace ToasterReskinLoader.hud;

// UIOverlayManagerController is internal to the game assembly, so it can't be
// named with typeof — resolve it (and its private handler) by name instead.
[HarmonyPatch]
internal static class GoalFlashSuppression_Prefix
{
    private static MethodBase TargetMethod()
        => AccessTools.Method(AccessTools.TypeByName("UIOverlayManagerController"), "Event_Everyone_OnGoalScored");

    // Return false to skip the vanilla handler (no FlashScreen call) when the
    // user has chosen to disable the goal flash. Returning true runs vanilla.
    private static bool Prefix()
    {
        return !(Settings.Current?.disableGoalScoredFlash ?? false);
    }
}
