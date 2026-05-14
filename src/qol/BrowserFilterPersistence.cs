// Persist server browser filter state across sessions.
//
// The base game's UIServerBrowser.Awake hard-codes filter defaults
// (search="", maxPing=100, showFull=true, ...) on every load — there is no
// vanilla persistence. This patch hooks UIServerBrowser.Show to
//   1. write the user's saved values back onto the controls (which fires the
//      base-game ChangeEvent handlers, so the filter actually applies), and
//   2. register save callbacks once per browser instance so subsequent edits
//      flow back into the QoL profile.
//
// Independent of the inline-filters feature: the user's preferences are
// remembered whether they're using our compact strip or the vanilla popup.

using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.qol;

internal static class BrowserFilterPersistence
{
    // Tracks the browser instance whose controls have already had save
    // callbacks attached, so we don't stack duplicates on each Show().
    private static UIServerBrowser _hookedFor;

    [HarmonyPatch(typeof(UIServerBrowser), "Show")]
    private static class ServerBrowser_Show_PersistFilters
    {
        private static void Postfix(UIServerBrowser __instance) => ApplyAndHook(__instance);
    }

    private static void ApplyAndHook(UIServerBrowser browser)
    {
        if (browser == null) return;
        var cfg = QoLRunner.Instance?.Config;
        if (cfg == null || !cfg.enableBrowserFilterPersistence) return;

        try
        {
            // Read controls from UIServerBrowser's private fields rather than
            // walking the visual tree under `filters`. The InlineServerBrowserFilters
            // patch yanks the controls out of the `filters` wrapper into a
            // separate strip — if its postfix runs before ours, a Q lookup
            // through `filters` returns null and we'd silently never hook
            // any save callbacks. The private fields point at the same
            // instances regardless of where they're parented.
            var search      = AccessTools.Field(typeof(UIServerBrowser), "searchTextField")?.GetValue(browser) as TextField;
            var maxPing     = AccessTools.Field(typeof(UIServerBrowser), "maxPingTextField")?.GetValue(browser) as IntegerField;
            var showFull    = AccessTools.Field(typeof(UIServerBrowser), "showFullToggle")?.GetValue(browser) as Toggle;
            var showEmpty   = AccessTools.Field(typeof(UIServerBrowser), "showEmptyToggle")?.GetValue(browser) as Toggle;
            var showPwd     = AccessTools.Field(typeof(UIServerBrowser), "showPasswordProtectedToggle")?.GetValue(browser) as Toggle;
            var showModded  = AccessTools.Field(typeof(UIServerBrowser), "showModdedToggle")?.GetValue(browser) as Toggle;
            var showUnreach = AccessTools.Field(typeof(UIServerBrowser), "showUnreachableToggle")?.GetValue(browser) as Toggle;

            // Apply saved values. Setting .value fires the base-game
            // ChangeEvent which re-runs the filter — exactly what we want.
            if (search      != null) search.value      = cfg.browserSearch ?? "";
            if (maxPing     != null) maxPing.value     = cfg.browserMaxPing;
            if (showFull    != null) showFull.value    = cfg.browserShowFull;
            if (showEmpty   != null) showEmpty.value   = cfg.browserShowEmpty;
            if (showPwd     != null) showPwd.value     = cfg.browserShowLocked;
            if (showModded  != null) showModded.value  = cfg.browserShowModded;
            if (showUnreach != null) showUnreach.value = cfg.browserShowUnreachable;

            // Hook save callbacks once per browser instance. Show() can
            // fire repeatedly; without this guard each open would stack
            // another callback on every control.
            if (_hookedFor == browser) return;
            _hookedFor = browser;

            if (search != null)
                search.RegisterCallback<ChangeEvent<string>>(ev => Save(c => c.browserSearch = ev.newValue ?? ""));
            if (maxPing != null)
                maxPing.RegisterCallback<ChangeEvent<int>>(ev => Save(c => c.browserMaxPing = ev.newValue));
            if (showFull != null)
                showFull.RegisterValueChangedCallback(ev => Save(c => c.browserShowFull = ev.newValue));
            if (showEmpty != null)
                showEmpty.RegisterValueChangedCallback(ev => Save(c => c.browserShowEmpty = ev.newValue));
            if (showPwd != null)
                showPwd.RegisterValueChangedCallback(ev => Save(c => c.browserShowLocked = ev.newValue));
            if (showModded != null)
                showModded.RegisterValueChangedCallback(ev => Save(c => c.browserShowModded = ev.newValue));
            if (showUnreach != null)
                showUnreach.RegisterValueChangedCallback(ev => Save(c => c.browserShowUnreachable = ev.newValue));
        }
        catch (Exception e) { Debug.LogWarning("[QoL] BrowserFilterPersistence apply/hook failed: " + e.Message); }
    }

    private static void Save(Action<QoLConfig> mutate)
    {
        var runner = QoLRunner.Instance;
        var cfg = runner?.Config;
        // Also short-circuit when the feature is off — registered
        // callbacks linger across toggle flips, and we don't want them
        // writing back when the user has disabled persistence.
        if (cfg == null || !cfg.enableBrowserFilterPersistence) return;
        try
        {
            mutate(cfg);
            runner.SaveAndRefresh();
        }
        catch (Exception e) { Debug.LogWarning("[QoL] BrowserFilterPersistence save failed: " + e.Message); }
    }
}