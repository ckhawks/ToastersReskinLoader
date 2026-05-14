// ESC handling for base-game secondary menus.
//
// Vanilla's pause-action handler only operates on the pause menu and only
// while phase == Playing. When a secondary menu (Settings, Mods, ServerBrowser,
// etc.) is visible we route ESC to that menu's own "close" event so its
// controller restores the previous view correctly (e.g. Settings → MainMenu in
// lobby, Settings → PauseMenu in game).
//
// Called from QoLRunner.Update; the runner gates on enableEscCloseMenus, so
// no Harmony patches live here — this is plain helper code.

using System;
using HarmonyLib;
using UnityEngine;

namespace ToasterReskinLoader.qol;

internal static class EscClosesMenus
{
    // Returns true if a secondary menu was closed (ESC was consumed).
    public static bool TryCloseTopmostSecondaryMenu()
    {
        try
        {
            var ui = MonoBehaviourSingleton<UIManager>.Instance;
            if (ui == null) return false;

            // Order matters: nested popups before their parents.
            if (ui.Identity != null && ui.Identity.IsVisible) { EventManager.TriggerEvent("Event_OnIdentityClickClose"); return true; }
            if (ui.Appearance != null && ui.Appearance.IsVisible) { EventManager.TriggerEvent("Event_OnAppearanceClickClose"); return true; }
            if (ui.Friends != null && ui.Friends.IsVisible) { EventManager.TriggerEvent("Event_OnFriendsClickClose"); return true; }
            if (ui.NewServer != null && ui.NewServer.IsVisible) { EventManager.TriggerEvent("Event_OnNewServerClickClose"); return true; }
            if (ui.ServerBrowser != null && ui.ServerBrowser.IsVisible) { EventManager.TriggerEvent("Event_OnServerBrowserClickClose"); return true; }
            if (ui.Settings != null && ui.Settings.IsVisible) { EventManager.TriggerEvent("Event_OnSettingsClickClose"); return true; }
            if (ui.Mods != null && ui.Mods.IsVisible) { EventManager.TriggerEvent("Event_OnModsClickClose"); return true; }
            if (ui.PlayerMenu != null && ui.PlayerMenu.IsVisible) { EventManager.TriggerEvent("Event_OnPlayerMenuClickBack"); return true; }
            // Vanilla's OnPauseActionPerformed only closes the pause menu
            // when `Phase==Playing && IsViewTopmostInteracting<UIPauseMenu>`,
            // so during TeamSelect / PositionSelect (overlays sit on top of
            // the pause menu, breaking the topmost check) ESC silently does
            // nothing. Close ourselves whenever the menu is visible and
            // vanilla wouldn't have touched it. We deliberately don't fire
            // when vanilla just opened it on the same ESC press — at that
            // point the menu is the topmost interacting view, so this
            // branch correctly skips.
            if (ui.PauseMenu != null && ui.PauseMenu.IsVisible
                && (GlobalStateManager.UIState.Phase != UIPhase.Playing
                    || !GlobalStateManager.UIState.IsViewTopmostInteracting<UIPauseMenu>()))
            {
                ui.PauseMenu.Hide();
                return true;
            }
            if (ui.Play != null && ui.Play.IsVisible) { EventManager.TriggerEvent("Event_OnPlayClickClose"); return true; }
        }
        catch (Exception e) { Debug.LogWarning("[QoL] ESC menu close failed: " + e.Message); }
        return false;
    }
}
