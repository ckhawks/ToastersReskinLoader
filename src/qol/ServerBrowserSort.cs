// Server browser tweaks:
//   * Reset sort to PLAYERS%-descending every time the browser opens so
//     populated servers float to the top by default. (Vanilla defaults to
//     Name-ascending because both sort enums default to 0.)
//   * Repurpose the vanilla PLAYERS column: rename its header to
//     PLAYERS% and override its sort comparator to use the
//     players / maxPlayers ratio instead of absolute player count.
//   * Saved-password indicator: rows that match an entry in
//     SavedServerPasswords have the vanilla "passwordProtected" USS class
//     stripped (suppressing the 🔒 lock icon) and get a 🔓 label inserted
//     immediately after NameLabel so it sits right next to the wrench
//     (modded) icon, mirroring how the vanilla lock + wrench cluster.
//
// All gated behind cfg.enableServerBrowserSortTweaks. The vanilla
// `ServerSortType` and `ServerSortDirection` enums are internal — we set
// their fields via AccessTools and use the raw int values (Name=0,
// Players=1, Ping=2; Ascending=0, Descending=1).

using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.qol;

internal static class ServerBrowserSort
{
    private const int SortType_Name    = 0;
    private const int SortType_Players = 1;
    private const int SortType_Ping    = 2;
    private const int SortDir_Ascending  = 0;
    private const int SortDir_Descending = 1;

    private const string UnlockBadgeName = "ToasterSavedPwUnlock";

    private static bool Enabled =>
        QoLRunner.Instance?.Config?.enableServerBrowserSortTweaks ?? false;

    // ─────────────────────────── reflection helpers ───────────────────────

    private static Dictionary<EndPoint, VisualElement> GetMap(UIServerBrowser ui)
    {
        var f = AccessTools.Field(typeof(UIServerBrowser), "endPointVisualElementMap");
        return f?.GetValue(ui) as Dictionary<EndPoint, VisualElement>;
    }

    private static ServerPreviewData GetPreview(UIServerBrowser ui, EndPoint ep)
    {
        var map = GetMap(ui);
        if (map == null || ep == null || !map.TryGetValue(ep, out var rowElem)) return null;
        var server = rowElem?.Q<VisualElement>("Server");
        var ud = server?.userData as Dictionary<string, object>;
        if (ud == null) return null;
        return ud.TryGetValue("previewData", out var pd) ? pd as ServerPreviewData : null;
    }

    private static EndPoint GetEndPointFromRow(UIServerBrowser ui, VisualElement row)
    {
        var map = GetMap(ui);
        if (map == null) return null;
        foreach (var kv in map)
        {
            if (kv.Value == row) return kv.Key;
        }
        return null;
    }

    private static int GetSortType(UIServerBrowser ui)
    {
        var f = AccessTools.Field(typeof(UIServerBrowser), "sortType");
        var v = f?.GetValue(ui);
        return v != null ? Convert.ToInt32(v) : SortType_Name;
    }

    private static int GetSortDirection(UIServerBrowser ui)
    {
        var f = AccessTools.Field(typeof(UIServerBrowser), "sortDirection");
        var v = f?.GetValue(ui);
        return v != null ? Convert.ToInt32(v) : SortDir_Ascending;
    }

    private static string MakeKey(EndPoint ep)
        => ep == null ? null : ep.ipAddress + ":" + ep.port;

    // ─────────────────────────── live toggle refresh ──────────────────────
    //
    // The QoL settings row flips cfg.enableServerBrowserSortTweaks, but
    // the patched methods only short-circuit on the *next* call. If the
    // browser is already open the user sees the prior frame's mutations
    // (PLAYERS% header, 🔓 badges, stripped passwordProtected class)
    // until something else triggers a re-render. This method drives that
    // re-render explicitly. When the feature is ON it's a no-op because
    // the postfixes already do the right work; when it's OFF it forces
    // vanilla's StyleSortButtons / StyleServer / SortServers to run with
    // our postfixes short-circuiting, leaving the UI in vanilla state.
    public static void RefreshForCurrentBrowser()
    {
        try
        {
            var ui = MonoBehaviourSingleton<UIManager>.Instance;
            var browser = ui?.ServerBrowser;
            if (browser == null) return;
            if (!browser.IsVisible) return;

            // Strip 🔓 badges and restore the vanilla lock when disabled —
            // vanilla StyleServer doesn't know about our badge, so it
            // would otherwise persist on every row.
            if (!Enabled)
            {
                var map = GetMap(browser);
                if (map != null)
                {
                    foreach (var kv in map)
                    {
                        var row = kv.Value?.Q<VisualElement>("Server");
                        var unlock = row?.Q<Label>(UnlockBadgeName);
                        unlock?.RemoveFromHierarchy();
                    }
                }
            }

            // Vanilla StyleSortButtons rewrites playersButton.text
            // unconditionally — calling it with the feature disabled
            // restores "PLAYERS" / "▼ PLAYERS" / "▲ PLAYERS" cleanly.
            AccessTools.Method(typeof(UIServerBrowser), "StyleSortButtons")?.Invoke(browser, null);

            // Re-run StyleServer for every row. With the feature ON our
            // postfix re-adds the 🔓 badge; with it OFF the postfix
            // short-circuits and vanilla's passwordProtected class re-
            // applies normally.
            var styleServer = AccessTools.Method(typeof(UIServerBrowser), "StyleServer");
            if (styleServer != null)
            {
                var map = GetMap(browser);
                if (map != null)
                {
                    foreach (var ep in map.Keys)
                        styleServer.Invoke(browser, new object[] { ep });
                }
            }

            // Re-sort: vanilla's sort runs first, then our postfix decides
            // whether to apply ratio-mode. Filters are unaffected.
            AccessTools.Method(typeof(UIServerBrowser), "SortServers")?.Invoke(browser, null);
        }
        catch (Exception e) { Debug.LogWarning("[QoL] sort-tweaks refresh failed: " + e.Message); }
    }

    // ─────────────────────────── Show: reset default sort ─────────────────

    [HarmonyPatch(typeof(UIServerBrowser), "Show")]
    private static class Show_ResetDefault_Postfix
    {
        private static void Postfix(UIServerBrowser __instance, bool __result)
        {
            if (!Enabled) return;
            if (__instance == null) return;
            try
            {
                var tField = AccessTools.Field(typeof(UIServerBrowser), "sortType");
                var dField = AccessTools.Field(typeof(UIServerBrowser), "sortDirection");
                tField?.SetValue(__instance, Enum.ToObject(tField.FieldType, SortType_Players));
                dField?.SetValue(__instance, Enum.ToObject(dField.FieldType, SortDir_Descending));

                AccessTools.Method(typeof(UIServerBrowser), "StyleSortButtons")?.Invoke(__instance, null);

                // Re-style every server row so badges show up retroactively
                // when the feature was enabled while the browser already
                // had data. StyleServer is the entry point that adds our
                // saved-password badge.
                var map = GetMap(__instance);
                if (map != null)
                {
                    var styleServer = AccessTools.Method(typeof(UIServerBrowser), "StyleServer");
                    if (styleServer != null)
                    {
                        foreach (var ep in map.Keys)
                            styleServer.Invoke(__instance, new object[] { ep });
                    }
                }

                // Re-run vanilla filtering after our StyleServer pass.
                // BrowserFilterPersistence's Show postfix sets toggle
                // values (firing ChangeEvent → vanilla FilterServers),
                // but Harmony postfix ordering is undefined — if our
                // postfix lands BEFORE persistence, the saved toggle
                // values applied later won't re-trigger filtering on
                // rows whose previewData was already populated from a
                // prior session of the browser. A direct FilterServers
                // here guarantees the visible set matches the current
                // toggle state.
                AccessTools.Method(typeof(UIServerBrowser), "FilterServers")?.Invoke(__instance, null);
                AccessTools.Method(typeof(UIServerBrowser), "SortServers")?.Invoke(__instance, null);
            }
            catch (Exception e) { Debug.LogWarning("[QoL] sort-tweaks Show postfix failed: " + e.Message); }
        }
    }

    // The "full enough for gameplay" cap. A 6v6 server with 12 active
    // players is just as joinable as a 30-slot server holding 12, so the
    // ratio caps both numerator and denominator at this value before
    // dividing. 12/12, 12/14, 12/62 → all 100%. 6/anything → 50%.
    private const int RatioGameplayCap = 12;

    // ─────────────────────────── SortServers postfix ──────────────────────
    //
    // Vanilla's SortServers ran first. When sort column is PLAYERS, we
    // override its absolute-count ordering with a capped players-ratio
    // (see RatioGameplayCap) in the same direction. Other columns (NAME,
    // PING) inherit vanilla's order untouched.
    [HarmonyPatch(typeof(UIServerBrowser), "SortServers")]
    private static class SortServers_RatioMode_Postfix
    {
        private static void Postfix(UIServerBrowser __instance)
        {
            if (!Enabled) return;
            try
            {
                var serverList = AccessTools.Field(typeof(UIServerBrowser), "serverList")?.GetValue(__instance) as VisualElement;
                if (serverList == null) return;
                int sortType = GetSortType(__instance);
                int sortDir  = GetSortDirection(__instance);
                if (sortType != SortType_Players) return;

                serverList.hierarchy.Sort(delegate(VisualElement a, VisualElement b)
                {
                    EndPoint epA = GetEndPointFromRow(__instance, a);
                    EndPoint epB = GetEndPointFromRow(__instance, b);
                    var pdA = GetPreview(__instance, epA);
                    var pdB = GetPreview(__instance, epB);
                    float rA = ComputeRatio(pdA);
                    float rB = ComputeRatio(pdB);
                    int dirMul = sortDir == SortDir_Ascending ? 1 : -1;
                    int cmp = rA.CompareTo(rB) * dirMul;
                    if (cmp != 0) return cmp;
                    // Tiebreaker: name ascending (stable, predictable).
                    string nA = pdA?.name ?? (epA?.ToString() ?? "");
                    string nB = pdB?.name ?? (epB?.ToString() ?? "");
                    return string.Compare(nA, nB, StringComparison.Ordinal);
                });
            }
            catch (Exception e) { Debug.LogWarning("[QoL] sort-tweaks SortServers postfix failed: " + e.Message); }
        }
    }

    // min(players, cap) / min(maxPlayers, cap). Returns -1 for missing
    // previewData so unpinged rows sort to the bottom in descending mode
    // (same behavior as the previous uncapped implementation).
    private static float ComputeRatio(ServerPreviewData pd)
    {
        if (pd == null || pd.maxPlayers <= 0) return -1f;
        int p = Math.Min(pd.players, RatioGameplayCap);
        int m = Math.Min(pd.maxPlayers, RatioGameplayCap);
        return m <= 0 ? -1f : (float)p / m;
    }

    // ─────────────────────────── StyleSortButtons postfix ─────────────────
    //
    // Vanilla just set the PLAYERS button text to one of
    //   "PLAYERS" / "▼ PLAYERS" / "▲ PLAYERS". Replace the word so the
    // header reads PLAYERS% (the column is now a ratio sort).
    [HarmonyPatch(typeof(UIServerBrowser), "StyleSortButtons")]
    private static class StyleSortButtons_RenameToPercent_Postfix
    {
        private static void Postfix(UIServerBrowser __instance)
        {
            if (!Enabled) return;
            try
            {
                var playersBtn = AccessTools.Field(typeof(UIServerBrowser), "playersButton")?.GetValue(__instance) as Button;
                if (playersBtn == null) return;
                if (!string.IsNullOrEmpty(playersBtn.text) && playersBtn.text.Contains("PLAYERS") && !playersBtn.text.Contains("PLAYERS%"))
                {
                    playersBtn.text = playersBtn.text.Replace("PLAYERS", "PLAYERS%");
                }
            }
            catch { }
        }
    }

    // ─────────────────────────── StyleServer postfix: badges ──────────────
    //
    // Inject the 🔓 saved-password indicator (when we have a saved entry
    // for this ip:port). It's placed immediately after NameLabel so it
    // sits directly left of the wrench (modded) icon — mirroring how the
    // vanilla 🔒 + 🛠 cluster in the same flex slot. We also strip the
    // "passwordProtected" USS class for those rows to suppress the
    // vanilla lock so they don't double up.
    [HarmonyPatch(typeof(UIServerBrowser), "StyleServer")]
    private static class StyleServer_AddBadges_Postfix
    {
        private static void Postfix(UIServerBrowser __instance, EndPoint endPoint)
        {
            if (!Enabled) return;
            if (__instance == null || endPoint == null) return;
            try
            {
                var map = GetMap(__instance);
                if (map == null || !map.TryGetValue(endPoint, out var rowElem) || rowElem == null) return;
                var serverRow = rowElem.Q<VisualElement>("Server");
                if (serverRow == null) return;
                string key = MakeKey(endPoint);

                var nameLabel = serverRow.Q<Label>("NameLabel");

                // Saved-password indicator: replace the vanilla 🔒 lock with
                // a 🔓 sitting immediately after NameLabel. The wrench
                // (modded) icon is rendered into the same flex slot by
                // vanilla USS, so anchoring our unlock to NameLabel's
                // sibling-after-position puts 🔓 directly left of the
                // wrench — matching how the vanilla 🔒 + 🛠 cluster.
                bool willAutoFill = (QoLRunner.Instance?.Config?.enableSavedServerPasswords ?? false)
                                    && HasSavedPasswordFor(key);
                var unlock = serverRow.Q<Label>(UnlockBadgeName);
                if (willAutoFill)
                {
                    if (serverRow.ClassListContains("passwordProtected"))
                        serverRow.RemoveFromClassList("passwordProtected");

                    if (unlock == null)
                    {
                        unlock = new Label("🔓")
                        {
                            name = UnlockBadgeName,
                        };
                        unlock.style.marginRight = 4;
                        unlock.style.color = new Color(0.6f, 1f, 0.6f);
                        unlock.style.unityFontStyleAndWeight = FontStyle.Bold;
                        unlock.tooltip = "Saved password — auto-fills on join.";
                    }

                    // Reposition each refresh so the unlock stays right
                    // after NameLabel even if vanilla / another mod
                    // shuffles children.
                    if (nameLabel != null && nameLabel.parent != null)
                    {
                        int targetIdx = nameLabel.parent.IndexOf(nameLabel) + 1;
                        if (unlock.parent != nameLabel.parent || nameLabel.parent.IndexOf(unlock) != targetIdx)
                        {
                            unlock.RemoveFromHierarchy();
                            // Re-resolve index in case the remove shifted things.
                            targetIdx = nameLabel.parent.IndexOf(nameLabel) + 1;
                            nameLabel.parent.Insert(targetIdx, unlock);
                        }
                    }
                    else if (unlock.parent == null)
                    {
                        serverRow.Add(unlock);
                    }
                }
                else if (unlock != null)
                {
                    unlock.RemoveFromHierarchy();
                }
            }
            catch (Exception e) { Debug.LogWarning("[QoL] sort-tweaks StyleServer postfix failed: " + e.Message); }
        }
    }

    private static bool HasSavedPasswordFor(string key)
    {
        var store = QoLRunner.Instance?.Config?.savedServerPasswords;
        return store != null && !string.IsNullOrEmpty(key) && store.ContainsKey(key);
    }
}
