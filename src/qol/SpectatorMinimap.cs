// Re-show the minimap for spectators.
//
// Vanilla `UIManagerController.Event_Everyone_OnPlayerGameStateChanged` only
// calls `Minimap.Show()` / `Hud.Show()` when the local player enters
// `PlayerPhase.Play`. Spectators (Team == Spectator, often PlayerPhase.Spectate)
// never get the minimap shown, which is bad for streamers and people setting
// up a config from the spectator slot.
//
// Postfix re-shows the minimap whenever the LOCAL player ends up on the
// Spectator team after a state change. `Show()` is idempotent so re-firing
// on unchanged-phase transitions is cheap.

using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ToasterReskinLoader.qol;

[HarmonyPatch(typeof(UIManagerController), "Event_Everyone_OnPlayerGameStateChanged")]
internal static class SpectatorMinimap_Postfix
{
    private static void Postfix(Dictionary<string, object> message)
    {
        if (!(QoLRunner.Instance?.Config?.enableSpectatorMinimap ?? false)) return;
        try
        {
            if (message == null) return;
            if (!(message.TryGetValue("player", out var pObj) && pObj is Player player)) return;
            if (!player.IsLocalPlayer) return;
            if (!(message.TryGetValue("newGameState", out var nObj) && nObj is PlayerGameState newState)) return;

            bool isSpectator = newState.Team == PlayerTeam.Spectator
                            || newState.Phase == PlayerPhase.Spectate;
            if (!isSpectator) return;

            var ui = MonoBehaviourSingleton<UIManager>.Instance;
            ui?.Minimap?.Show();
        }
        catch (Exception e) { Debug.LogWarning("[QoL] spectator-minimap show failed: " + e.Message); }
    }
}