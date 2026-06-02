// Input & Camera tweaks. Part of the "Tweaks" sidebar group.

using UnityEngine.UIElements;
using ToasterReskinLoader.core;
using ToasterReskinLoader.input;

namespace ToasterReskinLoader.ui.sections;

public static class InputCameraSection
{
    public static void CreateSection(VisualElement root)
    {
        var cfg = SettingsUI.RequireConfig(root,
            "Controller handling and the position-select free-look camera.");
        if (cfg == null) return;

        SettingsUI.ToggleRow(root, "Disable controller / gamepad input", cfg.disableControllerInput,
            v =>
            {
                cfg.disableControllerInput = v;
                Settings.Save();
                DisableControllerInput.Apply(v);
            });
        SettingsUI.Note(root,
            "Disable all connected game controllers (gamepads/joysticks). Use this if a plugged-in "
            + "controller steals UI focus in menus so your first click on a button gets eaten. Applies "
            + "immediately and to controllers connected later; turn it off to restore controller support. "
            + "Note: if your controller's stick also drifts the mouse cursor, that's a Steam Input / "
            + "DS4Windows stick-to-mouse mapping and must be turned off there — it arrives as real "
            + "mouse movement this can't block.");

        SettingsUI.Separator(root);

        SettingsUI.ToggleRow(root, "Free-look camera in position select", cfg.enablePositionSelectFreeLook,
            v => { cfg.enablePositionSelectFreeLook = v; Settings.Save(); });
        SettingsUI.Note(root,
            "Right-click to fly around like a spectator while picking a position; right-click or Esc to return.");
    }
}
