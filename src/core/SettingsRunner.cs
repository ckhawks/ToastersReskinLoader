// MonoBehaviour shell for the runtime feature surface. Config now lives in the
// static Settings holder; remaining responsibilities:
//   - bootstraps DevConsole + PositionSelectFreeLook and the Initialize() batch
//   - per-frame tick: ScoreboardPolish + ESC-to-close-secondary-menus
//   - SendChatMessage (the DevConsole / send-from-mod chat entry)
//
// Slated to dissolve fully (ticks -> a TickDriver, SendChatMessage -> a chat
// helper); see docs/settings-runtime-refactor-plan.md.

using System;
using UnityEngine;


using ToasterReskinLoader.serverbrowser;

using ToasterReskinLoader.diagnostics;

using ToasterReskinLoader.input;

using ToasterReskinLoader.hud;

namespace ToasterReskinLoader.core;

public sealed class SettingsRunner : MonoBehaviour
{
    internal static SettingsRunner _instance;
    public static SettingsRunner Instance => _instance;

    public static SettingsRunner Bootstrap()
    {
        if (_instance != null) return _instance;
        var go = new GameObject("ToasterPlayerQoL");
        UnityEngine.Object.DontDestroyOnLoad(go);
        var runner = go.AddComponent<SettingsRunner>();
        try { DevConsole.AttachTo(go); } catch (Exception e) { Debug.LogError("[QoL] DevConsole attach failed: " + e); }
        try { PositionSelectFreeLook.AttachTo(go); } catch (Exception e) { Debug.LogError("[QoL] PositionSelectFreeLook attach failed: " + e); }
        return runner;
    }

    public static void Teardown()
    {
        if (_instance == null) return;
        try { UnityEngine.Object.Destroy(_instance.gameObject); } catch { }
        _instance = null;
    }

    private void Awake()
    {
        _instance = this;
        // DisplaySettingsMigration runs standalone earlier in Plugin.OnEnable (before the reskin
        // profile can be re-saved), so it's intentionally not called here.
        try { Settings.Load(); }
        catch (Exception e) { Debug.LogError("[QoL] Settings.Load failed: " + e); }
        try { SavedServerPasswords.Initialize(); }
        catch (Exception e) { Debug.LogError("[QoL] SavedServerPasswords.Initialize failed: " + e); }
        try { ToasterReskinLoader.serverbrowser.ServerSlotQueue.Initialize(); }
        catch (Exception e) { Debug.LogError("[QoL] ServerSlotQueue.Initialize failed: " + e); }
        try { MainMenuButtons.Initialize(); }
        catch (Exception e) { Debug.LogError("[QoL] MainMenuButtons.Initialize failed: " + e); }
        try { ServerBrowserSort.Initialize(); }
        catch (Exception e) { Debug.LogError("[QoL] ServerBrowserSort.Initialize failed: " + e); }
        try { UiTextShadow.Initialize(); }
        catch (Exception e) { Debug.LogError("[QoL] UiTextShadow.Initialize failed: " + e); }
    }

    private void OnDestroy()
    {
        try { SavedServerPasswords.Teardown(); } catch { }
        try { ToasterReskinLoader.serverbrowser.ServerSlotQueue.Teardown(); } catch { }
        try { MainMenuButtons.Teardown(); } catch { }
        if (_instance == this) _instance = null;
    }

    // ESC closes secondary game menus (Settings, Mods, ServerBrowser, ...)
    // when Toaster's reskin menu is NOT open (Toaster has its own ESC patch
    // for that case) and the dev console is NOT open.
    private void Update()
    {
        // ScoreboardPolish runs every frame regardless of menu state so
        // the period clock keeps interpolating milliseconds during play.
        try { ScoreboardPolish.Tick(); } catch { }

        if (!Settings.Current.enableEscCloseMenus) return;
        if (DevConsole.Instance != null && DevConsole.Instance.IsOpen) return;

        var root = ToasterReskinLoader.ui.ReskinManagerMenu.rootContainer;
        if (root != null && root.style.display == UnityEngine.UIElements.DisplayStyle.Flex) return;

        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            // Close the topmost secondary menu (Settings, Mods, etc.).
            // Opening the pause menu in non-Playing phases is handled by
            // the OnPauseActionPerformed Harmony postfix in EscClosesMenus
            // so it runs in the input pipeline, not via polling.
            EscClosesMenus.TryCloseTopmostSecondaryMenu();
        }
    }


    // DevConsole / send-from-mod entry. Goes through the b310+ ChatManager
    // path; falls back silently if it isn't reachable.
    public void SendChatMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        try
        {
            var chatMgr = NetworkBehaviourSingleton<ChatManager>.Instance;
            if (chatMgr != null) chatMgr.Client_SendChatMessage(message, false, false);
        }
        catch (Exception e) { Debug.LogError("[QoL] SendChatMessage failed: " + e); }
    }
}