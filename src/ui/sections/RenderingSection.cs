// Rendering — shadow quality and glossiness. Merges the former Shadows and
// Glossiness pages into one. Part of the "Tweaks" sidebar group (Game).
//
// Both are personal/performance settings stored in SettingsConfig, not the
// reskin profile.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using ToasterReskinLoader.core;
using ToasterReskinLoader.display; // CrispyShadowsSwapper, GlossSwapper

namespace ToasterReskinLoader.ui.sections;

public static class RenderingSection
{
    private static SettingsConfig Cfg => SettingsRunner.Instance?.Config;
    private static void Save() => SettingsRunner.Instance?.SaveAndRefresh();

    public static void CreateSection(VisualElement root)
    {
        var cfg = SettingsUI.RequireConfig(root,
            "Shadow quality and surface glossiness. Personal/performance settings — not part of a reskin pack.");
        if (cfg == null) return;

        SettingsUI.Header(root, "Shadows");
        BuildShadows(root);

        SettingsUI.Separator(root);

        SettingsUI.Header(root, "Glossiness");
        BuildGloss(root);
    }

    // ── Shadows ──────────────────────────────────────────────────────────

    private static readonly Dictionary<string, int> ResolutionOptions = new Dictionary<string, int>
    {
        { "256 (Very Low)", 256 },
        { "512 (Low)", 512 },
        { "1024 (Medium)", 1024 },
        { "2048 (High)", 2048 },
        { "4096 (Very High)", 4096 },
        { "8192 (Ultra)", 8192 },
    };

    private static readonly Dictionary<string, int> CascadeOptions = new Dictionary<string, int>
    {
        { "1 Cascade", 1 },
        { "2 Cascades", 2 },
        { "3 Cascades", 3 },
        { "4 Cascades", 4 },
    };

    private static void ResetShadowsToDefault()
    {
        var d = new SettingsConfig();
        var c = Cfg;
        if (c == null) return;
        c.crispyShadowsEnabled = d.crispyShadowsEnabled;
        c.shadowResolution = d.shadowResolution;
        c.shadowDistance = d.shadowDistance;
        c.shadowCascadeCount = d.shadowCascadeCount;
        c.shadowSoftShadows = d.shadowSoftShadows;
        Save();
        CrispyShadowsSwapper.Apply();
    }

    private static void BuildShadows(VisualElement root)
    {
        var cfg = Cfg;
        SettingsUI.Note(root,
            "Crispy Shadows overrides the game's shadow map settings for sharper, higher-quality shadows.");

        var dependentControls = new List<VisualElement>();

        var enableRow = UITools.CreateConfigurationRow();
        enableRow.Add(UITools.CreateConfigurationLabel("Enable Crispy Shadows"));
        var enableToggle = UITools.CreateConfigurationCheckbox(cfg.crispyShadowsEnabled);
        enableToggle.value = cfg.crispyShadowsEnabled;
        enableToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
        {
            cfg.crispyShadowsEnabled = evt.newValue;
            Save();
            CrispyShadowsSwapper.Apply();
            UITools.UpdateDependentControlsState(dependentControls, evt.newValue);
        });
        enableRow.Add(enableToggle);
        root.Add(enableRow);

        // Resolution dropdown + VRAM estimate
        var resolutionRow = UITools.CreateConfigurationRow();
        resolutionRow.Add(UITools.CreateConfigurationLabel("Shadow Map Resolution"));
        var resolutionChoices = ResolutionOptions.Keys.ToList();
        string currentResolution = ResolutionOptions.FirstOrDefault(kv => kv.Value == cfg.shadowResolution).Key
            ?? "4096 (Very High)";
        var resolutionDropdown = UITools.CreateStringDropdownField(resolutionChoices, currentResolution);

        var vramLabel = UITools.CreateConfigurationLabel(
            $"Estimated VRAM: {CrispyShadowsSwapper.EstimateVRAM(cfg.shadowResolution)}");
        vramLabel.style.marginTop = 2;
        vramLabel.style.marginBottom = 8;
        vramLabel.style.fontSize = 13;
        vramLabel.style.color = new Color(0.7f, 0.7f, 0.7f);

        resolutionDropdown.RegisterCallback<ChangeEvent<string>>(evt =>
        {
            if (ResolutionOptions.TryGetValue(evt.newValue, out int resolution))
            {
                cfg.shadowResolution = resolution;
                Save();
                CrispyShadowsSwapper.Apply();
                vramLabel.text = $"Estimated VRAM: {CrispyShadowsSwapper.EstimateVRAM(resolution)}";
            }
        });
        resolutionRow.Add(resolutionDropdown);
        root.Add(resolutionRow);
        dependentControls.Add(resolutionRow);
        root.Add(vramLabel);
        dependentControls.Add(vramLabel);

        // Distance slider — applies live, saves on release
        var distanceRow = UITools.CreateConfigurationRow();
        distanceRow.Add(UITools.CreateConfigurationLabel("Shadow Distance"));
        var distanceSlider = UITools.CreateConfigurationSlider(10, 300, cfg.shadowDistance, 300);
        distanceSlider.RegisterCallback<ChangeEvent<float>>(evt =>
        {
            cfg.shadowDistance = evt.newValue;
            CrispyShadowsSwapper.Apply();
        });
        distanceSlider.RegisterCallback<PointerUpEvent>(evt => Save());
        distanceRow.Add(distanceSlider);
        root.Add(distanceRow);
        dependentControls.Add(distanceRow);

        // Cascades dropdown
        var cascadeRow = UITools.CreateConfigurationRow();
        cascadeRow.Add(UITools.CreateConfigurationLabel("Shadow Cascades"));
        var cascadeChoices = CascadeOptions.Keys.ToList();
        string currentCascade = CascadeOptions.FirstOrDefault(kv => kv.Value == cfg.shadowCascadeCount).Key
            ?? "4 Cascades";
        var cascadeDropdown = UITools.CreateStringDropdownField(cascadeChoices, currentCascade);
        cascadeDropdown.RegisterCallback<ChangeEvent<string>>(evt =>
        {
            if (CascadeOptions.TryGetValue(evt.newValue, out int cascades))
            {
                cfg.shadowCascadeCount = cascades;
                Save();
                CrispyShadowsSwapper.Apply();
            }
        });
        cascadeRow.Add(cascadeDropdown);
        root.Add(cascadeRow);
        dependentControls.Add(cascadeRow);

        // Soft shadows
        var softRow = UITools.CreateConfigurationRow();
        softRow.Add(UITools.CreateConfigurationLabel("Soft Shadows"));
        var softToggle = UITools.CreateConfigurationCheckbox(cfg.shadowSoftShadows);
        softToggle.value = cfg.shadowSoftShadows;
        softToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
        {
            cfg.shadowSoftShadows = evt.newValue;
            Save();
            CrispyShadowsSwapper.Apply();
        });
        softRow.Add(softToggle);
        root.Add(softRow);
        dependentControls.Add(softRow);

        root.Add(SettingsUI.RebuildButton(root, "Reset shadows to default", ResetShadowsToDefault, CreateSection));

        UITools.UpdateDependentControlsState(dependentControls, cfg.crispyShadowsEnabled);
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
        Save();
        GlossSwapper.RestoreAll();
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
        smoothRow.Add(UITools.CreateConfigurationLabel("Gloss"));
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

        root.Add(SettingsUI.RebuildButton(root, "Reset glossiness to default",
            () => { ResetGlossToDefault(); GlossSwapper.ReapplyAll(); }, CreateSection));

        UITools.UpdateDependentControlsState(dependentControls, cfg.glossRemoverEnabled);
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
