// Allow All-Chat / Team-Chat to open in non-Play phases (Spectate, Replay,
// TeamSelect, PositionSelect, etc.) while connected to a server.
//
// Vanilla `OnAllChatActionPerformed` / `OnTeamChatActionPerformed` gate on
// `Phase == Play`, so clients in non-Play phases can't open chat. We
// intercept both action handlers and decide whether to:
//   - run the original (vanilla phase gate applies — chat won't open)
//   - swallow the action and return (block — a modal text-input view is up)
//   - force-open chat ourselves (chat is wanted here)
//
// Controlled by enableChatAnyInGamePhase (default on). Main menu is always
// blocked — chat from the menu has no recipient, and pressing T/Y on text
// fields like the PARTYTIME extras input would otherwise hijack the keystroke.

using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.qol;

internal static class ChatAnyPhase
{
    // Whether our handler should take over for the current UI context. If
    // false, we let the original action handler run (which will gate on the
    // vanilla Phase == Playing rule).
    private static bool ShouldHandle()
    {
        var cfg = QoLRunner.Instance?.Config;
        if (cfg == null) return false;
        var ui = MonoBehaviourSingleton<UIManager>.Instance;
        if (ui == null) return false;

        // Main menu = not connected. Never handle here — chat from the
        // menu goes nowhere, and the IsTextInputFocused guard above the
        // prefix already covers the PARTYTIME hijack case.
        if (ui.MainMenu != null && ui.MainMenu.IsVisible) return false;

        // In game (server connected). Handle if the in-game flag is on —
        // covers TeamSelect / PositionSelect / Spectate / Replay etc.
        return cfg.enableChatAnyInGamePhase;
    }

    // True while the user is typing in any text-input control. The chat keys
    // (Y / U) are also valid characters, and the InputAction callback fires
    // independently of UI focus — so without this guard, hitting Y while
    // typing in the main-menu PARTYTIME field (or any text field) hijacks
    // the keystroke and opens chat. We block both our handler AND vanilla's
    // when a text input is focused.
    private static bool IsTextInputFocused()
    {
        try
        {
            var ui = MonoBehaviourSingleton<UIManager>.Instance;
            var fc = ui?.UIDocument?.rootVisualElement?.panel?.focusController;
            var focused = fc?.focusedElement as VisualElement;
            var cur = focused;
            while (cur != null)
            {
                if (cur is TextField || cur is IntegerField || cur is FloatField
                    || cur is LongField || cur is DoubleField)
                    return true;
                cur = cur.parent;
            }

            // Legacy Unity UI / TMP fields (the base game uses TMP_InputField
            // for some inputs, including the PARTYTIME extras box).
            var es = UnityEngine.EventSystems.EventSystem.current;
            var go = es != null ? es.currentSelectedGameObject : null;
            if (go != null)
            {
                if (go.GetComponent<TMPro.TMP_InputField>()?.isFocused ?? false) return true;
                if (go.GetComponent<UnityEngine.UI.InputField>()?.isFocused ?? false) return true;
            }
        }
        catch { }
        return false;
    }

    // Block chat opening when a modal text-input view is up. Excludes
    // background views like Play that can stay visible in LockerRoom phase,
    // and transient gameplay views like Team/Position select.
    private static bool ChatShouldBeBlocked()
    {
        try
        {
            // Dev console is a focusable text input — chat keys typed inside
            // it are user input, not chat-open requests.
            if (DevConsole.Instance != null && DevConsole.Instance.IsOpen) return true;

            var ui = MonoBehaviourSingleton<UIManager>.Instance;
            if (ui == null) return false;
            if (ui.Settings != null && ui.Settings.IsVisible) return true;
            if (ui.Mods != null && ui.Mods.IsVisible) return true;
            if (ui.PauseMenu != null && ui.PauseMenu.IsVisible) return true;
            if (ui.ServerBrowser != null && ui.ServerBrowser.IsVisible) return true;
            if (ui.NewServer != null && ui.NewServer.IsVisible) return true;
            if (ui.Identity != null && ui.Identity.IsVisible) return true;
            if (ui.Appearance != null && ui.Appearance.IsVisible) return true;
            if (ui.PlayerMenu != null && ui.PlayerMenu.IsVisible) return true;
            if (ui.Friends != null && ui.Friends.IsVisible) return true;
            // The Play view is the main-menu connect/server-list panel —
            // hitting Y/U here used to slip past every other check.
            if (ui.Play != null && ui.Play.IsVisible) return true;

            // Toaster's own reskin menu: not part of UIManager's view list,
            // so check the root container's display state directly.
            var trlRoot = ToasterReskinLoader.ui.ReskinMenu.rootContainer;
            if (trlRoot != null && trlRoot.style.display.value == UnityEngine.UIElements.DisplayStyle.Flex)
                return true;
        }
        catch { }
        return false;
    }

    private static void ForceOpenChat(UIChat chat, bool teamChat)
    {
        if (chat == null) return;
        try
        {
            // Chat already capturing input — don't re-StartInput, that would
            // wipe whatever the user is typing.
            if (chat.IsFocused) return;

            if (!chat.IsVisible) chat.Show();

            // Other UI views can sit on top of chat (e.g. Spectate overlay),
            // so even after Show() the textfield can render behind them.
            // Lift the chat view to the top of its siblings.
            var view = AccessTools.Field(typeof(UIView), "view")?.GetValue(chat) as VisualElement;
            view?.BringToFront();

            chat.StartInput(isTeamChat: teamChat);
        }
        catch (Exception e) { Debug.LogWarning("[QoL] ForceOpenChat failed: " + e.Message); }
    }

    [HarmonyPatch(typeof(UIManager), "OnAllChatActionPerformed")]
    private static class AllChat_AllowAnyPhase
    {
        private static bool Prefix(UIManager __instance)
        {
            // Always: never open chat while the user is typing in a text
            // input. Blocks both our handler and vanilla — the chat keys
            // (Y/U) should pass through to the focused field as characters.
            if (IsTextInputFocused()) return false;
            if (!ShouldHandle()) return true; // run original
            try
            {
                if (ChatShouldBeBlocked()) return false;
                ForceOpenChat(__instance?.Chat, teamChat: false);
                return false;
            }
            catch { }
            return true;
        }
    }

    [HarmonyPatch(typeof(UIManager), "OnTeamChatActionPerformed")]
    private static class TeamChat_AllowAnyPhase
    {
        private static bool Prefix(UIManager __instance)
        {
            // Always: never open chat while the user is typing in a text
            // input. Blocks both our handler and vanilla — the chat keys
            // (Y/U) should pass through to the focused field as characters.
            if (IsTextInputFocused()) return false;
            if (!ShouldHandle()) return true; // run original
            try
            {
                if (ChatShouldBeBlocked()) return false;
                ForceOpenChat(__instance?.Chat, teamChat: true);
                return false;
            }
            catch { }
            return true;
        }
    }
}
