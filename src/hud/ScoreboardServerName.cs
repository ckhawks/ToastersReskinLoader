// Full server name in the TAB scoreboard — always on.
//
// The in-game scoreboard reads the server name from the replicated
// NetworkVariable ServerManager.Server, whose Name field is a
// FixedString128Bytes. Anything past ~125 UTF-8 bytes (emoji count 2-4×
// each) is truncated at the server's string→FixedString128Bytes write and
// simply never reaches the client over the netcode channel — so no amount
// of scoreboard patching can reveal it. The scoreboard itself applies no
// truncation of its own; UIScoreboard.StyleServer already writes the whole
// replicated value.
//
// The untruncated name IS reachable another way: the server answers the
// same TCP "preview" request the server browser uses to populate its list
// (UIServerBrowser.PingServer), and TCPServerPreviewResponse.name is a
// plain unbounded string. After we join, GlobalStateManager still knows the
// server's ip:port (ConnectionState.Connection.EndPoint — the exact endpoint
// the browser pings), so we replay that preview request off-thread, then
// re-assert the full name onto the scoreboard's NameLabel.
//
// Behaviour:
//   * One preview fetch per endpoint per session; the result is cached and
//     re-applied on every StyleServer call (server change / player add /
//     remove), since each vanilla call rewrites the label to the truncated
//     value first.
//   * If the preview is unreachable (firewall, timeout), we leave the
//     vanilla truncated name untouched.
//   * All socket work runs on a background Task; the label is only ever
//     touched on the main thread (via ThreadManager for the async re-apply,
//     and directly inside the StyleServer postfix which is already on it).

using System;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.hud;

internal static class ScoreboardServerName
{
    private static readonly object Gate = new object();

    private enum FetchState { Fetching, Done, Failed }

    // Guarded by Gate. _key is the ip:port we've tracked for the current
    // connection; _fullName is the resolved untruncated name once Done.
    private static string _key;
    private static FetchState _state;
    private static string _fullName;

    // Main-thread only: the live scoreboard name label, re-cached each
    // StyleServer call (a scene reload rebuilds UIScoreboard).
    private static Label _nameLabel;

    private static readonly FieldInfo NameLabelField =
        AccessTools.Field(typeof(UIScoreboard), "nameLabel");

    [HarmonyPatch(typeof(UIScoreboard), "StyleServer")]
    private static class Patch_StyleServer
    {
        // Runs after vanilla has written the (possibly truncated) name.
        private static void Postfix(UIScoreboard __instance)
        {
            try { OnStyleServer(__instance); }
            catch (Exception e) { Plugin.LogWarning("[QoL] ScoreboardServerName postfix failed: " + e.Message); }
        }
    }

    private static void OnStyleServer(UIScoreboard scoreboard)
    {
        // The endpoint we're actually connected to (falls back to the last
        // connection if the active one has already been cleared mid-teardown).
        var state = GlobalStateManager.ConnectionState;
        var ep = state.Connection?.EndPoint ?? state.LastConnection?.EndPoint;
        if (ep == null || string.IsNullOrEmpty(ep.ipAddress)) return;

        _nameLabel = NameLabelField?.GetValue(scoreboard) as Label;
        if (_nameLabel == null) return;

        string key = ep.ipAddress + ":" + ep.port;

        bool startFetch = false;
        string applyName = null;
        lock (Gate)
        {
            if (key != _key)
            {
                // New server — reset and kick a fresh preview fetch.
                _key = key;
                _state = FetchState.Fetching;
                _fullName = null;
                startFetch = true;
            }
            else if (_state == FetchState.Done)
            {
                applyName = _fullName;
            }
        }

        // Re-assert our full name over vanilla's truncated write.
        if (applyName != null)
            _nameLabel.text = applyName;

        if (startFetch)
        {
            string ip = ep.ipAddress;
            ushort port = ep.port;
            Task.Run(() => FetchFullName(key, ip, port));
        }
    }

    // Background thread: open a preview connection, store the result, then
    // marshal a re-apply back onto the main thread.
    private static void FetchFullName(string key, string ip, ushort port)
    {
        string resolved = TryPreviewName(ip, port);

        lock (Gate)
        {
            if (key != _key) return; // moved on to another server
            if (resolved != null) { _state = FetchState.Done; _fullName = resolved; }
            else { _state = FetchState.Failed; }
        }

        if (resolved == null) return;

        // Apply now instead of waiting for the next StyleServer call. Reading
        // the singleton's Instance field off-thread is a plain field read;
        // Enqueue itself is internally locked and runs the action next Update.
        try
        {
            var tm = MonoBehaviourSingleton<ThreadManager>.Instance;
            tm?.Enqueue(() => ApplyIfCurrent(key, resolved));
        }
        catch (Exception e) { Plugin.LogWarning("[QoL] ScoreboardServerName enqueue failed: " + e.Message); }
    }

    // Main thread: write the resolved name if we're still on the same server
    // and the cached label is still live.
    private static void ApplyIfCurrent(string key, string fullName)
    {
        try
        {
            lock (Gate) { if (key != _key) return; }
            if (_nameLabel != null && _nameLabel.panel != null)
                _nameLabel.text = fullName;
        }
        catch (Exception e) { Plugin.LogWarning("[QoL] ScoreboardServerName apply failed: " + e.Message); }
    }

    // Mirrors UIServerBrowser.PingServer, trimmed to just fetch the name:
    // connect, send a preview request, read the preview response's name.
    // Returns null on any failure (unreachable / timeout / parse error).
    private static string TryPreviewName(string ip, ushort port)
    {
        TCPClient client = null;
        try
        {
            client = new TCPClient(new EndPoint(ip, port), 1000, 1000);
            string result = null;
            var done = new ManualResetEventSlim(false);

            client.OnConnected += delegate
            {
                try { client.SendMessage(JsonSerializer.Serialize(new TCPServerPreviewRequest(), (JsonSerializerOptions)null)); }
                catch (Exception e) { Plugin.LogWarning("[QoL] ScoreboardServerName send failed: " + e.Message); }
            };
            client.OnMessageReceived += delegate (string message)
            {
                try
                {
                    if (JsonSerializer.Deserialize<TCPServerMessage>(message, (JsonSerializerOptions)null).type == TCPServerMessageType.PreviewResponse)
                    {
                        result = JsonSerializer.Deserialize<TCPServerPreviewResponse>(message, (JsonSerializerOptions)null).name;
                        done.Set();
                    }
                }
                catch { /* partial / unexpected frame — ignore, wait for the next */ }
            };

            client.Connect();
            if (client.IsConnected)
                done.Wait(1500);
            return result;
        }
        catch (Exception e)
        {
            Plugin.LogWarning("[QoL] ScoreboardServerName preview fetch failed: " + e.Message);
            return null;
        }
        finally
        {
            try { client?.Disconnect(); } catch { }
        }
    }
}
