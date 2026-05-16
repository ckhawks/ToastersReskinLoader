// MonoBehaviour shell for the QoL feature surface. Responsibilities:
//   - holds the in-memory QoLConfig
//   - bridges read/write against QoLStorage (two side-car files in
//     reskinprofiles/: QoL.json + ServerPrefs.json), independent of the
//     visual reskin profile so reskin profiles can be shared cleanly
//   - bootstraps DevConsole
//   - exposes the small surface that DevConsole calls back into
//   - listens for ESC to close base-game secondary menus
//
// Class/namespace names are leftover from the PoncePlayerInput port; kept
// because every other QoL file references them.

using System;
using UnityEngine;

namespace ToasterReskinLoader.qol;

public sealed class QoLRunner : MonoBehaviour
{
    internal static QoLRunner _instance;
    public static QoLRunner Instance => _instance;

    private QoLConfig _cmd = new QoLConfig();
    public QoLConfig Config => _cmd;

    public static QoLRunner Bootstrap()
    {
        if (_instance != null) return _instance;
        var go = new GameObject("ToasterPlayerQoL");
        UnityEngine.Object.DontDestroyOnLoad(go);
        var runner = go.AddComponent<QoLRunner>();
        try { DevConsole.AttachTo(go); } catch (Exception e) { Debug.LogError("[QoL] DevConsole attach failed: " + e); }
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
        try { ReloadFromProfile(); }
        catch (Exception e) { Debug.LogError("[QoL] ReloadFromProfile failed: " + e); }
        try { SavedServerPasswords.Initialize(); }
        catch (Exception e) { Debug.LogError("[QoL] SavedServerPasswords.Initialize failed: " + e); }
        try { ToasterReskinLoader.qol.serverbrowser.ServerSlotQueue.Initialize(); }
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
        try { ToasterReskinLoader.qol.serverbrowser.ServerSlotQueue.Teardown(); } catch { }
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

        if (_cmd == null || !_cmd.enableEscCloseMenus) return;
        if (DevConsole.Instance != null && DevConsole.Instance.IsOpen) return;

        var root = ToasterReskinLoader.ui.ReskinMenu.rootContainer;
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


    public void ReloadFromProfile()
    {
        _cmd = QoLStorage.Load();
    }

    public void SaveAndRefresh()
    {
        try
        {
            QoLStorage.Save(_cmd);
        }
        catch (Exception e) { Debug.LogError("[QoL] SaveAndRefresh failed: " + e); }
    }

    // DevConsole calls these by name.
    public void SaveConfigsAndRefresh() => SaveAndRefresh();
    public void DoReload() => ReloadFromProfile();

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