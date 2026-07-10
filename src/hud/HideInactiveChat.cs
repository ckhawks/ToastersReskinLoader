// Chat panel QoL — two independent toggles:
//
//   * enableHideInactiveChat         hide the whole container once
//                                    every message has blurred.
//   * enableChatNoFade               keep blurred messages at full
//                                    opacity (defeats the per-row
//                                    fade-out).
//
// (Chat background is handled separately by the reskin's "Chat
// Background" setting — ChatSection.ApplyChatBackground.)
//
// Vanilla adds a "blurred" USS class to expired messages; we still rely
// on that as the "all messages blurred → hide container" signal but
// override the visible opacity so the user never sees the fade.
//
// Lifecycle hooks:
//   UIChatMessage.Blur      check if every row is blurred → hide.
//   UIChatMessage.Focus     un-hide (new message OR user opened chat).
//   UIChat.Show             apply visuals; hide if all rows blurred.
//   UIChat.AddChatMessage   pin new message's opacity to 1 when
//                            enableChatNoFade is on.
//   UIChat.StartInput       force show; add "[TEAM]" prefix on team chat.
//   UIChat.StopInput        re-check hide; clear "[TEAM]" prefix.
//   SmoothScrollToVerticalPosition  teleport instead of tween for
//                                    1.5s after a re-show (avoids the
//                                    distracting fast-scroll on the
//                                    first message after a hide).
//
// Toggles are runtime-safe: ChatSection calls RefreshVisualState()
// on flip so live transitions apply / revert every override immediately.

using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

using ToasterReskinLoader.core;

namespace ToasterReskinLoader.hud;

internal static class HideInactiveChat
{
    private static bool Enabled =>
        Settings.Current?.enableHideInactiveChat ?? false;

    // How long after a re-show we keep snapping every
    // SmoothScrollToVerticalPosition. Sized to cover typical burst
    // gaps (200-300ms between rapid messages) plus a healthy margin
    // for the GeometryChanged callbacks to run after the visual is
    // added to the scroll content.
    private const float SnapWindowSeconds = 1.5f;

    // Cached reflection handles for UIChat's private fields. AccessTools
    // caches internally so this is cheap, but stashing them locally avoids
    // the dictionary lookup on the hot path.
    private static System.Reflection.FieldInfo _fiChatContainer;
    private static System.Reflection.FieldInfo _fiUiChatMessages;
    private static System.Reflection.FieldInfo _fiScrollView;
    private static System.Reflection.FieldInfo _fiSmoothScrollTween;
    private static System.Reflection.FieldInfo _fiIsScrolling;
    private static System.Reflection.FieldInfo _fiAutoScroll;
    private static System.Reflection.FieldInfo _fiTextField;

    private static TextField GetTextField(UIChat chat)
    {
        if (chat == null) return null;
        if (_fiTextField == null) _fiTextField = AccessTools.Field(typeof(UIChat), "textField");
        return _fiTextField?.GetValue(chat) as TextField;
    }

    // Deadline (in Time.unscaledTime) until which any
    // SmoothScrollToVerticalPosition gets teleport-snapped instead of
    // tweened. Set in ShowChatContainer to (now + 1.5s) every time we
    // come out of a hidden state.
    //
    // Time window rather than single-shot bool because messages
    // frequently arrive in bursts (e.g. 5 lines in a second). Each
    // burst message fires its own SmoothScrollToVerticalPosition from
    // its GeometryChanged callback; consuming the flag on the first
    // one left the rest to tween, and stacked 0.2s tweens chained
    // visually into the "slow scroll down" effect the user saw.
    private static float _snapUntilUnscaledTime;
    // Independent flag so we don't have to rely on `style.display.value`
    // round-tripping. Set in HideContainerIfAllBlurred when we collapse
    // the panel; checked in ShowChatContainer to decide whether the
    // re-show is "waking up" from a hide vs. an idempotent show.
    private static bool _chatWasHidden;

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
            bool wasHidden = _chatWasHidden;
            _chatWasHidden = false;
            // Explicit Flex (not StyleKeyword.Null) — reverting to USS
            // would re-collapse if vanilla's default is display:none.
            container.style.display = DisplayStyle.Flex;
            // Window-based snap so a whole burst of messages on re-show
            // all teleport instead of only the first.
            if (wasHidden) _snapUntilUnscaledTime = Time.unscaledTime + SnapWindowSeconds;
        }
        catch { }
    }

    // ─────────────────────── visual styling ──────────────────────────────

    // Visual toggles per the QoL settings page:
    //   * cfg.enableChatNoFade       → expired messages stay opaque
    //                                  (overrides the .blurred USS fade).
    //   * cfg.enableUiTextShadow     → handled globally by
    //                                  UiTextShadow.cs; nothing to do here.
    //   * cfg.enableHideInactiveChat → the original hide-when-all-blurred
    //                                  logic.
    private static bool WantNoFade =>
        Settings.Current?.enableChatNoFade ?? false;

    // Single source of truth for the per-toggle visuals. Called from
    // the ChatSection toggle handlers so flips take effect live,
    // and idempotently from UIChat.Show / per-message hooks.
    public static void RefreshVisualState()
    {
        try
        {
            var chat = GetChat();
            if (chat == null) return;
            // Force-show first so flipping hide-inactive OFF resurfaces
            // a previously-collapsed panel before we re-paint it.
            ShowChatContainer(chat);
            ApplyAllMessageOpacity(chat);
        }
        catch (Exception e) { Debug.LogWarning("[QoL] hide-inactive-chat RefreshVisualState failed: " + e.Message); }
    }

    private static void ApplyAllMessageOpacity(UIChat chat)
    {
        var messages = GetUIChatMessages(chat);
        if (messages == null) return;
        var opacity = WantNoFade ? new StyleFloat(1f) : new StyleFloat(StyleKeyword.Null);
        for (int i = 0; i < messages.Count; i++)
        {
            var lbl = messages[i]?.VisualElement?.Q<Label>();
            if (lbl == null) continue;
            try { lbl.style.opacity = opacity; }
            catch { }
        }
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
            // Empty chat is not "all blurred" — preemptively collapsing
            // an empty container at startup leaves it stuck hidden if
            // any later show path reverts to USS instead of explicit
            // Flex. Only hide once there has been at least one message
            // AND every row is now blurred.
            if (messages == null || messages.Count == 0) return;
            for (int i = 0; i < messages.Count; i++)
            {
                var ve = messages[i]?.VisualElement;
                if (ve == null) continue;
                var lbl = ve.Q<Label>();
                // Any live (non-blurred) message keeps the panel up.
                if (lbl != null && !lbl.ClassListContains("blurred"))
                    return;
            }

            var container = GetChatContainer(chat);
            if (container != null)
            {
                container.style.display = DisplayStyle.None;
                _chatWasHidden = true;
            }
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
                if (lbl == null) return;
                if (lbl.ClassListContains("blurred"))
                {
                    // Defeat the visible fade when the user enabled
                    // enableChatNoFade — we still keep the "blurred"
                    // class itself (it's our signal for
                    // HideContainerIfAllBlurred), but override the
                    // opacity inline so the message stays at full
                    // strength visually.
                    if (WantNoFade)
                        try { lbl.style.opacity = 1f; } catch { }
                    HideContainerIfAllBlurred();
                }
            }
            catch (Exception e) { Debug.LogWarning("[QoL] hide-inactive-chat blur failed: " + e.Message); }
        }
    }

    // Always-on hook for new chat lines: if enableChatNoFade is on,
    // override the new label's opacity to 1 so the .blurred USS rule
    // can't fade it later. Surfacing the container on new arrivals is
    // handled by the UIChatMessage.Focus postfix below — vanilla
    // constructs every UIChatMessage with Focus(), so well-behaved
    // chat paths un-hide the panel automatically.
    [HarmonyPatch(typeof(UIChat), "AddChatMessage")]
    private static class UIChat_AddChatMessage_Postfix
    {
        private static void Postfix(UIChat __instance)
        {
            try
            {
                if (WantNoFade)
                {
                    var messages = GetUIChatMessages(__instance);
                    if (messages != null && messages.Count > 0)
                    {
                        var lbl = messages[messages.Count - 1]?.VisualElement?.Q<Label>();
                        if (lbl != null) lbl.style.opacity = 1f;
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning("[QoL] hide-inactive-chat add-message failed: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(UIChatMessage), "Focus")]
    private static class UIChatMessage_ShowOnFocus_Postfix
    {
        private static void Postfix(UIChatMessage __instance)
        {
            try
            {
                // A message just became live (new message OR user
                // opened chat) — make sure the container is visible.
                // Runs even when the flag is off so flipping it off
                // mid-session never leaves the container stuck hidden.
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
            // Apply the message-opacity override once on show so the
            // first paint already reflects whatever the user has toggled.
            // The helper internally checks its flag and is a no-op when off.
            try
            {
                ApplyAllMessageOpacity(__instance);
            }
            catch (Exception e) { Debug.LogWarning("[QoL] hide-inactive-chat show apply failed: " + e.Message); }

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
        // The `isTeamChat` arg has a default value (`false`) on
        // vanilla, so Harmony can match the call either way. We grab
        // the resolved IsTeamChat off the instance afterward to be
        // safe.
        private static void Postfix(UIChat __instance)
        {
            // User opened chat to type — always show the container, even
            // when the flag is off (cheap no-op when already visible).
            // (The "[TEAM]" indicator is native in B1117 via UIChat.inputPrefix,
            // so we no longer inject our own label — it would double up.)
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

    // When the chat container was just re-shown after being hidden, native's
    // RefreshContentAndScroll tweens the scroll over the entire (now-grown)
    // distance in 0.2s, which reads as a distracting fast scroll. In the snap
    // window we let native run, then kill its tween and teleport to the bottom.
    // (B1117 replaced SmoothScrollToVerticalPosition(float,bool) with the
    // parameterless RefreshContentAndScroll(), which sets verticalScroller
    // .highValue to the bottom and starts the tween.)
    [HarmonyPatch(typeof(UIChat), "RefreshContentAndScroll")]
    private static class UIChat_RefreshContentAndScroll_SnapAfterShow_Postfix
    {
        private static void Postfix(UIChat __instance)
        {
            if (!Enabled) return;
            if (Time.unscaledTime > _snapUntilUnscaledTime) return; // window expired → keep vanilla tween
            try
            {
                if (_fiScrollView == null) _fiScrollView = AccessTools.Field(typeof(UIChat), "scrollView");
                var scrollView = _fiScrollView?.GetValue(__instance) as ScrollView;
                if (scrollView == null) return;

                // Kill the DOTween smoothScrollTween native just started so it
                // doesn't animate over the snap we're about to do.
                if (_fiSmoothScrollTween == null)
                    _fiSmoothScrollTween = AccessTools.Field(typeof(UIChat), "smoothScrollTween");
                var existingTween = _fiSmoothScrollTween?.GetValue(__instance);
                if (existingTween != null)
                {
                    try { AccessTools.Method(existingTween.GetType(), "Kill",
                              new[] { typeof(bool) })?.Invoke(existingTween, new object[] { false }); }
                    catch { }
                    _fiSmoothScrollTween.SetValue(__instance, null);
                }

                // Teleport straight to the bottom native just computed (highValue).
                float targetY = scrollView.verticalScroller.highValue;
                scrollView.scrollOffset = new Vector2(scrollView.scrollOffset.x, targetY);

                // Mirror the flag bookkeeping vanilla does in the tween's
                // OnComplete so the next valueChanged callback computes
                // autoScroll correctly (= true at bottom).
                if (_fiIsScrolling == null) _fiIsScrolling = AccessTools.Field(typeof(UIChat), "isScrolling");
                if (_fiAutoScroll == null)  _fiAutoScroll  = AccessTools.Field(typeof(UIChat), "autoScroll");
                _fiIsScrolling?.SetValue(__instance, false);
                _fiAutoScroll?.SetValue(__instance, true);

                // Re-apply on the next frame too — UIToolkit's ScrollView clamps
                // scrollOffset against the current highValue, which may not have
                // caught up to the new content's geometry within this frame.
                float capturedTarget = targetY;
                scrollView.schedule.Execute(() =>
                {
                    try { scrollView.scrollOffset = new Vector2(scrollView.scrollOffset.x, capturedTarget); }
                    catch { }
                }).ExecuteLater(0);
            }
            catch (Exception e)
            {
                Plugin.LogWarning("[QoL] chat snap-after-show failed: " + e.Message);
            }
        }
    }
}
