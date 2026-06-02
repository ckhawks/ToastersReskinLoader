// AutoConnectMatchmaking — when a matchmaking match becomes READY (endpoint
// assigned, not yet connected), automatically fire the same event the
// vanilla "Connect" button would, instead of waiting for the user to click.
//
// Vanilla wiring:
//   * UIMatchmakingController.UpdateMatching() shows the Connect button when
//       BackendManager.PlayerState.MatchData != null
//       && MatchData.endPoint != null
//       && !BackendUtils.IsConnectedToMatchEndPoint()
//   * UIMatchmaking.OnClickMatchingConnect() fires
//       EventManager.TriggerEvent("Event_OnMatchmakingMatchingClickConnect")
//
// We listen for Event_OnPlayerMatchDataChanged (same trigger UpdateMatching
// uses), evaluate the same condition, and trigger the connect event when it
// flips true. A "did we already auto-fire for this match" guard prevents
// double-fires if the event ticks multiple times before disconnect/cancel,
// and resets when MatchData clears.

using System;
using System.Collections.Generic;

using ToasterReskinLoader.core;

namespace ToasterReskinLoader.qol;

public static class AutoConnectMatchmaking
{
    private static bool _subscribed;
    private static bool _firedForCurrentMatch;

    // Cached so AddEventListener and RemoveEventListener see the same delegate
    // instance — EventManager's underlying storage keys by delegate identity.
    private static readonly Action<Dictionary<string, object>> _onMatchDataChanged = OnMatchDataChanged;
    private static readonly Action<Dictionary<string, object>> _onConnectionStateChanged = OnConnectionStateChanged;

    public static void Enable()
    {
        if (_subscribed) return;
        _subscribed = true;
        EventManager.AddEventListener("Event_OnPlayerMatchDataChanged", _onMatchDataChanged);
        EventManager.AddEventListener("Event_OnConnectionStateChanged", _onConnectionStateChanged);
        Plugin.Log("[AutoConnectMatchmaking] Enabled");
    }

    public static void Disable()
    {
        if (!_subscribed) return;
        _subscribed = false;
        EventManager.RemoveEventListener("Event_OnPlayerMatchDataChanged", _onMatchDataChanged);
        EventManager.RemoveEventListener("Event_OnConnectionStateChanged", _onConnectionStateChanged);
        _firedForCurrentMatch = false;
        Plugin.Log("[AutoConnectMatchmaking] Disabled");
    }

    private static void OnMatchDataChanged(Dictionary<string, object> _)
    {
        TryAutoConnect();
    }

    private static void OnConnectionStateChanged(Dictionary<string, object> _)
    {
        // Re-arm so a subsequent match can auto-connect again.
        if (BackendUtils.IsConnectedToMatchEndPoint() ||
            BackendManager.PlayerState.MatchData == null)
        {
            _firedForCurrentMatch = false;
        }
    }

    private static void TryAutoConnect()
    {
        var matchData = BackendManager.PlayerState.MatchData;
        if (matchData == null)
        {
            _firedForCurrentMatch = false;
            return;
        }

        // Same readiness gate as UIMatchmakingController.UpdateMatching.
        if (matchData.endPoint == null) return;
        if (BackendUtils.IsConnectedToMatchEndPoint()) return;
        if (_firedForCurrentMatch) return;

        _firedForCurrentMatch = true;
        Plugin.Log("[AutoConnectMatchmaking] Match ready — firing auto-connect");
        try
        {
            EventManager.TriggerEvent("Event_OnMatchmakingMatchingClickConnect", null);
        }
        catch (Exception ex)
        {
            Plugin.LogError($"[AutoConnectMatchmaking] connect event failed: {ex.Message}");
            _firedForCurrentMatch = false;
        }
    }
}
