// Disable controller / gamepad input entirely.
//
// Why: with a controller (e.g. a PS5 DualSense) plugged in, the gamepad
// feeds Puck's UI input module and fights the mouse — the cursor gets
// "stolen" and the first click on a button is eaten, so you have to click
// twice. Puck has no option to ignore an attached pad, so this toggle
// disables every gamepad/joystick device at the Input System level. That
// kills both the menu cursor hijack and in-game gamepad steering, and it
// sticks across hot-plugs: a controller connected while the toggle is on
// gets disabled the moment it appears.
//
// Off restores the devices and stops listening, so vanilla controller
// support comes back immediately without a restart.

using System;
using UnityEngine.InputSystem;

namespace ToasterReskinLoader.qol;

internal static class DisableControllerInput
{
    private static bool _active;
    private static Action<InputDevice, InputDeviceChange> _deviceChangeHandler;

    public static void Apply(bool enabled)
    {
        if (enabled) Enable();
        else Disable();
    }

    public static void Enable()
    {
        // Always sweep currently-connected pads, even if already active, so a
        // mid-session reapply catches anything that slipped through.
        //
        // Note: this only stops input Unity itself reads from the pad — which
        // is what fixes the gamepad stealing UI focus (the "first click gets
        // eaten" symptom). It does NOT stop a stick-to-mouse mapping applied
        // by Steam Input / DS4Windows, since that arrives as real OS mouse
        // movement; that has to be turned off Steam-side.
        DisableAllControllers();
        if (_active) return;
        _active = true;
        _deviceChangeHandler = OnDeviceChange;
        InputSystem.onDeviceChange += _deviceChangeHandler;
    }

    public static void Disable()
    {
        if (!_active) return;
        _active = false;
        if (_deviceChangeHandler != null)
        {
            InputSystem.onDeviceChange -= _deviceChangeHandler;
            _deviceChangeHandler = null;
        }
        EnableAllControllers();
    }

    private static void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (!_active || !IsController(device)) return;
        // A controller that's just been plugged in / re-enabled comes up
        // enabled by default — disable it again. We deliberately ignore the
        // Disabled change our own DisableDevice call raises, so there's no loop.
        if (change == InputDeviceChange.Added
            || change == InputDeviceChange.Reconnected
            || change == InputDeviceChange.Enabled)
        {
            TryDisable(device);
        }
    }

    private static bool IsController(InputDevice device) =>
        device is Gamepad || device is Joystick;

    private static void DisableAllControllers()
    {
        var disabled = new System.Collections.Generic.List<string>();
        foreach (var device in InputSystem.devices)
        {
            if (!IsController(device)) continue;
            bool wasEnabled = device.enabled;
            TryDisable(device);
            if (wasEnabled) disabled.Add(device.name);
        }
        if (disabled.Count > 0)
            Plugin.Log($"[QoL] Disabled controller input for: {string.Join(", ", disabled)}");
    }

    private static void EnableAllControllers()
    {
        foreach (var device in InputSystem.devices)
            if (IsController(device)) TryEnable(device);
    }

    private static void TryDisable(InputDevice device)
    {
        try { if (device != null && device.enabled) InputSystem.DisableDevice(device); }
        catch (Exception e) { Plugin.LogWarning($"[QoL] Failed to disable controller '{device?.name}': {e.Message}"); }
    }

    private static void TryEnable(InputDevice device)
    {
        try { if (device != null && !device.enabled) InputSystem.EnableDevice(device); }
        catch (Exception e) { Plugin.LogWarning($"[QoL] Failed to re-enable controller '{device?.name}': {e.Message}"); }
    }
}
