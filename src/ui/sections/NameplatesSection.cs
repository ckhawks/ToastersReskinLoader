// Nameplates — how players and teams are identified on the ice (team
// indicator, floating name colors, jersey numbers, country flags). Part of
// the "Tweaks" sidebar group (HUD).

using UnityEngine;
using UnityEngine.UIElements;
using ToasterReskinLoader.core;
using ToasterReskinLoader.display; // TeamIndicatorSwapper

namespace ToasterReskinLoader.ui.sections;

public static class NameplatesSection
{
    public static void CreateSection(VisualElement root)
    {
        var cfg = SettingsUI.RequireConfig(root,
            "How players and teams are identified on the ice. Team colors and names are set per-team in the Players tab.");
        if (cfg == null) return;
        var runner = SettingsRunner.Instance;

        SettingsUI.ToggleRow(root, "Enable team indicator", cfg.teamIndicatorEnabled,
            v =>
            {
                cfg.teamIndicatorEnabled = v;
                runner.SaveAndRefresh();
                TeamIndicatorSwapper.UpdateVisibility();
            });
        SettingsUI.Note(root,
            "Shows a colored bar at the bottom of the screen indicating your current team. Uses your custom team colors.");

        SettingsUI.ToggleRow(root, "Color floating player names by team", cfg.enablePlayerUsernameTeamColors,
            v => { cfg.enablePlayerUsernameTeamColors = v; runner.SaveAndRefresh(); });
        SettingsUI.ToggleRow(root, "Show jersey number in player name", cfg.enableNumberedNames,
            v => { cfg.enableNumberedNames = v; runner.SaveAndRefresh(); });

        SettingsUI.ToggleRow(root, "Fix shared player country flags", cfg.enableFlagMaterialFix,
            v => { cfg.enableFlagMaterialFix = v; runner.SaveAndRefresh(); });
        SettingsUI.Note(root,
            "Vanilla bug: every player's helmet flag shares one material, so everyone ends up showing the same flag. "
            + "Takes effect as players (re)spawn.");
    }
}
