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
        var runner = SettingsRunner.Instance;

        SettingsUI.ToggleRow(root, "Close secondary menus with ESC", cfg.enableEscCloseMenus,
            v => { cfg.enableEscCloseMenus = v; runner.SaveAndRefresh(); });
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
