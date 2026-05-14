// Hide inactive chat (instead of fading individual rows).
//
// Vanilla `UIChatMessage.Blur()` adds a "blurred" USS class to expired
// messages — they stay on screen at low opacity. When
// `enableHideInactiveChat` is on, we DO NOT collapse individual message
// rows (that caused remaining rows to shift up one-by-one as each line
// expired — the "buggy fade" effect). Instead we wait until every row
// is in the blurred state and then hide the parent `chat` container in
// one shot, so the whole panel disappears together with no reshuffling.
//
// Lifecycle hooks:
//   UIChatMessage.Blur   → if every row is now blurred and chat isn't
//                           focused, hide the chat container.
//   UIChatMessage.Focus  → a new live message arrived (or user opened
//                           chat): force-show the container.
//   UIChat.Show          → newly visible chat with no live messages:
//                           collapse the empty box right away.
//   UIChat.StartInput    → user opened chat to type: force-show container.
//   UIChat.StopInput     → user closed chat: re-check; hide if all rows
//                           are already blurred.
//
// Toggling the flag is runtime-safe: when off, all postfixes short-circuit
// before touching styles. The Focus postfix's container un-hide runs
// unconditionally so flipping the flag off mid-session never leaves the
// container stuck at `display:None`.

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

    // Hide the chat container only when every existing message row is
    // already in the blurred (expired) state AND the user isn't typing.
    // We never hide individual rows here — keeping them in layout means
    // the panel collapses all at once instead of shifting line-by-line.
    private static void HideContainerIfAllBlurred(UIChat chat = null)
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
                    if (ve == null) continue;
                    var lbl = ve.Q<Label>();
                    // Any live (non-blurred) message keeps the panel up.
                    if (lbl != null && !lbl.ClassListContains("blurred"))
                        return;
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
                // Only act on messages vanilla actually expired (the
                // "blurred" class is now on the label). If Blur restarted
                // the expiry tween, leave the panel alone.
                var lbl = ve.Q<Label>();
                if (lbl != null && lbl.ClassListContains("blurred"))
                    HideContainerIfAllBlurred();
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
                // A message just became live (new message, or user opened
                // chat) — make sure the container is visible. Runs even
                // when the flag is off so flipping it off mid-session
                // never leaves the container stuck collapsed.
                ShowChatContainer();
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
            // Phase change just brought the chat view onscreen. If
            // there's nothing live to display, collapse the empty box
            // right away instead of waiting for a Blur that may never
            // come (zero messages → zero Blur callbacks).
            HideContainerIfAllBlurred(__instance);
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
            // User closed chat. Re-check — if every message is already
            // past its blur point, collapse the container immediately.
            HideContainerIfAllBlurred(__instance);
        }
    }
}
