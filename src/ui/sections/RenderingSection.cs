// Rendering — surface glossiness control. Part of the "Tweaks" sidebar group (Game).
//
// A personal/performance setting stored in SettingsConfig, not the reskin profile.
// (Shadow quality is native in B1117 via the video-settings Shadow Quality option.)

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ToasterReskinLoader.core;
using ToasterReskinLoader.display; // GlossSwapper

namespace ToasterReskinLoader.ui.sections;

public static class RenderingSection
{
    private static SettingsConfig Cfg => Settings.Current;
    private static void Save() => Settings.Save();

    public static void CreateSection(VisualElement root)
    {
        var cfg = SettingsUI.RequireConfig(root,
            "Surface glossiness. Personal/performance setting — not part of a reskin pack.");
        if (cfg == null) return;

        SettingsUI.Header(root, "Glossiness");
        BuildGloss(root);

        SettingsUI.Separator(root);
        SettingsUI.Header(root, "Color & Saturation");
        BuildColorGrade(root);
    }

    // ── Color & Saturation ───────────────────────────────────────────────
    // Runtime-only knobs (ColorGrade static state) — not stored in the profile
    // or settings config. Counteracts the flatter/grayer look the game took on
    // when its render pipeline was retuned (HDR buffer disabled, ambient dimmed).

    private static void BuildColorGrade(VisualElement root)
    {
        var cfg = Cfg;
        SettingsUI.Note(root,
            "A recent game update disabled HDR and dimmed the arena lighting, which makes colors look washed "
            + "out and gray. These sliders layer a color grade on top to bring saturation and contrast back.");

        var dependentControls = new List<VisualElement>();

        var enableRow = UITools.CreateConfigurationRow();
        enableRow.Add(UITools.CreateConfigurationLabel("Enable Color Correction"));
        var enableToggle = UITools.CreateConfigurationCheckbox(cfg.colorGradeEnabled);
        enableToggle.value = cfg.colorGradeEnabled;
        enableToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
        {
            cfg.colorGradeEnabled = evt.newValue;
            Save();
            ColorGrade.Apply();
            UITools.UpdateDependentControlsState(dependentControls, evt.newValue);
        });
        enableRow.Add(enableToggle);
        root.Add(enableRow);

        dependentControls.Add(AddGradeSlider(root, "Saturation", -100f, 100f,
            () => cfg.colorGradeSaturation, v => cfg.colorGradeSaturation = v));
        dependentControls.Add(AddGradeSlider(root, "Contrast", -100f, 100f,
            () => cfg.colorGradeContrast, v => cfg.colorGradeContrast = v));
        dependentControls.Add(AddGradeSlider(root, "Brightness", -2f, 2f,
            () => cfg.colorGradeExposure, v => cfg.colorGradeExposure = v));
        dependentControls.Add(AddGradeSlider(root, "Warmth", -100f, 100f,
            () => cfg.colorGradeWarmth, v => cfg.colorGradeWarmth = v));

        var presetRow = new VisualElement();
        presetRow.style.flexDirection = FlexDirection.Row;
        presetRow.style.marginTop = 8;
        presetRow.style.marginBottom = 4;
        root.Add(presetRow);
        AddGradePreset(presetRow, root, "Neutral", 0f, 0f, 0f, 0f);
        AddGradePreset(presetRow, root, "Vivid", 30f, 12f, 0.1f, 0f);
        AddGradePreset(presetRow, root, "Punchy", 45f, 25f, 0.15f, 5f);
        AddGradePreset(presetRow, root, "Warm", 20f, 8f, 0.1f, 20f);
        dependentControls.Add(presetRow);

        SettingsUI.Separator(root);

        var hdrRow = UITools.CreateConfigurationRow();
        hdrRow.Add(UITools.CreateConfigurationLabel("Re-enable HDR (experimental)"));
        var hdrToggle = UITools.CreateConfigurationCheckbox(cfg.colorGradeReenableHDR);
        hdrToggle.value = cfg.colorGradeReenableHDR;
        hdrToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
        {
            cfg.colorGradeReenableHDR = evt.newValue;
            Save();
            ColorGrade.Apply();
        });
        hdrRow.Add(hdrToggle);
        root.Add(hdrRow);
        SettingsUI.Note(root,
            "Turns the HDR color buffer back on — the root cause of the washed-out highlights. This is the most "
            + "faithful fix but reallocates the render buffer and may cost a little performance. Leave off if you "
            + "notice issues; the sliders above work without it.");

        root.Add(SettingsUI.RebuildButton(root, "Reset color to default",
            () => ColorGrade.ResetToDefault(), CreateSection));

        UITools.UpdateDependentControlsState(dependentControls, cfg.colorGradeEnabled);
    }

    private static VisualElement AddGradeSlider(VisualElement root, string label, float min, float max,
        Func<float> getter, Action<float> setter)
    {
        var row = UITools.CreateConfigurationRow();
        row.Add(UITools.CreateConfigurationLabel(label));
        var slider = UITools.CreateConfigurationSlider(min, max, getter(), 300);
        slider.RegisterCallback<ChangeEvent<float>>(evt =>
        {
            setter(evt.newValue);
            ColorGrade.Apply();
        });
        slider.RegisterCallback<PointerUpEvent>(evt => Save());
        row.Add(slider);
        root.Add(row);
        return row;
    }

    private static void AddGradePreset(VisualElement parent, VisualElement contentRoot, string name,
        float saturation, float contrast, float exposure, float warmth)
    {
        var btn = new Button { text = name };
        btn.style.flexGrow = 1;
        btn.style.height = 28;
        btn.style.marginRight = 4;
        btn.style.paddingLeft = 0;
        btn.style.paddingRight = 0;
        btn.style.paddingTop = 0;
        btn.style.paddingBottom = 0;
        btn.style.fontSize = 13;
        btn.style.unityTextAlign = TextAnchor.MiddleCenter;
        btn.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
        btn.style.color = Color.white;
        UITools.AddHoverEffectsForButton(btn);
        btn.RegisterCallback<ClickEvent>(evt =>
        {
            var cfg = Cfg;
            if (cfg != null)
            {
                cfg.colorGradeEnabled = true;
                cfg.colorGradeSaturation = saturation;
                cfg.colorGradeContrast = contrast;
                cfg.colorGradeExposure = exposure;
                cfg.colorGradeWarmth = warmth;
                Save();
            }
            ColorGrade.Apply();

            var title = contentRoot.childCount > 0 ? contentRoot[0] : null;
            contentRoot.Clear();
            if (title != null) contentRoot.Add(title);
            CreateSection(contentRoot);
        });
        parent.Add(btn);
    }

    // ── Glossiness ───────────────────────────────────────────────────────

    private static void ResetGlossToDefault()
    {
        var d = new SettingsConfig();
        var c = Cfg;
        if (c == null) return;
        c.glossRemoverEnabled = d.glossRemoverEnabled;
        c.glossSmoothness = d.glossSmoothness;
        c.glossAffectSticks = d.glossAffectSticks;
        c.glossAffectPlayers = d.glossAffectPlayers;
        c.glossAffectPucks = d.glossAffectPucks;
        c.reflectionReduceEnabled = d.reflectionReduceEnabled;
        c.reflectionIntensity = d.reflectionIntensity;
        Save();
        GlossSwapper.RestoreAll();
        GlossSwapper.ApplyReflectionIntensity();
        if (c.glossRemoverEnabled) GlossSwapper.Scan();
    }

    private static void BuildGloss(VisualElement root)
    {
        var cfg = Cfg;
        SettingsUI.Note(root,
            "Adjusts the glossy shine on sticks, players, and pucks. "
            + "Move the slider to 0 to make surfaces fully matte (no reflections), or to 1 to keep the original gloss.");

        var dependentControls = new List<VisualElement>();

        var enableRow = UITools.CreateConfigurationRow();
        enableRow.Add(UITools.CreateConfigurationLabel("Enable Glossiness Control"));
        var enableToggle = UITools.CreateConfigurationCheckbox(cfg.glossRemoverEnabled);
        enableToggle.value = cfg.glossRemoverEnabled;
        enableToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
        {
            cfg.glossRemoverEnabled = evt.newValue;
            Save();
            if (evt.newValue) GlossSwapper.Scan();
            else GlossSwapper.RestoreAll();
            UITools.UpdateDependentControlsState(dependentControls, evt.newValue);
        });
        enableRow.Add(enableToggle);
        root.Add(enableRow);

        var smoothRow = UITools.CreateConfigurationRow();
        smoothRow.Add(UITools.CreateConfigurationLabel("Gloss amount"));
        var smoothSlider = UITools.CreateConfigurationSlider(0f, 1f, cfg.glossSmoothness, 300);
        smoothSlider.RegisterCallback<ChangeEvent<float>>(evt =>
        {
            cfg.glossSmoothness = evt.newValue;
            GlossSwapper.ReapplyAll();
        });
        smoothSlider.RegisterCallback<PointerUpEvent>(evt => Save());
        smoothRow.Add(smoothSlider);
        root.Add(smoothRow);
        dependentControls.Add(smoothRow);

        var presetRow = new VisualElement();
        presetRow.style.flexDirection = FlexDirection.Row;
        presetRow.style.marginTop = 8;
        presetRow.style.marginBottom = 4;
        root.Add(presetRow);
        AddPreset(presetRow, root, "Matte", 0f);
        AddPreset(presetRow, root, "Low", 0.15f);
        AddPreset(presetRow, root, "Medium", 0.4f);
        AddPreset(presetRow, root, "Original", 0.5f);
        dependentControls.Add(presetRow);

        SettingsUI.Separator(root);
        SettingsUI.Header(root, "Apply To");

        AddCategoryToggle(root, dependentControls, "Sticks",
            () => cfg.glossAffectSticks, v => cfg.glossAffectSticks = v);
        AddCategoryToggle(root, dependentControls, "Players (body, helmet, jersey, etc.)",
            () => cfg.glossAffectPlayers, v => cfg.glossAffectPlayers = v);
        AddCategoryToggle(root, dependentControls, "Pucks",
            () => cfg.glossAffectPucks, v => cfg.glossAffectPucks = v);

        BuildEnvironmentReflections(root);

        root.Add(SettingsUI.RebuildButton(root, "Reset glossiness to default",
            () => { ResetGlossToDefault(); GlossSwapper.ReapplyAll(); }, CreateSection));

        UITools.UpdateDependentControlsState(dependentControls, cfg.glossRemoverEnabled);
    }

    // ── Environment Reflections ──────────────────────────────────────────
    // Global reflection-probe scale (RenderSettings.reflectionIntensity). Independent of
    // the gloss remover above — it's the only reliable runtime lever to drop the static
    // rink cubemap on URP Lit surfaces in a built game. Scene-wide, so opt-in.

    private static void BuildEnvironmentReflections(VisualElement root)
    {
        var cfg = Cfg;

        SettingsUI.Separator(root);
        SettingsUI.Header(root, "Environment Reflections");
        SettingsUI.Note(root,
            "The game maps a reflection of the rink (ice, boards, lights) onto glossy surfaces. It's baked and "
            + "doesn't move with the world, so it looks pasted-on and slides oddly across a spinning puck or a "
            + "stick. This dials that reflection down across the whole scene while keeping the direct shine from "
            + "the arena lights. Note: it's scene-wide, so the ice and boards lose some of their own reflectivity "
            + "too.");

        var dependentControls = new List<VisualElement>();

        var enableRow = UITools.CreateConfigurationRow();
        enableRow.Add(UITools.CreateConfigurationLabel("Reduce Environment Reflections"));
        var enableToggle = UITools.CreateConfigurationCheckbox(cfg.reflectionReduceEnabled);
        enableToggle.value = cfg.reflectionReduceEnabled;
        enableToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
        {
            cfg.reflectionReduceEnabled = evt.newValue;
            Save();
            GlossSwapper.ApplyReflectionIntensity();
            UITools.UpdateDependentControlsState(dependentControls, evt.newValue);
        });
        enableRow.Add(enableToggle);
        root.Add(enableRow);

        var amountRow = UITools.CreateConfigurationRow();
        amountRow.Add(UITools.CreateConfigurationLabel("Reflection amount"));
        var amountSlider = UITools.CreateConfigurationSlider(0f, 1f, cfg.reflectionIntensity, 300);
        amountSlider.RegisterCallback<ChangeEvent<float>>(evt =>
        {
            cfg.reflectionIntensity = evt.newValue;
            GlossSwapper.ApplyReflectionIntensity();
        });
        amountSlider.RegisterCallback<PointerUpEvent>(evt => Save());
        amountRow.Add(amountSlider);
        root.Add(amountRow);
        dependentControls.Add(amountRow);

        UITools.UpdateDependentControlsState(dependentControls, cfg.reflectionReduceEnabled);
    }

    private static void AddCategoryToggle(VisualElement container, List<VisualElement> dependents,
        string label, Func<bool> getter, Action<bool> setter)
    {
        var row = UITools.CreateConfigurationRow();
        row.Add(UITools.CreateConfigurationLabel(label));
        var toggle = UITools.CreateConfigurationCheckbox(getter());
        toggle.RegisterCallback<ChangeEvent<bool>>(evt =>
        {
            setter(evt.newValue);
            Save();
            GlossSwapper.ReapplyAll();
        });
        row.Add(toggle);
        container.Add(row);
        dependents.Add(row);
    }

    private static void AddPreset(VisualElement parent, VisualElement contentRoot, string name, float value)
    {
        var btn = new Button { text = name };
        btn.style.flexGrow = 1;
        btn.style.height = 28;
        btn.style.marginRight = 4;
        btn.style.paddingLeft = 0;
        btn.style.paddingRight = 0;
        btn.style.paddingTop = 0;
        btn.style.paddingBottom = 0;
        btn.style.fontSize = 13;
        btn.style.unityTextAlign = TextAnchor.MiddleCenter;
        btn.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
        btn.style.color = Color.white;
        UITools.AddHoverEffectsForButton(btn);
        btn.RegisterCallback<ClickEvent>(evt =>
        {
            var cfg = Cfg;
            if (cfg != null) cfg.glossSmoothness = value;
            Save();
            GlossSwapper.ReapplyAll();

            var title = contentRoot.childCount > 0 ? contentRoot[0] : null;
            contentRoot.Clear();
            if (title != null) contentRoot.Add(title);
            CreateSection(contentRoot);
        });
        parent.Add(btn);
    }
}
