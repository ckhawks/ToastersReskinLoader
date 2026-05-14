// Hide inactive chat (instead of fading).
//
// Vanilla `UIChatMessage.Blur()` adds a "blurred" USS class to expired messages —
// they stay on screen at low opacity. When `enableHideInactiveChat` is on we
// additionally collapse each expired message's VisualElement AND the parent
// `chat` container so the whole panel (incl. the gray box / scroll backing)
// disappears when no message is currently active and the user isn't typing.
//
// Lifecycle hooks:
//   UIChatMessage.Blur   → message expired and chat not focused: hide row;
//                           if every row is hidden, also hide the chat container.
//   UIChatMessage.Focus  → un-hide row (a new message arrives, or the user
//                           opens chat) and force-show the chat container.
//   UIChat.Show          → newly visible chat with no recent messages: collapse
//                           the container so the empty box doesn't sit on screen.
//   UIChat.StartInput    → user opened chat to type: force-show container.
//   UIChat.StopInput     → user closed chat: re-check; hide if no live messages.
//
// Toggling the flag is runtime-safe: when off, all postfixes short-circuit
// before touching styles. The Focus postfix runs its un-hide branch
// unconditionally so flipping the flag off mid-session never leaves a row
// or the container stuck at `display:None`.

using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.qol;

internal static class HideInactiveChat
{
    private static bool Enabled =>
        QoLRunner.Instance?.Config?.enableHideInactiveChat ?? false;

    // Cached reflection handles for UIChat's private fields. AccessTools
    // caches internally so this is cheap, but stashing them locally avoids
    // the dictionary lookup on the hot path.
    private static System.Reflection.FieldInfo _fiChatContainer;
    private static System.Reflection.FieldInfo _fiUiChatMessages;

    private static VisualElement GetChatContainer(UIChat chat)
    {
        if (chat == null) return null;
        if (_fiChatContainer == null) _fiChatContainer = AccessTools.Field(typeof(UIChat), "chat");
        return _fiChatContainer?.GetValue(chat) as VisualElement;
    }

    private static IList<UIChatMessage> GetUIChatMessages(UIChat chat)
    {
        if (chat == null) return null;
        if (_fiUiChatMessages == null) _fiUiChatMessages = AccessTools.Field(typeof(UIChat), "uiChatMessages");
        return _fiUiChatMessages?.GetValue(chat) as IList<UIChatMessage>;
    }

    private static UIChat GetChat() => MonoBehaviourSingleton<UIManager>.Instance?.Chat;

    private static void ShowChatContainer(UIChat chat = null)
    {
        try
        {
            chat = chat ?? GetChat();
            var container = GetChatContainer(chat);
            if (container == null) return;
            if (container.style.display.value == DisplayStyle.None)
                container.style.display = StyleKeyword.Null;
        }
        catch { }
    }

    // Hide the chat container if no message rows are currently visible AND
    // the user isn't actively typing. Called after each Blur and after
    // StopInput / Show.
    private static void HideContainerIfIdle(UIChat chat = null)
    {
        try
        {
            chat = chat ?? GetChat();
            if (chat == null) return;
            if (chat.IsFocused) return;

            var messages = GetUIChatMessages(chat);
            if (messages != null)
            {
                for (int i = 0; i < messages.Count; i++)
                {
                    var ve = messages[i]?.VisualElement;
                    if (ve != null && ve.style.display.value != DisplayStyle.None)
                        return; // at least one row is still visible
                }
            }

            var container = GetChatContainer(chat);
            if (container != null) container.style.display = DisplayStyle.None;
        }
        catch { }
    }

    // ---- message-level patches ----

    [HarmonyPatch(typeof(UIChatMessage), "Blur")]
    private static class UIChatMessage_HideOnBlur_Postfix
    {
        private static void Postfix(UIChatMessage __instance)
        {
            if (!Enabled) return;
            try
            {
                var ve = __instance?.VisualElement;
                if (ve == null) return;
                // Only collapse messages vanilla actually faded. If Blur
                // restarted the expiry tween (message not yet expired), the
                // label won't have the blurred class — leave it visible.
                var lbl = ve.Q<Label>();
                if (lbl != null && lbl.ClassListContains("blurred"))
                {
                    ve.style.display = DisplayStyle.None;
                    HideContainerIfIdle();
                }
            }
            catch (Exception e) { Debug.LogWarning("[QoL] hide-inactive-chat blur failed: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(UIChatMessage), "Focus")]
    private static class UIChatMessage_ShowOnFocus_Postfix
    {
        private static void Postfix(UIChatMessage __instance)
        {
            try
            {
                var ve = __instance?.VisualElement;
                if (ve == null) return;
                // Always un-hide on Focus — covers flag flips mid-session so
                // no row ends up permanently collapsed.
                if (ve.style.display.value == DisplayStyle.None)
                    ve.style.display = StyleKeyword.Null;
                // A message just became visible (new message, or user
                // opened chat), so the container must be visible too.
                if (Enabled) ShowChatContainer();
            }
            catch (Exception e) { Debug.LogWarning("[QoL] hide-inactive-chat focus failed: " + e.Message); }
        }
    }

    // ---- chat-level patches ----

    [HarmonyPatch(typeof(UIChat), "Show")]
    private static class UIChat_Show_Postfix
    {
        private static void Postfix(UIChat __instance)
        {
            if (!Enabled) return;
            // Phase change just brought the chat view onscreen. If there
            // are no live messages and the user isn't typing, collapse
            // the empty box right away instead of waiting for the first
            // Blur (which never fires when there are zero messages).
            HideContainerIfIdle(__instance);
        }
    }

    [HarmonyPatch(typeof(UIChat), "StartInput")]
    private static class UIChat_StartInput_Postfix
    {
        private static void Postfix(UIChat __instance)
        {
            // User opened chat to type — always show the container, even
            // when the flag is off (cheap no-op when already visible).
            ShowChatContainer(__instance);
        }
    }

    [HarmonyPatch(typeof(UIChat), "StopInput")]
    private static class UIChat_StopInput_Postfix
    {
        private static void Postfix(UIChat __instance)
        {
            if (!Enabled) return;
            // User closed chat. Vanilla now lets messages start expiring;
            // re-check the container so it collapses immediately if all
            // messages are already past their expiry window.
            HideContainerIfIdle(__instance);
        }
    }
}