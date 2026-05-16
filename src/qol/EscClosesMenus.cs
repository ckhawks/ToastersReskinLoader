// ESC handling for base-game secondary menus.
//
// Vanilla's pause-action handler only operates on the pause menu and only
// while phase == Playing. When a secondary menu (Settings, Mods, ServerBrowser,
// etc.) is visible we route ESC to that menu's own "close" event so its
// controller restores the previous view correctly (e.g. Settings → MainMenu in
// lobby, Settings → PauseMenu in game).
//
// During pos/team-select (which still reports Phase==Playing), the select
// overlays are initialized AFTER the pause menu, so they sit later in the
// UIDocument DOM and render above PauseMenuView. They also stay in
// `InteractingViews` until hidden, so vanilla's `IsViewTopmostInteracting<UIPauseMenu>`
// check fails and ESC can neither show a clickable pause menu nor close
// the one it just showed. The PauseMenuVisibilityPatch below ties select
// overlay state to pause-menu visibility so the menu is always the topmost
// interactive view when open.

using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

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

            // Chat input wins ESC: vanilla NavigationCancelEvent already
            // closes the chat textfield this frame; don't also try to
            // close some other secondary menu underneath.
            if (ui.Chat != null && ui.Chat.IsFocused) return true;

            // Order matters: nested popups before their parents.
            if (ui.Identity != null && ui.Identity.IsVisible) { EventManager.TriggerEvent("Event_OnIdentityClickClose"); return true; }
            if (ui.Appearance != null && ui.Appearance.IsVisible) { EventManager.TriggerEvent("Event_OnAppearanceClickClose"); return true; }
            if (ui.Friends != null && ui.Friends.IsVisible) { EventManager.TriggerEvent("Event_OnFriendsClickClose"); return true; }
            if (ui.NewServer != null && ui.NewServer.IsVisible) { EventManager.TriggerEvent("Event_OnNewServerClickClose"); return true; }
            if (ui.ServerBrowser != null && ui.ServerBrowser.IsVisible) { EventManager.TriggerEvent("Event_OnServerBrowserClickClose"); return true; }
            if (ui.Settings != null && ui.Settings.IsVisible) { EventManager.TriggerEvent("Event_OnSettingsClickClose"); return true; }
            if (ui.Mods != null && ui.Mods.IsVisible) { EventManager.TriggerEvent("Event_OnModsClickClose"); return true; }
            if (ui.PlayerMenu != null && ui.PlayerMenu.IsVisible) { EventManager.TriggerEvent("Event_OnPlayerMenuClickBack"); return true; }
            if (ui.Play != null && ui.Play.IsVisible) { EventManager.TriggerEvent("Event_OnPlayClickClose"); return true; }
        }
        catch (Exception e) { Debug.LogWarning("[QoL] ESC menu close failed: " + e.Message); }
        return false;
    }

    // When chat is open and the user hits ESC, vanilla fires both:
    //   (a) the chat textfield's NavigationCancelEvent → StopInput,
    //       which closes the chat input — what the user wants.
    //   (b) the PauseAction binding → OnPauseActionPerformed, which
    //       opens the pause menu — what the user does NOT want.
    //
    // This prefix kills (b) whenever the chat is currently focused so
    // the first ESC just closes the chat. A second ESC (with chat now
    // unfocused) takes the normal path and opens the pause menu.
    [HarmonyPatch(typeof(UIManager), "OnPauseActionPerformed")]
    private static class OnPauseActionPerformed_SkipIfChatFocused
    {
        private static bool Prefix(UIManager __instance)
        {
            try
            {
                if (__instance?.Chat != null && __instance.Chat.IsFocused) return false;
            }
            catch { }
            return true;
        }
    }

    // Open the pause menu when ESC is pressed during a connected non-Playing
    // phase (LockerRoom etc). Vanilla only opens it in Playing.
    [HarmonyPatch(typeof(UIManager), "OnPauseActionPerformed")]
    private static class OnPauseActionPerformed_OpenInLockerRoom
    {
        private static void Postfix(UIManager __instance)
        {
            try
            {
                var cfg = QoLRunner.Instance?.Config;
                if (cfg == null || !cfg.enableEscCloseMenus) return;
                if (__instance == null) return;

                if (DevConsole.Instance != null && DevConsole.Instance.IsOpen) return;
                var trlRoot = ToasterReskinLoader.ui.ReskinMenu.rootContainer;
                if (trlRoot != null && trlRoot.style.display == DisplayStyle.Flex) return;

                if (__instance.MainMenu != null && __instance.MainMenu.IsVisible) return;

                if (__instance.Settings      != null && __instance.Settings.IsVisible)      return;
                if (__instance.Mods          != null && __instance.Mods.IsVisible)          return;
                if (__instance.ServerBrowser != null && __instance.ServerBrowser.IsVisible) return;
                if (__instance.NewServer     != null && __instance.NewServer.IsVisible)     return;
                if (__instance.Identity      != null && __instance.Identity.IsVisible)      return;
                if (__instance.Appearance    != null && __instance.Appearance.IsVisible)    return;
                if (__instance.Friends       != null && __instance.Friends.IsVisible)       return;
                if (__instance.PlayerMenu    != null && __instance.PlayerMenu.IsVisible)    return;
                if (__instance.Play          != null && __instance.Play.IsVisible)          return;

                if (__instance.PauseMenu == null) return;
                if (GlobalStateManager.UIState.Phase == UIPhase.Playing) return;

                if (__instance.PauseMenu.IsVisible)
                {
                    __instance.PauseMenu.Hide();
                    return;
                }
                __instance.PauseMenu.Show();
                GlobalStateManager.SetUIState(new Dictionary<string, object> { { "isMouseRequired", true } });
            }
            catch (Exception e) { Plugin.LogError("[QoL][ESC] open-in-locker postfix failed: " + e); }
        }
    }

    // Hide select overlays while pause menu is open, restore them when it
    // closes. UIView.Show/Hide are virtual on the base class and inherited
    // by UIPauseMenu, so we patch UIView and filter by type — that catches
    // every open/close path (ESC, Settings round-trip, button clicks).
    [HarmonyPatch(typeof(UIView), nameof(UIView.Show))]
    private static class PauseMenu_Show_Postfix
    {
        private static bool _hidPositionSelect;
        private static bool _hidTeamSelect;

        internal static bool HidPositionSelect => _hidPositionSelect;
        internal static bool HidTeamSelect => _hidTeamSelect;

        internal static void ClearHiddenFlags()
        {
            _hidPositionSelect = false;
            _hidTeamSelect = false;
        }

        private static void Postfix(UIView __instance, bool __result)
        {
            if (!__result) return;
            if (!(__instance is UIPauseMenu)) return;
            try
            {
                var cfg = QoLRunner.Instance?.Config;
                if (cfg == null || !cfg.enableEscCloseMenus) return;
                var ui = MonoBehaviourSingleton<UIManager>.Instance;
                if (ui == null) return;

                if (ui.PositionSelect != null && ui.PositionSelect.IsVisible)
                {
                    ui.PositionSelect.Hide();
                    _hidPositionSelect = true;
                }
                if (ui.TeamSelect != null && ui.TeamSelect.IsVisible)
                {
                    ui.TeamSelect.Hide();
                    _hidTeamSelect = true;
                }
            }
            catch (Exception e) { Plugin.LogError("[QoL][ESC] PauseMenu show postfix failed: " + e); }
        }
    }

    [HarmonyPatch(typeof(UIView), nameof(UIView.Hide))]
    private static class PauseMenu_Hide_Postfix
    {
        private static void Postfix(UIView __instance, bool __result)
        {
            if (!__result) return;
            if (!(__instance is UIPauseMenu)) return;
            try
            {
                var ui = MonoBehaviourSingleton<UIManager>.Instance;
                if (ui == null) { PauseMenu_Show_Postfix.ClearHiddenFlags(); return; }

                // Only restore the select overlays if no other view took
                // pause menu's place (Settings round-trip will re-show pause
                // menu itself, but the user can also click SelectTeam /
                // SelectPosition which open the matching overlay — that's
                // fine because our flags only fire Show() on views we
                // explicitly hid). Disconnect tears the server down, so the
                // momentary re-show before teardown is harmless.
                if (PauseMenu_Show_Postfix.HidPositionSelect && ui.PositionSelect != null)
                {
                    ui.PositionSelect.Show();
                }
                if (PauseMenu_Show_Postfix.HidTeamSelect && ui.TeamSelect != null)
                {
                    ui.TeamSelect.Show();
                }
                PauseMenu_Show_Postfix.ClearHiddenFlags();
            }
            catch (Exception e) { Plugin.LogError("[QoL][ESC] PauseMenu hide postfix failed: " + e); }
        }
    }
}
