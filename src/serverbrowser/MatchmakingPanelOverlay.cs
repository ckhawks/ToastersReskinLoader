// MatchmakingPanelOverlay — shared reflection shim over the vanilla
// UIMatchmaking "Matching" panel.
//
// Both the server slot-queue (ServerSlotQueue) and the title-screen
// Quick Join overlay (MainMenuButtons) hijack this same vanilla panel so
// they get its styling for free. They used to each cache an identical set
// of UIMatchmaking MethodInfos and reimplement the panel lookup, so a
// change to one was easy to forget in the other. This centralizes the
// MethodInfo cache + panel/container lookup + the low-level setters.
//
// Every setter is self-contained and best-effort: it resolves the live
// panel itself and swallows reflection failures, mirroring the old
// "*Safe" wrappers. Callers layer their own presentation (label
// injection, rich-text phase strings) on top.
//
// This file also owns the *arbitration* policy for the panel, because
// three things now want it: vanilla matchmaking (ranked / party queue),
// ServerSlotQueue, and MainMenuButtons' Quick Join. The rule is simple:
//
//   Vanilla matchmaking always wins. It has a hard 60s join deadline and
//   a match already allocated server-side; the two mod overlays are
//   conveniences the user can trivially restart.
//
// The Harmony patches at the bottom enforce that rule for every claimant
// at once, so a future overlay only has to report itself in
// IsClaimedByMod rather than re-derive the whole interaction.

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

using ToasterReskinLoader.core;

namespace ToasterReskinLoader.serverbrowser;

internal static class MatchmakingPanelOverlay
{
    private static readonly Type Type = AccessTools.TypeByName("UIMatchmaking");
    private static readonly Type ControllerType = AccessTools.TypeByName("UIMatchmakingController");
    private static readonly MethodInfo _updateMatching = ControllerType?.GetMethod("UpdateMatching",
        BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo _setVisible    = Type?.GetMethod("SetMatchingVisibility");
    private static readonly MethodInfo _setPhaseText  = Type?.GetMethod("SetMatchingPhaseText");
    private static readonly MethodInfo _setConnectVis = Type?.GetMethod("SetMatchingConnectButtonVisibility");
    private static readonly MethodInfo _setCloseVis   = Type?.GetMethod("SetMatchingCloseButtonVisibility");
    private static readonly MethodInfo _setTimeVis    = Type?.GetMethod("SetMatchingTimeVisibility");
    private static readonly MethodInfo _setTimeText   = Type?.GetMethod("SetMatchingTimeText");
    // B1231 added a START MATCHMAKING button to the matchmaking view. Unlike
    // every other element here it is queried off the ROOT view rather than the
    // "Matching" container — UIMatchmaking.Initialize does
    // View.Q("StartMatchmakingButton") where PhaseLabel / TimeLabel /
    // ConnectButton / CloseIconButtonContainer all come off `matching`. It is
    // therefore a *sibling* of the panel, and SetVisible(false) does not hide
    // it; it has its own display toggle, and the only things that ever drive
    // that are UIMatchmakingController.Start and .UpdateMatching — the latter
    // being exactly what Patch_UpdateMatching suppresses while an overlay owns
    // the panel. Null on builds predating the button, where the setter no-ops.
    private static readonly MethodInfo _setStartVis   = Type?.GetMethod("SetMatchingStartMatchmakingButtonVisibility");
    private static readonly PropertyInfo _isVisible   = Type?.GetProperty("IsVisible");
    private static readonly FieldInfo _matchingField  = Type?.GetField("matching",
        BindingFlags.Instance | BindingFlags.NonPublic);

    // The live UIMatchmaking instance off UIManager.Matchmaking, or null.
    internal static object Panel
    {
        get
        {
            var ui = MonoBehaviourSingleton<UIManager>.Instance;
            if (ui == null) return null;
            return ui.GetType().GetField("Matchmaking",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(ui);
        }
    }

    internal static void SetIsVisible(bool v)     { var p = Panel; if (p == null) return; try { _isVisible?.SetValue(p, v); } catch { } }
    internal static void SetVisible(bool v)       { var p = Panel; if (p == null) return; try { _setVisible?.Invoke(p, new object[] { v }); } catch { } }
    internal static void SetPhaseText(string t)   { var p = Panel; if (p == null) return; try { _setPhaseText?.Invoke(p, new object[] { t }); } catch { } }
    internal static void SetConnectButton(bool v) { var p = Panel; if (p == null) return; try { _setConnectVis?.Invoke(p, new object[] { v }); } catch { } }
    internal static void SetCloseButton(bool v)   { var p = Panel; if (p == null) return; try { _setCloseVis?.Invoke(p, new object[] { v }); } catch { } }
    internal static void SetTimeVisible(bool v)   { var p = Panel; if (p == null) return; try { _setTimeVis?.Invoke(p, new object[] { v }); } catch { } }
    internal static void SetTimeText(int seconds) { var p = Panel; if (p == null) return; try { _setTimeText?.Invoke(p, new object[] { seconds }); } catch { } }
    internal static void SetStartMatchmakingButton(bool v) { var p = Panel; if (p == null) return; try { _setStartVis?.Invoke(p, new object[] { v }); } catch { } }

    // Set for the duration of a RepaintVanilla so Patch_UpdateMatching lets that
    // one call through. A repaint is an explicit hand-back — the caller is
    // asking vanilla to repossess the panel — so suppressing it on the grounds
    // that a mod still claims the panel is exactly backwards, and it deadlocks
    // the release: the claimant flag is what we are trying to get out from
    // under. Quick Join hits this directly, since QuickJoinInFlight is not
    // cleared until its background task unwinds, well after ClearOverlay runs.
    private static bool _repainting;

    // The inner "matching" VisualElement (the row that holds PhaseLabel),
    // or null. Used by the slot-queue's label-injection.
    internal static VisualElement GetMatchingContainer()
    {
        var p = Panel;
        if (p == null || _matchingField == null) return null;
        return _matchingField.GetValue(p) as VisualElement;
    }

    // Force vanilla to repaint the panel from its own matchmaking state.
    //
    // UIMatchmakingController.UpdateMatching is private and only ever runs off
    // the group / match / connection change events. Once one of those has
    // already fired — e.g. a ranked match was found while an overlay owned the
    // panel — nothing re-runs it on its own, so whoever releases the panel has
    // to poke it directly or it just keeps showing whatever the overlay left
    // behind (or nothing at all) until the next unrelated state change.
    internal static void RepaintVanilla()
    {
        if (_updateMatching == null) return;
        try
        {
            // UIMatchmakingController lives on the same GameObject as
            // UIMatchmaking (its Awake does GetComponent<UIMatchmaking>()), so
            // we can reach it from the panel instead of scanning the scene.
            if (Panel is not Component panel) return;
            var controller = panel.GetComponent(ControllerType);
            if (controller == null) return;
            _repainting = true;
            try { _updateMatching.Invoke(controller, null); }
            finally { _repainting = false; }
        }
        catch { }
    }

    // ─────────────────────────── arbitration ──────────────────────────────

    // True while the vanilla matchmaking flow owns the panel — i.e. exactly
    // the two states UIMatchmakingController.UpdateMatching paints it for:
    //   * GroupData != null                    → "LOOKING FOR A MATCH..."
    //   * MatchData != null && not yet connected to that match
    //                                          → "MATCH FOUND!" / "MATCH READY!"
    // Once the player is actually connected to the match endpoint vanilla
    // hides the panel again, so that state is not a conflict — MatchData stays
    // populated for the whole match and gating on it alone would lock the mod
    // overlays out for as long as the player is in a ranked game.
    internal static bool IsVanillaMatchmakingActive()
    {
        try
        {
            var state = BackendManager.PlayerState;
            if (state.GroupData != null) return true;
            return state.MatchData != null && !BackendUtils.IsConnectedToMatchEndPoint();
        }
        catch (Exception e)
        {
            // Can't tell → treat the panel as free. Failing open keeps the mod
            // overlays working rather than silently disabling them if the
            // backend state shape changes in a future Puck build.
            Plugin.LogDebug("[QoL] matchmaking-panel: state probe failed: " + e.Message);
            return false;
        }
    }

    // True while one of our overlays is the thing actually on screen. Both
    // claimants are in this namespace and already reference each other, so a
    // direct check beats a registration list for two entries.
    //
    // The !IsVanillaMatchmakingActive() term applies the arbitration rule up
    // front so every consumer inherits it. It matters because a claimant's
    // flag outlives the hand-over: both overlays stand down asynchronously
    // (Quick Join's flag isn't cleared until its background task unwinds),
    // so for a short window after matchmaking takes the panel a mod overlay
    // still reports itself as a claimant while vanilla is what's painted.
    // Answering "yes, ours" there would make Patch_BackendMatchingClose
    // swallow the X the user just pressed to cancel their own matchmaking.
    internal static bool IsClaimedByMod()
        => (ServerSlotQueue.IsActive || MainMenuButtons.IsQuickJoinActive)
           && !IsVanillaMatchmakingActive();

    // ───────────────────────── ownership patches ──────────────────────────

    // UIMatchmakingController.UpdateMatching runs on player / match /
    // connection state changes and repaints the panel from vanilla state.
    // While one of our overlays is up, suppress it so it can't overwrite our
    // phase text, hide our close button, or (via its else branch) blank the
    // panel outright on an unrelated connection-state change.
    //
    // IsClaimedByMod already carries the exception that is the whole point of
    // the arbitration: if matchmaking itself has something to show, vanilla
    // always gets to paint. That has to hold even before the owning overlay
    // has processed its own stand-down — vanilla's controller registers its
    // listeners from Awake on a scene that is already loaded when the plugin
    // enables, so EventManager (which dispatches in registration order) runs
    // UpdateMatching BEFORE our handlers on the very event that hands the
    // panel over. Suppressing it there would blank the panel until some
    // later, unrelated state change — which is exactly the "restart the game
    // to get the Connect button back" bug.
    [HarmonyPatch]
    private static class Patch_UpdateMatching
    {
        static MethodBase TargetMethod()
            => AccessTools.Method(AccessTools.TypeByName("UIMatchmakingController"), "UpdateMatching");

        [HarmonyPrefix]
        static bool Prefix() => _repainting || !IsClaimedByMod();
    }

    // The X on the matchmaking panel raises one vanilla event that
    // BackendManagerController turns into a playerStopMatchmakingRequest.
    // While the panel is showing one of OUR overlays that X means "cancel the
    // overlay" and must not reach into the player's matchmaking state.
    //
    // We can't gate the suppression on IsClaimedByMod at the point the vanilla
    // handler runs: EventManager dispatches listeners synchronously in
    // registration order, and the owning overlay's own close handler may have
    // already cleared its active flag by then. Latching the decision around
    // the click itself makes it independent of listener order.
    private static bool _closeClickIsOurs;

    [HarmonyPatch]
    private static class Patch_MatchingCloseClick
    {
        // TargetMethods (not TargetMethod) so a missing symbol yields an empty
        // patch set instead of throwing — Plugin.OnEnable rethrows on PatchAll
        // failure, and these two are auxiliary: losing them should not take
        // the whole mod down with them.
        static IEnumerable<MethodBase> TargetMethods()
        {
            var m = AccessTools.Method(AccessTools.TypeByName("UIMatchmaking"), "OnClickMatchingClose");
            if (m != null) yield return m;
        }

        [HarmonyPrefix]
        static void Prefix() => _closeClickIsOurs = IsClaimedByMod();

        // Finalizer rather than a postfix: a postfix is skipped when the
        // original throws, and a latch stuck at true would swallow the next
        // genuine stop-matchmaking request. A void finalizer always runs and
        // leaves the original exception untouched.
        [HarmonyFinalizer]
        static void Finalizer() => _closeClickIsOurs = false;
    }

    [HarmonyPatch]
    private static class Patch_BackendMatchingClose
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            var m = AccessTools.Method(AccessTools.TypeByName("BackendManagerController"),
                                       "Event_OnMatchmakingMatchingClickClose");
            if (m != null) yield return m;
        }

        [HarmonyPrefix]
        static bool Prefix() => !_closeClickIsOurs;
    }
}
