using System;
using HarmonyLib;
using UnityEngine;

namespace ToasterReskinLoader.swappers;

/// <summary>
/// Throttles the local player's stamina bar updates to the minimap refresh rate,
/// keeping perf-sensitive UI bars in sync with the user's chosen tick rate.
/// </summary>
public static class StaminaSwapper
{
    private static float _lastApplyTime = -1f;
    private static float _pendingValue;
    private static bool _hasPending;
    private static bool _bypass;
    private static UIHUD _hud;

    private static float CurrentInterval()
    {
        int rate = qol.QoLRunner.Instance?.Config?.minimapRefreshRate ?? 60;
        if (rate <= 0) rate = 60;
        return 1f / rate;
    }

    [HarmonyPatch(typeof(UIHUD), nameof(UIHUD.SetStamina))]
    public static class SetStaminaPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(UIHUD __instance, float value)
        {
            if (_bypass) return true;

            _hud = __instance;
            float now = Time.unscaledTime;
            if (now - _lastApplyTime >= CurrentInterval())
            {
                _lastApplyTime = now;
                _hasPending = false;
                return true;
            }

            _pendingValue = value;
            _hasPending = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(UIMinimap), "Update")]
    public static class UIMinimapUpdateFlushPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (!_hasPending || _hud == null) return;
            if (Time.unscaledTime - _lastApplyTime < CurrentInterval()) return;

            float v = _pendingValue;
            _hasPending = false;
            _lastApplyTime = Time.unscaledTime;

            _bypass = true;
            try { _hud.SetStamina(v); }
            catch (Exception e) { Plugin.LogDebug($"StaminaSwapper flush error: {e.Message}"); }
            finally { _bypass = false; }
        }
    }
}
