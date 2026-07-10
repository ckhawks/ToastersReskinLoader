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

        SettingsUI.Note(root,
            "The game's own team-color bar automatically uses your custom team colors (set per-team "
            + "in the Players tab) when team-color customization is enabled.");

        SettingsUI.ToggleRow(root, "Color floating player names by team", cfg.enablePlayerUsernameTeamColors,
            v => { cfg.enablePlayerUsernameTeamColors = v; Settings.Save(); });
    }
}
