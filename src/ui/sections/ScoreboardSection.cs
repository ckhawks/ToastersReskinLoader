// Scoreboard & clock tweaks. Part of the "Tweaks" sidebar group (HUD).

using System;
using System.Collections.Generic;
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

        // How many sub-second place values to show on the clock.
        {
            var row = UITools.CreateConfigurationRow();
            row.Add(UITools.CreateConfigurationLabel("Milliseconds precision"));
            var labels = new List<string> { "1 (tenths)", "2 (hundredths)", "3 (milliseconds)" };
            var currentIdx = Math.Min(2, Math.Max(0, cfg.scoreboardMillisecondsDigits - 1));
            var dd = UITools.CreateStringDropdownField(labels, labels[currentIdx]);
            dd.RegisterCallback<ChangeEvent<string>>(evt =>
            {
                var idx = labels.IndexOf(evt.newValue);
                if (idx < 0) idx = 2;
                cfg.scoreboardMillisecondsDigits = idx + 1;
                Settings.Save();
            });
            row.Add(dd);
            root.Add(row);
        }

        SettingsUI.ToggleRow(root, "Only show milliseconds in the final 5 seconds",
            cfg.enableScoreboardMillisecondsLast5Only,
            v => { cfg.enableScoreboardMillisecondsLast5Only = v; Settings.Save(); });
        SettingsUI.ToggleRow(root, "Clock turns amber then red in the final 30 seconds", cfg.enableScoreboardClockColor,
            v => { cfg.enableScoreboardClockColor = v; Settings.Save(); });

        SettingsUI.ToggleRow(root, "Disable goal-scored screen flash", cfg.disableGoalScoredFlash,
            v => { cfg.disableGoalScoredFlash = v; Settings.Save(); });
        SettingsUI.Note(root,
            "Hides the full-screen team-colored flash when a goal is scored. The goal slow-motion is unaffected.");
    }
}
