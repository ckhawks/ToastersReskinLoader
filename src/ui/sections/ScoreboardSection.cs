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

        SettingsUI.ToggleRow(root, "Clock shows milliseconds", cfg.enableScoreboardMilliseconds,
            v => { cfg.enableScoreboardMilliseconds = v; Settings.Save(); });
        SettingsUI.ToggleRow(root, "Clock turns amber then red in the final 30 seconds", cfg.enableScoreboardClockColor,
            v => { cfg.enableScoreboardClockColor = v; Settings.Save(); });

        SettingsUI.ToggleRow(root, "Disable goal-scored screen flash", cfg.disableGoalScoredFlash,
            v => { cfg.disableGoalScoredFlash = v; Settings.Save(); });
        SettingsUI.Note(root,
            "Hides the full-screen team-colored flash when a goal is scored. The goal slow-motion is unaffected.");
    }
}
