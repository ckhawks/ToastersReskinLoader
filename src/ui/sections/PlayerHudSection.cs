// Player HUD — on-screen player/team identification: the team indicator bar
// and floating player nameplates. Part of the "Tweaks" sidebar group (HUD).

using UnityEngine.UIElements;
using ToasterReskinLoader.core;
using ToasterReskinLoader.display; // TeamIndicatorSwapper

namespace ToasterReskinLoader.ui.sections;

public static class PlayerHudSection
{
    public static void CreateSection(VisualElement root)
    {
        var cfg = SettingsUI.RequireConfig(root,
            "On-screen player and team identification. Team colors and names are set per-team in the Players tab.");
        if (cfg == null) return;

        SettingsUI.ToggleRow(root, "Enable team indicator", cfg.teamIndicatorEnabled,
            v =>
            {
                cfg.teamIndicatorEnabled = v;
                Settings.Save();
                TeamIndicatorSwapper.UpdateVisibility();
            });
        SettingsUI.Note(root,
            "Shows a colored bar at the bottom of the screen indicating your current team. Uses your custom team colors.");

        SettingsUI.ToggleRow(root, "Color floating player names by team", cfg.enablePlayerUsernameTeamColors,
            v => { cfg.enablePlayerUsernameTeamColors = v; Settings.Save(); });
    }
}
