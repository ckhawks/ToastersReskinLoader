// Multiplayer enhancements — friends, party, matchmaking. Part of the
// "Tweaks" sidebar group.

using UnityEngine.UIElements;
using ToasterReskinLoader.core;
using ToasterReskinLoader.social;
using ToasterReskinLoader.social.friendspanel;
using ToasterReskinLoader.social.probe;
using ToasterReskinLoader.serverbrowser;

namespace ToasterReskinLoader.ui.sections;

public static class MultiplayerSection
{
    public static void CreateSection(VisualElement root)
    {
        var cfg = SettingsUI.RequireConfig(root,
            "Optional enhancements for friends, parties, and matchmaking.");
        if (cfg == null) return;

        SettingsUI.ToggleRow(root, "Use enhanced friends list", cfg.enableBetterFriendsList,
            v =>
            {
                cfg.enableBetterFriendsList = v;
                Settings.Save();
                if (v) BetterFriendsList.Enable();
                else   BetterFriendsList.Disable();
            });
        SettingsUI.Note(root, "Shows clearer online/offline status and sorts online friends to the top.");

        SettingsUI.ToggleRow(root, "Show friends-in-game board", cfg.enableFriendsBoard,
            v =>
            {
                cfg.enableFriendsBoard = v;
                Settings.Save();
                if (v) FriendsBoard.Enable();
                else   FriendsBoard.Disable();
            });
        SettingsUI.Note(root, "Adds a board to the right of the main menu listing which of your friends are currently in Puck.");

        SettingsUI.ToggleRow(root, "Show party members in locker room", cfg.enablePartyLineup,
            v =>
            {
                cfg.enablePartyLineup = v;
                Settings.Save();
                PartyLineup.RefreshFromConfig();
            });

        SettingsUI.ToggleRow(root, "Enable matchmaking probe ping panel", cfg.enableProbePing,
            v =>
            {
                cfg.enableProbePing = v;
                Settings.Save();
                if (v) ProbePing.Enable();
                else   ProbePing.Disable();
            });
        SettingsUI.Note(root, "Adds a panel to the Play menu listing matchmaking regions with live ping.");

        SettingsUI.ToggleRow(root, "Auto-connect to matchmaking matches", cfg.enableAutoConnectMatchmaking,
            v =>
            {
                cfg.enableAutoConnectMatchmaking = v;
                Settings.Save();
                if (v) AutoConnectMatchmaking.Enable();
                else   AutoConnectMatchmaking.Disable();
            });
    }
}
