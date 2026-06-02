// Fixes — toggles that correct vanilla rendering bugs/regressions. Most
// players just leave these on. Part of the "Tweaks" sidebar group (Game).

using UnityEngine.UIElements;
using ToasterReskinLoader.core;

namespace ToasterReskinLoader.ui.sections;

public static class FixesSection
{
    public static void CreateSection(VisualElement root)
    {
        var cfg = SettingsUI.RequireConfig(root, "Corrections for vanilla rendering bugs. Safe to leave on.");
        if (cfg == null) return;
        var runner = SettingsRunner.Instance;

        SettingsUI.ToggleRow(root, "Fix shared player country flags", cfg.enableFlagMaterialFix,
            v => { cfg.enableFlagMaterialFix = v; runner.SaveAndRefresh(); });
        SettingsUI.Note(root,
            "Vanilla bug: every player's helmet flag shares one material, so everyone ends up showing the same flag. "
            + "Takes effect as players (re)spawn.");

        SettingsUI.ToggleRow(root, "Restore Unicode glyphs", cfg.enableUnicodeFontFallback,
            v =>
            {
                cfg.enableUnicodeFontFallback = v;
                runner.SaveAndRefresh();
                if (v) UnicodeFontFallback.Apply();
                else   UnicodeFontFallback.Disable();
            });
        SettingsUI.Note(root,
            "The game's bundled font only includes basic Latin characters, so symbols like ▶ ★ ▼ render as blank "
            + "boxes. This attaches a system font so they display correctly.");
    }
}
