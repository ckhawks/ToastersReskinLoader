// Input & Camera tweaks. Part of the "Tweaks" sidebar group.

using UnityEngine.UIElements;
using ToasterReskinLoader.core;

namespace ToasterReskinLoader.ui.sections;

public static class InputCameraSection
{
    public static void CreateSection(VisualElement root)
    {
        var cfg = SettingsUI.RequireConfig(root,
            "Free-look and free-fly cameras.");
        if (cfg == null) return;

        SettingsUI.ToggleRow(root, "Free-look camera in position select", cfg.enablePositionSelectFreeLook,
            v => { cfg.enablePositionSelectFreeLook = v; Settings.Save(); });
        SettingsUI.Note(root,
            "Right-click to fly around like a spectator while picking a position; right-click or Esc to return.");

        SettingsUI.ToggleRow(root, "Free camera", cfg.enableFreeCamera,
            v => { cfg.enableFreeCamera = v; Settings.Save(); });
        SettingsUI.KeyBindRow(root, "Free camera toggle key", cfg.freeCameraKey,
            k => { cfg.freeCameraKey = k; Settings.Save(); });
        SettingsUI.ToggleRow(root, "Hide free camera hint bar", cfg.freeCameraHideHint,
            v => { cfg.freeCameraHideHint = v; Settings.Save(); });
        SettingsUI.Note(root,
            "Press the toggle key anywhere to detach into a free-fly camera for inspecting reskins and screenshots. " +
            "WASD move, Space/Ctrl up/down, Shift boost, mouse to look, scroll wheel to zoom. Press it again to return. " +
            "Even works in the main menu!");
    }
}
