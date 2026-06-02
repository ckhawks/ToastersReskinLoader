// General tweaks — small base-game UX patches and interface fixes that don't
// fit a more specific page. Part of the "Tweaks" sidebar group.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ToasterReskinLoader.core;
using ToasterReskinLoader.hud;
using ToasterReskinLoader.ui;

namespace ToasterReskinLoader.ui.sections;

public static class GeneralSection
{
    public static void CreateSection(VisualElement root)
    {
        var cfg = SettingsUI.RequireConfig(root,
            "Small base-game UX patches and interface fixes.");
        if (cfg == null) return;
        var runner = SettingsRunner.Instance;

        SettingsUI.ToggleRow(root, "Close secondary menus with ESC", cfg.enableEscCloseMenus,
            v => { cfg.enableEscCloseMenus = v; runner.SaveAndRefresh(); });
        SettingsUI.ToggleRow(root, "Open chat in any in-game phase", cfg.enableChatAnyInGamePhase,
            v => { cfg.enableChatAnyInGamePhase = v; runner.SaveAndRefresh(); });
        SettingsUI.ToggleRow(root, "Enable scoreboard in any in-game phase", cfg.enableScoreboardAnyInGamePhase,
            v => { cfg.enableScoreboardAnyInGamePhase = v; runner.SaveAndRefresh(); });
        SettingsUI.ToggleRow(root, "Show minimap for spectators", cfg.enableSpectatorMinimap,
            v => { cfg.enableSpectatorMinimap = v; runner.SaveAndRefresh(); });

        // Minimap rotation mode — mutually exclusive dropdown.
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
                runner.SaveAndRefresh();
            });
            row.Add(dd);
            root.Add(row);
        }

        SettingsUI.ToggleRow(root, "Color floating player names by team", cfg.enablePlayerUsernameTeamColors,
            v => { cfg.enablePlayerUsernameTeamColors = v; runner.SaveAndRefresh(); });
        SettingsUI.ToggleRow(root, "Show jersey number in player name", cfg.enableNumberedNames,
            v => { cfg.enableNumberedNames = v; runner.SaveAndRefresh(); });
        SettingsUI.ToggleRow(root, "Show player count on team select buttons", cfg.enableTeamButtonPlayerCount,
            v => { cfg.enableTeamButtonPlayerCount = v; runner.SaveAndRefresh(); });
        SettingsUI.ToggleRow(root, "Text drop-shadow on all game UI", cfg.enableUiTextShadow,
            v =>
            {
                cfg.enableUiTextShadow = v;
                runner.SaveAndRefresh();
                UiTextShadow.RefreshForCurrentState();
            });
        SettingsUI.ToggleRow(root, "Restore Unicode glyphs", cfg.enableUnicodeFontFallback,
            v =>
            {
                cfg.enableUnicodeFontFallback = v;
                runner.SaveAndRefresh();
                if (v) UnicodeFontFallback.Apply();
                else   UnicodeFontFallback.Disable();
            });

        SettingsUI.ToggleRow(root, "Fix shared player country flags", cfg.enableFlagMaterialFix,
            v => { cfg.enableFlagMaterialFix = v; runner.SaveAndRefresh(); });
        SettingsUI.Note(root,
            "Vanilla bug: every player's helmet flag shares one material, so everyone ends up showing the same flag. "
            + "Takes effect as players (re)spawn.");

        SettingsUI.ToggleRow(root, "Use enhanced mod menu (search, sort, badges, update checker)", cfg.enableEnhancedModMenu,
            v => { cfg.enableEnhancedModMenu = v; runner.SaveAndRefresh(); });
        SettingsUI.Note(root,
            "Restart the game for an off→on toggle to take full effect; changes apply to the next mod menu open.");

        SettingsUI.ToggleRow(root, "Darken vanilla checkbox/input backgrounds", cfg.enableVanillaUIRetheme,
            v =>
            {
                cfg.enableVanillaUIRetheme = v;
                runner.SaveAndRefresh();
                if (v) VanillaUIRetheme.Enable();
                else   VanillaUIRetheme.Disable();
            });
    }
}
