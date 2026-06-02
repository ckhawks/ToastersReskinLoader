// Multiplayer enhancements — friends, party, matchmaking. Part of the
// "Tweaks" sidebar group.

using UnityEngine.UIElements;
using ToasterReskinLoader.core;
using ToasterReskinLoader.social;
using ToasterReskinLoader.social.beacon;
using ToasterReskinLoader.serverbrowser;

namespace ToasterReskinLoader.ui.sections;

public static class MultiplayerSection
{
    public static void CreateSection(VisualElement root)
    {
        var cfg = SettingsUI.RequireConfig(root,
            "Optional enhancements for friends, parties, and matchmaking.");
        if (cfg == null) return;
        var runner = QoLRunner.Instance;

        SettingsUI.ToggleRow(root, "Use enhanced friends list", cfg.enableBetterFriendsList,
            v =>
            {
                cfg.enableBetterFriendsList = v;
                runner.SaveAndRefresh();
                if (v) BetterFriendsList.Enable();
                else   BetterFriendsList.Disable();
            });

        SettingsUI.ToggleRow(root, "Show party members in locker room", cfg.enablePartyLineup,
            v =>
            {
                cfg.enablePartyLineup = v;
                runner.SaveAndRefresh();
                PartyLineup.RefreshFromConfig();
            });

        SettingsUI.ToggleRow(root, "Enable matchmaking beacon ping panel", cfg.enableBeaconPing,
            v =>
            {
                cfg.enableBeaconPing = v;
                runner.SaveAndRefresh();
                if (v) BeaconPing.Enable();
                else   BeaconPing.Disable();
            });

        SettingsUI.ToggleRow(root, "Auto-connect to matchmaking matches", cfg.enableAutoConnectMatchmaking,
            v =>
            {
                cfg.enableAutoConnectMatchmaking = v;
                runner.SaveAndRefresh();
                if (v) AutoConnectMatchmaking.Enable();
                else   AutoConnectMatchmaking.Disable();
            });
    }
}
