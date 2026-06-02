// Scoreboard & clock tweaks. Part of the "Tweaks" sidebar group (HUD).

using UnityEngine.UIElements;
using ToasterReskinLoader.core;

namespace ToasterReskinLoader.ui.sections;

public static class ScoreboardSection
{
    public static void CreateSection(VisualElement root)
    {
        var cfg = SettingsUI.RequireConfig(root, "Scoreboard visibility and clock polish.");
        if (cfg == null) return;
        var runner = SettingsRunner.Instance;

        SettingsUI.ToggleRow(root, "Enable scoreboard in any in-game phase", cfg.enableScoreboardAnyInGamePhase,
            v => { cfg.enableScoreboardAnyInGamePhase = v; runner.SaveAndRefresh(); });
        SettingsUI.ToggleRow(root, "Clock shows milliseconds", cfg.enableScoreboardMilliseconds,
            v => { cfg.enableScoreboardMilliseconds = v; runner.SaveAndRefresh(); });
        SettingsUI.ToggleRow(root, "Clock turns red in final 30s", cfg.enableScoreboardClockColor,
            v => { cfg.enableScoreboardClockColor = v; runner.SaveAndRefresh(); });
    }
}
