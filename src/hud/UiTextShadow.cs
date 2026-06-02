// UiTextShadow — applies a UI Toolkit text-shadow to every
// `TextElement` (the base class for `Label` AND the text-bearing parts
// of `TextField`) under a curated set of in-game UI views.
//
// Scope is intentionally limited to gameplay HUD overlays so menu /
// popup text (Pause Menu, Settings, Mods, Server Browser, popups,
// etc.) stays at vanilla styling. The whitelist:
//
//     UIManager.GameState        — top score / period / clock
//     UIManager.Chat             — chat messages + the text-input cursor
//     UIManager.Scoreboard       — hold-to-view player list
//     UIManager.Hud              — speed indicator + stamina
//     UIManager.Announcements    — goal / assist banner
//     UIManager.Usernames        — floating nameplate text
//
// The minimap is deliberately NOT in this list: its only text is the
// per-player jersey numbers, and a drop-shadow on those little glyphs
// reads as muddy rather than crisp.
//
// Each is found via reflection against the named UIManager field, then
// we walk that view's root once for all current TextElements and
// register a GeometryChangedEvent re-walk callback so labels that
// mount lazily (per-chat message, per-goal banner, etc.) also pick up
// the shadow.
//
// Gated by `cfg.enableUiTextShadow`. `RefreshForCurrentState()` is
// called from the QoL toggle handler so flips re-walk every whitelisted
// view immediately.

using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

using ToasterReskinLoader.core;

namespace ToasterReskinLoader.hud;

internal static class UiTextShadow
{
    // CSS analogue: text-shadow: 1px 1px 2px rgba(0,0,0,0.85).
    private static readonly TextShadow ShadowOn = new TextShadow
    {
        offset = new Vector2(1f, 1f),
        blurRadius = 2f,
        color = new Color(0f, 0f, 0f, 0.85f),
    };

    // UIManager field names to walk. Anything not in this list (menus,
    // popups, the matchmaking panel etc.) stays at vanilla shadowing.
    private static readonly string[] InGameViewFields =
    {
        "GameState",
        "Chat",
        "Scoreboard",
        "Hud",
        "Announcements",
        "Usernames",
    };

    private static bool Enabled => QoLRunner.Instance?.Config?.enableUiTextShadow ?? true;

    public static void Initialize()
    {
        try { ApplyAcrossAllInGameViews(); }
        catch (Exception e) { Plugin.LogError("[QoL] UiTextShadow init failed: " + e); }
    }

    // Called from GeneralSection on toggle flip.
    public static void RefreshForCurrentState()
    {
        try { ApplyAcrossAllInGameViews(); }
        catch (Exception e) { Plugin.LogWarning("[QoL] UiTextShadow refresh failed: " + e.Message); }
    }

    // Apply (or clear) the shadow on a single subtree on demand. For
    // elements created lazily AFTER the per-view walk — e.g. the chat
    // input's "[TEAM]" label, which BaseField only instantiates the first
    // time a non-empty label is set, and which no chat-root
    // GeometryChanged necessarily covers (adding it doesn't resize the
    // root). Honors the on/off state like the regular walk.
    internal static void ApplyToSubtree(VisualElement root)
    {
        if (root == null) return;
        try { WalkAndApply(root); }
        catch (Exception e) { Plugin.LogWarning("[QoL] UiTextShadow ApplyToSubtree failed: " + e.Message); }
    }

    // Catch any in-game view that becomes visible after our initial
    // walk. UIView.Show is virtual on the base class and overridden by
    // each concrete view — we patch the base method and filter by the
    // concrete type so the hook is single-source-of-truth.
    [HarmonyPatch(typeof(UIView), nameof(UIView.Show))]
    private static class UIView_Show_Postfix
    {
        [HarmonyPostfix]
        private static void Postfix(UIView __instance, bool __result)
        {
            if (!__result) return;
            if (__instance == null) return;
            if (!IsInGameView(__instance)) return;
            try
            {
                var root = __instance.View;
                if (root != null)
                {
                    WalkAndApply(root);
                    HookGeometryRewalk(root);
                }
            }
            catch { }
        }
    }

    // GeometryChangedEvent re-walks miss some chat messages in
    // practice (the new child's geometry can land between events).
    // Re-walk the whole chat view after each chat-line append so the
    // shadow lands on every label consistently.
    [HarmonyPatch(typeof(UIChat), "AddChatMessage")]
    private static class UIChat_AddChatMessage_Postfix
    {
        [HarmonyPostfix]
        private static void Postfix(UIChat __instance)
        {
            if (!Enabled || __instance == null) return;
            try
            {
                var root = __instance.View;
                if (root != null) WalkAndApply(root);
            }
            catch { }
        }
    }

    // ─────────────────────────── helpers ──────────────────────────────────

    private static bool IsInGameView(UIView v)
    {
        string name = v.GetType().Name;
        switch (name)
        {
            case "UIGameState":
            case "UIChat":
            case "UIScoreboard":
            case "UIHUD":
            case "UIAnnouncements":
            case "UIUsernames":
                return true;
            default:
                return false;
        }
    }

    private static void ApplyAcrossAllInGameViews()
    {
        var ui = MonoBehaviourSingleton<UIManager>.Instance;
        if (ui == null) return;
        foreach (var fieldName in InGameViewFields)
        {
            var field = AccessTools.Field(typeof(UIManager), fieldName);
            var view = field?.GetValue(ui);
            if (view == null) continue;
            var root = view.GetType()
                .GetProperty("View", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(view) as VisualElement;
            if (root == null) continue;
            WalkAndApply(root);
            HookGeometryRewalk(root);
        }
    }

    private static void HookGeometryRewalk(VisualElement root)
    {
        // Re-register every call so we know we're hooked exactly once
        // per root — register without an unregister is harmless but
        // could stack if the same root mounted twice somehow.
        root.UnregisterCallback<GeometryChangedEvent>(OnRootGeometry);
        root.RegisterCallback<GeometryChangedEvent>(OnRootGeometry);
    }

    private static void OnRootGeometry(GeometryChangedEvent evt)
    {
        // When the feature is off there's nothing to add on a geometry
        // change — the one-time clear is handled by RefreshForCurrentState.
        // Bail before the (not-free) TextElement subtree query rather than
        // walk + write Null forever after the first disable.
        if (!Enabled) return;
        if (evt.target is VisualElement ve) WalkAndApply(ve);
    }

    private static void WalkAndApply(VisualElement root)
    {
        bool on = Enabled;
        // TextElement is the base class for Label + the text content
        // of TextField, so one query covers every shadowable element
        // inside the in-game views.
        foreach (var te in root.Query<TextElement>().Build())
        {
            try
            {
                // Assigning style.textShadow always marks the element dirty
                // even when the value is unchanged, which churns layout on
                // the frequent GeometryChanged re-walks (chat especially).
                // Only write when our inline value actually differs.
                // (resolvedStyle doesn't expose textShadow, so we compare
                // against the inline style we previously set.)
                StyleTextShadow cur = te.style.textShadow;
                if (on)
                {
                    bool alreadyOn = cur.keyword == StyleKeyword.Undefined
                                     && cur.value.Equals(ShadowOn);
                    if (!alreadyOn) te.style.textShadow = ShadowOn;
                }
                else if (cur.keyword != StyleKeyword.Null)
                {
                    te.style.textShadow = StyleKeyword.Null;
                }
            }
            catch { }
        }
    }
}
