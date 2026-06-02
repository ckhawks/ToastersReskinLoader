// Tiny per-frame tick host — the only piece of the old SettingsRunner that
// genuinely needs a MonoBehaviour: ScoreboardPolish clock interpolation + the
// ESC-to-close-secondary-menus poll. Also owns the DontDestroyOnLoad GameObject
// that hosts DevConsole / PositionSelectFreeLook.
//
// Config moved to the static Settings holder; feature init/teardown moved to
// Plugin.OnEnable/OnDisable; SendChatMessage moved to DevConsole.

using System;
using UnityEngine;
using ToasterReskinLoader.hud;         // ScoreboardPolish, EscClosesMenus
using ToasterReskinLoader.diagnostics; // DevConsole
using ToasterReskinLoader.input;       // PositionSelectFreeLook

namespace ToasterReskinLoader.core;

public sealed class TickDriver : MonoBehaviour
{
    private static TickDriver _instance;

    public static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("ToasterPlayerQoL");
        UnityEngine.Object.DontDestroyOnLoad(go);
        _instance = go.AddComponent<TickDriver>();
        try { DevConsole.AttachTo(go); } catch (Exception e) { Debug.LogError("[QoL] DevConsole attach failed: " + e); }
        try { PositionSelectFreeLook.AttachTo(go); } catch (Exception e) { Debug.LogError("[QoL] PositionSelectFreeLook attach failed: " + e); }
    }

    public static void Teardown()
    {
        if (_instance == null) return;
        try { UnityEngine.Object.Destroy(_instance.gameObject); } catch { }
        _instance = null;
    }

    private void Update()
    {
        // ScoreboardPolish runs every frame regardless of menu state so the
        // period clock keeps interpolating milliseconds during play.
        try { ScoreboardPolish.Tick(); } catch { }

        if (!Settings.Current.enableEscCloseMenus) return;
        if (DevConsole.Instance != null && DevConsole.Instance.IsOpen) return;

        var root = ToasterReskinLoader.ui.ReskinManagerMenu.rootContainer;
        if (root != null && root.style.display == UnityEngine.UIElements.DisplayStyle.Flex) return;

        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            // Close the topmost secondary menu (Settings, Mods, etc.). Opening the
            // pause menu in non-Playing phases is handled by the OnPauseActionPerformed
            // Harmony postfix in EscClosesMenus, not via polling.
            EscClosesMenus.TryCloseTopmostSecondaryMenu();
        }
    }
}
