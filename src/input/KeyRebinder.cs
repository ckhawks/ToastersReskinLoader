// Tiny shared "press a key to rebind" capture helper. The settings UI calls
// Begin() with a callback; the next keyboard key pressed (or Esc to cancel) is
// reported back. It's polled once per frame from TickDriver so it works no
// matter which feature owns the binding.

using System;
using UnityEngine.InputSystem;

namespace ToasterReskinLoader.input;

internal static class KeyRebinder
{
    private static Action<Key> _callback;

    /// True while we're waiting for the user to press a key.
    public static bool IsCapturing => _callback != null;

    /// Arm capture. onResult is invoked with the chosen Key, or Key.None if the
    /// user cancels (Esc). Re-arming replaces any pending capture.
    public static void Begin(Action<Key> onResult)
    {
        _callback = onResult;
    }

    public static void Tick()
    {
        if (_callback == null) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.escapeKey.wasPressedThisFrame) { Finish(Key.None); return; }

        foreach (var key in kb.allKeys)
        {
            if (!key.wasPressedThisFrame) continue;
            Key code = key.keyCode;
            if (code == Key.None || code == Key.Escape) continue;
            Finish(code);
            return;
        }
    }

    private static void Finish(Key result)
    {
        var cb = _callback;
        _callback = null;
        cb?.Invoke(result);
    }
}
