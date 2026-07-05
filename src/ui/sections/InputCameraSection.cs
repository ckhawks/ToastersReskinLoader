// Input & Camera tweaks. Part of the "Tweaks" sidebar group.

using UnityEngine.UIElements;
using ToasterReskinLoader.core;

namespace ToasterReskinLoader.ui.sections;

public static class InputCameraSection
{
    public static void CreateSection(VisualElement root)
    {
        var cfg = SettingsUI.RequireConfig(root,
            "The position-select free-look camera.");
        if (cfg == null) return;

        SettingsUI.ToggleRow(root, "Free-look camera in position select", cfg.enablePositionSelectFreeLook,
            v => { cfg.enablePositionSelectFreeLook = v; Settings.Save(); });
        SettingsUI.Note(root,
            "Right-click to fly around like a spectator while picking a position; right-click or Esc to return.");
    }
}
