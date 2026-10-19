// Official community server badges.
//
// puckstats.io publishes which servers belong to which community
// (GET /api/server-list/official — contract in puckstats'
// docs/official-servers.md). Rows whose ip:port is in that map get the
// community's logo in front of the server name, and the row's hover
// tooltip names the community. The point is impersonation: anyone can
// name a server "Toaster's Rink #3", but only the community (or a
// puckstats admin) can put an ip:port on this list.
//
// Freshness: communities that scale on demand register a server within
// seconds of provisioning it. The feed is fetched once at startup and
// again when the browser opens or REFRESH is clicked — before the
// endpoint list comes back — so it is usually in hand by the time the
// rows exist. That is the same moment a new server first appears in the
// list at all, so no polling is needed. Throttled to one fetch per
// MinFetchInterval to match the server's own 10s memo.
//
// Every logo in the feed is downloaded as soon as the feed arrives, not
// when a row first needs it, so rows created afterwards get their logo
// on creation. Logos are kept for the session: paths are immutable on
// the puckstats side (a new logo gets a new key), so a path never goes
// stale. Failures are NOT cached permanently — a transient error retries
// after LogoRetryAfter.

using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

using ToasterReskinLoader.api;
using ToasterReskinLoader.core;

namespace ToasterReskinLoader.serverbrowser;

internal static class OfficialServerBadges
{
    private const string FeedPath = "/api/server-list/official";
    internal const string BadgeName = "ToasterCommunityBadge";
    private const float MinFetchInterval = 10f;
    private const float LogoRetryAfter = 60f;
    private const int BadgeSize = 32;
    private const int BadgeGap = 4;
    private const string MeasureHookClass = "toaster-badge-measure-hooked";

    private sealed class Community
    {
        public string name;
        public string logoPath;
        public string logoSmallPath;
    }

    // ip:port → community. Replaced wholesale on each successful fetch;
    // a failed fetch keeps the last good map.
    private static Dictionary<string, Community> _byServer = new();
    private static string _lastFeedText;
    private static float _lastFetchAt = -999f;
    private static bool _fetching;

    // Keyed by the primary (small) logo path.
    private static readonly Dictionary<string, Texture2D> _logos = new();
    private static readonly Dictionary<string, float> _logoFailedAt = new();
    private static readonly Dictionary<string, List<Action<Texture2D>>> _logoPending = new();

    internal static bool Enabled =>
        Settings.Current?.enableOfficialServerBadges ?? true;

    /// <summary>Community name for a row's ip:port, or null.</summary>
    internal static string CommunityNameFor(string key)
    {
        if (!Enabled || string.IsNullOrEmpty(key)) return null;
        return _byServer.TryGetValue(key, out var c) ? c.name : null;
    }

    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        try
        {
            // Both fire before the endpoint list is requested, so the feed
            // races the master-server round trip instead of following it.
            EventManager.AddEventListener("Event_OnServerBrowserShow", _ => RequestFeed());
            EventManager.AddEventListener("Event_OnServerBrowserClickRefresh", _ => RequestFeed());
        }
        catch (Exception e) { Plugin.LogWarning("[OfficialBadges] event hook failed: " + e.Message); }
        RequestFeed();
    }

    // Called by the QoL toggle so the badges appear / vanish on an open
    // browser without waiting for the next refresh.
    internal static void RefreshForCurrentBrowser()
    {
        try
        {
            _lastFetchAt = -999f;
            ApplyToAllRows();
            RequestFeed();
        }
        catch (Exception e) { Plugin.LogWarning("[OfficialBadges] refresh failed: " + e.Message); }
    }

    private static UIServerBrowser CurrentBrowser()
        => MonoBehaviourSingleton<UIManager>.Instance?.ServerBrowser;

    // ─────────────────────────── patches ──────────────────────────────────

    [HarmonyPatch(typeof(UIServerBrowser), "UpdateEndPoints")]
    private static class Patch_UpdateEndPoints
    {
        // Fallback for refreshes that don't go through the two browser
        // events (e.g. TRL's quick-join). Runs even when the preview-cache
        // prefix skipped vanilla; throttled, so usually a no-op.
        [HarmonyPostfix]
        static void Postfix() => RequestFeed();
    }

    [HarmonyPatch(typeof(UIServerBrowser), "StyleServer")]
    private static class Patch_StyleServer
    {
        // StyleServer re-runs whenever a row is created or re-styled (ping
        // result, filter change), so re-applying here keeps the badge on
        // rows that vanilla rebuilt from the template.
        [HarmonyPostfix]
        static void Postfix(UIServerBrowser __instance, EndPoint endPoint)
        {
            if (__instance == null || endPoint == null) return;
            try
            {
                var map = ServerBrowserSort.GetMap(__instance);
                if (map == null || !map.TryGetValue(endPoint, out var rowElem)) return;
                ApplyToRow(endPoint, rowElem);
            }
            catch (Exception e) { Plugin.LogWarning("[OfficialBadges] StyleServer postfix failed: " + e.Message); }
        }
    }

    // ─────────────────────────── feed ─────────────────────────────────────

    private static void RequestFeed()
    {
        if (!Enabled || _fetching) return;
        if (Time.realtimeSinceStartup - _lastFetchAt < MinFetchInterval) return;
        var runner = AppearanceAPI.Runner;
        if (runner == null) return;

        _fetching = true;
        _lastFetchAt = Time.realtimeSinceStartup;
        runner.StartCoroutine(FetchFeed());
    }

    private static IEnumerator FetchFeed()
    {
        using var request = UnityWebRequest.Get(AppearanceAPI.BaseUrl + FeedPath);
        request.timeout = 10;
        yield return request.SendWebRequest();
        _fetching = false;

        if (request.result != UnityWebRequest.Result.Success)
        {
            Plugin.LogWarning($"[OfficialBadges] feed fetch failed: {request.error}");
            yield break;
        }

        string text = request.downloadHandler.text;
        if (text == _lastFeedText) yield break;

        try
        {
            _byServer = ParseFeed(text);
            _lastFeedText = text;
        }
        catch (Exception e)
        {
            Plugin.LogWarning("[OfficialBadges] feed parse failed: " + e.Message);
            yield break;
        }

        PrefetchLogos();
        ApplyToAllRows();
    }

    // Start every logo download now. Rows that need one before it lands
    // register their own callback in GetLogo and fill in when it does.
    private static void PrefetchLogos()
    {
        var seen = new HashSet<string>();
        foreach (var c in _byServer.Values)
        {
            string path = c.logoSmallPath ?? c.logoPath;
            if (path != null && seen.Add(path))
                GetLogo(path, c.logoPath, _ => { });
        }
    }

    private static Dictionary<string, Community> ParseFeed(string text)
    {
        var root = JObject.Parse(text);
        var communities = new Dictionary<string, Community>();
        if (root["communities"] is JObject cs)
        {
            foreach (var kv in cs)
            {
                if (kv.Value is not JObject o) continue;
                communities[kv.Key] = new Community
                {
                    name          = (string)o["name"],
                    logoPath      = (string)o["logoPath"],
                    logoSmallPath = (string)o["logoSmallPath"],
                };
            }
        }

        var byServer = new Dictionary<string, Community>();
        if (root["servers"] is JObject ss)
        {
            foreach (var kv in ss)
            {
                string id = kv.Value?.ToString();
                if (id != null && communities.TryGetValue(id, out var c))
                    byServer[kv.Key] = c;
            }
        }
        return byServer;
    }

    // ─────────────────────────── rows ─────────────────────────────────────

    private static void ApplyToAllRows()
    {
        var browser = CurrentBrowser();
        if (browser == null) return;
        var map = ServerBrowserSort.GetMap(browser);
        if (map == null) return;
        foreach (var kv in map)
        {
            try { ApplyToRow(kv.Key, kv.Value); }
            catch (Exception e) { Plugin.LogWarning("[OfficialBadges] row apply failed: " + e.Message); }
        }
    }

    private static void ApplyToRow(EndPoint endPoint, VisualElement rowElem)
    {
        var serverRow = rowElem?.Q<VisualElement>("Server");
        if (serverRow == null) return;
        HookInsetMeasure(serverRow, serverRow.Q<Label>("NameLabel"));
        var badge = serverRow.Q<Image>(BadgeName);

        Community community = null;
        if (Enabled) _byServer.TryGetValue(ServerBrowserSort.MakeKey(endPoint), out community);
        string path = community?.logoSmallPath ?? community?.logoPath;
        var nameLabel = serverRow.Q<Label>("NameLabel");
        if (path == null)
        {
            if (badge != null)
            {
                badge.RemoveFromHierarchy();
                ServerBrowserSort.ApplyNameIndent(serverRow, nameLabel);
            }
            return;
        }

        if (nameLabel?.parent == null) return;

        if (badge == null)
        {
            badge = new Image
            {
                name = BadgeName,
                scaleMode = ScaleMode.ScaleToFit,
                // Not a Button and not pickable: the game's '.server Button'
                // USS makes any Button in a row cover the whole row, and a
                // pickable element would eat clicks meant for the
                // full-row connect button underneath.
                pickingMode = PickingMode.Ignore,
            };
            badge.style.width = BadgeSize;
            badge.style.flexShrink = 0;
            // Absolute over the row's left edge, spanning its height;
            // ScaleToFit keeps the logo square and vertically centred, and
            // shrinks it on a row shorter than BadgeSize. NameLabel ignores
            // flex siblings inserted before it, hence absolute + a matching
            // NameLabel margin (ServerBrowserSort.ApplyNameIndent) — the
            // same approach as the favorite star.
            badge.style.position = Position.Absolute;
            badge.style.top = 0;
            badge.style.bottom = 0;
        }

        if (badge.parent != serverRow) serverRow.Add(badge);

        // userData pins which logo this element is waiting for, so a late
        // download for a row that has since changed community is dropped.
        badge.userData = path;
        if (_logos.TryGetValue(path, out var cached) && cached != null)
        {
            badge.image = cached;
            badge.style.display = DisplayStyle.Flex;
            ServerBrowserSort.ApplyNameIndent(serverRow, nameLabel);
            return;
        }

        // Hidden until the texture arrives, so a slow or failed download
        // leaves no empty gap beside the name.
        badge.style.display = DisplayStyle.None;
        ServerBrowserSort.ApplyNameIndent(serverRow, nameLabel);
        var badgeRef = badge;
        GetLogo(path, community.logoPath, tex =>
        {
            if (tex == null || badgeRef.userData as string != path) return;
            badgeRef.image = tex;
            badgeRef.style.display = DisplayStyle.Flex;
            ServerBrowserSort.ApplyNameIndent(serverRow, nameLabel);
        });
    }

    // Width the badge takes in front of the server name, after placing it
    // where the name text would start (past the star, if any). 0 when the
    // row has no badge or its logo is still loading. Called by
    // ServerBrowserSort.ApplyNameIndent, which owns NameLabel's margin
    // because the favorite star shares it.
    internal static float LeftIndentFor(VisualElement serverRow, Label nameLabel, float starWidth)
    {
        var badge = serverRow?.Q<Image>(BadgeName);
        if (badge == null || badge.style.display == DisplayStyle.None) return 0;

        // A row shorter than BadgeSize draws the logo at the row's height
        // (ScaleToFit), so size the element to match or the gap to the
        // name would grow by the difference.
        float size = BadgeSize;
        float rowHeight = serverRow.resolvedStyle.height;
        if (!float.IsNaN(rowHeight) && rowHeight > 0f) size = Mathf.Min(size, rowHeight);

        badge.style.width = size;
        badge.style.left = TextInset() + starWidth;
        return size + BadgeGap;
    }

    // How far into the row the name text starts in the unmodified layout:
    // NameLabel's left edge plus its own border and padding. The badge
    // starts there so it lines up with the text of rows that have no
    // badge. Every row comes from one template, so it is measured once.
    //
    // Only measured on a row nothing has indented yet (no star, no badge:
    // NameLabel has no inline margin). Measuring an indented row means
    // subtracting our own margin, and resolvedStyle can report the new
    // margin before the layout pass that applies it, which gave a
    // negative inset and pinned the badge to the row's edge.
    private static float _textInset = -1f;
    private const float ProvisionalInset = 4f;

    private static float TextInset() => _textInset >= 0f ? _textInset : ProvisionalInset;

    // An inline style that was never set (or was cleared with
    // StyleKeyword.Null) reads back as Null. One holding a value reads
    // back as Undefined, so Undefined means "indented", not "unset".
    private static bool IsUnindented(Label nameLabel)
        => nameLabel.style.marginLeft.keyword == StyleKeyword.Null;

    private static void HookInsetMeasure(VisualElement serverRow, Label nameLabel)
    {
        if (_textInset >= 0f || nameLabel == null) return;
        if (nameLabel.ClassListContains(MeasureHookClass)) return;
        nameLabel.AddToClassList(MeasureHookClass);
        nameLabel.RegisterCallback<GeometryChangedEvent>(_ =>
        {
            if (_textInset >= 0f || !IsUnindented(nameLabel)) return;
            var row = serverRow.worldBound;
            var label = nameLabel.worldBound;
            if (float.IsNaN(row.x) || float.IsNaN(label.x) || label.width <= 0f) return;

            var ls = nameLabel.resolvedStyle;
            _textInset = Mathf.Max(0f, label.xMin - row.xMin
                                       - serverRow.resolvedStyle.borderLeftWidth
                                       + ls.borderLeftWidth
                                       + ls.paddingLeft);
            Plugin.Log($"[OfficialBadges] name text inset measured at {_textInset:0.#}px");
            // Rows badged before this used the provisional inset.
            ApplyToAllRows();
        });
    }

    /// <summary>The badge element on a row, if any, for tooltip hit-testing.</summary>
    internal static VisualElement BadgeOn(VisualElement serverRow)
        => serverRow?.Q<Image>(BadgeName);

    // ─────────────────────────── logos ────────────────────────────────────

    private static void GetLogo(string path, string fallbackPath, Action<Texture2D> onDone)
    {
        if (_logos.TryGetValue(path, out var tex)) { onDone(tex); return; }
        if (_logoFailedAt.TryGetValue(path, out var failedAt)
            && Time.realtimeSinceStartup - failedAt < LogoRetryAfter)
        {
            onDone(null);
            return;
        }
        if (_logoPending.TryGetValue(path, out var waiting)) { waiting.Add(onDone); return; }

        var runner = AppearanceAPI.Runner;
        if (runner == null) { onDone(null); return; }
        _logoPending[path] = new List<Action<Texture2D>> { onDone };
        runner.StartCoroutine(DownloadLogo(path, fallbackPath));
    }

    private static IEnumerator DownloadLogo(string path, string fallbackPath)
    {
        byte[] bytes = null;
        long status = 0;
        using (var request = UnityWebRequest.Get(AppearanceAPI.BaseUrl + path))
        {
            request.timeout = 15;
            yield return request.SendWebRequest();
            status = request.responseCode;
            if (request.result == UnityWebRequest.Result.Success)
                bytes = request.downloadHandler.data;
        }

        // A logo uploaded before puckstats started writing 64px copies may
        // have no small file. Use the full-size one instead.
        if (bytes == null && status == 404 && fallbackPath != null && fallbackPath != path)
        {
            using var fallback = UnityWebRequest.Get(AppearanceAPI.BaseUrl + fallbackPath);
            fallback.timeout = 15;
            yield return fallback.SendWebRequest();
            if (fallback.result == UnityWebRequest.Result.Success)
                bytes = fallback.downloadHandler.data;
        }

        Texture2D tex = null;
        if (bytes != null)
        {
            try
            {
                // Mip chain on: the 64px source is drawn at 32px, and
                // without mips the downscale aliases badly.
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                t.wrapMode = TextureWrapMode.Clamp;
                t.filterMode = FilterMode.Trilinear;
                if (t.LoadImage(bytes, true)) tex = t;
                else UnityEngine.Object.Destroy(t);
            }
            catch (Exception e) { Plugin.LogWarning("[OfficialBadges] logo decode failed: " + e.Message); }
        }

        if (tex != null)
        {
            _logos[path] = tex;
            _logoFailedAt.Remove(path);
        }
        else
        {
            _logoFailedAt[path] = Time.realtimeSinceStartup;
            Plugin.LogWarning($"[OfficialBadges] logo unavailable: {path} (HTTP {status})");
        }

        if (_logoPending.TryGetValue(path, out var callbacks))
        {
            _logoPending.Remove(path);
            foreach (var cb in callbacks)
            {
                try { cb(tex); }
                catch (Exception e) { Plugin.LogWarning("[OfficialBadges] logo callback failed: " + e.Message); }
            }
        }
    }
}
