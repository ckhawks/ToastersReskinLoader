// Scoreboard polish — three independently-toggled effects on the
// in-game score / period / clock UI:
//
//   * Text shadow      (cfg.enableScoreboardTextShadow)
//     UI Toolkit text-shadow applied to score numbers, period label,
//     and the clock — CSS analogue: 2px offset, 4px blur, 70% black.
//
//   * Milliseconds     (cfg.enableScoreboardMilliseconds)
//     Clock label re-rendered each frame as MM:SS.mmm. Vanilla
//     GameManager.Server_Tick decrements GameState.Tick by 1 each
//     second (clamped ≥ 0), so Tick = remaining seconds and we
//     interpolate the sub-second part locally between server updates.
//     The interpolation window is clamped to 1s past the last received
//     tick so a paused / between-period server doesn't drift our local
//     clock into the past.
//
//   * Clock color      (cfg.enableScoreboardClockColor)
//     Lerps timeLabel.style.color from white at ≥30s to pure red at
//     0s, then alpha-pulses the last 5s at 2 Hz for an urgent flash.
//
// All three flags default true; flipping any of them in the QoL menu
// takes effect on the next frame (text + color), or the next state
// change (text shadow — applied only when its state changes, so we
// don't re-allocate the TextShadow struct every frame).

using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.qol;

internal static class ScoreboardPolish
{
    private static QoLConfig Cfg => QoLRunner.Instance?.Config;

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
        // with the other QoL modules and to give QoLRunner a stable
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
                // the next QoLRunner.Update tick.
                Render();
            }
            catch (Exception e) { Plugin.LogWarning("[QoL] ScoreboardPolish SetTick postfix failed: " + e.Message); }
        }
    }

    // Called every frame from QoLRunner.Update so the ms counter
    // rolls between server ticks and the color/flash animate smoothly.
    public static void Tick()
    {
        if (!_haveTick || _timeLabel == null) return;
        try { Render(); }
        catch (Exception e) { Plugin.LogWarning("[QoL] ScoreboardPolish Tick failed: " + e.Message); }
    }

    // ─────────────────────────── label cache ──────────────────────────────

    private static void EnsureLabels(UIGameState gs)
    {
        if (_timeLabel != null) return;
        _timeLabel      = AccessTools.Field(typeof(UIGameState), "timeLabel")?.GetValue(gs) as Label;
        _blueScoreLabel = AccessTools.Field(typeof(UIGameState), "blueScoreLabel")?.GetValue(gs) as Label;
        _redScoreLabel  = AccessTools.Field(typeof(UIGameState), "redScoreLabel")?.GetValue(gs) as Label;
        _phaseLabel     = AccessTools.Field(typeof(UIGameState), "phaseLabel")?.GetValue(gs) as Label;
    }

    // ─────────────────────────── render loop ──────────────────────────────

    private static void Render()
    {
        var cfg = Cfg;
        if (cfg == null) return;

        // Clamp the local interpolation window so a paused/between-
        // period server doesn't drift the displayed value into the
        // past. 1.0s mirrors Server_Tick's 1s cadence.
        float elapsed   = Mathf.Min(Time.unscaledTime - _lastTickRealTime, 1.0f);
        float effective = Mathf.Max(0f, _lastTick - elapsed);

        UpdateText(cfg.enableScoreboardMilliseconds, effective);
        UpdateColor(cfg.enableScoreboardClockColor, effective);
    }

    private static void UpdateText(bool wantMilliseconds, float effective)
    {
        if (!wantMilliseconds)
        {
            // Vanilla SetTick already wrote "MM:SS" before this postfix
            // runs, so when ms is off we just leave that text alone.
            return;
        }
        TimeSpan ts = TimeSpan.FromSeconds(effective);
        string text;
        if (ts.TotalHours < 1.0)
            text = $"{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
        else
            text = $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
        _timeLabel.text = text;
    }

    private static void UpdateColor(bool wantColor, float effective)
    {
        if (!wantColor)
        {
            // Clear our inline override so vanilla USS takes over the
            // label color again. StyleKeyword.Null on a per-frame
            // write is cheap and idempotent.
            try { _timeLabel.style.color = StyleKeyword.Null; } catch { }
            return;
        }

        Color color;
        if (effective <= 30f && effective >= 0f)
        {
            float t = effective / 30f; // 1 at 30s, 0 at 0s
            color = Color.Lerp(Color.red, Color.white, t);
        }
        else
        {
            color = Color.white;
        }

        // Last 5 seconds: smooth 2 Hz alpha pulse so the clock flashes
        // for visibility without a jarring on/off blink.
        if (effective <= 5f && effective > 0f)
        {
            float pulse = 0.35f + 0.65f * 0.5f * (1f + Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI * 2f));
            color.a *= pulse;
        }

        _timeLabel.style.color = color;
    }
}
