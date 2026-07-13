// In-browser "REFRESH VISIBLE" button.
//
// Vanilla's REFRESH re-hits the master server (WebSocket) for the full
// endpoint list, then pings every server. This button skips the master
// roundtrip and re-pings only the rows currently *shown* in the list —
// i.e. the endpoints whose row passes the active filters — to freshen
// their player counts + names cheaply.
//
// It reuses the vanilla PingServer / SetServerPreviewData / StyleServer /
// FilterServer / SortServers plumbing through open-instance delegates
// (same technique as ServerPreviewCachePatches), fanning the pings out
// across a bounded set of dedicated worker threads so a scan of the
// visible set finishes in roughly (N / concurrency) x timeout. Because it
// calls the real (Harmony-patched) PingServer + SetServerPreviewData, the
// preview cache is transparently refreshed by those patches' postfixes.
//
// The button is injected into the Filters footer next to REFRESH. Injection
// is idempotent and driven off UpdateEndPoints (fires on every open/refresh)
// so it survives Initialize having run before the mod loaded and any DOM
// rebuilds.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine.UIElements;

using ToasterReskinLoader.core;

namespace ToasterReskinLoader.serverbrowser;

internal static class RefreshVisibleServers
{
    private static bool Enabled =>
        Settings.Current?.enableRefreshVisibleButton ?? true;

    private const string ButtonName = "ToasterRefreshVisibleButton";
    private const string ButtonText = "REFRESH VISIBLE";

    // --- Reflection handles (bound once at static init) ---------------------
    private static readonly FieldInfo FiltersField =
        AccessTools.Field(typeof(UIServerBrowser), "filters");
    private static readonly FieldInfo RefreshButtonField =
        AccessTools.Field(typeof(UIServerBrowser), "refreshButton");
    private static readonly FieldInfo EndPointMapField =
        AccessTools.Field(typeof(UIServerBrowser), "endPointVisualElementMap");

    private static readonly MethodInfo PingServerMethod =
        AccessTools.Method(typeof(UIServerBrowser), "PingServer");
    private static readonly MethodInfo SetPreviewMethod =
        AccessTools.Method(typeof(UIServerBrowser), "SetServerPreviewData");
    private static readonly MethodInfo StyleServerMethod =
        AccessTools.Method(typeof(UIServerBrowser), "StyleServer");
    private static readonly MethodInfo FilterServerMethod =
        AccessTools.Method(typeof(UIServerBrowser), "FilterServer");
    private static readonly MethodInfo SortServersMethod =
        AccessTools.Method(typeof(UIServerBrowser), "SortServers");

    private delegate ServerPreviewData PingServerDelegate(UIServerBrowser self, EndPoint endPoint, int connectTimeout, int responseTimeout);
    private delegate void SetPreviewDelegate(UIServerBrowser self, EndPoint endPoint, ServerPreviewData data);
    private delegate void StyleServerDelegate(UIServerBrowser self, EndPoint endPoint);
    private delegate void FilterServerDelegate(UIServerBrowser self, EndPoint endPoint);
    private delegate void SortServersDelegate(UIServerBrowser self);

    private static T TryCreateDelegate<T>(MethodInfo m, string label) where T : Delegate
    {
        if (m == null) return null;
        try { return (T)Delegate.CreateDelegate(typeof(T), m); }
        catch (Exception e)
        {
            Plugin.LogError($"RefreshVisibleServers: failed to bind {label} delegate: {e.Message}");
            return null;
        }
    }

    private static readonly PingServerDelegate _pingServer =
        TryCreateDelegate<PingServerDelegate>(PingServerMethod, nameof(PingServerMethod));
    private static readonly SetPreviewDelegate _setPreview =
        TryCreateDelegate<SetPreviewDelegate>(SetPreviewMethod, nameof(SetPreviewMethod));
    private static readonly StyleServerDelegate _styleServer =
        TryCreateDelegate<StyleServerDelegate>(StyleServerMethod, nameof(StyleServerMethod));
    private static readonly FilterServerDelegate _filterServer =
        TryCreateDelegate<FilterServerDelegate>(FilterServerMethod, nameof(FilterServerMethod));
    private static readonly SortServersDelegate _sortServers =
        TryCreateDelegate<SortServersDelegate>(SortServersMethod, nameof(SortServersMethod));

    // Weak-ish handle to the injected button so the settings toggle can
    // show/hide it live, and so a wave-in-progress can restore its label.
    private static Button _button;
    private static UIServerBrowser _browser;

    // Guards against overlapping waves (double-click / re-entry).
    private static int _waveRunning;
    private static int _waveTotal;
    private static int _waveDone;

    // ── Injection ───────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(UIServerBrowser), "Initialize")]
    private static class Patch_Initialize
    {
        [HarmonyPostfix]
        static void Postfix(UIServerBrowser __instance) => EnsureButton(__instance);
    }

    [HarmonyPatch(typeof(UIServerBrowser), "UpdateEndPoints")]
    private static class Patch_UpdateEndPoints
    {
        // Runs on every open/refresh regardless of whether the cache prefix
        // skipped vanilla — keeps the button present across DOM rebuilds.
        [HarmonyPostfix]
        static void Postfix(UIServerBrowser __instance) => EnsureButton(__instance);
    }

    private static void EnsureButton(UIServerBrowser instance)
    {
        if (instance == null) return;
        try
        {
            _browser = instance;

            if (RefreshButtonField?.GetValue(instance) is not Button refreshButton) return;
            var footer = refreshButton.parent;
            if (footer == null) return;

            // Already present under the same footer — just re-sync and bail.
            if (_button != null && _button.parent == footer)
            {
                MatchRefreshHeight(_button, refreshButton);
                ApplyVisibility();
                return;
            }

            _button?.RemoveFromHierarchy();

            var btn = new Button(OnClick) { name = ButtonName, text = ButtonText };
            // Mirror vanilla's REFRESH styling.
            foreach (var cls in refreshButton.GetClasses())
                btn.AddToClassList(cls);
            btn.style.marginRight = 8; // gap between our button and REFRESH

            // Sit immediately to the LEFT of the refresh button.
            int idx = footer.IndexOf(refreshButton);
            if (idx >= 0)
                footer.Insert(idx, btn);
            else
                footer.Add(btn);

            _button = btn;
            MatchRefreshHeight(btn, refreshButton);
            ApplyVisibility();
        }
        catch (Exception e)
        {
            Plugin.LogError($"RefreshVisibleServers EnsureButton failed: {e}");
        }
    }

    // Pin our button to the refresh button's height. The shared "button" USS
    // sizes to the label, so our longer "REFRESH VISIBLE" text renders a touch
    // shorter/taller than "REFRESH"; copying the resolved height keeps them
    // flush. resolvedStyle can read 0 before the first layout pass, so also
    // re-apply on the next scheduler tick once geometry is settled.
    private static void MatchRefreshHeight(Button ours, Button refresh)
    {
        if (ours == null || refresh == null) return;
        void Apply()
        {
            float h = refresh.resolvedStyle.height;
            if (h > 0) ours.style.height = h;
        }
        try
        {
            Apply();
            ours.schedule.Execute(Apply).StartingIn(50);
        }
        catch (Exception e) { Plugin.LogError($"RefreshVisibleServers MatchRefreshHeight failed: {e.Message}"); }
    }

    /// Show/hide the button to match the live setting. Called from the
    /// settings toggle so flipping it updates an already-open browser.
    public static void ApplyVisibility()
    {
        var btn = _button;
        if (btn == null) return;
        try { btn.style.display = Enabled ? DisplayStyle.Flex : DisplayStyle.None; }
        catch (Exception e) { Plugin.LogError($"RefreshVisibleServers ApplyVisibility failed: {e}"); }
    }

    // ── Click → visible-only ping wave ───────────────────────────────────────

    private static void OnClick()
    {
        if (!Enabled) return;
        var instance = _browser;
        if (instance == null) return;

        if (_pingServer == null || _setPreview == null || _styleServer == null
            || _filterServer == null || _sortServers == null)
        {
            Plugin.LogError("RefreshVisibleServers: missing delegate binding, cannot refresh");
            return;
        }

        // One wave at a time.
        if (Interlocked.CompareExchange(ref _waveRunning, 1, 0) != 0) return;

        try
        {
            var visible = CollectVisibleEndpoints(instance);
            if (visible.Count == 0)
            {
                Interlocked.Exchange(ref _waveRunning, 0);
                Plugin.LogDebug("RefreshVisibleServers: no visible rows to refresh");
                return;
            }

            var cfg = Settings.Current;
            int concurrency = Math.Max(1, cfg?.serverBrowserPingConcurrency ?? 16);
            int connectTimeout = Math.Max(50, cfg?.serverBrowserPingConnectTimeoutMs ?? 1000);
            int responseTimeout = Math.Max(50, cfg?.serverBrowserPingResponseTimeoutMs ?? 1000);

            Interlocked.Exchange(ref _waveTotal, visible.Count);
            Interlocked.Exchange(ref _waveDone, 0);
            SetButtonBusy(0, visible.Count);

            StartWave(instance, visible, concurrency, connectTimeout, responseTimeout);
        }
        catch (Exception e)
        {
            Plugin.LogError($"RefreshVisibleServers OnClick failed: {e}");
            Interlocked.Exchange(ref _waveRunning, 0);
            RestoreButton();
        }
    }

    // Rows whose display is Flex are the ones currently shown by the active
    // filters (vanilla FilterServer toggles this, matching UpdateResultCount).
    private static List<EndPoint> CollectVisibleEndpoints(UIServerBrowser instance)
    {
        var result = new List<EndPoint>();
        if (EndPointMapField?.GetValue(instance) is not Dictionary<EndPoint, VisualElement> map)
            return result;
        foreach (var kv in map)
        {
            if (kv.Key == null || kv.Value == null) continue;
            if (kv.Value.style.display.value == DisplayStyle.Flex)
                result.Add(kv.Key);
        }
        return result;
    }

    private static void StartWave(
        UIServerBrowser instance, List<EndPoint> endpoints,
        int concurrency, int connectTimeout, int responseTimeout)
    {
        EnsureThreadPoolFloor(concurrency);

        var queue = new ConcurrentQueue<EndPoint>();
        foreach (var ep in endpoints) queue.Enqueue(ep);

        int workerCount = Math.Max(1, Math.Min(concurrency, queue.Count));
        var workers = new Task[workerCount];
        for (int i = 0; i < workerCount; i++)
        {
            workers[i] = Task.Factory.StartNew(
                () =>
                {
                    while (queue.TryDequeue(out var ep))
                        PingOne(instance, ep, connectTimeout, responseTimeout);
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        Task.Run(async () =>
        {
            try { await Task.WhenAll(workers).ConfigureAwait(false); }
            catch (Exception e) { Plugin.LogError($"RefreshVisibleServers: ping wave failed: {e}"); }
            finally { FinishWave(instance); }
        });
    }

    // One server's blocking ping, then the UIElements apply marshalled to the
    // main thread. Row lookups inside SetServerPreviewData/StyleServer/etc.
    // no-op for endpoints a concurrent full refresh has since removed.
    private static void PingOne(
        UIServerBrowser instance, EndPoint ep, int connectTimeout, int responseTimeout)
    {
        ServerPreviewData data = null;
        try { data = _pingServer(instance, ep, connectTimeout, responseTimeout); }
        catch (Exception e) { Plugin.LogError($"RefreshVisibleServers: PingServer({ep}) threw: {e.Message}"); }
        finally
        {
            var dataCapture = data;
            MainThreadDispatcher.Run(() =>
            {
                try
                {
                    _setPreview(instance, ep, dataCapture);
                    _styleServer(instance, ep);
                    _filterServer(instance, ep);
                }
                catch (Exception e)
                {
                    Plugin.LogError($"RefreshVisibleServers: ping-complete UI apply failed: {e.Message}");
                }
            });

            int total = Volatile.Read(ref _waveTotal);
            int done = Interlocked.Increment(ref _waveDone);
            if (done > total) done = total;
            int doneCapture = done;
            MainThreadDispatcher.Run(() => SetButtonBusy(doneCapture, total));
        }
    }

    private static void FinishWave(UIServerBrowser instance)
    {
        MainThreadDispatcher.Run(() =>
        {
            try { _sortServers?.Invoke(instance); }
            catch (Exception e) { Plugin.LogError($"RefreshVisibleServers: final sort failed: {e.Message}"); }
            finally
            {
                RestoreButton();
                Interlocked.Exchange(ref _waveRunning, 0);
            }
        });
    }

    // ── Button state ─────────────────────────────────────────────────────────

    private static void SetButtonBusy(int done, int total)
    {
        var btn = _button;
        if (btn == null) return;
        try
        {
            btn.SetEnabled(false);
            btn.text = done >= total ? "REFRESHING..." : $"REFRESHING {done}/{total}...";
        }
        catch (Exception e) { Plugin.LogError($"RefreshVisibleServers SetButtonBusy failed: {e.Message}"); }
    }

    private static void RestoreButton()
    {
        var btn = _button;
        if (btn == null) return;
        try
        {
            btn.text = ButtonText;
            btn.SetEnabled(true);
        }
        catch (Exception e) { Plugin.LogError($"RefreshVisibleServers RestoreButton failed: {e.Message}"); }
    }

    // Raise the ThreadPool floor before a wave so the TCP client's per-connection
    // helper tasks don't queue behind our blocked pings (see the cache's wave for
    // the full rationale). Idempotent; never lowers the floor.
    private static void EnsureThreadPoolFloor(int concurrency)
    {
        try
        {
            int needed = Math.Max(16, concurrency * 4);
            ThreadPool.GetMinThreads(out int curWorker, out int curIo);
            int newWorker = Math.Max(curWorker, needed);
            int newIo = Math.Max(curIo, needed);
            if (newWorker != curWorker || newIo != curIo)
                ThreadPool.SetMinThreads(newWorker, newIo);
        }
        catch (Exception e)
        {
            Plugin.LogError($"RefreshVisibleServers: EnsureThreadPoolFloor failed: {e.Message}");
        }
    }
}
