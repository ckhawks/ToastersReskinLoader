// MainMenuButtons — injects two optional shortcuts into the title screen
// (UIMainMenu) right under the vanilla Play button:
//
//   * QUICK JOIN (default on)   — refresh the master server list, score
//                                  every server that passes the user's
//                                  saved browser filters, and auto-join
//                                  the best match. "Best" = highest
//                                  player ratio that isn't full, ping
//                                  tiebreaker, mild bump for official
//                                  "Toaster's Rink" servers (by name).
//   * SERVER BROWSER (default off) — opens the vanilla UIServerBrowser
//                                  from the title screen so the user
//                                  doesn't have to step through Play.
//
// Both buttons are styled to match the vanilla menu buttons by copying
// the PlayButton's USS class list — that way they pick up whatever skin
// the game / TR is currently rendering with. UIMainMenu.Initialize is
// patched (postfix) to inject them; on first injection we cache the
// MainMenu container + the original button index range so we can keep
// the buttons in a sensible position even after vanilla updates the
// layout.
//
// Quick Join hijacks the UIMatchmaking panel briefly to show "FINDING
// BEST SERVER…" while the refresh wave runs. The ServerSlotQueue uses
// the same panel — if the slot queue is already running we surface a
// brief notice instead of clobbering it.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using ToasterReskinLoader.qol.serverbrowser;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.qol;

internal static class MainMenuButtons
{
    private const string QuickJoinButtonName = "ToasterQuickJoinButton";
    private const string BrowserButtonName   = "ToasterMainMenuBrowserButton";

    // Cached references — refreshed when the underlying buttons are
    // detached (e.g. after a panel rebuild).
    private static Button _quickJoinButton;
    private static Button _browserButton;

    // Quick-join scoring config. The name bonus nudges official Toaster's
    // Rink servers up the ranking; they're named "Toaster's Rink …", not
    // tagged "[TR]", so match the name (apostrophe optional, straight or
    // curly).
    private const int  QuickJoinTimeoutMs = 5000;
    private const int  QuickJoinPollMs    = 250;
    private const float TrBonus           = 0.05f;
    private static readonly Regex TrTagPattern = new(@"toaster['’]?s\s+rink", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static bool QuickJoinInFlight;
    private static CancellationTokenSource _qjCts;

    // Set true when the user opened the browser via OUR main-menu button.
    // Vanilla's Event_OnServerBrowserClickClose handler routes to UIPlay
    // / UIPauseMenu depending on the game phase, but if we put the user
    // there, closing should return them where they came from (the title
    // screen). Consumed on the next close.
    private static bool _openedFromMainMenu;

    // ─────────────────────────── lifecycle ────────────────────────────────
    //
    // UIMainMenu.Initialize runs early during the game's startup — before
    // our Harmony.PatchAll lands. That makes a [HarmonyPatch(Initialize)]
    // postfix useless for first-show injection (nothing to patch *into*
    // for the call that already happened). Instead we listen for the
    // Event_OnMainMenuShow event (UIMainMenu fires this from its Show()
    // override every time the menu opens) and also do an immediate
    // injection if the menu is already visible at plugin enable time.

    public static void Initialize()
    {
        try
        {
            EventManager.AddEventListener("Event_OnMainMenuShow", OnMainMenuShow);
            // Subscribe AFTER vanilla so our handler runs after vanilla
            // has shown UIPlay/UIPauseMenu — we can then swap to
            // MainMenu when the user opened the browser via our button.
            EventManager.AddEventListener("Event_OnServerBrowserClickClose", OnServerBrowserClose);
            // Drop the "opened from main menu" intent any time the
            // connection state flips (they joined a server, got
            // disconnected, etc.) so a stale flag doesn't survive long
            // enough to redirect a subsequent vanilla-opened close.
            EventManager.AddEventListener("Event_OnConnectionStateChanged", OnConnectionStateChanged);
            // The matchmaking panel's X button raises this event; we
            // use it to cancel an in-flight Quick Join. The slot queue
            // also listens but only acts when ITS state is active, so
            // there's no conflict (we're mutually exclusive in
            // practice — slot queue suppresses our overlay).
            EventManager.AddEventListener("Event_OnMatchmakingMatchingClickClose", OnMatchmakingClose);
            // Catch the case where the main menu is already open by the
            // time our plugin loaded — schedule the inject for the next
            // frame so any in-flight UI setup has settled.
            MarshalToMain(RefreshForCurrentMenu);
            Plugin.Log("[QoL] MainMenuButtons: initialized");
        }
        catch (Exception e) { Plugin.LogError("[QoL] MainMenuButtons.Initialize failed: " + e); }
    }

    public static void Teardown()
    {
        try { EventManager.RemoveEventListener("Event_OnMainMenuShow", OnMainMenuShow); } catch { }
        try { EventManager.RemoveEventListener("Event_OnServerBrowserClickClose", OnServerBrowserClose); } catch { }
        try { EventManager.RemoveEventListener("Event_OnConnectionStateChanged", OnConnectionStateChanged); } catch { }
        try { EventManager.RemoveEventListener("Event_OnMatchmakingMatchingClickClose", OnMatchmakingClose); } catch { }
    }

    private static void OnMatchmakingClose(Dictionary<string, object> _)
    {
        if (!QuickJoinInFlight) return;
        Plugin.Log("[QoL] quick-join: cancelled by user");
        try { _qjCts?.Cancel(); } catch { }
        MarshalToMain(ClearOverlay);
    }

    private static void OnMainMenuShow(Dictionary<string, object> _)
    {
        try { RefreshForCurrentMenu(); }
        catch (Exception e) { Plugin.LogError("[QoL] OnMainMenuShow failed: " + e); }
    }

    private static void OnServerBrowserClose(Dictionary<string, object> _)
    {
        if (!_openedFromMainMenu) return;
        _openedFromMainMenu = false;
        try
        {
            // Vanilla's handler already ran (it subscribed earlier in
            // UIManagerController.Awake, before our Initialize). It just
            // showed UIPlay (LockerRoom phase) or UIPauseMenu (Playing
            // phase) and hid the browser. Undo that and show MainMenu
            // instead so the user lands back where they pressed the
            // button.
            var ui = MonoBehaviourSingleton<UIManager>.Instance;
            if (ui == null) return;
            ui.Play?.Hide();
            ui.PauseMenu?.Hide();
            ui.MainMenu?.Show();
        }
        catch (Exception e) { Plugin.LogWarning("[QoL] OnServerBrowserClose redirect failed: " + e.Message); }
    }

    private static void OnConnectionStateChanged(Dictionary<string, object> _)
    {
        // Any join / disconnect invalidates the "came from main menu"
        // intent — by the time the browser is closeable again the user
        // is in a totally different flow.
        _openedFromMainMenu = false;
    }

    // Public so the QoL settings toggles can ask us to re-apply state
    // without waiting for the next Show event.
    public static void RefreshForCurrentMenu()
    {
        try
        {
            var menu = MonoBehaviourSingleton<UIManager>.Instance?.MainMenu;
            if (menu == null)
            {
                Plugin.Log("[QoL] MainMenuButtons: UIManager.MainMenu is null, skipping inject");
                return;
            }
            RebuildButtons(menu);
        }
        catch (Exception e) { Plugin.LogError("[QoL] MainMenuButtons refresh failed: " + e); }
    }

    private static void RebuildButtons(UIMainMenu menu)
    {
        var cfg = QoLRunner.Instance?.Config;
        if (cfg == null)
        {
            Plugin.Log("[QoL] MainMenuButtons.RebuildButtons: no config yet, skipping");
            return;
        }

        var mainMenuContainer = AccessTools
            .Field(typeof(UIMainMenu), "mainMenu")
            ?.GetValue(menu) as VisualElement;
        if (mainMenuContainer == null)
        {
            Plugin.LogWarning("[QoL] MainMenuButtons: mainMenu container not found, skipping inject");
            return;
        }

        var playButton = mainMenuContainer.Q<Button>("PlayButton");
        if (playButton == null)
        {
            Plugin.LogWarning("[QoL] MainMenuButtons: PlayButton not found, skipping inject");
            return;
        }

        Plugin.Log($"[QoL] MainMenuButtons.RebuildButtons: quickJoin={cfg.enableMainMenuQuickJoin} browser={cfg.enableMainMenuServerBrowser}");

        // Quick Join button — slot 0 immediately after Play.
        if (cfg.enableMainMenuQuickJoin)
        {
            if (_quickJoinButton == null || _quickJoinButton.parent != mainMenuContainer)
            {
                _quickJoinButton = MakeMenuButton(playButton, "QUICK JOIN", QuickJoinButtonName, OnClickQuickJoin);
                int idx = mainMenuContainer.IndexOf(playButton) + 1;
                mainMenuContainer.Insert(idx, _quickJoinButton);
            }
        }
        else if (_quickJoinButton != null)
        {
            _quickJoinButton.RemoveFromHierarchy();
            _quickJoinButton = null;
        }

        // Server Browser button — slot immediately after Quick Join.
        if (cfg.enableMainMenuServerBrowser)
        {
            if (_browserButton == null || _browserButton.parent != mainMenuContainer)
            {
                _browserButton = MakeMenuButton(playButton, "SERVER BROWSER", BrowserButtonName, OnClickServerBrowser);
                int anchor = _quickJoinButton != null && _quickJoinButton.parent == mainMenuContainer
                    ? mainMenuContainer.IndexOf(_quickJoinButton) + 1
                    : mainMenuContainer.IndexOf(playButton) + 1;
                mainMenuContainer.Insert(anchor, _browserButton);
            }
        }
        else if (_browserButton != null)
        {
            _browserButton.RemoveFromHierarchy();
            _browserButton = null;
        }
    }

    // Clones the vanilla PlayButton's class list so the new buttons
    // adopt whatever the active stylesheet looks like (matches the
    // theme automatically across Toaster skins).
    private static Button MakeMenuButton(Button template, string text, string name, Action onClick)
    {
        var btn = new Button(onClick) { name = name, text = text };
        foreach (var cls in template.GetClasses())
            btn.AddToClassList(cls);
        return btn;
    }

    // ─────────────────────────── click handlers ───────────────────────────

    private static void OnClickServerBrowser()
    {
        try
        {
            var ui = MonoBehaviourSingleton<UIManager>.Instance;
            if (ui == null) return;
            // Vanilla UIPlayController does the same swap when its
            // "Server Browser" button is clicked: hide the source view,
            // show the browser. We additionally flag the origin so the
            // close handler can return the user to MainMenu instead of
            // UIPlay (vanilla's default).
            _openedFromMainMenu = true;
            ui.ServerBrowser?.Show();
            ui.MainMenu?.Hide();
        }
        catch (Exception e) { Plugin.LogError("[QoL] OnClickServerBrowser failed: " + e); }
    }

    private static void OnClickQuickJoin()
    {
        if (QuickJoinInFlight)
        {
            Plugin.Log("[QoL] quick-join: already in flight");
            return;
        }
        QuickJoinInFlight = true;
        _qjCts = new CancellationTokenSource();
        var token = _qjCts.Token;
        try { Task.Run(() => QuickJoinAsync(token)); }
        catch (Exception e)
        {
            QuickJoinInFlight = false;
            try { _qjCts.Dispose(); } catch { }
            _qjCts = null;
            Plugin.LogError("[QoL] quick-join kickoff failed: " + e);
        }
    }

    // ──────────────────────── quick-join flow ─────────────────────────────

    private static async Task QuickJoinAsync(CancellationToken token)
    {
        try
        {
            // Show our overlay on the matchmaking panel. We piggyback on
            // the same panel ServerSlotQueue uses but with our own phase
            // text; the queue and quick-join are mutually exclusive in
            // practice (a slot-queue is already running for a target,
            // quick-join is for picking a fresh target).
            MarshalToMain(() => SetOverlay("FINDING BEST SERVER", "Scanning available servers"));

            // Kick the refresh wave on the main thread. UIServerBrowser
            // is a MonoBehaviour and Refresh() touches Unity APIs.
            MarshalToMain(TriggerRefresh);

            // Poll the server browser map. We're done when:
            //   * refreshButton is back-enabled (vanilla's "wave done"
            //     signal), OR
            //   * QuickJoinTimeoutMs elapses (give up waiting), OR
            //   * the user clicked X (token cancelled).
            var deadline = DateTime.UtcNow.AddMilliseconds(QuickJoinTimeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                try { await Task.Delay(QuickJoinPollMs, token).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
                if (token.IsCancellationRequested) return;
                bool done = false;
                MarshalToMainSync(() => done = IsRefreshComplete());
                if (done) break;
            }
            if (token.IsCancellationRequested) return;

            // Collect candidates on main, score off main.
            List<Candidate> candidates = null;
            MarshalToMainSync(() => candidates = CollectCandidates());

            if (candidates == null || candidates.Count == 0)
            {
                MarshalToMain(() => SetOverlay("NO MATCH FOUND", "no servers pass your saved filters"));
                try { await Task.Delay(2000, token).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
                MarshalToMain(ClearOverlay);
                return;
            }

            var best = candidates.OrderByDescending(c => c.Score).First();
            Plugin.Log($"[QoL] quick-join: best is {best.IpAddress}:{best.Port} ({best.Name}) — {best.Players}/{best.MaxPlayers} @ {best.Ping}ms (score {best.Score:0.000})");

            MarshalToMain(() => SetOverlay("CONNECTING", best.Name));
            MarshalToMain(() => Connect(best));

            // Tear down our overlay shortly after dispatching the connect.
            // The vanilla connection flow will drive its own UI from here.
            try { await Task.Delay(800, token).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
            MarshalToMain(ClearOverlay);
        }
        catch (Exception e) { Plugin.LogError("[QoL] quick-join failed: " + e); }
        finally
        {
            QuickJoinInFlight = false;
            try { _qjCts?.Dispose(); } catch { }
            _qjCts = null;
            // Always ensure the overlay is torn down on exit — covers
            // the cancel-mid-flight path where the early `return` skipped
            // ClearOverlay above.
            MarshalToMain(ClearOverlay);
        }
    }

    // ────────────────────── candidate / scoring model ─────────────────────

    private sealed class Candidate
    {
        public string IpAddress;
        public ushort Port;
        public string Name;
        public int    Players;
        public int    MaxPlayers;
        public int    Ping;
        public float  Score;
    }

    private static List<Candidate> CollectCandidates()
    {
        var list = new List<Candidate>();
        try
        {
            var browser = MonoBehaviourSingleton<UIManager>.Instance?.ServerBrowser;
            if (browser == null) return list;

            // Reuse ServerBrowserSort's map/preview accessors so the two
            // copies don't drift if vanilla's field name or the userData
            // shape changes in a future Puck build.
            var map = ServerBrowserSort.GetMap(browser);
            if (map == null) return list;

            var cfg = QoLRunner.Instance?.Config ?? new QoLConfig();

            foreach (var kv in map)
            {
                var ep = kv.Key;
                var rowRoot = kv.Value;
                if (ep == null || rowRoot == null) continue;

                var preview = ServerBrowserSort.GetPreviewFromRow(rowRoot);
                if (preview == null) continue;

                // Mirror vanilla FilterServer + user's saved filters.
                if (preview.ping > cfg.browserMaxPing) continue;
                if (preview.players >= preview.maxPlayers) continue; // skip full per user pref
                if (preview.players <= 0 && !cfg.browserShowEmpty) continue;
                if (preview.isPasswordProtected && !cfg.browserShowLocked) continue;
                if ((preview.clientRequiredModIds?.Length ?? 0) > 0 && !cfg.browserShowModded) continue;

                // Score: closest-to-full not over. Ping is the tiebreaker
                // — we encode it as a small subtract on the ratio so the
                // single Score field captures the full ordering. TR
                // bonus is a small additive on top.
                float ratio = preview.maxPlayers > 0
                    ? (float)preview.players / preview.maxPlayers
                    : 0f;
                float pingPenalty = preview.ping / 10000f; // 100ms -> 0.01
                float trBonus = !string.IsNullOrEmpty(preview.name) && TrTagPattern.IsMatch(preview.name)
                    ? TrBonus
                    : 0f;
                float score = ratio + trBonus - pingPenalty;

                list.Add(new Candidate
                {
                    IpAddress  = ep.ipAddress,
                    Port       = ep.port,
                    Name       = preview.name,
                    Players    = preview.players,
                    MaxPlayers = preview.maxPlayers,
                    Ping       = preview.ping,
                    Score      = score,
                });
            }
        }
        catch (Exception e) { Plugin.LogWarning("[QoL] CollectCandidates failed: " + e.Message); }
        return list;
    }

    // ─────────────────────── browser control + status ─────────────────────

    private static void TriggerRefresh()
    {
        try
        {
            var browser = MonoBehaviourSingleton<UIManager>.Instance?.ServerBrowser;
            browser?.Refresh();
        }
        catch (Exception e) { Plugin.LogWarning("[QoL] quick-join TriggerRefresh failed: " + e.Message); }
    }

    private static bool IsRefreshComplete()
    {
        try
        {
            var browser = MonoBehaviourSingleton<UIManager>.Instance?.ServerBrowser;
            if (browser == null) return true;
            var btn = AccessTools.Field(typeof(UIServerBrowser), "refreshButton")?.GetValue(browser) as Button;
            // Vanilla disables the refresh button while the ping wave is
            // in flight and re-enables it at the end. enabledSelf flips
            // back to true once the wave settles.
            return btn?.enabledSelf ?? false;
        }
        catch { return true; }
    }

    private static void Connect(Candidate best)
    {
        try
        {
            // Pull the saved password (if SavedServerPasswords has one);
            // empty string is fine for unlocked servers.
            string key = best.IpAddress + ":" + best.Port;
            string password = QoLRunner.Instance?.Config?.savedServerPasswords?.TryGetValue(key, out var pw) == true ? pw : "";

            var cm = MonoBehaviourSingleton<ConnectionManager>.Instance;
            if (cm == null)
            {
                Plugin.LogError("[QoL] quick-join: ConnectionManager null, cannot join");
                return;
            }
            Plugin.Log($"[QoL] quick-join: Client_StartClient({best.IpAddress}:{best.Port}, pw={(string.IsNullOrEmpty(password) ? "no" : "yes")})");
            cm.Client_StartClient(best.IpAddress, best.Port, password);
        }
        catch (Exception e) { Plugin.LogError("[QoL] quick-join Connect failed: " + e); }
    }

    // ─────────────────────────── matchmaking-panel overlay ────────────────
    //
    // Uses the same vanilla UIMatchmaking panel ServerSlotQueue uses, but
    // only briefly (≤ QuickJoinTimeoutMs + 1s) and only when the slot
    // queue isn't already showing it. If the queue is active we just
    // log a notice and skip — concurrent panels make for confusing UI.

    private static void SetOverlay(string phase, string subtitle)
    {
        // If the slot queue owns the panel, defer to it. We don't try to
        // stack — the queue is the more important indicator (long-lived).
        if (ServerSlotQueue.IsActive)
        {
            Plugin.Log("[QoL] quick-join: slot queue active, suppressing overlay");
            return;
        }
        if (MatchmakingPanelOverlay.Panel == null) return;
        try
        {
            MatchmakingPanelOverlay.SetIsVisible(true);
            MatchmakingPanelOverlay.SetVisible(true);
            MatchmakingPanelOverlay.SetConnectButton(false);
            // X button visible so the user can bail out of a stuck or
            // slow quick-join — close click is routed back here via
            // Event_OnMatchmakingMatchingClickClose → OnMatchmakingClose.
            MatchmakingPanelOverlay.SetCloseButton(true);
            MatchmakingPanelOverlay.SetTimeVisible(false);
            // Two-tier layout using UI Toolkit rich-text size tags —
            // small all-caps category on top, larger bold detail below.
            // Matches the slot-queue panel's hierarchy without needing
            // the full label-injection machinery for a 5-second overlay.
            string rich =
                $"<size=12><color=#cccccc>{phase}</color></size>\n" +
                $"<size=22><b>{subtitle}</b></size>";
            MatchmakingPanelOverlay.SetPhaseText(rich);
        }
        catch (Exception e) { Plugin.LogWarning("[QoL] quick-join SetOverlay failed: " + e.Message); }
    }

    private static void ClearOverlay()
    {
        if (ServerSlotQueue.IsActive) return;
        if (MatchmakingPanelOverlay.Panel == null) return;
        try
        {
            MatchmakingPanelOverlay.SetVisible(false);
            MatchmakingPanelOverlay.SetPhaseText(string.Empty);
            MatchmakingPanelOverlay.SetConnectButton(false);
            MatchmakingPanelOverlay.SetCloseButton(false);
            MatchmakingPanelOverlay.SetTimeVisible(false);
        }
        catch { }
    }

    // ─────────────────────────── threading helpers ────────────────────────

    private static void MarshalToMain(Action a)
    {
        try { MonoBehaviourSingleton<ThreadManager>.Instance?.Enqueue(a); }
        catch (Exception e) { Plugin.LogWarning("[QoL] quick-join marshal failed: " + e.Message); }
    }

    // Marshal an action and block the calling Task thread until it
    // completes on main. Used for the small handful of poll/collect
    // calls where we need the result synchronously.
    private static void MarshalToMainSync(Action a)
    {
        using var done = new ManualResetEventSlim(false);
        try
        {
            MonoBehaviourSingleton<ThreadManager>.Instance?.Enqueue(() =>
            {
                try { a(); }
                catch (Exception e) { Plugin.LogWarning("[QoL] quick-join marshal-sync inner: " + e.Message); }
                // Guard the Set: if our Wait already timed out (e.g. main
                // thread stalled on a loading screen) the `using` below has
                // disposed `done`, and a late Set() would throw
                // ObjectDisposedException onto the main-thread pump.
                finally { try { done.Set(); } catch { } }
            });
            // Reasonable hard cap so a hung main thread doesn't pin our
            // background task forever.
            done.Wait(QuickJoinTimeoutMs);
        }
        catch (Exception e) { Plugin.LogWarning("[QoL] quick-join marshal-sync failed: " + e.Message); }
    }
}
