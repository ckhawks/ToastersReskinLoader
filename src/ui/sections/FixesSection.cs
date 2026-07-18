// Fixes — toggles that correct vanilla rendering bugs/regressions. Most
// players just leave these on. Part of the "Tweaks" sidebar group (Game).

using UnityEngine.UIElements;
using ToasterReskinLoader.core;
using ToasterReskinLoader.social;

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

        SettingsUI.ToggleRow(root, "Fix Steam server presence", cfg.enableServerIpPresenceFix,
            v =>
            {
                cfg.enableServerIpPresenceFix = v;
                Settings.Save();
                if (v) RichPresenceIpFix.Enable();
                else   RichPresenceIpFix.Disable();
            });
        SettingsUI.Note(root,
            "Puck broadcasts an empty server IP to Steam, which breaks the \"Join Game\" button and hides which "
            + "server you're on from friends. This rewrites your Steam presence with the real address you connected "
            + "to. Only affects your own presence, and friends only benefit if they also run this mod.");
    }
}
