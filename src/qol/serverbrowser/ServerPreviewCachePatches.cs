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

                int hits = 0;
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
                    // Order matters: SetServerPreviewData triggers our own
                    // postfix which would *remove* the endpoint from the
                    // stale set (treating the seed as a "live" response).
                    // Add to the stale set AFTER the seed call but BEFORE
                    // StyleServer, so StyleServer's postfix sees it as stale
                    // and applies the "?/maxPlayers" + "?" mask.
                    SetPreviewMethod?.Invoke(__instance, new object[] { ep, synth });
                    lock (_staleLock) _staleEndpoints.Add(ep);
                    StyleServerMethod?.Invoke(__instance, new object[] { ep });
                    hits++;
                }

                if (hits > 0)
                {
                    FilterServersMethod?.Invoke(__instance, null);
                    SortServersMethod?.Invoke(__instance, null);
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

                var row = rowRoot.Query<VisualElement>("Server").First();
                if (row == null) return;

                var playersLabel = row.Query<Label>("PlayersLabel").First();
                var pingLabel = row.Query<Label>("PingLabel").First();

                if (!ServerPreviewCache.TryGet(endPoint, out var cached)) return;

                if (playersLabel != null) playersLabel.text = $"?/{cached.maxPlayers}";
                if (pingLabel != null) pingLabel.text = "?";
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
                        SetPreviewMethod?.Invoke(__instance, new object[] { endPoint, null });
                        StyleServerMethod?.Invoke(__instance, new object[] { endPoint });
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
