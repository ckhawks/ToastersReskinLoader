// Server browser preview cache — Harmony hooks.
//
// Flow per refresh:
//   1. Vanilla UpdateEndPoints() rebuilds the row map with previewData=null,
//      so every row renders as the "unreachable" IP:port placeholder, then
//      kicks off the async ping wave.
//   2. Our UpdateEndPoints postfix seeds each endpoint that has a cache hit
//      with a synthetic ServerPreviewData (cached name/maxPlayers/flags +
//      cached ping for sort), then re-runs Filter/Sort/Style so the list
//      shows cached rows in the right order immediately.
//   3. Our StyleServer postfix masks the players count to "?/maxPlayers"
//      and ping to "?" while the row is still "stale" (no live response
//      yet this refresh) — the cached ping is used for internal sort but
//      never displayed.
//   4. As live pings come in, vanilla calls SetServerPreviewData(endpoint,
//      data). Our SetServerPreviewData postfix removes the endpoint from
//      the stale set (so future StyleServer calls render the live values)
//      and upserts the cache on success / evicts on null. Cache flushes
//      to disk on a 2s throttle so a refresh wave produces a handful of
//      writes, not hundreds.
//
// Eviction model: dead servers fall out of the master server's endpoint
// list, so cache entries for them simply stop being referenced; ping
// failures evict explicitly so a momentarily-down server's stale data
// stops appearing in subsequent sessions.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.qol.serverbrowser;

internal static class ServerPreviewCachePatches
{
    private static bool Enabled =>
        QoLRunner.Instance?.Config?.enableServerPreviewCache ?? true;

    // Endpoints currently showing cached-only data (no live ping response
    // received yet this refresh wave). Populated by the UpdateEndPoints
    // postfix, cleared per-endpoint by the SetServerPreviewData postfix.
    private static readonly HashSet<EndPoint> _staleEndpoints = new();
    private static readonly object _staleLock = new();

    private static DateTime _lastFlush = DateTime.MinValue;
    private static readonly TimeSpan FlushThrottle = TimeSpan.FromSeconds(2);

    private static readonly MethodInfo SetPreviewMethod =
        AccessTools.Method(typeof(UIServerBrowser), "SetServerPreviewData");
    private static readonly MethodInfo StyleServerMethod =
        AccessTools.Method(typeof(UIServerBrowser), "StyleServer");
    private static readonly MethodInfo FilterServersMethod =
        AccessTools.Method(typeof(UIServerBrowser), "FilterServers");
    private static readonly MethodInfo SortServersMethod =
        AccessTools.Method(typeof(UIServerBrowser), "SortServers");

    // Open-instance delegates so the per-row calls below avoid per-call
    // object[] boxing/allocation from MethodInfo.Invoke. A refresh wave hits
    // these hundreds of times — at that volume the allocations show up.
    private delegate void SetPreviewDelegate(UIServerBrowser self, EndPoint endPoint, ServerPreviewData data);
    private delegate void StyleServerDelegate(UIServerBrowser self, EndPoint endPoint);
    private delegate void FilterServersDelegate(UIServerBrowser self);
    private delegate void SortServersDelegate(UIServerBrowser self);

    private static readonly SetPreviewDelegate _setPreview =
        SetPreviewMethod != null
            ? (SetPreviewDelegate)Delegate.CreateDelegate(typeof(SetPreviewDelegate), SetPreviewMethod)
            : null;
    private static readonly StyleServerDelegate _styleServer =
        StyleServerMethod != null
            ? (StyleServerDelegate)Delegate.CreateDelegate(typeof(StyleServerDelegate), StyleServerMethod)
            : null;
    private static readonly FilterServersDelegate _filterServers =
        FilterServersMethod != null
            ? (FilterServersDelegate)Delegate.CreateDelegate(typeof(FilterServersDelegate), FilterServersMethod)
            : null;
    private static readonly SortServersDelegate _sortServers =
        SortServersMethod != null
            ? (SortServersDelegate)Delegate.CreateDelegate(typeof(SortServersDelegate), SortServersMethod)
            : null;
    private static readonly FieldInfo EndPointMapField =
        AccessTools.Field(typeof(UIServerBrowser), "endPointVisualElementMap");
    private static readonly FieldInfo RefreshButtonField =
        AccessTools.Field(typeof(UIServerBrowser), "refreshButton");
    private static readonly FieldInfo ServerBrowserField =
        AccessTools.Field(typeof(UIServerBrowser), "serverBrowser");

    // Refresh-progress state: every PingServer call (success OR null) ticks
    // _refreshDone; UpdateEndPoints postfix resets _refreshTotal at the start
    // of a wave. Button text + enabled state mirror that ratio. Tracked
    // statically because there's only ever one UIServerBrowser instance, but
    // we hold a weak ref to its button + remember the original label so we
    // can restore it cleanly even if the browser is destroyed mid-wave.
    private static int _refreshTotal;
    private static int _refreshDone;
    private static Button _refreshButton;
    private static string _refreshButtonOriginalText;
    private static Label _cacheCountLabel;

    [HarmonyPatch(typeof(UIServerBrowser), "UpdateEndPoints")]
    private static class Patch_UpdateEndPoints
    {
        [HarmonyPostfix]
        static void Postfix(UIServerBrowser __instance, EndPoint[] endPoints)
        {
            if (!Enabled || endPoints == null) return;
            try
            {
                lock (_staleLock) _staleEndpoints.Clear();

                CaptureRefreshButton(__instance);
                Interlocked.Exchange(ref _refreshTotal, endPoints.Length);
                Interlocked.Exchange(ref _refreshDone, 0);
                UpdateRefreshButton(0, endPoints.Length);
                EnsureCacheCountLabel(__instance);

                // Garbage-collect cached entries the master server is no
                // longer advertising. Without this the lifetime cache only
                // grows, so "Cached: N" drifts arbitrarily far above the
                // current refresh wave's denominator.
                var keepKeys = new HashSet<string>();
                foreach (var ep in endPoints)
                {
                    if (ep == null) continue;
                    keepKeys.Add(ServerPreviewCache.Key(ep));
                }
                int evicted = ServerPreviewCache.RetainOnly(keepKeys);
                if (evicted > 0)
                {
                    MaybeFlush();
                    Plugin.LogDebug($"ServerPreviewCache: evicted {evicted} stale entries (no longer in master list)");
                }

                UpdateCacheCountLabel();

                // Pass 1: seed previews. Each _setPreview triggers our own
                // SetServerPreviewData postfix which removes that endpoint
                // from the stale set, so we add to stale AFTER all seeds.
                List<EndPoint> seeded = null;
                foreach (var ep in endPoints)
                {
                    if (ep == null) continue;
                    if (!ServerPreviewCache.TryGet(ep, out var cached)) continue;

                    var synth = new ServerPreviewData
                    {
                        name = cached.name,
                        players = 0,
                        maxPlayers = cached.maxPlayers,
                        isPasswordProtected = cached.isPasswordProtected,
                        clientRequiredModIds = cached.clientRequiredModIds ?? Array.Empty<string>(),
                        ping = cached.lastPingMs,
                    };
                    _setPreview?.Invoke(__instance, ep, synth);
                    (seeded ??= new List<EndPoint>()).Add(ep);
                }

                int hits = seeded?.Count ?? 0;
                if (hits > 0)
                {
                    // Single-lock bulk-add to the stale set, then style each
                    // row so StyleServer's postfix sees it as stale and
                    // applies the "?/maxPlayers" + "?" mask.
                    lock (_staleLock)
                    {
                        foreach (var ep in seeded) _staleEndpoints.Add(ep);
                    }
                    foreach (var ep in seeded) _styleServer?.Invoke(__instance, ep);
                }

                if (hits > 0)
                {
                    _filterServers?.Invoke(__instance);
                    _sortServers?.Invoke(__instance);
                }

                Plugin.LogDebug($"ServerPreviewCache: seeded {hits}/{endPoints.Length} rows from cache");
            }
            catch (Exception e)
            {
                Plugin.LogError($"ServerPreviewCache UpdateEndPoints postfix failed: {e}");
            }
        }
    }

    [HarmonyPatch(typeof(UIServerBrowser), "SetServerPreviewData")]
    private static class Patch_SetServerPreviewData
    {
        [HarmonyPostfix]
        static void Postfix(EndPoint endPoint, ServerPreviewData previewData)
        {
            if (!Enabled || endPoint == null) return;
            try
            {
                bool wasStale;
                lock (_staleLock) wasStale = _staleEndpoints.Remove(endPoint);

                if (previewData != null)
                {
                    ServerPreviewCache.Upsert(endPoint, previewData);
                    MaybeFlush();
                    UpdateCacheCountLabel();
                }
                else if (wasStale)
                {
                    // Live ping confirmed unreachable — drop the cache so we
                    // don't keep showing it on subsequent opens.
                    ServerPreviewCache.Evict(endPoint);
                    MaybeFlush();
                    UpdateCacheCountLabel();
                }
            }
            catch (Exception e)
            {
                Plugin.LogError($"ServerPreviewCache SetServerPreviewData postfix failed: {e}");
            }
        }
    }

    // UQuery walks the visual tree on every call, so during a refresh wave
    // this postfix would re-resolve the same three elements per row dozens
    // of times. Stash them on the row's userData on first lookup and reuse.
    private sealed class RowLabels
    {
        public Label playersLabel;
        public Label pingLabel;
    }

    [HarmonyPatch(typeof(UIServerBrowser), "StyleServer")]
    private static class Patch_StyleServer
    {
        [HarmonyPostfix]
        static void Postfix(UIServerBrowser __instance, EndPoint endPoint)
        {
            if (!Enabled || endPoint == null) return;
            bool isStale;
            lock (_staleLock) isStale = _staleEndpoints.Contains(endPoint);
            if (!isStale) return;
            try
            {
                if (EndPointMapField?.GetValue(__instance) is not Dictionary<EndPoint, VisualElement> map) return;
                if (!map.TryGetValue(endPoint, out var rowRoot)) return;

                var labels = rowRoot.userData as RowLabels;
                if (labels == null)
                {
                    var row = rowRoot.Q<VisualElement>("Server");
                    if (row == null) return;
                    labels = new RowLabels
                    {
                        playersLabel = row.Q<Label>("PlayersLabel"),
                        pingLabel = row.Q<Label>("PingLabel"),
                    };
                    rowRoot.userData = labels;
                }

                if (!ServerPreviewCache.TryGet(endPoint, out var cached)) return;

                if (labels.playersLabel != null) labels.playersLabel.text = $"?/{cached.maxPlayers}";
                if (labels.pingLabel != null) labels.pingLabel.text = "?";
            }
            catch (Exception e)
            {
                Plugin.LogError($"ServerPreviewCache StyleServer postfix failed: {e}");
            }
        }
    }

    [HarmonyPatch(typeof(UIServerBrowser), "PingServer")]
    private static class Patch_PingServer
    {
        [HarmonyPostfix]
        static void Postfix(UIServerBrowser __instance, EndPoint endPoint, ServerPreviewData __result)
        {
            if (!Enabled) return;
            try
            {
                int total = Volatile.Read(ref _refreshTotal);
                if (total > 0)
                {
                    int done = Interlocked.Increment(ref _refreshDone);
                    if (done > total) done = total;
                    UpdateRefreshButton(done, total);
                }

                // Failed ping for a row we seeded from cache: clear the
                // synthetic preview ourselves so the row falls back to
                // vanilla's "unreachable IP:port" rendering. If vanilla's
                // async loop *also* calls SetServerPreviewData(null), the
                // second clear is harmless — our SetServerPreviewData
                // postfix's wasStale check will already be false.
                if (__result == null && endPoint != null)
                {
                    bool wasStale;
                    lock (_staleLock) wasStale = _staleEndpoints.Remove(endPoint);
                    if (wasStale)
                    {
                        ServerPreviewCache.Evict(endPoint);
                        MaybeFlush();
                        UpdateCacheCountLabel();
                        _setPreview?.Invoke(__instance, endPoint, null);
                        _styleServer?.Invoke(__instance, endPoint);
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.LogError($"ServerPreviewCache PingServer postfix failed: {e}");
            }
        }
    }

    private static void CaptureRefreshButton(UIServerBrowser instance)
    {
        try
        {
            if (RefreshButtonField?.GetValue(instance) is not Button btn) return;
            if (!ReferenceEquals(_refreshButton, btn))
            {
                _refreshButton = btn;
                _refreshButtonOriginalText = btn.text;
            }
        }
        catch (Exception e)
        {
            Plugin.LogError($"ServerPreviewCache CaptureRefreshButton failed: {e}");
        }
    }

    private static void UpdateRefreshButton(int done, int total)
    {
        var btn = _refreshButton;
        if (btn == null) return;
        try
        {
            if (done >= total)
            {
                btn.text = _refreshButtonOriginalText ?? "REFRESH";
                btn.style.fontSize = StyleKeyword.Null;
                btn.SetEnabled(true);
            }
            else
            {
                btn.text = $"REFRESHING {done}/{total}...";
                btn.style.fontSize = 14;
                btn.SetEnabled(false);
            }
        }
        catch (Exception e)
        {
            Plugin.LogError($"ServerPreviewCache UpdateRefreshButton failed: {e}");
        }
    }

    private static void EnsureCacheCountLabel(UIServerBrowser instance)
    {
        try
        {
            // Parent to the serverBrowser root so the InlineServerBrowserFilters
            // patch — which yanks refreshButton into its own button row — can't
            // squeeze the label between REFRESH and NEW SERVER.
            if (ServerBrowserField?.GetValue(instance) is not VisualElement serverBrowser) return;

            if (_cacheCountLabel != null && _cacheCountLabel.parent == serverBrowser) return;
            _cacheCountLabel?.RemoveFromHierarchy();

            var lbl = new Label("");
            lbl.name = "ToasterServerCacheCountLabel";
            lbl.style.color = new UnityEngine.Color(0.65f, 0.65f, 0.65f);
            lbl.style.fontSize = 13;
            lbl.style.marginTop = 6;
            lbl.style.marginBottom = 2;
            lbl.style.marginRight = 8;
            lbl.style.unityTextAlign = UnityEngine.TextAnchor.MiddleRight;
            lbl.style.alignSelf = Align.FlexEnd;
            lbl.style.flexShrink = 0;

            // Sit immediately above the inline filters strip if present;
            // otherwise just append (vanilla layout — filters live in a popup
            // and the buttons live at the bottom, so end-of-panel is fine).
            var strip = serverBrowser.Q<VisualElement>("PPKB_InlineFilters");
            if (strip != null && strip.parent == serverBrowser)
            {
                int idx = serverBrowser.IndexOf(strip);
                serverBrowser.Insert(idx, lbl);
            }
            else
            {
                serverBrowser.Add(lbl);
            }
            _cacheCountLabel = lbl;
        }
        catch (Exception e)
        {
            Plugin.LogError($"ServerPreviewCache EnsureCacheCountLabel failed: {e}");
        }
    }

    private static void UpdateCacheCountLabel()
    {
        var lbl = _cacheCountLabel;
        if (lbl == null) return;
        try
        {
            int n = ServerPreviewCache.Count;
            lbl.text = $"Cached: {n} server{(n == 1 ? "" : "s")}";
        }
        catch (Exception e)
        {
            Plugin.LogError($"ServerPreviewCache UpdateCacheCountLabel failed: {e}");
        }
    }

    private static void MaybeFlush()
    {
        var now = DateTime.UtcNow;
        if (now - _lastFlush < FlushThrottle) return;
        _lastFlush = now;
        ServerPreviewCache.FlushIfDirty();
    }
}
