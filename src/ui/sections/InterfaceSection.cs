// Interface tweaks — menu behavior and global text/UI fixes. Part of the
// "Tweaks" sidebar group (Game).

using UnityEngine.UIElements;
using ToasterReskinLoader.core;
using ToasterReskinLoader.hud; // UiTextShadow

namespace ToasterReskinLoader.ui.sections;

public static class InterfaceSection
{
    public static void CreateSection(VisualElement root)
    {
        var cfg = SettingsUI.RequireConfig(root, "Menu behavior and global UI/text fixes.");
        if (cfg == null) return;

        SettingsUI.ToggleRow(root, "Close secondary menus with ESC", cfg.enableEscCloseMenus,
            v => { cfg.enableEscCloseMenus = v; Settings.Save(); });
        SettingsUI.ToggleRow(root, "Show player count on team select buttons", cfg.enableTeamButtonPlayerCount,
            v => { cfg.enableTeamButtonPlayerCount = v; Settings.Save(); });
        SettingsUI.ToggleRow(root, "Text drop-shadow on all game UI", cfg.enableUiTextShadow,
            v =>
            {
                cfg.enableUiTextShadow = v;
                Settings.Save();
                UiTextShadow.RefreshForCurrentState();
            });
        SettingsUI.ToggleRow(root, "Use enhanced mod menu (search, sort, badges, update checker)", cfg.enableEnhancedModMenu,
            v => { cfg.enableEnhancedModMenu = v; Settings.Save(); });
        SettingsUI.Note(root,
            "Restart the game for an off→on toggle to take full effect; changes apply to the next mod menu open.");

        SettingsUI.ToggleRow(root, "Darken vanilla checkbox/input backgrounds", cfg.enableVanillaUIRetheme,
            v =>
            {
                cfg.enableVanillaUIRetheme = v;
                Settings.Save();
                if (v) VanillaUIRetheme.Enable();
                else   VanillaUIRetheme.Disable();
            });
    }
}
