using System;
using System.Collections.Generic;
using HarmonyLib;
using Unity.Netcode;

namespace ToasterReskinLoader;

// Vanilla bug: Player.PlayerPosition (the cached PlayerPosition reference) is
// assigned only in OnPlayerPlayerPositionReferenceChanged, the OnValueChanged
// callback for the PlayerPositionReference NetworkVariable. Unity Netcode does
// NOT fire OnValueChanged on the initial sync to a late-joining client, so for
// any player who picked a position before we joined, our local Player.PlayerPosition
// stays null until that player next changes position. The scoreboard reads this
// field directly (UIScoreboard.StylePlayer), so the position column is blank.
//
// Two-pronged fix to cover both spawn orders on a late joiner:
//   1) Postfix Player.OnNetworkSpawn: if the referenced PlayerPosition is already
//      spawned client-side, resolve it now.
//   2) Listen for Event_Everyone_OnPlayerPositionClaimedByPlayerChanged, which
//      PlayerPosition fires from its own ProcessInitialNetworkVariableValues
//      (OnNetworkPostSpawn). If the player exists by then, fill in its cached
//      PlayerPosition and fire the scoreboard refresh event.
public static class PatchScoreboardLateJoinPosition
{
    static bool listenerRegistered;

    public static void EnsureEventListener()
    {
        if (listenerRegistered) return;
        listenerRegistered = true;
        EventManager.AddEventListener(
            "Event_Everyone_OnPlayerPositionClaimedByPlayerChanged",
            new Action<Dictionary<string, object>>(OnPositionClaimedChanged));
    }

    static void OnPositionClaimedChanged(Dictionary<string, object> message)
    {
        try
        {
            if (!message.TryGetValue("newClaimedByPlayer", out var playerObj)) return;
            var player = playerObj as Player;
            if (player == null) return;

            if (!message.TryGetValue("playerPosition", out var posObj)) return;
            var position = posObj as PlayerPosition;
            if (position == null) return;

            if (player.PlayerPosition == position) return; // already in sync

            player.PlayerPosition = position;
            EventManager.TriggerEvent("Event_Everyone_OnPlayerPositionChanged",
                new Dictionary<string, object>
                {
                    { "player", player },
                    { "oldPlayerPosition", null },
                    { "newPlayerPosition", position }
                });
        }
        catch (Exception e)
        {
            Plugin.LogError($"PatchScoreboardLateJoinPosition listener failed: {e}");
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.OnNetworkSpawn))]
    class PatchPlayerOnNetworkSpawn
    {
        [HarmonyPostfix]
        static void Postfix(Player __instance)
        {
            try
            {
                EnsureEventListener();

                if (__instance == null) return;
                if (__instance.PlayerPosition != null) return;
                var nvRef = __instance.PlayerPositionReference;
                if (nvRef == null) return;

                NetworkObjectReference reference = nvRef.Value;
                if (reference.TryGet(out NetworkObject networkObject, null))
                {
                    var pos = networkObject.GetComponent<PlayerPosition>();
                    if (pos != null)
                    {
                        __instance.PlayerPosition = pos;
                        EventManager.TriggerEvent("Event_Everyone_OnPlayerPositionChanged",
                            new Dictionary<string, object>
                            {
                                { "player", __instance },
                                { "oldPlayerPosition", null },
                                { "newPlayerPosition", pos }
                            });
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.LogError($"PatchScoreboardLateJoinPosition postfix failed: {e}");
            }
        }
    }
}
