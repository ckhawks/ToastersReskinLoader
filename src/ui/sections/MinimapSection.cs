// Minimap settings — colors, icon scale, refresh rate, plus spectator
// visibility and rotation. Consolidates what used to be split between the HUD
// and General pages. Part of the "Tweaks" sidebar group (HUD).

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ToasterReskinLoader.core;
using ToasterReskinLoader.display;  // MinimapSwapper
using ToasterReskinLoader.swappers; // TeamColorSwapper

namespace ToasterReskinLoader.ui.sections;

public static class MinimapSection
{
    private static SettingsConfig Cfg => Settings.Current;
    private static void SaveQoL() => Settings.Save();

    private static void ResetMinimapToDefault()
    {
        var d = new SettingsConfig();
        var c = Cfg;
        if (c == null) return;
        c.blueMinimapNumberColor = d.blueMinimapNumberColor;
        c.redMinimapNumberColor = d.redMinimapNumberColor;
        c.minimapPuckColor = d.minimapPuckColor;
        c.minimapPlayerScale = d.minimapPlayerScale;
        c.minimapPuckScale = d.minimapPuckScale;
        c.minimapRefreshRate = d.minimapRefreshRate;
        c.localPlayerMinimapIconEnabled = d.localPlayerMinimapIconEnabled;
        c.blueLocalPlayerMinimapIconColor = d.blueLocalPlayerMinimapIconColor;
        c.redLocalPlayerMinimapIconColor = d.redLocalPlayerMinimapIconColor;
        SaveQoL();
    }

    public static void CreateSection(VisualElement root)
    {
        var cfg = SettingsUI.RequireConfig(root, "Minimap colors, icon scale, refresh rate, and behavior.");
        if (cfg == null) return;

        // ── Behavior (spectator + rotation, formerly on the General page) ──
        SettingsUI.ToggleRow(root, "Show minimap while spectating or watching replays", cfg.enableSpectatorMinimap,
            v => { cfg.enableSpectatorMinimap = v; Settings.Save(); });

        {
            var row = UITools.CreateConfigurationRow();
            row.Add(UITools.CreateConfigurationLabel("Minimap rotation"));
            var labels = new List<string> { "Off (vanilla)", "Rotated 90°", "Follow player orientation" };
            var values = new List<string> { "off", "rotate90", "followPlayer" };
            var currentIdx = Math.Max(0, values.IndexOf(cfg.minimapRotationMode ?? "off"));
            var dd = UITools.CreateStringDropdownField(labels, labels[currentIdx]);
            dd.RegisterCallback<ChangeEvent<string>>(evt =>
            {
                var idx = labels.IndexOf(evt.newValue);
                if (idx < 0) idx = 0;
                cfg.minimapRotationMode = values[idx];
                Settings.Save();
            });
            row.Add(dd);
            root.Add(row);
        }

        SettingsUI.Separator(root);
        SettingsUI.Header(root, "Colors");

        var blueNumberColor = UITools.CreateColorConfigurationRow("Blue Player Number Color",
            cfg.blueMinimapNumberColor, false,
            newColor => { cfg.blueMinimapNumberColor = newColor; ToasterReskinLoaderAPI.NotifyMinimapSettingsChanged(); },
            () => SaveQoL());
        root.Add(blueNumberColor);

        var redNumberColor = UITools.CreateColorConfigurationRow("Red Player Number Color",
            cfg.redMinimapNumberColor, false,
            newColor => { cfg.redMinimapNumberColor = newColor; ToasterReskinLoaderAPI.NotifyMinimapSettingsChanged(); },
            () => SaveQoL());
        root.Add(redNumberColor);

        var puckColor = UITools.CreateColorConfigurationRow("Puck Color",
            cfg.minimapPuckColor, false,
            newColor => { cfg.minimapPuckColor = newColor; ToasterReskinLoaderAPI.NotifyMinimapSettingsChanged(); },
            () => SaveQoL());
        root.Add(puckColor);

        // ── Local player icon color (gated) ──
        var localIconDependentControls = new List<VisualElement>();

        var localIconRow = UITools.CreateConfigurationRow();
        localIconRow.Add(UITools.CreateConfigurationLabel("Custom Local Player Icon Color"));
        var localIconToggle = UITools.CreateConfigurationCheckbox(cfg.localPlayerMinimapIconEnabled);
        localIconToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
        {
            cfg.localPlayerMinimapIconEnabled = evt.newValue;
            SaveQoL();
            ToasterReskinLoaderAPI.NotifyMinimapSettingsChanged();
            TeamColorSwapper.RefreshAll();
            UITools.UpdateDependentControlsState(localIconDependentControls, evt.newValue);
        });
        localIconRow.Add(localIconToggle);
        root.Add(localIconRow);

        var blueLocalIconColor = UITools.CreateColorConfigurationRow("Blue Local Player Icon Color",
            cfg.blueLocalPlayerMinimapIconColor, false,
            newColor => { cfg.blueLocalPlayerMinimapIconColor = newColor; ToasterReskinLoaderAPI.NotifyMinimapSettingsChanged(); },
            () => SaveQoL());
        root.Add(blueLocalIconColor);
        localIconDependentControls.Add(blueLocalIconColor);

        var redLocalIconColor = UITools.CreateColorConfigurationRow("Red Local Player Icon Color",
            cfg.redLocalPlayerMinimapIconColor, false,
            newColor => { cfg.redLocalPlayerMinimapIconColor = newColor; ToasterReskinLoaderAPI.NotifyMinimapSettingsChanged(); },
            () => SaveQoL());
        root.Add(redLocalIconColor);
        localIconDependentControls.Add(redLocalIconColor);

        UITools.UpdateDependentControlsState(localIconDependentControls, cfg.localPlayerMinimapIconEnabled);

        SettingsUI.Separator(root);
        SettingsUI.Header(root, "Scale & Refresh");

        SettingsUI.SliderRow(root, "Player Icon Scale", 0.5f, 3f, cfg.minimapPlayerScale,
            val => { cfg.minimapPlayerScale = val; SaveQoL(); ToasterReskinLoaderAPI.NotifyMinimapSettingsChanged(); });
        SettingsUI.SliderRow(root, "Puck Icon Scale", 0.5f, 3f, cfg.minimapPuckScale,
            val => { cfg.minimapPuckScale = val; SaveQoL(); ToasterReskinLoaderAPI.NotifyMinimapSettingsChanged(); });
        SettingsUI.SliderRow(root, "Minimap Refresh Rate", 1f, 120f, cfg.minimapRefreshRate,
            val => { cfg.minimapRefreshRate = Mathf.RoundToInt(val); SaveQoL(); MinimapSwapper.ApplyRefreshRate(); });

        root.Add(SettingsUI.RebuildButton(root, "Reset minimap to default",
            () => { ResetMinimapToDefault(); ToasterReskinLoaderAPI.NotifyMinimapSettingsChanged(); },
            CreateSection));
    }
}
