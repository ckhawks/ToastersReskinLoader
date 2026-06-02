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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using ToasterReskinLoader.qol.beacon;
using UnityEngine.UIElements;

using ToasterReskinLoader.core;

namespace ToasterReskinLoader.qol.serverbrowser;

internal static class ServerPreviewCachePatches
{
    private static bool Enabled =>
        QoLRunner.Instance?.Config?.enableServerPreviewCache ?? false;

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
    private static readonly MethodInfo FilterServerMethod =
        AccessTools.Method(typeof(UIServerBrowser), "FilterServer");
    private static readonly MethodInfo SortServersMethod =
        AccessTools.Method(typeof(UIServerBrowser), "SortServers");
    private static readonly MethodInfo RemoveAllServersMethod =
        AccessTools.Method(typeof(UIServerBrowser), "RemoveAllServers");
    private static readonly MethodInfo AddServerMethod =
        AccessTools.Method(typeof(UIServerBrowser), "AddServer");
    private static readonly MethodInfo PingServerMethod =
        AccessTools.Method(typeof(UIServerBrowser), "PingServer");

    // Open-instance delegates so the per-row calls below avoid per-call
    // object[] boxing/allocation from MethodInfo.Invoke. A refresh wave hits
    // these hundreds of times — at that volume the allocations show up.
    private delegate void SetPreviewDelegate(UIServerBrowser self, EndPoint endPoint, ServerPreviewData data);
    private delegate void StyleServerDelegate(UIServerBrowser self, EndPoint endPoint);
    private delegate void FilterServersDelegate(UIServerBrowser self);
    private delegate void FilterServerDelegate(UIServerBrowser self, EndPoint endPoint);
    private delegate void SortServersDelegate(UIServerBrowser self);
    private delegate void RemoveAllServersDelegate(UIServerBrowser self);
    private delegate void AddServerDelegate(UIServerBrowser self, EndPoint endPoint);
    private delegate ServerPreviewData PingServerDelegate(UIServerBrowser self, EndPoint endPoint, int connectTimeout, int responseTimeout);

    // CreateDelegate can throw if AccessTools.Method resolves a base-class
    // declaration whose signature/declaring-type doesn't line up with our
    // delegate type. Wrap so a bad bind doesn't kill the whole patch class
    // at static-init; callers below null-check anyway, so a missing delegate
    // just skips the optimized path.
    private static T TryCreateDelegate<T>(MethodInfo m, string label) where T : Delegate
    {
        if (m == null) return null;
        try { return (T)Delegate.CreateDelegate(typeof(T), m); }
        catch (Exception e)
        {
            Plugin.LogError($"ServerPreviewCache: failed to bind {label} delegate: {e.Message}");
            return null;
        }
    }

    private static readonly SetPreviewDelegate _setPreview =
        TryCreateDelegate<SetPreviewDelegate>(SetPreviewMethod, nameof(SetPreviewMethod));
    private static readonly StyleServerDelegate _styleServer =
        TryCreateDelegate<StyleServerDelegate>(StyleServerMethod, nameof(StyleServerMethod));
    private static readonly FilterServersDelegate _filterServers =
        TryCreateDelegate<FilterServersDelegate>(FilterServersMethod, nameof(FilterServersMethod));
    private static readonly FilterServerDelegate _filterServer =
        TryCreateDelegate<FilterServerDelegate>(FilterServerMethod, nameof(FilterServerMethod));
    private static readonly SortServersDelegate _sortServers =
        TryCreateDelegate<SortServersDelegate>(SortServersMethod, nameof(SortServersMethod));
    private static readonly RemoveAllServersDelegate _removeAllServers =
        TryCreateDelegate<RemoveAllServersDelegate>(RemoveAllServersMethod, nameof(RemoveAllServersMethod));
    private static readonly AddServerDelegate _addServer =
        TryCreateDelegate<AddServerDelegate>(AddServerMethod, nameof(AddServerMethod));
    private static readonly PingServerDelegate _pingServer =
        TryCreateDelegate<PingServerDelegate>(PingServerMethod, nameof(PingServerMethod));
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
        // Full replacement for vanilla UpdateEndPoints. Vanilla does its row
        // setup synchronously then kicks off a single Task.Run that pings
        // every endpoint sequentially — a 50-server refresh stalls for
        // ~50s if any tail is unreachable. We replicate the row setup,
        // seed from cache, then fan the ping wave out across N workers
        // using a semaphore so refresh finishes in roughly (N/concurrency)
        // × timeout.
        //
        // Returns false to skip vanilla (which would otherwise run its
        // sequential loop in parallel with ours and double-write rows).
        // If any delegate bind failed at static init, fall back to vanilla
        // so the browser still works.
        [HarmonyPrefix]
        static bool Prefix(UIServerBrowser __instance, EndPoint[] endPoints)
        {
            if (!Enabled || endPoints == null) return true;
            // Fast scan off → let vanilla do its sequential ping wave; the
            // postfix below still seeds from cache so cached rows appear
            // immediately.
            if (!(QoLRunner.Instance?.Config?.enableFastServerBrowserScanning ?? false)) return true;
            if (_removeAllServers == null || _addServer == null || _pingServer == null
                || _filterServers == null || _sortServers == null || _setPreview == null
                || _styleServer == null || _filterServer == null)
            {
                Plugin.LogError("ServerPreviewCache: missing delegate binding, falling back to vanilla UpdateEndPoints");
                return true;
            }

            try
            {
                // --- Vanilla row setup (main thread) -------------------------
                _removeAllServers(__instance);
                try { if (RefreshButtonField?.GetValue(__instance) is Button btn) btn.SetEnabled(false); }
                catch (Exception e) { Plugin.LogError($"ServerPreviewCache: disable refresh button failed: {e.Message}"); }

                foreach (var ep in endPoints)
                {
                    if (ep == null) continue;
                    _addServer(__instance, ep);
                }
                _filterServers(__instance);
                _sortServers(__instance);

                SeedFromCache(__instance, endPoints);

                // --- Bounded-parallel ping wave ----------------------------
                var cfg = QoLRunner.Instance?.Config;
                int concurrency = Math.Max(1, cfg?.serverBrowserPingConcurrency ?? 16);
                int connectTimeout = Math.Max(50, cfg?.serverBrowserPingConnectTimeoutMs ?? 1000);
                int responseTimeout = Math.Max(50, cfg?.serverBrowserPingResponseTimeoutMs ?? 1000);

                StartParallelPingWave(__instance, endPoints, concurrency, connectTimeout, responseTimeout);
            }
            catch (Exception e)
            {
                Plugin.LogError($"ServerPreviewCache UpdateEndPoints prefix failed: {e}");
                // Don't fall through to vanilla here — we've already done row
                // setup, running vanilla would duplicate rows. Just leave the
                // user with seeded-from-cache state and no live refresh wave.
            }
            return false;
        }

        // Runs only when the prefix returned true (fast scan disabled or
        // delegate bind failed) — vanilla has now done its own row setup, so
        // we just seed from cache on top of it.
        [HarmonyPostfix]
        static void Postfix(UIServerBrowser __instance, EndPoint[] endPoints)
        {
            if (!Enabled || endPoints == null) return;
            if (QoLRunner.Instance?.Config?.enableFastServerBrowserScanning ?? false)
            {
                // Prefix handled it (or chose to fall back, in which case
                // delegate bindings are missing and SeedFromCache can't run
                // either).
                return;
            }
            if (_setPreview == null || _styleServer == null
                || _filterServers == null || _sortServers == null) return;
            try { SeedFromCache(__instance, endPoints); }
            catch (Exception e) { Plugin.LogError($"ServerPreviewCache UpdateEndPoints postfix failed: {e}"); }
        }
    }

    // Cache bookkeeping + seed shared between the fast-scan prefix and the
    // vanilla-fallback postfix.
    private static void SeedFromCache(UIServerBrowser instance, EndPoint[] endPoints)
    {
        lock (_staleLock) _staleEndpoints.Clear();

        CaptureRefreshButton(instance);
        Interlocked.Exchange(ref _refreshTotal, endPoints.Length);
        Interlocked.Exchange(ref _refreshDone, 0);
        UpdateRefreshButton(0, endPoints.Length);
        EnsureCacheCountLabel(instance);

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
            _setPreview(instance, ep, synth);
            (seeded ??= new List<EndPoint>()).Add(ep);
        }

        int hits = seeded?.Count ?? 0;
        if (hits > 0)
        {
            lock (_staleLock)
            {
                foreach (var ep in seeded) _staleEndpoints.Add(ep);
            }
            foreach (var ep in seeded) _styleServer(instance, ep);
            _filterServers(instance);
            _sortServers(instance);
        }

        Plugin.LogDebug($"ServerPreviewCache: seeded {hits}/{endPoints.Length} rows from cache");
    }

    private static void StartParallelPingWave(
        UIServerBrowser instance, EndPoint[] endPoints,
        int concurrency, int connectTimeout, int responseTimeout)
    {
        // Snapshot the endpoint list so a follow-up refresh doesn't stomp
        // mid-wave. Capture instance too — patches see the same singleton,
        // but holding the ref keeps it alive across the wave.
        var snapshot = (EndPoint[])endPoints.Clone();

        // Each live connection makes SimpleTcpClient spin up ~3 ThreadPool
        // tasks (DataReceiver + idle/connection monitors), and the response
        // we wait on is only delivered by DataReceiver. Raise the pool floor
        // before the wave so a cold pool (min == ProcessorCount, grows only
        // ~1-2 threads/sec) doesn't make those tasks queue behind our blocked
        // pings — that queueing is what made the first browser open time out
        // ~95% of servers while a second refresh "magically" worked.
        EnsureThreadPoolFloor(concurrency);

        // Drain the endpoints from a shared queue using a fixed set of
        // DEDICATED (LongRunning) threads — not ThreadPool threads. PingServer
        // blocks its caller for up to connect+response timeout; running that
        // on the pool would starve the very pool DataReceiver needs. Dedicated
        // threads sleep on I/O without touching the pool, and we cap them at
        // `concurrency` so a 200-server list spins up ~16 threads, not 200.
        var queue = new ConcurrentQueue<EndPoint>();
        foreach (var ep in snapshot)
        {
            if (ep != null) queue.Enqueue(ep);
        }

        int workerCount = Math.Max(1, Math.Min(concurrency, queue.Count));
        if (workerCount == 0)
        {
            FinishWave(instance);
            return;
        }

        var workers = new Task[workerCount];
        for (int i = 0; i < workerCount; i++)
        {
            workers[i] = Task.Factory.StartNew(
                () =>
                {
                    while (queue.TryDequeue(out var ep))
                    {
                        PingOne(instance, ep, connectTimeout, responseTimeout);
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        // Watch the dedicated workers from a single pool continuation (cheap —
        // it just awaits) so we can do the settle-sort once the wave drains.
        Task.Run(async () =>
        {
            try { await Task.WhenAll(workers).ConfigureAwait(false); }
            catch (Exception e) { Plugin.LogError($"ServerPreviewCache: parallel ping wave failed: {e}"); }
            finally { FinishWave(instance); }
        });
    }

    // One server's blocking ping + UI apply. Runs on a dedicated worker
    // thread; only the UIElements touches are marshalled to the main thread.
    private static void PingOne(
        UIServerBrowser instance, EndPoint ep, int connectTimeout, int responseTimeout)
    {
        ServerPreviewData data = null;
        try
        {
            data = _pingServer(instance, ep, connectTimeout, responseTimeout);
        }
        catch (Exception e)
        {
            Plugin.LogError($"ServerPreviewCache: PingServer({ep}) threw: {e.Message}");
        }
        finally
        {
            var dataCapture = data;
            // UIElements is main-thread-only. Vanilla ignores this and gets
            // away with it; we marshal so we don't corrupt the panel pick
            // cache the way the cache-count label bug did earlier.
            BeaconMainThread.Run(() =>
            {
                try
                {
                    _setPreview(instance, ep, dataCapture);
                    _styleServer(instance, ep);
                    _filterServer(instance, ep);
                }
                catch (Exception e)
                {
                    Plugin.LogError($"ServerPreviewCache: ping-complete UI apply failed: {e.Message}");
                }
            });
        }
    }

    private static void FinishWave(UIServerBrowser instance)
    {
        // Final sort once the wave settles so ping-based ordering reflects the
        // live values, not the cache seeds.
        BeaconMainThread.Run(() =>
        {
            try { _sortServers?.Invoke(instance); }
            catch (Exception e) { Plugin.LogError($"ServerPreviewCache: final sort failed: {e.Message}"); }
        });
    }

    // Raise the ThreadPool's minimum thread count (never lower it) to a floor
    // sized off concurrency, not ProcessorCount: the burst we need to absorb
    // is SimpleTcpClient's ~3 tasks per live connection, which tracks the
    // number of simultaneous pings, not the core count. Idempotent — each wave
    // takes the max with whatever is already set.
    private static void EnsureThreadPoolFloor(int concurrency)
    {
        try
        {
            int needed = Math.Max(16, concurrency * 4);
            ThreadPool.GetMinThreads(out int curWorker, out int curIo);
            int newWorker = Math.Max(curWorker, needed);
            int newIo = Math.Max(curIo, needed);
            if (newWorker != curWorker || newIo != curIo)
            {
                if (!ThreadPool.SetMinThreads(newWorker, newIo))
                    Plugin.LogError($"ServerPreviewCache: SetMinThreads({newWorker},{newIo}) rejected");
            }
        }
        catch (Exception e)
        {
            Plugin.LogError($"ServerPreviewCache: EnsureThreadPoolFloor failed: {e.Message}");
        }
    }

    [HarmonyPatch(typeof(UIServerBrowser), "SetServerPreviewData")]
    private static class Patch_SetServerPreviewData
    {
        // May be invoked from vanilla's async ping wave on a background
        // thread — keep cache mutation (lock-protected) inline but marshal
        // the cache-count label refresh to the main thread.
        [HarmonyPostfix]
        static void Postfix(EndPoint endPoint, ServerPreviewData previewData)
        {
            if (!Enabled || endPoint == null) return;
            try
            {
                bool wasStale;
                lock (_staleLock) wasStale = _staleEndpoints.Remove(endPoint);

                bool cacheChanged = false;
                if (previewData != null)
                {
                    ServerPreviewCache.Upsert(endPoint, previewData);
                    MaybeFlush();
                    cacheChanged = true;
                }
                else if (wasStale)
                {
                    // Live ping confirmed unreachable — drop the cache so we
                    // don't keep showing it on subsequent opens.
                    ServerPreviewCache.Evict(endPoint);
                    MaybeFlush();
                    cacheChanged = true;
                }

                if (cacheChanged) BeaconMainThread.Run(UpdateCacheCountLabel);
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

    // Side-table keyed by row VisualElement so we don't stomp the row's
    // userData (vanilla or another mod may want it). ConditionalWeakTable
    // entries drop automatically when the row is GC'd.
    private static readonly ConditionalWeakTable<VisualElement, RowLabels> _rowLabels = new();

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

                if (!_rowLabels.TryGetValue(rowRoot, out var labels))
                {
                    var row = rowRoot.Q<VisualElement>("Server");
                    if (row == null) return;
                    labels = new RowLabels
                    {
                        playersLabel = row.Q<Label>("PlayersLabel"),
                        pingLabel = row.Q<Label>("PingLabel"),
                    };
                    _rowLabels.Add(rowRoot, labels);
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
        // PingServer runs on a ThreadPool worker (vanilla wraps it in Task.Run
        // inside UpdateEndPoints), so this postfix executes on a background
        // thread. UIElements is not thread-safe — touching button/label state
        // from here is what corrupts the panel's pick cache and kills mouse
        // input across the whole game until restart. Counter increments and
        // cache mutation stay on the worker (thread-safe under their own
        // locks); every UIElements touch is marshalled to the main thread.
        [HarmonyPostfix]
        static void Postfix(UIServerBrowser __instance, EndPoint endPoint, ServerPreviewData __result)
        {
            if (!Enabled) return;
            try
            {
                int total = Volatile.Read(ref _refreshTotal);
                int doneForUi = 0;
                bool publishProgress = false;
                if (total > 0)
                {
                    int done = Interlocked.Increment(ref _refreshDone);
                    if (done > total) done = total;
                    doneForUi = done;
                    publishProgress = true;
                }

                bool cacheChanged = false;
                if (__result == null && endPoint != null)
                {
                    bool wasStale;
                    lock (_staleLock) wasStale = _staleEndpoints.Remove(endPoint);
                    if (wasStale)
                    {
                        ServerPreviewCache.Evict(endPoint);
                        MaybeFlush();
                        cacheChanged = true;
                    }
                }

                if (publishProgress || cacheChanged)
                {
                    int doneCapture = doneForUi;
                    int totalCapture = total;
                    bool progressCapture = publishProgress;
                    bool cacheCapture = cacheChanged;
                    BeaconMainThread.Run(() =>
                    {
                        if (progressCapture) UpdateRefreshButton(doneCapture, totalCapture);
                        if (cacheCapture) UpdateCacheCountLabel();
                    });
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
