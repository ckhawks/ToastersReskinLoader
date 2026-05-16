// Chat panel QoL — three independent toggles:
//
//   * enableHideInactiveChat         hide the whole container once
//                                    every message has blurred.
//   * enableChatNoFade               keep blurred messages at full
//                                    opacity (defeats the per-row
//                                    fade-out).
//   * enableChatTransparentContainer force the chat panel's bg fully
//                                    transparent. Captures the
//                                    original USS-painted bg on first
//                                    touch and restores it on flip-off.
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
// Toggles are runtime-safe: PlayerQoLSection calls RefreshVisualState()
// on flip so live transitions apply / revert every override immediately.

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

    // Descendants we've ever written background overrides to. Tracked
    // so a live flip-off reverts every previously-touched element even
    // though their resolvedStyle.backgroundColor is now (0,0,0,0) (the
    // value WE wrote). Without this the descendant-discovery filter
    // ("only override visible backgrounds") would skip them on revert
    // and the dark backdrop would stay gone — but also never come back.
    private static readonly System.Collections.Generic.HashSet<VisualElement> _touchedDescendants
        = new System.Collections.Generic.HashSet<VisualElement>();

    // Original (pre-modification) bg styles per element. Captured the
    // first time we observe a non-empty resolvedStyle so a live flip-OFF
    // can write back exactly what vanilla painted. StyleKeyword.Null
    // would be simpler but doesn't work reliably here — the very first
    // capture in UIChat.Show happens BEFORE USS resolves, so we have to
    // hold the inline values we read post-resolution.
    private struct OriginalBg
    {
        public StyleColor color;
        public StyleBackground image;
    }
    private static readonly System.Collections.Generic.Dictionary<VisualElement, OriginalBg> _originalBg
        = new System.Collections.Generic.Dictionary<VisualElement, OriginalBg>();

    private static void CaptureOriginalBg(VisualElement v)
    {
        if (v == null) return;
        if (_originalBg.ContainsKey(v)) return;
        try
        {
            // Only commit once styles have actually resolved — a
            // pre-resolution read returns (0,0,0,0) + empty image even
            // when vanilla USS will paint something on the next layout
            // pass. Caching that empty value would permanently restore
            // to nothing. Skipping leaves the slot empty and lets the
            // next call retry once styles settle.
            var c = v.resolvedStyle.backgroundColor;
            var b = v.resolvedStyle.backgroundImage;
            bool hasColor = c.a > 0.001f;
            bool hasImage = b.texture != null
                         || b.sprite != null
                         || b.vectorImage != null
                         || b.renderTexture != null;
            if (!hasColor && !hasImage) return;

            _originalBg[v] = new OriginalBg
            {
                color = new StyleColor(c),
                image = new StyleBackground(b),
            };
        }
        catch { }
    }

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

    // Three independent visual toggles per the QoL settings page:
    //   * cfg.enableChatNoFade               → expired messages stay
    //                                          opaque (overrides the
    //                                          .blurred USS fade).
    //   * cfg.enableChatTransparentContainer → chat container's dark
    //                                          USS backdrop forced to
    //                                          fully transparent.
    //   * cfg.enableUiTextShadow             → handled globally by
    //                                          UiTextShadow.cs;
    //                                          nothing to do here.
    //   * cfg.enableHideInactiveChat         → the original hide-when-
    //                                          all-blurred logic.
    private static bool WantNoFade =>
        QoLRunner.Instance?.Config?.enableChatNoFade ?? true;
    private static bool WantTransparentContainer =>
        QoLRunner.Instance?.Config?.enableChatTransparentContainer ?? true;

    // Single source of truth for the per-toggle visuals. Called from
    // the PlayerQoLSection toggle handlers so flips take effect live,
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
            ApplyContainerTransparency(chat);
            ApplyAllMessageOpacity(chat);
        }
        catch (Exception e) { Debug.LogWarning("[QoL] hide-inactive-chat RefreshVisualState failed: " + e.Message); }
    }

    private static void ApplyAllMessageOpacity(UIChat chat)
    {
        var messages = GetUIChatMessages(chat);
        if (messages == null) return;
        bool on = WantNoFade;
        for (int i = 0; i < messages.Count; i++)
        {
            var lbl = messages[i]?.VisualElement?.Q<Label>();
            if (lbl == null) continue;
            try { lbl.style.opacity = on ? new StyleFloat(1f) : new StyleFloat(StyleKeyword.Null); }
            catch { }
        }
    }

    // Override the chat panel's bg to fully transparent (toggle ON) or
    // restore vanilla (toggle OFF). Vanilla can paint via background
    // color, background-image sprite, or both, on any of: outer
    // ChatView, inner Chat element, ScrollView, scroll content, scroll
    // viewport, plus any themed descendant. We hit the named five
    // unconditionally and also sweep descendants under ChatView so
    // theme USS rules we don't know by name still get covered.
    //
    // Restore relies on a captured original — not StyleKeyword.Null —
    // because the first sweep on UIChat.Show runs before USS has
    // resolved on the panel; see CaptureOriginalBg for details.
    //
    // Skips the text input (textField + its inner input element) so
    // the user can still see where they're typing.
    private static void ApplyContainerTransparency(UIChat chat)
    {
        bool on = WantTransparentContainer;
        StyleColor transparentColor = new StyleColor(new Color(0f, 0f, 0f, 0f));

        void Set(VisualElement v)
        {
            if (v == null) return;
            CaptureOriginalBg(v);
            try
            {
                if (on)
                {
                    v.style.backgroundColor = transparentColor;
                    v.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                }
                else if (_originalBg.TryGetValue(v, out var orig))
                {
                    v.style.backgroundColor = orig.color;
                    v.style.backgroundImage = orig.image;
                }
            }
            catch { }
        }

        var view = chat?.View;
        Set(view);
        Set(GetChatContainer(chat));

        if (_fiScrollView == null) _fiScrollView = AccessTools.Field(typeof(UIChat), "scrollView");
        if (_fiScrollView?.GetValue(chat) is ScrollView scrollView)
        {
            Set(scrollView);
            Set(scrollView.contentContainer);
            Set(scrollView.contentViewport);
        }

        // Two-pass descendant sweep:
        //   1) discover anything that paints a visible bg (color or
        //      image), record it.
        //   2) re-apply current state to every element we've ever
        //      recorded. The persistent set is required because once
        //      we've cleared bg on flip-ON, the discovery filter would
        //      skip those same elements on flip-OFF and vanilla
        //      styling would never come back.
        var tf = GetTextField(chat);
        var inputInner = tf?.Q(className: "unity-base-text-field__input");
        if (view != null)
        {
            foreach (var ve in view.Query<VisualElement>().Build())
            {
                if (ve == null) continue;
                if (ve == tf || ve == inputInner) continue;
                if (tf != null && tf.Contains(ve)) continue;
                if (_touchedDescendants.Contains(ve)) continue;

                var bg = ve.resolvedStyle.backgroundColor;
                bool hasBgColor = bg.a > 0.001f;
                var bi = ve.resolvedStyle.backgroundImage;
                bool hasBgImage = bi.texture != null
                               || bi.sprite != null
                               || bi.vectorImage != null
                               || bi.renderTexture != null;

                if (!hasBgColor && !hasBgImage) continue;
                CaptureOriginalBg(ve);
                _touchedDescendants.Add(ve);
            }
        }

        foreach (var ve in _touchedDescendants)
        {
            if (ve == null) continue;
            try
            {
                if (on)
                {
                    ve.style.backgroundColor = transparentColor;
                    ve.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                }
                else if (_originalBg.TryGetValue(ve, out var orig))
                {
                    ve.style.backgroundColor = orig.color;
                    ve.style.backgroundImage = orig.image;
                }
            }
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
            // Apply every visual override once on show so the first
            // paint already reflects whatever the user has toggled.
            // Each helper internally checks its own flag and is a
            // no-op when off.
            try
            {
                ApplyContainerTransparency(__instance);
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
            ShowChatContainer(__instance);

            // Show a "[TEAM]" prefix on the input when typing in team
            // chat. UI Toolkit renders BaseField<T>.label to the left
            // of the input element automatically, so we just set the
            // property — no DOM injection required. All-chat keeps the
            // default empty label.
            try
            {
                var tf = GetTextField(__instance);
                if (tf != null)
                    tf.label = __instance.IsTeamChat ? "  [TEAM]" : string.Empty;
            }
            catch (Exception e) { Debug.LogWarning("[QoL] team-chat label apply failed: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(UIChat), "StopInput")]
    private static class UIChat_StopInput_Postfix
    {
        private static void Postfix(UIChat __instance)
        {
            // Clear the [TEAM] prefix regardless of the hide-inactive
            // flag so it doesn't linger after the input collapses.
            try
            {
                var tf = GetTextField(__instance);
                if (tf != null) tf.label = string.Empty;
            }
            catch (Exception e) { Debug.LogWarning("[QoL] team-chat label clear failed: " + e.Message); }

            if (!Enabled) return;
            // User closed chat. Re-check — if every message is already
            // past its blur point, collapse the container immediately.
            HideContainerIfAllBlurred(__instance);
        }
    }

    // When the chat container was just re-shown after being hidden, the
    // first scroll-to-bottom call would otherwise tween over the entire
    // (now-grown) scroll distance in 0.2s, which reads as a distracting
    // fast scroll. Detect that case and teleport instead.
    [HarmonyPatch(typeof(UIChat), "SmoothScrollToVerticalPosition")]
    private static class UIChat_SmoothScroll_SnapAfterShow_Prefix
    {
        private static bool Prefix(UIChat __instance, float position, bool isBottomPosition)
        {
            if (!Enabled) return true;
            if (Time.unscaledTime > _snapUntilUnscaledTime) return true; // window expired → vanilla tween
            try
            {
                if (_fiScrollView == null) _fiScrollView = AccessTools.Field(typeof(UIChat), "scrollView");
                var scrollView = _fiScrollView?.GetValue(__instance) as ScrollView;
                if (scrollView == null) return true;

                // 1. Kill any in-flight DOTween smoothScrollTween. Vanilla
                //    does this at the start of SmoothScrollToVerticalPosition,
                //    but since we return false to skip vanilla we have to
                //    kill it ourselves — otherwise a prior tween keeps
                //    running and overwrites our scrollOffset write next
                //    frame.
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

                // 2. Mirror vanilla's target math: position is the bottom-of
                //    -new-child; subtract viewport height when isBottomPosition
                //    so the new message sits flush at the bottom of the
                //    viewport.
                float viewport = scrollView.contentViewport?.resolvedStyle.height ?? 0f;
                float targetY = position - (isBottomPosition ? viewport : 0f);
                if (targetY < 0f) targetY = 0f;
                scrollView.scrollOffset = new Vector2(scrollView.scrollOffset.x, targetY);

                // 3. Mirror the flag bookkeeping vanilla does in its
                //    tween's OnComplete handler so the next valueChanged
                //    callback computes autoScroll correctly (= true at
                //    bottom). Without this autoScroll would be stuck off
                //    after the snap, and subsequent messages would never
                //    fire SmoothScrollToVerticalPosition.
                if (_fiIsScrolling == null) _fiIsScrolling = AccessTools.Field(typeof(UIChat), "isScrolling");
                if (_fiAutoScroll == null)  _fiAutoScroll  = AccessTools.Field(typeof(UIChat), "autoScroll");
                _fiIsScrolling?.SetValue(__instance, false);
                _fiAutoScroll?.SetValue(__instance, true);

                // 4. Re-apply on the next frame too. UIToolkit's ScrollView
                //    clamps scrollOffset against the CURRENT verticalScroller
                //    highValue — if the new content's geometry hasn't fully
                //    propagated to the scroller yet (which happens during a
                //    cascade of GeometryChangedEvents in one frame), the
                //    initial write can get truncated to a smaller max. The
                //    scheduled follow-up runs after the next layout pass
                //    when highValue has caught up.
                float capturedTarget = targetY;
                scrollView.schedule.Execute(() =>
                {
                    try { scrollView.scrollOffset = new Vector2(scrollView.scrollOffset.x, capturedTarget); }
                    catch { }
                }).ExecuteLater(0);

                return false; // skip vanilla tween
            }
            catch (Exception e)
            {
                Plugin.LogWarning("[QoL] chat snap-to-bottom failed: " + e.Message);
                return true;
            }
        }
    }
}
