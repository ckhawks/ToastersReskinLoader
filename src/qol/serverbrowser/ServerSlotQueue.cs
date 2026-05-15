// ServerSlotQueue — auto-retry into a full server, in the background.
//
// Concept (adapted from the b202 ServerQueue mod):
//   * When a join attempt is rejected with ConnectionRejectionCode.ServerFull,
//     start a background poll on JUST that one server's TCP preview port.
//     We open our own TCPClient with the 3-arg constructor (the same
//     overload BeaconPinger uses) because vanilla's UIServerBrowser.PingServer
//     2-arg overload doesn't actually enforce the connect timeout and
//     blocks on the ~64s OS-level TCP SYN_SENT timeout on unreachable
//     hosts.
//   * Every 1 second, ping the target. As soon as players < maxPlayers,
//     fire ConnectionManager.Client_StartClient with the saved password
//     (if SavedServerPasswords has one) to auto-join — even if the user
//     is currently in a different server or in local practice. They get
//     yanked in the moment a slot opens.
//   * UI: hijack the existing UIMatchmaking "Matching" panel so we get
//     vanilla styling for free. The phase label animates ("WAITING FOR
//     SLOT", "...", "...", ".") on a 4Hz tick so the user can see the
//     queue is actually breathing — without animation the panel looks
//     frozen and indistinguishable from a stuck state. A smaller italic
//     subtitle label is injected below the phase label to show the
//     target server name. Visibility, IsVisible on the parent UIView,
//     and button state are re-asserted every tick so anything that
//     drives the matchmaking panel (UIMatchmakingController.UpdateMatching,
//     scene transitions, joining another server) can't visually steal
//     the panel from us. UIMatchmakingController.UpdateMatching is also
//     patched to short-circuit while our queue is active.
//
// Cancel paths:
//   * User clicks the X on the panel → cancel + hide.
//   * User joins the TARGET server successfully → cancel + hide.
//   * Joining a different server / entering practice → queue keeps
//     running, panel stays floating. We're explicitly NOT cancelling
//     here because the user asked to be able to do other things while
//     waiting on the slot.
//
// Failure mode (per user pref): show a "SERVER UNREACHABLE — RETRYING"
// status but keep trying — only the user's cancel ends the queue.
//
// Threading: TCP ping is synchronous and blocking, so the polling loop
// runs on a Task. UI updates and Client_StartClient calls marshal back
// to the main thread via ThreadManager.Enqueue.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.qol.serverbrowser;

internal static class ServerSlotQueue
{
    // Loop ticks at 4Hz. Every 4 ticks (≈1s) we ping the target server;
    // every tick we update the animated dots and re-assert panel state.
    private const int LoopTickMs           = 250;
    private const int PingEveryNTicks      = 3;
    private const int PingConnectTimeoutMs  = 1000;
    private const int PingResponseTimeoutMs = 1000;

    // Queue state (single in-flight queue per session — the user can
    // only be trying to join one server at a time anyway).
    private static EndPoint _targetEndPoint;
    private static string  _targetName;
    private static string  _targetPassword;
    private static DateTime _startedUtc;
    private static CancellationTokenSource _cts;

    // Latest poll result (volatile flag set by the background task, read
    // by the main-thread render). Lets us decouple animation cadence from
    // ping cadence — the dots keep rolling even mid-ping.
    private static volatile bool _pingingNow;
    private static volatile bool _lastPingUnreachable;
    private static volatile bool _joining;
    private static int _consecutiveFailures;

    // Log every Nth ping result so a long-running queue doesn't bury
    // the player log with status lines. Special-case events
    // (slot-opened, first failure, connection errors) still log
    // unconditionally so anything actionable is always visible.
    private const int LogEveryNPings = 10;
    private static int _pingCount;

    // Lazily-cached UIMatchmaking phase Label + our injected subtitle
    // and the column wrapper we use to stack them vertically. Keeping
    // references lets us avoid a Q lookup every tick.
    private static UnityEngine.UIElements.Label _phaseLabel;
    private static UnityEngine.UIElements.Label _subtitleLabel;
    private static UnityEngine.UIElements.VisualElement _stackWrap;
    private static UnityEngine.UIElements.VisualElement _phaseOriginalParent;
    private static int _phaseOriginalIndex;

    internal static bool IsActive => _cts != null;

    // ─────────────────────────── lifecycle ────────────────────────────────

    internal static void Initialize()
    {
        try
        {
            EventManager.AddEventListener("Event_OnConnectionRejected", OnConnectionRejected);
            EventManager.AddEventListener("Event_OnMatchmakingMatchingClickClose", OnUserCancelClicked);
            // OnClientConnected only cancels when the new connection
            // matches OUR target endpoint — that way the user can join
            // a different server (or play local-host practice) while
            // we keep monitoring the target in the background.
            EventManager.AddEventListener("Event_OnClientConnected", OnClientConnected);
            Plugin.Log("[QoL] slot-queue: initialized + listeners attached");
        }
        catch (Exception e) { Plugin.LogError("[QoL] slot-queue init failed: " + e); }
    }

    internal static void Teardown()
    {
        CancelInternal(silent: true);
        try
        {
            EventManager.RemoveEventListener("Event_OnConnectionRejected", OnConnectionRejected);
            EventManager.RemoveEventListener("Event_OnMatchmakingMatchingClickClose", OnUserCancelClicked);
            EventManager.RemoveEventListener("Event_OnClientConnected", OnClientConnected);
        }
        catch { }
    }

    // ─────────────────────────── event handlers ───────────────────────────

    private static void OnConnectionRejected(Dictionary<string, object> message)
    {
        // Unconditional entry-point log so the user can confirm the event
        // is reaching us at all — saves a round-trip when "nothing
        // happens" reports come in. Filtering decisions are logged below.
        Plugin.Log("[QoL] slot-queue: Event_OnConnectionRejected fired");

        if (!(QoLRunner.Instance?.Config?.enableServerSlotQueue ?? true))
        {
            Plugin.Log("[QoL] slot-queue: feature disabled, ignoring rejection");
            return;
        }
        try
        {
            var rejection = message["connectionRejection"] as ConnectionRejection;
            if (rejection == null)
            {
                Plugin.Log("[QoL] slot-queue: rejection event with no payload");
                return;
            }
            if (rejection.code != ConnectionRejectionCode.ServerFull)
            {
                Plugin.Log($"[QoL] slot-queue: rejection code is {rejection.code} (need ServerFull) — ignoring");
                return;
            }

            var lastConn = GlobalStateManager.ConnectionState.LastConnection;
            if (lastConn?.EndPoint == null)
            {
                Plugin.LogError("[QoL] slot-queue: ServerFull rejection but no LastConnection.EndPoint");
                return;
            }

            // If we're already queuing for THIS endpoint, treat the
            // rejection as a "still full" probe response from our own
            // retry — keep the elapsed-time counter rolling and don't
            // tear down the panel between attempts.
            bool sameTarget = _targetEndPoint != null
                              && _targetEndPoint.ipAddress == lastConn.EndPoint.ipAddress
                              && _targetEndPoint.port == lastConn.EndPoint.port;
            if (!sameTarget) CancelInternal(silent: true);

            _targetEndPoint = lastConn.EndPoint;
            _targetName     = ResolveServerName(_targetEndPoint) ?? lastConn.EndPoint.ToString();
            _targetPassword = lastConn.Password ?? TryGetSavedPassword(_targetEndPoint);
            if (!sameTarget) _startedUtc = DateTime.UtcNow;
            _joining        = false;
            if (_cts == null) _cts = new CancellationTokenSource();

            Plugin.Log($"[QoL] slot-queue: {(sameTarget ? "still-full retry" : "starting queue")} for {_targetEndPoint.ipAddress}:{_targetEndPoint.port} ({_targetName})");
            ShowQueuePanel(_targetName);

            // Only kick a fresh poll task on the first arm. A still-full
            // re-rejection (same target) means our previous PollLoopAsync
            // is still alive on the existing _cts — re-launching here
            // would double-poll.
            if (!sameTarget)
            {
                Task.Run(() => PollLoopAsync(_cts.Token));
            }
        }
        catch (Exception e) { Plugin.LogError("[QoL] slot-queue OnConnectionRejected failed: " + e); }
    }

    private static void OnUserCancelClicked(Dictionary<string, object> _)
    {
        if (!IsActive) return;
        Plugin.Log("[QoL] slot-queue: cancelled by user");
        CancelInternal(silent: false);
    }

    // Only end the queue when the user actually connects to the target
    // endpoint. Joining a different server or going into local practice
    // leaves the queue running in the background so the user can keep
    // monitoring the target — and we yank them in as soon as a slot
    // opens.
    private static void OnClientConnected(Dictionary<string, object> _)
    {
        if (!IsActive) return;
        try
        {
            var conn = GlobalStateManager.ConnectionState.Connection;
            var ep = conn?.EndPoint;
            var target = _targetEndPoint;
            bool matchesTarget = ep != null && target != null
                                 && ep.ipAddress == target.ipAddress
                                 && ep.port == target.port;
            if (matchesTarget)
            {
                Plugin.Log("[QoL] slot-queue: joined target — closing panel");
                CancelInternal(silent: false);
            }
            else
            {
                Plugin.Log($"[QoL] slot-queue: connected to {(ep?.ToString() ?? "?")}, keeping queue alive for {target?.ipAddress}:{target?.port}");
                // Connecting to a different server resets the "joining"
                // animation — we're not joining the target right now,
                // we're connected to a side server.
                _joining = false;
            }
        }
        catch (Exception e) { Plugin.LogWarning("[QoL] slot-queue OnClientConnected check failed: " + e.Message); }
    }

    private static void CancelInternal(bool silent)
    {
        var cts = _cts;
        _cts = null;
        if (cts != null)
        {
            try { cts.Cancel(); cts.Dispose(); } catch { }
        }
        _targetEndPoint = null;
        _targetName = null;
        _targetPassword = null;
        _joining = false;
        _lastPingUnreachable = false;
        _startedUtc = DateTime.MinValue;
        if (!silent) HideQueuePanel();
    }

    // ─────────────────────────── main loop ────────────────────────────────
    //
    // Single Task that ticks at 4Hz. Each tick:
    //   * Re-asserts the matchmaking panel state (in case
    //     UIMatchmakingController or anything else hid it / cleared the
    //     phase text between our calls).
    //   * Updates the animated phase text. Animation runs every tick so
    //     the panel always looks alive even when no ping is in flight.
    //   * Every 4th tick (≈1s), spawns a non-awaited ping on a worker
    //     thread. The ping handler updates _lastPingUnreachable when
    //     done and, on slot detection, marshals the auto-join to main.
    private static async Task PollLoopAsync(CancellationToken token)
    {
        _consecutiveFailures = 0;
        _lastPingUnreachable = false;
        int tick = 0;
        while (!token.IsCancellationRequested)
        {
            try
            {
                int frame = tick;
                MarshalToMainThread(() => RenderTick(frame));

                if (tick % PingEveryNTicks == 0)
                {
                    // Fire-and-forget ping. If a previous ping is still
                    // running we skip launching another (TCP connect +
                    // up-to-1s response wait > 250ms tick).
                    if (!_pingingNow)
                    {
                        _ = Task.Run(() => RunOnePing(token), token);
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning("[QoL] ServerSlotQueue tick failed: " + e.Message); }

            try { await Task.Delay(LoopTickMs, token).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
            tick++;
        }
    }

    private static void RunOnePing(CancellationToken token)
    {
        if (token.IsCancellationRequested) return;
        var ep = _targetEndPoint;
        if (ep == null) return;
        _pingingNow = true;
        try
        {
            (int players, int maxPlayers)? preview = PingTargetServer(ep);
            if (token.IsCancellationRequested) return;

            // Log throttle — print every 10th status ping so the log
            // doesn't fill up during a long wait. Slot-open and the first
            // failure after a successful streak still log unconditionally
            // since they're state transitions worth always seeing.
            int n = ++_pingCount;
            bool verbose = (n % LogEveryNPings) == 0;

            if (preview.HasValue)
            {
                bool wasUnreachable = _lastPingUnreachable;
                _lastPingUnreachable = false;
                _consecutiveFailures = 0;

                if (verbose || wasUnreachable)
                    Plugin.Log($"[QoL] slot-queue ping #{n} {ep.ipAddress}:{ep.port} -> {preview.Value.players}/{preview.Value.maxPlayers}");

                if (preview.Value.players < preview.Value.maxPlayers)
                {
                    Plugin.Log($"[QoL] slot-queue: slot opened on {ep.ipAddress}:{ep.port} ({preview.Value.players}/{preview.Value.maxPlayers}) — auto-joining");
                    _joining = true;
                    MarshalToMainThread(TryJoin);
                }
            }
            else
            {
                bool wasReachable = !_lastPingUnreachable;
                _lastPingUnreachable = true;
                _consecutiveFailures++;
                if (verbose || wasReachable)
                    Plugin.Log($"[QoL] slot-queue ping #{n} {ep.ipAddress}:{ep.port} -> unreachable (#{_consecutiveFailures})");
            }
        }
        catch (Exception e) { Plugin.LogError("[QoL] slot-queue ping threw: " + e.Message); }
        finally { _pingingNow = false; }
    }

    // Custom TCP preview ping. Modeled on vanilla UIServerBrowser.PingServer
    // but uses the 3-arg TCPClient overload (the 2-arg one vanilla uses
    // ignores the requested connect timeout and waits ~64s on the OS-level
    // SYN_SENT timeout, which is why we used to see one ping per minute).
    // Returns (players, maxPlayers) on success, null on failure / timeout.
    private static (int players, int maxPlayers)? PingTargetServer(EndPoint endPoint)
    {
        TCPClient tcp = null;
        try
        {
            tcp = new TCPClient(endPoint, PingConnectTimeoutMs, PingResponseTimeoutMs);
            (int p, int m)? captured = null;
            var responseEvent = new ManualResetEventSlim(false);

            tcp.OnConnected += () =>
            {
                try
                {
                    string msg = JsonSerializer.Serialize(new TCPServerPreviewRequest());
                    tcp.SendMessage(msg);
                }
                catch { }
            };
            tcp.OnMessageReceived += msg =>
            {
                try
                {
                    var typed = JsonSerializer.Deserialize<TCPServerMessage>(msg);
                    if (typed.type == TCPServerMessageType.PreviewResponse)
                    {
                        var resp = JsonSerializer.Deserialize<TCPServerPreviewResponse>(msg);
                        captured = (resp.players, resp.maxPlayers);
                        responseEvent.Set();
                    }
                }
                catch { }
            };

            tcp.Connect();
            if (tcp.IsConnected)
            {
                responseEvent.Wait(PingResponseTimeoutMs);
                tcp.Disconnect();
            }
            return captured;
        }
        catch (Exception e)
        {
            Plugin.Log($"[QoL] slot-queue ping exception: {e.Message}");
            return null;
        }
        finally
        {
            try { tcp?.Disconnect(); } catch { }
        }
    }

    // Phase color per state. Default state (waiting) is white so it
    // reads as a neutral label sitting above the bigger server-name
    // line; connecting / unreachable keep accent colors so the user
    // can spot a state change at a glance.
    private static readonly Color PhaseColorWaiting     = Color.white;
    private static readonly Color PhaseColorConnecting  = new Color(0.45f, 0.95f, 0.55f); // soft green
    private static readonly Color PhaseColorUnreachable = new Color(0.95f, 0.45f, 0.45f); // soft red

    // Render one tick of the queue panel on the main thread. Idempotent
    // and cheap — safe to call every 250ms.
    private static void RenderTick(int frame)
    {
        if (!IsActive) return;
        EnsurePanelShown();

        // Phase shows the action; subtitle (smaller, italic) shows the
        // target server name. Split is cleaner than cramming both into
        // the same Label, and keeps font sizes independent.
        string status;
        Color phaseColor;
        if (_joining)
        {
            status = "CONNECTING";
            phaseColor = PhaseColorConnecting;
        }
        else if (_lastPingUnreachable)
        {
            status = "SERVER UNREACHABLE — RETRYING";
            phaseColor = PhaseColorUnreachable;
        }
        else
        {
            status = "WAITING FOR A SLOT";
            phaseColor = PhaseColorWaiting;
        }
        SetPhaseTextSafe(status);
        if (_phaseLabel != null) _phaseLabel.style.color = phaseColor;

        if (_subtitleLabel != null)
            _subtitleLabel.text = _targetName ?? "";

        // Drive the elapsed-time label ourselves. UIMatchmaking formats
        // it as MM:SS (or HH:MM:SS over an hour). Reset _startedUtc on
        // cancel keeps fresh queues from inheriting old elapsed.
        if (_startedUtc != DateTime.MinValue)
        {
            int sec = (int)(DateTime.UtcNow - _startedUtc).TotalSeconds;
            SetTimeVisibleSafe(true);
            SetTimeTextSafe(sec);
        }
    }

    private static void TryJoin()
    {
        try
        {
            var ep = _targetEndPoint;
            if (ep == null) { Plugin.LogDebug("[QoL] slot-queue TryJoin: no target"); return; }
            var cm = MonoBehaviourSingleton<ConnectionManager>.Instance;
            if (cm == null)
            {
                Plugin.LogError("[QoL] slot-queue TryJoin: ConnectionManager.Instance is null");
                return;
            }
            Plugin.Log($"[QoL] slot-queue: Client_StartClient({ep.ipAddress}:{ep.port}, pw={(string.IsNullOrEmpty(_targetPassword) ? "no" : "yes")})");
            cm.Client_StartClient(ep.ipAddress, ep.port, _targetPassword ?? "");
        }
        catch (Exception e) { Plugin.LogError("[QoL] slot-queue TryJoin failed: " + e); }
    }

    private static string TryGetSavedPassword(EndPoint ep)
    {
        if (ep == null) return null;
        string key = ep.ipAddress + ":" + ep.port;
        var store = QoLRunner.Instance?.Config?.savedServerPasswords;
        return (store != null && store.TryGetValue(key, out var pw)) ? pw : null;
    }

    private static string ResolveServerName(EndPoint ep)
    {
        if (ep == null) return null;
        string key = ep.ipAddress + ":" + ep.port;
        // Try the saved-passwords name cache first (populated by the
        // server browser as it pings each row this session).
        var name = SavedServerPasswords.GetCachedServerName(key);
        if (!string.IsNullOrEmpty(name)) return name;
        // Fall back to favorites cache.
        return ServerBrowserSort.GetFavoriteCachedName(key);
    }

    // ─────────────────────────── UI driver ────────────────────────────────

    // Reflection cache for the matchmaking panel setters.
    private static readonly Type UIMatchmakingType = AccessTools.TypeByName("UIMatchmaking");
    private static readonly MethodInfo SetVisible      = UIMatchmakingType?.GetMethod("SetMatchingVisibility");
    private static readonly MethodInfo SetPhaseText    = UIMatchmakingType?.GetMethod("SetMatchingPhaseText");
    private static readonly MethodInfo SetConnectVis   = UIMatchmakingType?.GetMethod("SetMatchingConnectButtonVisibility");
    private static readonly MethodInfo SetCloseVis     = UIMatchmakingType?.GetMethod("SetMatchingCloseButtonVisibility");
    private static readonly MethodInfo SetTimeVis      = UIMatchmakingType?.GetMethod("SetMatchingTimeVisibility");
    private static readonly MethodInfo SetTimeText     = UIMatchmakingType?.GetMethod("SetMatchingTimeText");
    private static readonly PropertyInfo IsVisibleProp = UIMatchmakingType?.GetProperty("IsVisible");
    private static readonly FieldInfo    MatchingField = UIMatchmakingType?.GetField("matching",
        BindingFlags.Instance | BindingFlags.NonPublic);

    private static void ShowQueuePanel(string name)
    {
        EnsurePanelShown();
        SetPhaseTextSafe($"WAITING FOR SLOT TO OPEN");
    }

    // Idempotent. Re-applies the on-screen + button state we want every
    // call. Driven each tick so any race with UIMatchmakingController
    // can't visually hide the queue between our event handler and the
    // panel paint. The time label is driven separately in RenderTick.
    //
    // Also re-asserts UIView.IsVisible = true. When the user joins
    // another server / goes to practice, the game's view manager flips
    // UIView.IsVisible = false on most overlays (including UIMatchmaking)
    // which sets display:none on the whole view, hiding our inner panel
    // entirely. Forcing it back to true every tick keeps the queue
    // floating across scenes.
    private static void EnsurePanelShown()
    {
        var panel = GetMatchmakingPanel();
        if (panel == null) return;
        try
        {
            IsVisibleProp?.SetValue(panel, true);
            SetVisible?.Invoke(panel,    new object[] { true });
            SetConnectVis?.Invoke(panel, new object[] { false });
            SetCloseVis?.Invoke(panel,   new object[] { true });
            EnsureLabelInjections(panel);
        }
        catch (Exception e) { Debug.LogWarning("[QoL] ServerSlotQueue EnsurePanelShown failed: " + e.Message); }
    }

    // Wrap the vanilla phase Label and our injected server-name Label in
    // a small vertical column so they stack:
    //
    //     WAITING FOR A SLOT          (phase, small, white)
    //     [Comp Tweaks] PHL Public 2  (subtitle, big, bold)
    //
    // The column slot sits exactly where vanilla's phase label used to
    // live, so the time + close icons stay where they were. Runs
    // idempotently every tick because scene transitions can rebuild
    // the DOM (we'd lose the wrapper otherwise).
    private static void EnsureLabelInjections(object panel)
    {
        if (panel == null || MatchingField == null) return;
        var matching = MatchingField.GetValue(panel) as UnityEngine.UIElements.VisualElement;
        if (matching == null) return;

        var phaseLabel = matching.Q<UnityEngine.UIElements.Label>("PhaseLabel");
        if (phaseLabel == null) return;

        // Refresh cached references if vanilla rebuilt the DOM (scene
        // change). The previous phaseLabel may have been destroyed —
        // re-discover from the current tree and re-install our wrapper.
        if (!ReferenceEquals(_phaseLabel, phaseLabel))
        {
            _phaseLabel = phaseLabel;
            _stackWrap = null;
            _subtitleLabel = null;
        }

        // Phase: small + bold all-caps category label. Color is set per
        // tick in RenderTick based on state.
        _phaseLabel.style.fontSize = 13;
        _phaseLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _phaseLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
        _phaseLabel.style.whiteSpace = UnityEngine.UIElements.WhiteSpace.NoWrap;
        _phaseLabel.style.marginTop = 0;
        _phaseLabel.style.marginBottom = 0;

        if (_stackWrap == null || _stackWrap.parent == null)
        {
            // Remember where the phase label originally lived so Hide
            // can put it back when we tear the queue down.
            _phaseOriginalParent = phaseLabel.parent;
            _phaseOriginalIndex  = phaseLabel.parent.IndexOf(phaseLabel);

            // Build the subtitle (server name): big, bold, white. This
            // is the primary identifier the user cares about, so it
            // gets the prominent slot.
            _subtitleLabel = new UnityEngine.UIElements.Label
            {
                name = "ToasterSlotQueueSubtitle",
            };
            _subtitleLabel.style.fontSize = 20;
            _subtitleLabel.style.color = Color.white;
            _subtitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _subtitleLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            _subtitleLabel.style.whiteSpace = UnityEngine.UIElements.WhiteSpace.NoWrap;
            _subtitleLabel.style.overflow = UnityEngine.UIElements.Overflow.Hidden;
            _subtitleLabel.style.textOverflow = UnityEngine.UIElements.TextOverflow.Ellipsis;
            _subtitleLabel.style.marginTop = 0;
            _subtitleLabel.style.marginBottom = 0;

            _stackWrap = new UnityEngine.UIElements.VisualElement
            {
                name = "ToasterSlotQueueStack",
            };
            _stackWrap.style.flexDirection = UnityEngine.UIElements.FlexDirection.Column;
            _stackWrap.style.alignItems = UnityEngine.UIElements.Align.FlexStart;
            _stackWrap.style.justifyContent = UnityEngine.UIElements.Justify.Center;
            _stackWrap.style.flexGrow = 1;
            _stackWrap.style.flexShrink = 1;
            _stackWrap.style.minWidth = 0; // allow ellipsis to actually clip

            // Yank phase out of its current parent, drop into wrapper,
            // then place wrapper where phase used to be.
            phaseLabel.RemoveFromHierarchy();
            _stackWrap.Add(phaseLabel);
            _stackWrap.Add(_subtitleLabel);
            _phaseOriginalParent.Insert(_phaseOriginalIndex, _stackWrap);
        }
    }

    private static void SetTimeVisibleSafe(bool visible)
    {
        var panel = GetMatchmakingPanel();
        if (panel == null) return;
        try { SetTimeVis?.Invoke(panel, new object[] { visible }); }
        catch { }
    }

    private static void SetTimeTextSafe(int seconds)
    {
        var panel = GetMatchmakingPanel();
        if (panel == null) return;
        try { SetTimeText?.Invoke(panel, new object[] { seconds }); }
        catch { }
    }

    private static void SetPhaseTextSafe(string text)
    {
        var panel = GetMatchmakingPanel();
        if (panel == null) return;
        try { SetPhaseText?.Invoke(panel, new object[] { text }); }
        catch { }
    }

    private static void HideQueuePanel()
    {
        var panel = GetMatchmakingPanel();
        if (panel == null) return;
        try
        {
            SetVisible?.Invoke(panel,    new object[] { false });
            SetPhaseText?.Invoke(panel,  new object[] { string.Empty });
            SetCloseVis?.Invoke(panel,   new object[] { false });
            SetConnectVis?.Invoke(panel, new object[] { false });
            SetTimeVis?.Invoke(panel,    new object[] { false });

            // Tear down our DOM mutations so the panel goes back to a
            // pristine vanilla state for any future matchmaking use:
            //   1. Move the phase label back to its original parent at
            //      its original index.
            //   2. Drop the wrapper + subtitle.
            //   3. Clear the inline style overrides we put on phaseLabel
            //      so the base USS font/weight take over again.
            if (_stackWrap != null)
            {
                if (_phaseLabel != null && _phaseOriginalParent != null)
                {
                    try
                    {
                        _phaseLabel.RemoveFromHierarchy();
                        int idx = Math.Min(_phaseOriginalIndex, _phaseOriginalParent.childCount);
                        _phaseOriginalParent.Insert(idx, _phaseLabel);
                    }
                    catch { }
                }
                _stackWrap.RemoveFromHierarchy();
                _stackWrap = null;
            }
            if (_subtitleLabel != null)
            {
                _subtitleLabel.RemoveFromHierarchy();
                _subtitleLabel = null;
            }
            if (_phaseLabel != null)
            {
                _phaseLabel.style.fontSize = StyleKeyword.Null;
                _phaseLabel.style.unityFontStyleAndWeight = StyleKeyword.Null;
                _phaseLabel.style.unityTextAlign = StyleKeyword.Null;
                _phaseLabel.style.color = StyleKeyword.Null;
                _phaseLabel.style.whiteSpace = StyleKeyword.Null;
                _phaseLabel.style.marginTop = StyleKeyword.Null;
                _phaseLabel.style.marginBottom = StyleKeyword.Null;
            }
            _phaseOriginalParent = null;
            _phaseOriginalIndex = 0;
        }
        catch (Exception e) { Debug.LogWarning("[QoL] ServerSlotQueue HideQueuePanel failed: " + e.Message); }
    }

    private static object GetMatchmakingPanel()
    {
        var ui = MonoBehaviourSingleton<UIManager>.Instance;
        if (ui == null) return null;
        // UIManager.Matchmaking is the live UIMatchmaking instance.
        var prop = ui.GetType().GetField("Matchmaking",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        return prop?.GetValue(ui);
    }

    private static void MarshalToMainThread(Action action)
    {
        try { MonoBehaviourSingleton<ThreadManager>.Instance?.Enqueue(action); }
        catch (Exception e) { Debug.LogWarning("[QoL] ServerSlotQueue marshal failed: " + e.Message); }
    }

    // ─────────────────────── vanilla controller patch ─────────────────────
    //
    // UIMatchmakingController.UpdateMatching runs on player/match/connection
    // state changes and resets the panel based on its own idle logic. While
    // our queue is up, suppress that update so it doesn't overwrite our
    // phase text or hide the close button.
    [HarmonyPatch]
    private static class Patch_UpdateMatching
    {
        static MethodBase TargetMethod()
            => AccessTools.Method(AccessTools.TypeByName("UIMatchmakingController"), "UpdateMatching");

        [HarmonyPrefix]
        static bool Prefix() => !IsActive;
    }
}
