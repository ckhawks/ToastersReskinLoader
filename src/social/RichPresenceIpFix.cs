using System;
using HarmonyLib;
using Steamworks;

namespace ToasterReskinLoader.social;

/// <summary>
/// Fixes Puck's empty-IP Steam rich presence. Puck builds its "playing"/"spectating"
/// presence from the networked Server struct, whose IpAddress the host never populates
/// (ServerManager.IpAddress is declared but never assigned anywhere in the game) — so
/// steam_player_group and connect ship with an EMPTY ip (e.g. ":30609",
/// "+ipAddress  +port 30609"). That breaks Steam "Join Game" and makes a friend's
/// server unresolvable.
///
/// A CLIENT, however, knows the real address it dialed
/// (GlobalStateManager.ConnectionState.Connection.EndPoint). The postfix runs right
/// after the game sets its (broken) presence and overwrites the two IP-bearing keys
/// with that real endpoint. Only fixes the local player's OWN broadcast, so friends
/// see a resolvable server only if they also run this mod. Listen-server hosts have no
/// client connection and are left as-is (they have no public IP to advertise).
///
/// Standalone (gated by <c>enableServerIpPresenceFix</c>, default on) so it works even
/// when the Better Friends List feature is off; it used to live inside that feature.
/// </summary>
public static class RichPresenceIpFix
{
    private static readonly Harmony _harmony = new Harmony(Plugin.MOD_GUID + ".rppresenceip");

    public static bool IsEnabled { get; private set; }

    public static void Enable()
    {
        if (IsEnabled) return;
        _harmony.Patch(
            AccessTools.Method(typeof(SteamIntegrationManager), nameof(SteamIntegrationManager.SetRichPresencePlaying)),
            postfix: new HarmonyMethod(typeof(RichPresenceIpFix), nameof(Postfix)));
        _harmony.Patch(
            AccessTools.Method(typeof(SteamIntegrationManager), nameof(SteamIntegrationManager.SetRichPresenceSpectating)),
            postfix: new HarmonyMethod(typeof(RichPresenceIpFix), nameof(Postfix)));
        IsEnabled = true;
    }

    public static void Disable()
    {
        if (!IsEnabled) return;
        _harmony.UnpatchSelf();
        IsEnabled = false;
    }

    private static void Postfix()
    {
        try
        {
            var ep = GlobalStateManager.ConnectionState.Connection?.EndPoint;
            if (ep == null || string.IsNullOrEmpty(ep.ipAddress))
                return;

            SteamFriends.SetRichPresence("steam_player_group", $"{ep.ipAddress}:{ep.port}");
            SteamFriends.SetRichPresence("connect", $"+ipAddress {ep.ipAddress} +port {ep.port}");
            Plugin.LogDebug($"Rewrote rich-presence IP to {ep.ipAddress}:{ep.port}");
        }
        catch (Exception ex)
        {
            Plugin.LogError($"Rich-presence IP fix failed: {ex.Message}");
        }
    }
}
