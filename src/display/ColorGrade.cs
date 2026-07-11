// Color & saturation control — counteracts the flatter, grayer, "foggy" look the
// game picked up when the render pipeline was retuned (HDR color buffer disabled,
// ambient dimmed, SSAO strengthened). Rather than editing the game's baked assets,
// this layers a runtime global Volume on top of the game's own, with a
// ColorAdjustments (saturation / contrast / brightness) + WhiteBalance (warmth)
// override the user can dial to taste.
//
// State is persisted in SettingsConfig (personal/perf, not the shared reskin
// profile) — see the colorGrade* fields there. This class is the applier: it
// reads the config and pushes it to the runtime Volume + pipeline HDR flag.
// Everything URP-typed is wrapped in try/catch so a pipeline-version mismatch
// degrades to a no-op instead of taking the plugin down.

using System;
using System.Reflection;
using ToasterReskinLoader.core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ToasterReskinLoader.display;

public static class ColorGrade
{
    private static SettingsConfig Cfg => Settings.Current;

    // ── Backing objects ──────────────────────────────────────────────────
    private static GameObject _host;
    private static Volume _volume;
    private static VolumeProfile _profile;
    private static ColorAdjustments _colorAdjustments;
    private static WhiteBalance _whiteBalance;

    // Original asset HDR flag, captured the first time we flip it, so the
    // toggle can restore whatever the game shipped with.
    private static bool _hdrCaptured;
    private static bool _originalHDR;

    /// <summary>
    /// Pushes the persisted config to the runtime Volume (creating it on first
    /// use) and, if requested, to the pipeline HDR flag. Safe to call repeatedly
    /// and from UI callbacks on every slider step. If the effect has never been
    /// enabled we skip creating the Volume entirely, so the common (untouched)
    /// case costs nothing.
    /// </summary>
    public static void Apply()
    {
        try
        {
            var cfg = Cfg;
            if (cfg == null) return;

            // Nothing to do and nothing built yet — don't spawn a Volume just to
            // hold a disabled, neutral grade.
            if (_volume == null && !cfg.colorGradeEnabled && !cfg.colorGradeReenableHDR)
            {
                ApplyHDR();
                return;
            }

            EnsureVolume();
            if (_volume == null) return;

            bool on = cfg.colorGradeEnabled;
            _volume.enabled = on;

            SetOverride(_colorAdjustments?.saturation,   on, cfg.colorGradeSaturation);
            SetOverride(_colorAdjustments?.contrast,     on, cfg.colorGradeContrast);
            SetOverride(_colorAdjustments?.postExposure, on, cfg.colorGradeExposure);
            SetOverride(_whiteBalance?.temperature,      on, cfg.colorGradeWarmth);

            ApplyHDR();
        }
        catch (Exception e)
        {
            Plugin.LogError($"ColorGrade.Apply failed: {e.Message}");
        }
    }

    /// <summary>
    /// Resets every knob to neutral in the config, persists, and re-applies
    /// (turns the effect off).
    /// </summary>
    public static void ResetToDefault()
    {
        var cfg = Cfg;
        if (cfg != null)
        {
            var d = new SettingsConfig();
            cfg.colorGradeEnabled = d.colorGradeEnabled;
            cfg.colorGradeSaturation = d.colorGradeSaturation;
            cfg.colorGradeContrast = d.colorGradeContrast;
            cfg.colorGradeExposure = d.colorGradeExposure;
            cfg.colorGradeWarmth = d.colorGradeWarmth;
            cfg.colorGradeReenableHDR = d.colorGradeReenableHDR;
            Settings.Save();
        }
        Apply();
    }

    private static void EnsureVolume()
    {
        if (_volume != null && _host != null) return;

        _host = new GameObject("TRL_ColorGrade");
        UnityEngine.Object.DontDestroyOnLoad(_host);
        _host.hideFlags = HideFlags.HideAndDontSave;

        _profile = ScriptableObject.CreateInstance<VolumeProfile>();
        _profile.hideFlags = HideFlags.HideAndDontSave;

        _colorAdjustments = _profile.Add<ColorAdjustments>(true);
        _whiteBalance     = _profile.Add<WhiteBalance>(true);

        _volume = _host.AddComponent<Volume>();
        _volume.isGlobal = true;
        // Sit above the game's global volume (Default Volume Profile) so our
        // grading wins the blend.
        _volume.priority = 1000f;
        _volume.weight = 1f;
        _volume.profile = _profile;
    }

    // Toggles a single VolumeParameter's override on/off and sets its value.
    // When the feature is disabled we clear overrideState so the game's own
    // grading passes through untouched.
    private static void SetOverride(VolumeParameter<float> param, bool active, float value)
    {
        if (param == null) return;
        param.overrideState = active;
        param.value = value;
    }

    private static void ApplyHDR()
    {
        try
        {
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset == null) return;

            var field = typeof(UniversalRenderPipelineAsset)
                .GetField("m_SupportsHDR", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) return;

            if (!_hdrCaptured)
            {
                _originalHDR = (bool)field.GetValue(asset);
                _hdrCaptured = true;
            }

            bool target = (Cfg?.colorGradeReenableHDR ?? false) ? true : _originalHDR;
            if ((bool)field.GetValue(asset) != target)
            {
                field.SetValue(asset, target);
                // Cameras gate HDR on their own allowHDR flag too; make sure the
                // active ones can actually use the re-enabled buffer.
                if (target)
                {
                    foreach (var cam in Camera.allCameras)
                        if (cam != null) cam.allowHDR = true;
                }
            }
        }
        catch (Exception e)
        {
            Plugin.LogError($"ColorGrade.ApplyHDR failed: {e.Message}");
        }
    }
}
