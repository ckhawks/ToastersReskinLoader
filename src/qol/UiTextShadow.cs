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
//     UIManager.Minimap          — any labels overlaid on the minimap
//     UIManager.PlayerUsernames  — floating nameplate text
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

namespace ToasterReskinLoader.qol;

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
        "Minimap",
        "PlayerUsernames",
    };

    private static bool Enabled => QoLRunner.Instance?.Config?.enableUiTextShadow ?? true;

    public static void Initialize()
    {
        try { ApplyAcrossAllInGameViews(); }
        catch (Exception e) { Plugin.LogError("[QoL] UiTextShadow init failed: " + e); }
    }

    // Called from PlayerQoLSection on toggle flip.
    public static void RefreshForCurrentState()
    {
        try { ApplyAcrossAllInGameViews(); }
        catch (Exception e) { Plugin.LogWarning("[QoL] UiTextShadow refresh failed: " + e.Message); }
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
            case "UIMinimap":
            case "UIPlayerUsernames":
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
                if (on) te.style.textShadow = ShadowOn;
                else    te.style.textShadow = StyleKeyword.Null;
            }
            catch { }
        }
    }
}
