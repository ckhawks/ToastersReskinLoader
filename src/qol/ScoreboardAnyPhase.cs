// Allow the scoreboard hold-to-view in any in-game phase.
//
// Vanilla `UIManager.OnScoreboardActionStarted` gates on
// `UIPhase.Playing && !IsInteracting` — the scoreboard never shows during
// TeamSelect / PositionSelect / Spectate. We replace the phase gate with a
// looser check: still skip when interacting (vanilla's IsInteracting covers
// modal text inputs), still defer to vanilla in the main menu / when our
// own panels are up, but otherwise show the scoreboard regardless of phase.
//
// The Cancel handler (which hides on key release) is left untouched.

using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ToasterReskinLoader.qol;

internal static class ScoreboardAnyPhase
{
    private static bool ShouldHandle()
    {
        var cfg = QoLRunner.Instance?.Config;
        if (cfg == null || !cfg.enableScoreboardAnyInGamePhase) return false;
        var ui = MonoBehaviourSingleton<UIManager>.Instance;
        if (ui == null) return false;

        // Main menu: no useful scoreboard data — let vanilla's phase gate
        // suppress.
        if (ui.MainMenu != null && ui.MainMenu.IsVisible) return false;

        // Our own UIs are open; defer to vanilla which will block.
        if (ui.Settings != null && ui.Settings.IsVisible) return false;
        if (ui.Mods != null && ui.Mods.IsVisible) return false;
        if (ui.PauseMenu != null && ui.PauseMenu.IsVisible) return false;
        if (ui.ServerBrowser != null && ui.ServerBrowser.IsVisible) return false;
        if (ui.NewServer != null && ui.NewServer.IsVisible) return false;
        if (ui.Identity != null && ui.Identity.IsVisible) return false;
        if (ui.Appearance != null && ui.Appearance.IsVisible) return false;
        if (ui.PlayerMenu != null && ui.PlayerMenu.IsVisible) return false;
        if (ui.Friends != null && ui.Friends.IsVisible) return false;
        if (ui.Play != null && ui.Play.IsVisible) return false;

        // Toaster's own reskin menu — also a modal-ish UI.
        var trlRoot = ToasterReskinLoader.ui.ReskinMenu.rootContainer;
        if (trlRoot != null && trlRoot.style.display.value == UnityEngine.UIElements.DisplayStyle.Flex)
            return false;

        return true;
    }

    [HarmonyPatch(typeof(UIManager), "OnScoreboardActionStarted")]
    private static class Scoreboard_AllowAnyPhase
    {
        private static bool Prefix(UIManager __instance, InputAction.CallbackContext context)
        {
            if (!ShouldHandle()) return true; // run vanilla (which gates on Phase==Playing)
            try
            {
                __instance?.Scoreboard?.Show();
                return false;
            }
            catch (Exception e) { Debug.LogWarning("[QoL] scoreboard-any-phase failed: " + e.Message); }
            return true;
        }
    }
}
