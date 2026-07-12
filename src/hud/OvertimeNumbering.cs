// Numbers successive overtime periods on the scoreboard phase label.
//
// Vanilla Utils.GetHumanizedGamePhase collapses every overtime into the flat
// string "OVERTIME" (Play phase + isOvertime), so a game that runs to a third
// or fourth OT shows the same label the whole way. This postfix rewrites the
// later ones to "OVERTIME 2", "OVERTIME 3", … while leaving the first as plain
// "OVERTIME", matching how broadcasts label them (1OT is just "overtime").
//
// The overtime index is (current period − number of regulation periods). The
// client never receives maxPeriods (GameModeClientConfig only carries
// GoalieCreaseProtection), so we learn it: every non-overtime state carries the
// current regulation period, and the last one seen before OT begins IS the
// regulation-period count. We assign (not max) so a server that reconfigures
// its period count between games re-learns it from that game's regulation play
// instead of holding a stale higher value. If the plugin loads mid-overtime and
// never saw regulation, we fall back to 3 (the vanilla default maxPeriods).
//
// Always-on: no toggle, auto-registered by harmony.PatchAll(). We only touch
// the string when vanilla already produced exactly "OVERTIME", so the Play +
// isOvertime gate comes for free — every other phase/label passes through
// untouched.

using System;
using HarmonyLib;

namespace ToasterReskinLoader.hud;

internal static class OvertimeNumbering
{
    // Vanilla StandardGameModeConfig.maxPeriods default; used until we observe
    // a real regulation period for this game.
    private const int DefaultRegulationPeriods = 3;

    // Last regulation (non-overtime) period number observed. Once OT starts this
    // stops updating, so it holds the regulation-period count for the game.
    private static int _regulationPeriods;

    [HarmonyPatch(typeof(Utils), "GetHumanizedGamePhase")]
    private static class Patch_GetHumanizedGamePhase
    {
        static void Postfix(int period, bool isOvertime, ref string __result)
        {
            try
            {
                // Any non-overtime phase reports the live regulation period.
                if (!isOvertime)
                {
                    if (period > 0) _regulationPeriods = period;
                    return;
                }

                // Only rewrite the plain OT label; every other phase during
                // overtime (FACE-OFF, INTERMISSION, REPLAY, …) keeps its text.
                if (__result != "OVERTIME") return;

                int regulation = _regulationPeriods > 0 ? _regulationPeriods : DefaultRegulationPeriods;
                int overtimeIndex = period - regulation;

                // 1st OT stays "OVERTIME"; number the rest.
                if (overtimeIndex >= 2)
                    __result = $"OVERTIME {overtimeIndex}";
            }
            catch (Exception e) { Plugin.LogWarning("[QoL] OvertimeNumbering postfix failed: " + e.Message); }
        }
    }
}
