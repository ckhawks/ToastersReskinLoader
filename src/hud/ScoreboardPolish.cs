// Scoreboard polish — two independently-toggled effects on the
// in-game clock label. (The text shadow that used to live here moved to
// the unified UiTextShadow module — cfg.enableUiTextShadow.)
//
//   * Milliseconds     (cfg.enableScoreboardMilliseconds, default OFF)
//     Clock label re-rendered each frame as MM:SS.mmm. Vanilla
//     GameManager.Server_Tick decrements GameState.Tick by 1 each second,
//     holding at 0:00 for a full second before expiring the phase, so the
//     displayed integer is the received Tick and we count the sub-second
//     part DOWN within that second (Tick.999 → Tick.000), matching the
//     vanilla digits like a real scoreboard clock — including the final
//     0:00 second, which counts 00:00.999 → 00:00.000 instead of freezing.
//     The interpolation window is clamped to 1s past the last received
//     tick so a paused / between-period server doesn't drift the fraction.
//
//   * Clock color      (cfg.enableScoreboardClockColor, default ON)
//     timeLabel.style.color ramps over the final 30s: a smooth amber→red
//     lerp from 30s down to 10s, solid red for the last 10s, plus a 2 Hz
//     alpha pulse over the final 5s. Above 30s the clock keeps its
//     vanilla color. Gated to the Warmup / Play phases — in FaceOff /
//     score / replay / intermission the displayed tick is stale, so we
//     leave the clock at its vanilla color instead of flashing a frozen
//     number.
//
// Flipping either flag in the QoL menu takes effect on the next frame.

using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

using ToasterReskinLoader.core;

namespace ToasterReskinLoader.hud;

internal static class ScoreboardPolish
{
    private static SettingsConfig Cfg => Settings.Current;

    // Cached labels (looked up via reflection on the first SetTick).
    private static Label _timeLabel;
    private static Label _blueScoreLabel;
    private static Label _redScoreLabel;
    private static Label _phaseLabel;

    // Server-authoritative tick + the real time we received it. We
    // interpolate locally between ticks for millisecond precision.
    private static int _lastTick;
    private static float _lastTickRealTime;
    private static bool _haveTick;

    public static void Initialize()
    {
        // No event subscription needed — the Harmony postfix below
        // captures SetTick directly. Initialize exists for symmetry
        // with the other QoL modules and to give the tick host a stable
        // hook point.
    }

    [HarmonyPatch(typeof(UIGameState), "SetTick")]
    private static class Patch_SetTick
    {
        [HarmonyPostfix]
        static void Postfix(UIGameState __instance, int tick)
        {
            try
            {
                _lastTick         = tick;
                _lastTickRealTime = Time.unscaledTime;
                _haveTick         = true;
                EnsureLabels(__instance);
                // Render once immediately so the very first SetTick
                // call shows the polished output without waiting for
                // the next TickDriver.Update tick.
                Render();
            }
            catch (Exception e) { Plugin.LogWarning("[QoL] ScoreboardPolish SetTick postfix failed: " + e.Message); }
        }
    }

    // Called every frame from TickDriver.Update so the ms counter
    // rolls between server ticks and the color/flash animate smoothly.
    public static void Tick()
    {
        // Bail if we have no label yet, or the cached one has been
        // detached from its panel (scene reload). The next SetTick on the
        // rebuilt UIGameState re-caches a live label via EnsureLabels.
        if (!_haveTick || _timeLabel == null || _timeLabel.panel == null) return;
        try { Render(); }
        catch (Exception e) { Plugin.LogWarning("[QoL] ScoreboardPolish Tick failed: " + e.Message); }
    }

    // ─────────────────────────── label cache ──────────────────────────────

    private static void EnsureLabels(UIGameState gs)
    {
        // Re-resolve when we have no label yet OR the cached one has been
        // detached (panel == null). A scene reload rebuilds UIGameState and
        // leaves our cached references pointing at dead VisualElements —
        // without the panel check, EnsureLabels would early-out and every
        // ms/color write would silently target the orphaned label for the
        // rest of the session.
        if (_timeLabel != null && _timeLabel.panel != null) return;
        _timeLabel      = AccessTools.Field(typeof(UIGameState), "timeLabel")?.GetValue(gs) as Label;
        _blueScoreLabel = AccessTools.Field(typeof(UIGameState), "blueScoreLabel")?.GetValue(gs) as Label;
        _redScoreLabel  = AccessTools.Field(typeof(UIGameState), "redScoreLabel")?.GetValue(gs) as Label;
        _phaseLabel     = AccessTools.Field(typeof(UIGameState), "phaseLabel")?.GetValue(gs) as Label;
    }

    // ─────────────────────────── render loop ──────────────────────────────

    // True while we currently hold an inline color override on the clock
    // label. Lets us write the StyleKeyword.Null reset exactly ONCE on the
    // off-transition instead of every frame (the label is otherwise dirtied
    // continuously when both effects are off — Tick runs every frame).
    private static bool _colorOverrideActive;

    private static void Render()
    {
        var cfg = Cfg;
        if (cfg == null) return;

        bool wantMs    = cfg.enableScoreboardMilliseconds;
        bool wantColor = cfg.enableScoreboardClockColor;

        // Both effects off: nothing to draw. Clear any leftover color
        // override once, then bail so we're not writing styles every frame.
        if (!wantMs && !wantColor)
        {
            ClearColorOverride();
            return;
        }

        // Fraction remaining within the currently displayed second, counting .999 → .000.
        // elapsed is clamped to Server_Tick's 1s cadence so a paused/between-period server
        // holds the fraction at .000 (matching the frozen vanilla digit) instead of running
        // negative. Adding it to _lastTick keeps the whole-second part equal to the vanilla
        // clock, so the last 0:00 second counts 00:00.999 → 00:00.000 instead of freezing.
        float elapsed   = Mathf.Min(Time.unscaledTime - _lastTickRealTime, 1.0f);
        float fraction  = Mathf.Clamp(1.0f - elapsed, 0f, 0.999f);
        float effective = _lastTick + fraction;

        if (wantMs) UpdateText(effective);
        UpdateColor(wantColor, effective);
    }

    private static void UpdateText(float effective)
    {
        TimeSpan ts = TimeSpan.FromSeconds(effective);
        string text;
        if (ts.TotalHours < 1.0)
            text = $"{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
        else
            text = $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
        _timeLabel.text = text;
    }

    // The clock only counts down meaningfully during Warmup and Play; in
    // FaceOff / BlueScore / RedScore / Replay / Intermission the tick is
    // frozen, so animating its color (or flashing it red) just draws the
    // eye to a stale number. Gate the color effect to the active phases.
    private static bool InActiveClockPhase()
    {
        try
        {
            var gm = NetworkBehaviourSingleton<GameManager>.Instance;
            if (gm == null) return false;
            var phase = gm.Phase;
            return phase == GamePhase.Warmup || phase == GamePhase.Play;
        }
        catch { return false; }
    }

    // Clear our inline color override so vanilla USS owns the label again.
    // Guarded on _colorOverrideActive so it writes only on the transition.
    private static void ClearColorOverride()
    {
        if (!_colorOverrideActive) return;
        try { _timeLabel.style.color = StyleKeyword.Null; } catch { }
        _colorOverrideActive = false;
    }

    // Warning starts amber and ramps to red, so the color shifts smoothly
    // as the clock winds down instead of snapping between flat steps.
    private static readonly Color ClockAmber = new Color(1f, 0.85f, 0.1f);

    // Color ramp over the final 30s: amber → red lerp from 30s down to 10s,
    // then solid red for the last 10s, with a 2 Hz alpha pulse over the
    // final 5s. Above 30s (or off / wrong game phase) the clock keeps its
    // vanilla color.
    private static void UpdateColor(bool wantColor, float effective)
    {
        if (!wantColor || !InActiveClockPhase())
        {
            ClearColorOverride();
            return;
        }

        // Plenty of time left — hand the color back to vanilla.
        if (effective > 30f)
        {
            ClearColorOverride();
            return;
        }

        // 30s → 10s: continuous amber→red lerp (t = 1 at 30s, 0 at 10s).
        // 10s → 0s: hold solid red.
        Color color = effective >= 10f
            ? Color.Lerp(Color.red, ClockAmber, (effective - 10f) / 20f)
            : Color.red;

        // Final 5s: 2 Hz alpha pulse on top of the color so the clock
        // flashes for visibility without a jarring on/off blink.
        if (effective <= 5f && effective > 0f)
        {
            float pulse = 0.35f + 0.65f * 0.5f * (1f + Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI * 2f));
            color.a *= pulse;
        }

        _timeLabel.style.color = color;
        _colorOverrideActive = true;
    }
}
