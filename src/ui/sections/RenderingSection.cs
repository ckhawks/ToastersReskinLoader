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
