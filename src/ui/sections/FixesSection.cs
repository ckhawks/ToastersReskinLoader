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

        SettingsUI.ToggleRow(root, "Restore Unicode glyphs", cfg.enableUnicodeFontFallback,
            v =>
            {
                cfg.enableUnicodeFontFallback = v;
                Settings.Save();
                if (v) UnicodeFontFallback.Apply();
                else   UnicodeFontFallback.Disable();
            });
        SettingsUI.Note(root,
            "The game's bundled font only includes basic Latin characters, so symbols like ▶ ★ ▼ render as blank "
            + "boxes. This attaches a system font so they display correctly.");
    }
}
