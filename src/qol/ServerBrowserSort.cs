// Server browser tweaks:
//   * Reset sort to PLAYERS%-descending every time the browser opens so
//     populated servers float to the top by default. (Vanilla defaults to
//     Name-ascending because both sort enums default to 0.)
//   * Repurpose the vanilla PLAYERS column: rename its header to
//     PLAYERS% and override its sort comparator to use the
//     players / maxPlayers ratio (capped at RatioGameplayCap) instead of
//     absolute player count.
//   * Saved-password indicator: rows that match an entry in
//     SavedServerPasswords have the vanilla "passwordProtected" USS class
//     stripped (suppressing the 🔒 lock icon) and get a 🔓 label inserted
//     immediately after NameLabel so it sits right next to the wrench
//     (modded) icon, mirroring how the vanilla lock + wrench cluster.
//   * Favorites: ★/☆ button at the start of every row. Clicking adds /
//     removes the ip:port from cfg.favoriteServers (with the friendly
//     name cached so the QoL management UI can render it offline).
//     Favorited rows always sort above non-favorites regardless of the
//     active column.
//   * Row hover tooltip: when the row has the modded wrench icon, the
//     tooltip explains how many of the required mods the user already
//     has installed (so a quick hover answers "can I join this?"). When
//     the row is password-protected, the tooltip indicates whether we
//     have a saved password for it. The 🔓 saved-password badge keeps
//     its own more-specific tooltip, so hovering precisely on it still
//     shows the badge text instead of the row text.
//
// Gated per-store behind cfg.enableServerFavorites (★ button + sort
// favorites to top) and cfg.enableServerBlocks (right-click block +
// hide blocked rows). Either flag being on activates the shared
// browser-side scaffolding (sort tweaks, row context-menu). The vanilla
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

    private const string UnlockBadgeName  = "ToasterSavedPwUnlock";
    private const string FavStarName      = "ToasterFavoriteStar";
    private const string ContextMenuName  = "ToasterServerContextMenu";
    private const string RowMarkerClass   = "toaster-row-ctx-hooked";
    private const string TooltipMarkerCls = "toaster-row-tt-hooked";
    private const string HoverTooltipName = "ToasterServerHoverTooltip";

    // Unicode BLACK STAR / WHITE STAR plus U+FE0E (VS-15) — forces text
    // presentation so the OS-font fallback picks Segoe UI Symbol's flat
    // glyph instead of Segoe UI Emoji's color emoji. Without VS-15 the
    // emoji presentation wins and the star renders as a yellow
    // pictograph that ignores style.color tinting.
    private const string GlyphStarFilled = "★︎";
    private const string GlyphStarEmpty  = "☆︎";

    private static bool FavoritesEnabled =>
        QoLRunner.Instance?.Config?.enableServerFavorites ?? false;
    private static bool BlocksEnabled =>
        QoLRunner.Instance?.Config?.enableServerBlocks ?? false;
    // Any browser-side scaffolding (sort patches, row context-menu hook,
    // row tooltip) activates when EITHER favorites or blocks is on. The
    // finer-grained gates inside (star button visibility, block-row
    // hiding) check FavoritesEnabled / BlocksEnabled directly.
    private static bool Enabled => FavoritesEnabled || BlocksEnabled;

    // Mod-title cache so the row tooltip can list required mods by
    // name instead of bare Steam Workshop IDs. Populated lazily:
    //   * Installed mods already carry their SteamWorkshopItem.Details
    //     because vanilla resolved them at Mod.Initialize.
    //   * For mods the user doesn't have, we kick off
    //     SteamWorkshopManager.GetItemDetails on the first hover and
    //     listen for Event_OnSteamWorkshopItemDetails to backfill.
    //     Next hover will show the resolved title.
    private static readonly Dictionary<string, string> _modTitleCache = new Dictionary<string, string>();
    private static readonly HashSet<string> _modDetailsRequested = new HashSet<string>();
    private static bool _initialized;

    // Called once by QoLRunner.Awake.
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        try
        {
            EventManager.AddEventListener("Event_OnSteamWorkshopItemDetails", OnSteamWorkshopItemDetails);
        }
        catch (Exception e) { Plugin.LogWarning("[QoL] ServerBrowserSort init failed: " + e.Message); }
    }

    private static void OnSteamWorkshopItemDetails(Dictionary<string, object> msg)
    {
        try
        {
            string id    = msg.TryGetValue("id",    out var idObj)    ? idObj    as string : null;
            string title = msg.TryGetValue("title", out var titleObj) ? titleObj as string : null;
            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(title))
                _modTitleCache[id] = title;
        }
        catch { }
    }

    // Returns the resolved title for a mod id, or null if not yet
    // known. First call for an unknown id kicks off the workshop
    // details request so the next hover gets the real name.
    private static string GetModTitle(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (_modTitleCache.TryGetValue(id, out string cached)) return cached;
        try
        {
            foreach (var m in ModManager.Mods)
            {
                if (m == null || m.Id != id) continue;
                string t = m.SteamWorkshopItem?.Details?.Title;
                if (!string.IsNullOrEmpty(t))
                {
                    _modTitleCache[id] = t;
                    return t;
                }
                break;
            }
        }
        catch { }
        if (_modDetailsRequested.Add(id))
        {
            try { SteamWorkshopManager.GetItemDetails(id); } catch { }
        }
        return null;
    }

    // ──────────────────────────── favorites API ───────────────────────────

    public static bool IsFavorite(string key)
    {
        var s = QoLRunner.Instance?.Config?.favoriteServers;
        return s != null && !string.IsNullOrEmpty(key) && s.ContainsKey(key);
    }

    // Toggle a key in/out of favorites and persist. The friendly name is
    // cached on add so the QoL management UI can show it even when the
    // server isn't in the current browser listing (e.g. offline).
    public static void ToggleFavorite(string key, string cachedName)
    {
        if (string.IsNullOrEmpty(key)) return;
        var runner = QoLRunner.Instance;
        var s = runner?.Config?.favoriteServers;
        if (s == null) return;
        if (s.ContainsKey(key)) s.Remove(key);
        else                    s[key] = cachedName ?? "";
        runner.SaveAndRefresh();
    }

    public static List<string> SnapshotFavoriteKeys()
    {
        var s = QoLRunner.Instance?.Config?.favoriteServers;
        if (s == null) return new List<string>();
        var list = new List<string>(s.Keys);
        list.Sort(StringComparer.Ordinal);
        return list;
    }

    public static string GetFavoriteCachedName(string key)
    {
        var s = QoLRunner.Instance?.Config?.favoriteServers;
        return (s != null && !string.IsNullOrEmpty(key) && s.TryGetValue(key, out var n)) ? n : null;
    }

    public static void RemoveFavorite(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        var runner = QoLRunner.Instance;
        var s = runner?.Config?.favoriteServers;
        if (s == null) return;
        if (s.Remove(key)) runner.SaveAndRefresh();
    }

    public static void RemoveAllFavorites()
    {
        var runner = QoLRunner.Instance;
        var s = runner?.Config?.favoriteServers;
        if (s == null || s.Count == 0) return;
        s.Clear();
        runner.SaveAndRefresh();
    }

    // ──────────────────────────── blocking API ────────────────────────────

    public static bool IsBlocked(string key)
    {
        var s = QoLRunner.Instance?.Config?.blockedServers;
        return s != null && !string.IsNullOrEmpty(key) && s.ContainsKey(key);
    }

    // Toggle a key in/out of the block list. Blocking removes the key
    // from favorites at the same time — keeping both states would be
    // confusing ("favorited but hidden from list").
    public static void ToggleBlock(string key, string cachedName)
    {
        if (string.IsNullOrEmpty(key)) return;
        var runner = QoLRunner.Instance;
        var b = runner?.Config?.blockedServers;
        if (b == null) return;
        if (b.ContainsKey(key))
        {
            b.Remove(key);
        }
        else
        {
            b[key] = cachedName ?? "";
            runner.Config.favoriteServers?.Remove(key);
        }
        runner.SaveAndRefresh();
    }

    public static List<string> SnapshotBlockedKeys()
    {
        var s = QoLRunner.Instance?.Config?.blockedServers;
        if (s == null) return new List<string>();
        var list = new List<string>(s.Keys);
        list.Sort(StringComparer.Ordinal);
        return list;
    }

    public static string GetBlockedCachedName(string key)
    {
        var s = QoLRunner.Instance?.Config?.blockedServers;
        return (s != null && !string.IsNullOrEmpty(key) && s.TryGetValue(key, out var n)) ? n : null;
    }

    public static void RemoveBlock(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        var runner = QoLRunner.Instance;
        var s = runner?.Config?.blockedServers;
        if (s == null) return;
        if (s.Remove(key)) runner.SaveAndRefresh();
    }

    public static void RemoveAllBlocks()
    {
        var runner = QoLRunner.Instance;
        var s = runner?.Config?.blockedServers;
        if (s == null || s.Count == 0) return;
        s.Clear();
        runner.SaveAndRefresh();
    }

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
    // The QoL settings rows flip cfg.enableServerFavorites / cfg.enableServerBlocks, but
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

            // Strip the ★ button + 🔓 badge and any open context menu
            // when disabled — vanilla StyleServer doesn't know about any
            // of these so without an explicit clear they'd persist on
            // every row. Also drop the row marker class so re-enabling
            // re-attaches the right-click handler.
            if (!Enabled)
            {
                CloseContextMenu();
                var map = GetMap(browser);
                if (map != null)
                {
                    foreach (var kv in map)
                    {
                        var row = kv.Value?.Q<VisualElement>("Server");
                        if (row == null) continue;
                        row.Q<Button>(FavStarName)?.RemoveFromHierarchy();
                        row.Q<Label>(UnlockBadgeName)?.RemoveFromHierarchy();
                        var nameLbl = row.Q<Label>("NameLabel");
                        if (nameLbl != null) nameLbl.style.marginLeft = StyleKeyword.Null;
                        row.RemoveFromClassList(RowMarkerClass);
                        row.RemoveFromClassList(TooltipMarkerCls);
                    }
                }
                _hoverTooltip?.RemoveFromHierarchy();
                _hoverTooltip = null;
                // Unhide anything we previously hid via the block filter
                // so the user gets the vanilla list back instantly. The
                // FilterServers call below would also do this except for
                // rows the user blocked while the feature was on — those
                // need an explicit un-display.
                if (map != null)
                {
                    foreach (var kv in map)
                    {
                        if (kv.Value != null) kv.Value.style.display = DisplayStyle.Flex;
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
    // Vanilla's SortServers ran first. We re-sort with a primary tier of
    // "favorites above non-favorites" plus a secondary tier matching the
    // active column. The PLAYERS column uses our capped ratio (see
    // RatioGameplayCap); NAME / PING replicate vanilla's comparator so
    // the visible order matches the column the user clicked on.
    [HarmonyPatch(typeof(UIServerBrowser), "SortServers")]
    private static class SortServers_FavoritesAndRatio_Postfix
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
                int dirMul   = sortDir == SortDir_Ascending ? 1 : -1;

                serverList.hierarchy.Sort(delegate(VisualElement a, VisualElement b)
                {
                    EndPoint epA = GetEndPointFromRow(__instance, a);
                    EndPoint epB = GetEndPointFromRow(__instance, b);
                    string keyA = MakeKey(epA);
                    string keyB = MakeKey(epB);

                    // Tier 1: favorites first regardless of column.
                    bool favA = IsFavorite(keyA);
                    bool favB = IsFavorite(keyB);
                    if (favA != favB) return favA ? -1 : 1;

                    var pdA = GetPreview(__instance, epA);
                    var pdB = GetPreview(__instance, epB);
                    string nA = pdA?.name ?? (epA?.ToString() ?? "");
                    string nB = pdB?.name ?? (epB?.ToString() ?? "");

                    int cmp;
                    switch (sortType)
                    {
                        case SortType_Players:
                            cmp = ComputeRatio(pdA).CompareTo(ComputeRatio(pdB)) * dirMul;
                            if (cmp != 0) return cmp;
                            return string.Compare(nA, nB, StringComparison.Ordinal);
                        case SortType_Ping:
                            int pingA = pdA?.ping ?? int.MaxValue;
                            int pingB = pdB?.ping ?? int.MaxValue;
                            cmp = pingA.CompareTo(pingB) * dirMul;
                            if (cmp != 0) return cmp;
                            return string.Compare(nA, nB, StringComparison.Ordinal);
                        case SortType_Name:
                        default:
                            return string.Compare(nA, nB, StringComparison.Ordinal) * dirMul;
                    }
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

    // ─────────────────────────── FilterServer postfix: blocks ────────────
    //
    // Vanilla FilterServer assigns the row's display style based on its
    // filter conditions. We layer on top: if the user has blocked this
    // ip:port, force display = None regardless of vanilla's decision.
    [HarmonyPatch(typeof(UIServerBrowser), "FilterServer")]
    private static class FilterServer_HideBlocked_Postfix
    {
        private static void Postfix(UIServerBrowser __instance, EndPoint endPoint)
        {
            if (!Enabled || endPoint == null) return;
            try
            {
                string key = MakeKey(endPoint);
                if (!IsBlocked(key)) return;
                var map = GetMap(__instance);
                if (map == null || !map.TryGetValue(endPoint, out var row) || row == null) return;
                row.style.display = DisplayStyle.None;
            }
            catch (Exception e) { Debug.LogWarning("[QoL] sort-tweaks FilterServer postfix failed: " + e.Message); }
        }
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

    // Star width budget we reserve via NameLabel.marginLeft when a row
    // is favorited so the star doesn't visually overlap the server name.
    // Vanilla NameLabel ignores flex siblings prepended via Insert(0),
    // so we use an absolute-positioned star + matching marginLeft on
    // NameLabel instead.
    private const int StarReservedWidth = 22;

    // ─────────────────────────── StyleServer postfix: badges ──────────────
    //
    // Two injections:
    //   1. ★ favorite button — ONLY rendered on rows that are already
    //      favorited. Clicking it un-favorites. Adding a NEW favorite
    //      happens via the right-click context menu (registered below).
    //   2. 🔓 saved-password indicator (when we have a saved entry for
    //      this ip:port). Placed immediately after NameLabel so it sits
    //      directly left of the wrench (modded) icon — mirroring how the
    //      vanilla 🔒 + 🛠 cluster in the same flex slot. We also strip
    //      the "passwordProtected" USS class for those rows to suppress
    //      the vanilla lock so they don't double up.
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
                var previewData = GetPreview(__instance, endPoint);

                // Favorite ★ — only present on favorited rows. The button
                // is absolute-positioned so it lays on top of the row
                // without disturbing the flex children; NameLabel gets a
                // matching marginLeft to keep the server name out from
                // under the star.
                var existingStar = serverRow.Q<Button>(FavStarName);
                if (FavoritesEnabled && IsFavorite(key))
                {
                    if (existingStar == null)
                    {
                        var star = new Button(() =>
                        {
                            string cachedName = GetPreview(__instance, endPoint)?.name ?? "";
                            ToggleFavorite(key, cachedName);
                            // Force a row restyle on the next frame so
                            // the now-unfavorited star vanishes cleanly
                            // (handled by this same postfix). Mutating
                            // hierarchy in our own click handler would
                            // race the click pipeline.
                            serverRow.schedule.Execute(() =>
                            {
                                AccessTools.Method(typeof(UIServerBrowser), "SortServers")?.Invoke(__instance, null);
                            }).ExecuteLater(0);
                        })
                        {
                            name = FavStarName,
                            text = GlyphStarFilled,
                        };
                        star.tooltip = "Unfavorite";
                        star.style.position = Position.Absolute;
                        star.style.left = 4;
                        star.style.top = 0;
                        star.style.paddingLeft = 4;
                        star.style.paddingRight = 4;
                        star.style.paddingTop = 0;
                        star.style.paddingBottom = 0;
                        star.style.color = new Color(1f, 0.85f, 0.3f);
                        star.style.unityFontStyleAndWeight = FontStyle.Bold;
                        star.style.backgroundColor = new Color(0, 0, 0, 0);
                        star.style.borderTopWidth = 0;
                        star.style.borderRightWidth = 0;
                        star.style.borderBottomWidth = 0;
                        star.style.borderLeftWidth = 0;
                        serverRow.Add(star);
                    }
                    if (nameLabel != null) nameLabel.style.marginLeft = StarReservedWidth;
                }
                else
                {
                    existingStar?.RemoveFromHierarchy();
                    if (nameLabel != null) nameLabel.style.marginLeft = StyleKeyword.Null;
                }

                // Refresh the favorite's cached name when we see the
                // server again with a fresh previewData — server owners
                // rename rooms; without this the management UI would
                // keep showing the name from when the user first
                // starred the row.
                if (previewData != null && !string.IsNullOrEmpty(previewData.name) && IsFavorite(key))
                {
                    var s = QoLRunner.Instance?.Config?.favoriteServers;
                    if (s != null && (!s.TryGetValue(key, out var cur) || cur != previewData.name))
                    {
                        s[key] = previewData.name;
                        // No SaveAndRefresh here — name refresh is best-
                        // effort cosmetic. Next intentional config save
                        // flushes it to disk.
                    }
                }

                // Right-click context menu — registered once per row via
                // a marker class so repeated StyleServer passes don't
                // stack callbacks. Mirrors the b202 ServerQueue mod's
                // contextual menu (Favorite / Block / Copy IP).
                if (!serverRow.ClassListContains(RowMarkerClass))
                {
                    serverRow.AddToClassList(RowMarkerClass);
                    var rowRef = serverRow;
                    var browserRef = __instance;
                    serverRow.RegisterCallback<PointerDownEvent>(evt =>
                    {
                        if (evt.button != 1) return; // right mouse only
                        evt.StopPropagation();
                        ShowContextMenu(browserRef, endPoint, rowRef, evt.position);
                    });
                }

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

                // Row tooltip: combines wrench (modded) + lock (password)
                // info. Vanilla's built-in UIToolkit tooltip property
                // proved unreliable on these rows (Button children seem
                // to absorb the hover event before the tooltip system
                // resolves it), so we drive our own floating label via
                // MouseEnter/MouseLeave instead.
                AttachHoverTooltip(serverRow, () => ComputeRowTooltip(GetPreview(__instance, endPoint), key));
            }
            catch (Exception e) { Debug.LogWarning("[QoL] sort-tweaks StyleServer postfix failed: " + e.Message); }
        }
    }

    private static bool HasSavedPasswordFor(string key)
    {
        var store = QoLRunner.Instance?.Config?.savedServerPasswords;
        return store != null && !string.IsNullOrEmpty(key) && store.ContainsKey(key);
    }

    // ─────────────────────────── hover tooltip ────────────────────────────

    // Singleton floating Label attached to the panel root, reused across
    // rows. We toggle text + position on MouseEnter and hide on
    // MouseLeave. `pickingMode = Ignore` so the tooltip itself never
    // catches the mouse — otherwise hovering over the tooltip would
    // re-trigger leave events on the row and flicker.
    private static Label _hoverTooltip;

    private static void AttachHoverTooltip(VisualElement row, Func<string> textFn)
    {
        if (row == null || row.ClassListContains(TooltipMarkerCls)) return;
        row.AddToClassList(TooltipMarkerCls);

        // Tooltip should only appear when the cursor is over the wrench
        // / lock icon strip — not the whole row. The icons render in
        // vanilla's flex space between NameLabel and PlayersLabel, so
        // we use those two elements' worldBounds as a live hit region.
        // MouseMove drives show/hide so the tooltip appears or vanishes
        // as the cursor crosses the icon band, even within one hover.
        row.RegisterCallback<MouseMoveEvent>(evt =>
        {
            if (!Enabled) { HideHoverTooltip(); return; }
            try
            {
                if (!IsMouseInIconStrip(row, evt.mousePosition))
                {
                    HideHoverTooltip();
                    return;
                }
                string text = textFn?.Invoke();
                if (string.IsNullOrEmpty(text)) { HideHoverTooltip(); return; }
                var tooltip = EnsureHoverTooltip(row);
                if (tooltip == null) return;
                tooltip.text = text;
                tooltip.style.display = DisplayStyle.Flex;
                PositionTooltip(tooltip, evt.mousePosition);
            }
            catch (Exception e) { Debug.LogWarning("[QoL] tooltip move failed: " + e.Message); }
        });

        // MouseLeave is the only reliable way to know the cursor left
        // the row entirely (mouse-move stops firing as soon as the
        // pointer moves out). Without this the tooltip lingers when
        // the user moves off the row through the icon strip side.
        row.RegisterCallback<MouseLeaveEvent>(_ => HideHoverTooltip());
    }

    // Hit region: between NameLabel's right edge and PlayersLabel's
    // left edge, both queried live so any layout change (panel resize,
    // user changes USS, etc.) is honored. A 2px outward fudge on both
    // sides covers anti-alias slop.
    private static bool IsMouseInIconStrip(VisualElement row, Vector2 mousePos)
    {
        try
        {
            var nameLabel    = row.Q<Label>("NameLabel");
            var playersLabel = row.Q<Label>("PlayersLabel");
            if (nameLabel == null || playersLabel == null) return false;
            float left  = nameLabel.worldBound.xMax - 2;
            float right = playersLabel.worldBound.xMin + 2;
            if (right <= left) return false;
            return mousePos.x >= left && mousePos.x <= right
                && mousePos.y >= row.worldBound.yMin
                && mousePos.y <= row.worldBound.yMax;
        }
        catch { return false; }
    }

    private static void HideHoverTooltip()
    {
        try { if (_hoverTooltip != null) _hoverTooltip.style.display = DisplayStyle.None; }
        catch { }
    }

    private static Label EnsureHoverTooltip(VisualElement rowForPanel)
    {
        if (_hoverTooltip != null && _hoverTooltip.parent != null) return _hoverTooltip;

        var panelRoot = rowForPanel?.panel?.visualTree;
        if (panelRoot == null) return null;

        var lbl = new Label { name = HoverTooltipName };
        lbl.pickingMode = PickingMode.Ignore;
        lbl.style.position = Position.Absolute;
        lbl.style.backgroundColor = new Color(0.10f, 0.10f, 0.10f, 0.97f);
        lbl.style.color = Color.white;
        lbl.style.fontSize = 13;
        lbl.style.paddingLeft = 8;
        lbl.style.paddingRight = 8;
        lbl.style.paddingTop = 4;
        lbl.style.paddingBottom = 4;
        lbl.style.borderTopWidth = 1;
        lbl.style.borderBottomWidth = 1;
        lbl.style.borderLeftWidth = 1;
        lbl.style.borderRightWidth = 1;
        lbl.style.borderTopColor = new Color(0.4f, 0.4f, 0.4f);
        lbl.style.borderBottomColor = new Color(0.4f, 0.4f, 0.4f);
        lbl.style.borderLeftColor = new Color(0.4f, 0.4f, 0.4f);
        lbl.style.borderRightColor = new Color(0.4f, 0.4f, 0.4f);
        lbl.style.borderTopLeftRadius = 4;
        lbl.style.borderTopRightRadius = 4;
        lbl.style.borderBottomLeftRadius = 4;
        lbl.style.borderBottomRightRadius = 4;
        lbl.style.whiteSpace = WhiteSpace.Normal;
        lbl.style.maxWidth = 480;
        lbl.style.display = DisplayStyle.None;
        panelRoot.Add(lbl);
        _hoverTooltip = lbl;
        return _hoverTooltip;
    }

    private static void PositionTooltip(VisualElement tooltip, Vector2 mousePos)
    {
        // Offset slightly down-right of the cursor so it doesn't sit
        // under the pointer (and flicker as the cursor moves).
        tooltip.style.left = mousePos.x + 14;
        tooltip.style.top  = mousePos.y + 18;
    }

    // Builds the hover tooltip for a server row from its preview data.
    //   Modded → header + one line per required mod showing status
    //            (✓ enabled / ⚠ not enabled / ✗ missing) and the
    //            workshop title (resolved lazily, see GetModTitle).
    //   Locked → "🔒 Password required" / "🔓 Saved password — auto-fills".
    // Returns null when neither applies (vanilla rows get no tooltip).
    private static string ComputeRowTooltip(ServerPreviewData pd, string ipPort)
    {
        if (pd == null) return null;
        var lines = new List<string>();

        var required = pd.clientRequiredModIds ?? Array.Empty<string>();
        if (required.Length > 0)
            BuildModListLines(required, lines);

        if (pd.isPasswordProtected)
        {
            if (lines.Count > 0) lines.Add(string.Empty); // spacer between sections
            lines.Add(HasSavedPasswordFor(ipPort)
                ? "🔓 Saved password — auto-fills on join"
                : "🔒 Password required to join");
        }

        return lines.Count > 0 ? string.Join("\n", lines) : null;
    }

    private static void BuildModListLines(string[] requiredIds, List<string> outLines)
    {
        HashSet<string> enabled, ready;
        try
        {
            enabled = new HashSet<string>();
            foreach (var m in ModManager.EnabledMods)
                if (m != null && !string.IsNullOrEmpty(m.Id)) enabled.Add(m.Id);
            ready = new HashSet<string>();
            foreach (var m in ModManager.ReadyMods)
                if (m != null && !string.IsNullOrEmpty(m.Id)) ready.Add(m.Id);
        }
        catch
        {
            outLines.Add($"🛠 {requiredIds.Length} required mod{(requiredIds.Length == 1 ? "" : "s")}");
            return;
        }

        int missing = 0;
        foreach (var id in requiredIds)
            if (!enabled.Contains(id)) missing++;

        string plural = requiredIds.Length == 1 ? "" : "s";
        outLines.Add(missing == 0
            ? $"🛠 All {requiredIds.Length} required mod{plural} installed"
            : $"🛠 {missing} of {requiredIds.Length} required mod{plural} not ready");

        // Detail lines: one per mod, with status marker + resolved
        // title (id when title isn't cached yet — the next hover will
        // upgrade once Steam returns details).
        foreach (var id in requiredIds)
        {
            string marker;
            string suffix;
            if (enabled.Contains(id))      { marker = "✓"; suffix = string.Empty; }
            else if (ready.Contains(id))   { marker = "⚠"; suffix = " (not enabled)"; }
            else                            { marker = "✗"; suffix = " (missing)"; }

            string title = GetModTitle(id);
            string display = string.IsNullOrEmpty(title) ? id : title;
            outLines.Add($"  {marker} {display}{suffix}");
        }
    }

    private static void UpdateStarText(Button star, string key)
    {
        if (star == null) return;
        star.text = IsFavorite(key) ? GlyphStarFilled : GlyphStarEmpty;
    }

    // ─────────────────────────── context menu ─────────────────────────────
    //
    // Floating popup attached to the panel root. Vanilla doesn't ship a
    // contextual-menu styling we can latch onto, so we build the menu
    // from scratch with the same dark palette as the QoL menu (matches
    // UITools.StyleConfigButton). Click-outside closes via a one-shot
    // PointerDown handler registered on the panel root.

    // Holds the currently-open menu (if any) and the close-on-outside
    // handler reference so we can deregister cleanly.
    private static VisualElement _openMenu;
    private static VisualElement _openMenuRoot;
    private static EventCallback<PointerDownEvent> _openMenuOutsideHandler;

    private static void CloseContextMenu()
    {
        try
        {
            if (_openMenuRoot != null && _openMenuOutsideHandler != null)
                _openMenuRoot.UnregisterCallback(_openMenuOutsideHandler);
            _openMenu?.RemoveFromHierarchy();
        }
        catch { }
        _openMenu = null;
        _openMenuRoot = null;
        _openMenuOutsideHandler = null;
    }

    private static void ShowContextMenu(UIServerBrowser browser, EndPoint endPoint, VisualElement row, Vector2 pointerPos)
    {
        CloseContextMenu();
        if (row?.panel?.visualTree == null) return;
        string key = MakeKey(endPoint);
        if (string.IsNullOrEmpty(key)) return;

        var pd = GetPreview(browser, endPoint);
        string serverName = !string.IsNullOrEmpty(pd?.name) ? pd.name : key;

        var menu = new VisualElement { name = ContextMenuName };
        menu.style.position = Position.Absolute;
        menu.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.97f);
        menu.style.borderTopWidth = 1;
        menu.style.borderBottomWidth = 1;
        menu.style.borderLeftWidth = 1;
        menu.style.borderRightWidth = 1;
        menu.style.borderTopColor = new Color(0.4f, 0.4f, 0.4f);
        menu.style.borderBottomColor = new Color(0.4f, 0.4f, 0.4f);
        menu.style.borderLeftColor = new Color(0.4f, 0.4f, 0.4f);
        menu.style.borderRightColor = new Color(0.4f, 0.4f, 0.4f);
        menu.style.borderTopLeftRadius = 4;
        menu.style.borderTopRightRadius = 4;
        menu.style.borderBottomLeftRadius = 4;
        menu.style.borderBottomRightRadius = 4;
        menu.style.paddingTop = 4;
        menu.style.paddingBottom = 4;
        menu.style.paddingLeft = 4;
        menu.style.paddingRight = 4;
        menu.style.minWidth = 220;

        // Header — server name in muted italic so it reads as context, not an action.
        var header = new Label(serverName);
        header.style.color = new Color(0.7f, 0.7f, 0.7f);
        header.style.unityFontStyleAndWeight = FontStyle.Italic;
        header.style.fontSize = 12;
        header.style.paddingLeft = 8;
        header.style.paddingTop = 2;
        header.style.paddingBottom = 4;
        header.style.whiteSpace = WhiteSpace.NoWrap;
        header.style.overflow = Overflow.Hidden;
        header.style.textOverflow = TextOverflow.Ellipsis;
        menu.Add(header);

        bool fav = IsFavorite(key);
        AddContextMenuItem(menu, fav ? GlyphStarFilled + " Remove from favorites" : GlyphStarEmpty + " Add to favorites", () =>
        {
            ToggleFavorite(key, pd?.name ?? "");
            UpdateStarText(row.Q<Button>(FavStarName), key);
            AccessTools.Method(typeof(UIServerBrowser), "SortServers")?.Invoke(browser, null);
            CloseContextMenu();
        });

        bool blocked = IsBlocked(key);
        AddContextMenuItem(menu, blocked ? "Unblock server" : "Block server", () =>
        {
            ToggleBlock(key, pd?.name ?? "");
            // Re-filter so the row hides immediately on block / reappears
            // on unblock without needing a manual refresh.
            AccessTools.Method(typeof(UIServerBrowser), "FilterServers")?.Invoke(browser, null);
            UpdateStarText(row.Q<Button>(FavStarName), key);
            CloseContextMenu();
        });

        AddContextMenuItem(menu, "Copy ip:port", () =>
        {
            try { GUIUtility.systemCopyBuffer = key; } catch { }
            CloseContextMenu();
        });

        var rootForMenu = row.panel.visualTree;

        // Position at the pointer. pointerPos is in panel-local coordinates
        // for the root, so it can be used directly as left/top.
        menu.style.left = pointerPos.x;
        menu.style.top = pointerPos.y;

        rootForMenu.Add(menu);

        // Click outside the menu closes it. Use TrickleDown so we receive
        // the event before the row's own RegisterCallback fires (which
        // would otherwise re-open it on a second right-click).
        EventCallback<PointerDownEvent> outsideHandler = null;
        outsideHandler = evt =>
        {
            if (_openMenu == null) return;
            if (_openMenu.worldBound.Contains(evt.position)) return;
            CloseContextMenu();
        };
        rootForMenu.RegisterCallback(outsideHandler, TrickleDown.TrickleDown);

        _openMenu = menu;
        _openMenuRoot = rootForMenu;
        _openMenuOutsideHandler = outsideHandler;
    }

    private static void AddContextMenuItem(VisualElement menu, string text, Action onClick)
    {
        var item = new Button(() => onClick?.Invoke()) { text = text };
        item.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f);
        item.style.color = Color.white;
        item.style.unityTextAlign = TextAnchor.MiddleLeft;
        item.style.paddingLeft = 12;
        item.style.paddingRight = 12;
        item.style.paddingTop = 6;
        item.style.paddingBottom = 6;
        item.style.marginTop = 2;
        item.style.borderTopWidth = 0;
        item.style.borderBottomWidth = 0;
        item.style.borderLeftWidth = 0;
        item.style.borderRightWidth = 0;
        item.style.borderTopLeftRadius = 2;
        item.style.borderTopRightRadius = 2;
        item.style.borderBottomLeftRadius = 2;
        item.style.borderBottomRightRadius = 2;
        item.RegisterCallback<MouseEnterEvent>(_ =>
        {
            item.style.backgroundColor = Color.white;
            item.style.color = Color.black;
        });
        item.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            item.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f);
            item.style.color = Color.white;
        });
        menu.Add(item);
    }
}
