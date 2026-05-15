// Remember server passwords across sessions.
//
// Vanilla flow (decompiled from ConnectionManagerController and
// UIPopupManagerController):
//   1) Client_StartClient(ip, port, password) — password may be empty.
//   2) Server rejects with ConnectionRejectionCode.MissingPassword or
//      InvalidPassword. HandleConnectionRejection sets ReconnectionState
//      to (phase: AwaitingPassword, password: null) and triggers
//      Event_OnConnectionRejected.
//   3) UIPopupManagerController.Event_OnReconnectionStateChanged sees
//      phase=AwaitingPassword + password=null and shows the
//      "PASSWORD REQUIRED" popup (PopupMissingPasswordContent).
//   4) User types a password; the popup writes it back into
//      ReconnectionState.Password; ConnectionManagerController.
//      Event_OnReconnectionStateChanged sees the password change and
//      calls Client_StartClient(ip, port, typedPassword).
//
// What we add:
//   * After HandleConnectionRejection fires its Event_OnConnectionRejected,
//     if we have a saved password for ConnectionState.LastConnection's
//     ip:port AND we haven't already tried it this top-level join attempt,
//     write it into ReconnectionState.Password ourselves and hide the
//     popup that just appeared. The reconnect listener picks the password
//     change up and retries — the popup flashes briefly but the user is
//     never asked to type.
//   * On Event_OnClientConnected we save (or overwrite) the password the
//     server just accepted, keyed by ip:port — unless the user opted out
//     by unchecking the Remember box on the popup.
//   * If the auto-filled password is rejected (InvalidPassword), the
//     per-attempt flag prevents a loop: we let the popup stay up and the
//     user type the new password. On success we overwrite the saved entry.
//
// Per-attempt state is cleared on the user-initiated join events
// (Event_OnMainMenuClickJoinServer / Event_OnServerBrowserClickEndPoint /
// Event_OnMatchmakingMatchingClickConnect) so each fresh click gets a
// fresh auto-fill chance.
//
// Storage: profile JSON, plaintext, keyed by "ip:port". Same threat model
// as every other locally-saved password.

using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.qol;

internal static class SavedServerPasswords
{
    // ip:port of join attempts we've already auto-filled this session.
    // Prevents an InvalidPassword retry loop with a stale saved value.
    private static readonly HashSet<string> _alreadyTriedSavedFor = new HashSet<string>();

    // ip:port -> whether to persist the password the server accepts.
    // Default-true if absent. Set false when the user unchecks Remember.
    private static readonly Dictionary<string, bool> _rememberFor = new Dictionary<string, bool>();

    // ip:port -> friendly server name, populated passively by the server
    // browser. Used by the management UI to show "Server Name" instead of
    // a bare ip:port. In-memory only; rebuilt as the browser pings.
    private static readonly Dictionary<string, string> _serverNameCache = new Dictionary<string, string>();

    private static bool Enabled =>
        QoLRunner.Instance?.Config?.enableSavedServerPasswords ?? false;

    private static Dictionary<string, string> Store =>
        QoLRunner.Instance?.Config?.savedServerPasswords;

    // Called once from QoLRunner.Awake — registers our EventManager
    // listeners. Harmony patches in the nested classes are picked up
    // automatically by PatchAll.
    internal static void Initialize()
    {
        try
        {
            EventManager.AddEventListener("Event_OnMainMenuClickJoinServer", OnUserInitiatedJoin);
            EventManager.AddEventListener("Event_OnServerBrowserClickEndPoint", OnUserInitiatedJoin);
            EventManager.AddEventListener("Event_OnMatchmakingMatchingClickConnect", OnUserInitiatedJoin);
            EventManager.AddEventListener("Event_OnConnectionRejected", OnConnectionRejected);
            EventManager.AddEventListener("Event_OnClientConnected", OnClientConnected);
        }
        catch (Exception e) { Debug.LogWarning("[QoL] SavedServerPasswords init failed: " + e.Message); }
    }

    internal static void Teardown()
    {
        try
        {
            EventManager.RemoveEventListener("Event_OnMainMenuClickJoinServer", OnUserInitiatedJoin);
            EventManager.RemoveEventListener("Event_OnServerBrowserClickEndPoint", OnUserInitiatedJoin);
            EventManager.RemoveEventListener("Event_OnMatchmakingMatchingClickConnect", OnUserInitiatedJoin);
            EventManager.RemoveEventListener("Event_OnConnectionRejected", OnConnectionRejected);
            EventManager.RemoveEventListener("Event_OnClientConnected", OnClientConnected);
        }
        catch { }
    }

    // ──────────────────────────────── helpers ─────────────────────────────

    private static string KeyForLastConnection()
    {
        var ep = GlobalStateManager.ConnectionState.LastConnection?.EndPoint;
        return ep == null ? null : ep.ipAddress + ":" + ep.port;
    }

    private static string KeyForCurrentConnection()
    {
        var ep = GlobalStateManager.ConnectionState.Connection?.EndPoint;
        return ep == null ? null : ep.ipAddress + ":" + ep.port;
    }

    internal static bool HasSaved(string key)
        => !string.IsNullOrEmpty(key) && Store != null && Store.ContainsKey(key);

    internal static bool TryGetSaved(string key, out string password)
    {
        password = null;
        if (string.IsNullOrEmpty(key) || Store == null) return false;
        return Store.TryGetValue(key, out password) && !string.IsNullOrEmpty(password);
    }

    internal static void Remove(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        var store = Store;
        if (store == null) return;
        if (store.Remove(key)) QoLRunner.Instance?.SaveAndRefresh();
    }

    internal static void RemoveAll()
    {
        var store = Store;
        if (store == null || store.Count == 0) return;
        store.Clear();
        QoLRunner.Instance?.SaveAndRefresh();
    }

    // Snapshot used by the management UI in PlayerQoLSection.
    internal static List<string> SnapshotKeys()
    {
        var store = Store;
        if (store == null) return new List<string>();
        var list = new List<string>(store.Keys);
        list.Sort(StringComparer.Ordinal);
        return list;
    }

    // Friendly name lookup populated by the server-browser hook. Returns
    // null when we haven't seen this ip:port pinged this session.
    internal static string GetCachedServerName(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        return _serverNameCache.TryGetValue(key, out string n) ? n : null;
    }

    internal static void SetRememberFor(string key, bool remember)
    {
        if (string.IsNullOrEmpty(key)) return;
        _rememberFor[key] = remember;
    }

    internal static bool GetRememberFor(string key)
    {
        if (string.IsNullOrEmpty(key)) return true;
        return !_rememberFor.TryGetValue(key, out bool v) || v;
    }

    // ───────────────────────────── event handlers ─────────────────────────

    private static void OnUserInitiatedJoin(Dictionary<string, object> _)
    {
        // Every user-initiated join is a fresh top-level attempt — reset
        // the per-attempt state so the auto-fill gets a chance again.
        _alreadyTriedSavedFor.Clear();
        _rememberFor.Clear();
    }

    private static void OnConnectionRejected(Dictionary<string, object> message)
    {
        if (!Enabled) return;
        try
        {
            var rejection = (ConnectionRejection)message["connectionRejection"];

            if (rejection.code != ConnectionRejectionCode.MissingPassword
                && rejection.code != ConnectionRejectionCode.InvalidPassword) return;

            string key = KeyForLastConnection();
            if (string.IsNullOrEmpty(key)) return;

            // Auto-forget: when our auto-filled password was the one that
            // just got rejected (we already tried this attempt + still
            // have a saved entry), the saved entry is stale — drop it now
            // so the next attempt prompts the user fresh instead of
            // wasting another round-trip with the wrong value. The popup
            // is already on screen (vanilla's HandleConnectionRejection
            // showed it) with the red "Incorrect password" label, so the
            // user can just type the new one.
            if (rejection.code == ConnectionRejectionCode.InvalidPassword
                && _alreadyTriedSavedFor.Contains(key)
                && HasSaved(key))
            {
                Remove(key);
                return;
            }

            if (_alreadyTriedSavedFor.Contains(key)) return;
            if (!TryGetSaved(key, out string saved)) return;

            _alreadyTriedSavedFor.Add(key);

            // Trigger the vanilla retry path by setting the password.
            // ConnectionManagerController.Event_OnReconnectionStateChanged
            // detects the change and calls Client_StartClient with it.
            GlobalStateManager.SetReconnectionState(new Dictionary<string, object>
            {
                { "password", saved }
            });

            // The popup was already shown by UIPopupManagerController
            // earlier in this synchronous event chain — pull it down so
            // the flash is minimal.
            var ui = MonoBehaviourSingleton<UIManager>.Instance;
            ui?.PopupManager?.HidePopup("missingPassword");
        }
        catch (Exception e) { Debug.LogWarning("[QoL] SavedServerPasswords auto-fill failed: " + e.Message); }
    }

    private static void OnClientConnected(Dictionary<string, object> _)
    {
        if (!Enabled) return;
        try
        {
            var conn = GlobalStateManager.ConnectionState.Connection;
            if (conn == null) return;
            string pwd = conn.Password;
            if (string.IsNullOrEmpty(pwd))
            {
                // No password used — nothing to save and nothing to clear
                // (a saved password for a passwordless server stays as-is
                //  in case the server later turns protection back on).
                return;
            }

            string key = KeyForCurrentConnection();
            if (string.IsNullOrEmpty(key)) return;

            var store = Store;
            if (store == null) return;

            bool remember = GetRememberFor(key);
            if (remember)
            {
                store[key] = pwd;
            }
            else
            {
                store.Remove(key);
            }
            QoLRunner.Instance?.SaveAndRefresh();

            // Reset per-attempt state on a finalized success.
            _alreadyTriedSavedFor.Remove(key);
            _rememberFor.Remove(key);
        }
        catch (Exception e) { Debug.LogWarning("[QoL] SavedServerPasswords save failed: " + e.Message); }
    }

    // ─────────────── popup injection: Remember box + error label ──────────

    // Inject UI into the password popup right after its TextField is wired.
    // - Adds an "Incorrect password — try again" label above the field
    //   when the most recent rejection was InvalidPassword.
    // - Adds a "Remember password for this server" checkbox below.
    [HarmonyPatch(typeof(PopupMissingPasswordContent), nameof(PopupMissingPasswordContent.Initialize))]
    private static class Popup_Initialize_Postfix
    {
        private static void Postfix(PopupMissingPasswordContent __instance)
        {
            if (!Enabled) return;
            try
            {
                var root = __instance?.VisualElement;
                if (root == null) return;

                string key = KeyForLastConnection();
                bool alreadySaved = HasSaved(key);

                // Read the current rejection straight off GlobalStateManager
                // rather than a cached value: vanilla's HandleConnectionRejection
                // updates ConnectionState.ConnectionRejection BEFORE it triggers
                // the AwaitingPassword reconnection state change that brought us
                // here, so this field always reflects the rejection that caused
                // *this* popup. (The Event_OnConnectionRejected listener fires
                // later in the same chain — too late to drive the popup UI.)
                var rejection = GlobalStateManager.ConnectionState.ConnectionRejection;
                if (rejection != null && rejection.code == ConnectionRejectionCode.InvalidPassword)
                {
                    var err = new Label("Incorrect password — please try again.");
                    err.style.color = new Color(1f, 0.45f, 0.45f);
                    err.style.unityFontStyleAndWeight = FontStyle.Bold;
                    err.style.fontSize = 13;
                    err.style.marginTop = 6;
                    err.style.marginBottom = 6;
                    err.style.whiteSpace = WhiteSpace.Normal;

                    // Place above the password field if possible.
                    var pwdField = root.Q<VisualElement>("PasswordTextField");
                    if (pwdField != null && pwdField.parent != null)
                    {
                        int idx = pwdField.parent.IndexOf(pwdField);
                        pwdField.parent.Insert(idx, err);
                    }
                    else
                    {
                        root.Insert(0, err);
                    }
                }

                // Remember checkbox. Lower the label font size below the
                // popup body default — the inner "unity-toggle__label" is
                // what renders the text, so target that on attach.
                var remember = new Toggle("Remember password for this server")
                {
                    value = GetRememberFor(key)
                };
                remember.style.marginTop = 10;
                remember.style.fontSize = 13;
                ToasterReskinLoader.ui.UITools.StyleConfigCheckboxBox(remember);
                remember.RegisterCallback<AttachToPanelEvent>(_ =>
                {
                    var lbl = remember.Q<Label>(className: "unity-toggle__label");
                    if (lbl != null) lbl.style.fontSize = 13;
                });
                remember.RegisterCallback<ChangeEvent<bool>>(evt => SetRememberFor(key, evt.newValue));
                root.Add(remember);

                // Hint reflects whether there's already a saved entry. The
                // current OnClientConnected logic does:
                //   checked  + submit → save (overwrite or create entry)
                //   unchecked + submit → remove any existing entry, don't save
                // So when an entry exists, unchecking is the in-popup "forget"
                // path (alternative to the Forget button in the QoL menu).
                var hint = new Label(alreadySaved
                    ? "A saved password exists for this server. Submit with the box unchecked to forget it."
                    : "Stored as plaintext in your TRL profile after the server accepts it.");
                hint.style.fontSize = 11;
                hint.style.color = new Color(0.7f, 0.7f, 0.7f);
                hint.style.marginTop = 2;
                hint.style.whiteSpace = WhiteSpace.Normal;
                root.Add(hint);
            }
            catch (Exception e) { Debug.LogWarning("[QoL] SavedServerPasswords popup inject failed: " + e.Message); }
        }
    }

    // Passively cache friendly server names as the vanilla server browser
    // pings each server. The cache is in-memory only; it doesn't matter if
    // it's empty (the management UI just falls back to ip:port).
    [HarmonyPatch(typeof(UIServerBrowser), "SetServerPreviewData")]
    private static class ServerBrowser_CacheName_Postfix
    {
        private static void Postfix(EndPoint endPoint, ServerPreviewData previewData)
        {
            try
            {
                if (endPoint == null || previewData == null) return;
                if (string.IsNullOrEmpty(previewData.name)) return;
                _serverNameCache[endPoint.ipAddress + ":" + endPoint.port] = previewData.name;
            }
            catch { }
        }
    }
}
