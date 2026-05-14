// MonoBehaviour shell for the QoL feature surface. Responsibilities:
//   - holds the in-memory QoLConfig
//   - bridges read/write against ReskinProfileManager.currentProfile.playerQoL
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
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // ESC closes secondary game menus (Settings, Mods, ServerBrowser, ...)
    // when Toaster's reskin menu is NOT open (Toaster has its own ESC patch
    // for that case) and the dev console is NOT open.
    private void Update()
    {
        if (_cmd == null || !_cmd.enableEscCloseMenus) return;
        if (DevConsole.Instance != null && DevConsole.Instance.IsOpen) return;

        var root = ToasterReskinLoader.ui.ReskinMenu.rootContainer;
        if (root != null && root.style.display == UnityEngine.UIElements.DisplayStyle.Flex) return;

        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            EscClosesMenus.TryCloseTopmostSecondaryMenu();
        }
    }


    public void ReloadFromProfile()
    {
        var p = ToasterReskinLoader.ReskinProfileManager.currentProfile?.playerQoL;
        _cmd = p?.ToConfig() ?? new QoLConfig();
    }

    public void SaveAndRefresh()
    {
        try
        {
            var prof = ToasterReskinLoader.ReskinProfileManager.currentProfile;
            if (prof != null)
            {
                if (prof.playerQoL == null)
                    prof.playerQoL = new ToasterReskinLoader.qol.QoLProfile();
                prof.playerQoL.FromConfig(_cmd);
                ToasterReskinLoader.ReskinProfileManager.SaveProfile();
            }
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