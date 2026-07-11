// AnnouncementHideGuard — the goal announcement ("BLUE SCORES!") banner is shown
// by UIAnnouncements.ShowScore and, in the shipped game, only hidden when the
// score game phase transitions to the next phase. Two ways it can linger on
// screen:
//   * the client leaves the server mid-score-phase, so the transition that would
//     hide it never arrives, or
//   * the phase (a coalescing NetworkVariable) skips past the score phase, so the
//     exact score->next edge the vanilla hide waits on is never observed.
//
// Mirror of the vanilla PUCK-321 fix: clear the banner on client stop and on any
// state change into a non-score phase. Subscribes to the events directly rather
// than patching a controller handler so it works regardless of which game build
// the mod loads against.

using System;
using System.Collections.Generic;

using ToasterReskinLoader.core;

namespace ToasterReskinLoader.hud;

public static class AnnouncementHideGuard
{
    private static bool _subscribed;

    // Cached so AddEventListener and RemoveEventListener see the same delegate
    // instance — EventManager keys its storage by delegate identity.
    private static readonly Action<Dictionary<string, object>> _onClientStopped = OnClientStopped;
    private static readonly Action<Dictionary<string, object>> _onGameStateChanged = OnGameStateChanged;

    public static void Enable()
    {
        if (_subscribed) return;
        _subscribed = true;
        EventManager.AddEventListener("Event_OnClientStopped", _onClientStopped);
        EventManager.AddEventListener("Event_Everyone_OnGameStateChanged", _onGameStateChanged);
        Plugin.Log("[AnnouncementHideGuard] Enabled");
    }

    public static void Disable()
    {
        if (!_subscribed) return;
        _subscribed = false;
        EventManager.RemoveEventListener("Event_OnClientStopped", _onClientStopped);
        EventManager.RemoveEventListener("Event_Everyone_OnGameStateChanged", _onGameStateChanged);
        Plugin.Log("[AnnouncementHideGuard] Disabled");
    }

    private static void OnClientStopped(Dictionary<string, object> _)
    {
        HideScore();
    }

    private static void OnGameStateChanged(Dictionary<string, object> message)
    {
        if (message == null) return;
        if (!(message.TryGetValue("newGameState", out var nObj) && nObj is GameState newState)) return;

        bool isScorePhase = newState.Phase == GamePhase.BlueScore || newState.Phase == GamePhase.RedScore;
        if (!isScorePhase)
            HideScore();
    }

    private static void HideScore()
    {
        try
        {
            UIManager.Instance?.Announcements?.HideScore();
        }
        catch (Exception e)
        {
            Plugin.LogDebug($"[AnnouncementHideGuard] HideScore failed: {e.Message}");
        }
    }
}
