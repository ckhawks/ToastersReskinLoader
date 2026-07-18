using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace ToasterReskinLoader.api;

/// <summary>
/// Exports the player's equipped reskins + non-reskin mod settings to the puckstats
/// usage backend so the website can rank reskins by real usage and graph it over time.
///
/// Kept separate from <see cref="AppearanceAPI"/> (which owns character appearance + XP);
/// this class only borrows AppearanceAPI's cached Steam ticket + coroutine runner so it
/// doesn't re-trigger the global Steamworks auth callback. Gated by two independent
/// opt-outs: ShareReskinAnalytics (equips) and ShareSettingsAnalytics (settings). The
/// client sends raw slot facts only; the server owns the usage formula. See puckstats
/// docs/reskins/05-config-analytics-ingest.md + 08-settings-analytics-ingest.md.
/// </summary>
public static class UsageAnalyticsAPI
{
    // Low-priority telemetry: we send a single snapshot a short delay after game open
    // (and when a share toggle is switched back on). Deliberately NOT re-sent on every
    // reskin change or on a periodic heartbeat -- this data does not need to be live.
    private const float SEND_DELAY = 10f;

    private static Coroutine pendingPost;

    // Borrowed from AppearanceAPI so we share its single Steam ticket + runner.
    private static MonoBehaviour Runner => AppearanceAPI.Runner;
    private static string Ticket => AppearanceAPI.CachedTicket;
    private static string BaseUrl => AppearanceAPI.BaseUrl;

    // Non-reskin settings keys that must never be exported: remembered UI geometry /
    // search state (noise). The sensitive per-server credential dicts are already
    // [JsonIgnore] on SettingsConfig and are skipped below; free-text team names live on
    // the reskin Profile, not the settings snapshot, so they are never reached here.
    private static readonly HashSet<string> SettingsExclude = new()
    {
        "devConsoleX", "devConsoleY", "devConsoleW", "devConsoleH", "browserSearch",
    };

    /// <summary>True if either analytics share toggle is on.</summary>
    private static bool AnyEnabled()
    {
        var s = Plugin.modSettings;
        return s != null && (s.ShareReskinAnalytics || s.ShareSettingsAnalytics);
    }

    /// <summary>
    /// Queue a one-shot, short-delayed export of whichever analytics the user has opted
    /// into (equipped reskins and/or mod settings). Fired once on game open and when a
    /// share toggle is switched back on. No-op when both toggles are off or the API isn't
    /// initialized yet. Repeated calls coalesce into a single send.
    /// </summary>
    public static void QueuePost()
    {
        if (Runner == null) return;
        if (!AnyEnabled()) return;

        if (pendingPost != null)
            Runner.StopCoroutine(pendingPost);
        pendingPost = Runner.StartCoroutine(DebouncedPost());
    }

    /// <summary>
    /// Opt-out purge: tell the backend to forget this user's stored equip data. Call once
    /// when ShareReskinAnalytics is turned OFF. The settings data is unaffected, and any
    /// still-pending send re-checks the toggle so it won't re-populate the purged data.
    /// </summary>
    public static void PurgeReskinEquips()
    {
        if (Runner == null) return;
        Runner.StartCoroutine(PurgeCoroutine($"{BaseUrl}/api/reskins/equips"));
    }

    /// <summary>Opt-out purge for the settings snapshot. Call when ShareSettingsAnalytics is turned OFF.</summary>
    public static void PurgeSettings()
    {
        if (Runner == null) return;
        Runner.StartCoroutine(PurgeCoroutine($"{BaseUrl}/api/reskins/settings"));
    }

    /// <summary>Stops any pending send. Call from AppearanceAPI.Cleanup while the runner is still set.</summary>
    internal static void Cleanup()
    {
        if (pendingPost != null && Runner != null)
            Runner.StopCoroutine(pendingPost);
        pendingPost = null;
    }

    private static IEnumerator DebouncedPost()
    {
        yield return new WaitForSeconds(SEND_DELAY);
        pendingPost = null;
        yield return SendAnalytics();
    }

    /// <summary>
    /// Posts the equip snapshot and/or settings snapshot, each gated by its own share
    /// toggle (re-checked here so a mid-debounce flip is honored). Waits for the ticket.
    /// </summary>
    private static IEnumerator SendAnalytics()
    {
        if (!AnyEnabled()) yield break;

        yield return WaitForTicket();
        if (Ticket == null)
        {
            Plugin.LogDebug("[UsageAnalytics] Skipping: no Steam ticket");
            yield break;
        }

        if (Plugin.modSettings.ShareReskinAnalytics)
            yield return PostReskinEquips();
        if (Plugin.modSettings.ShareSettingsAnalytics)
            yield return PostSettingsSnapshot();
    }

    // Wait up to 10s for AppearanceAPI to cache the shared Steam ticket.
    private static IEnumerator WaitForTicket()
    {
        float elapsed = 0f;
        while (Ticket == null && elapsed < 10f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private static IEnumerator PostReskinEquips()
    {
        ReskinProfileManager.BuildEquipAnalytics(out var slots, out var pucks);

        var payload = new ReskinEquipsPayload
        {
            ticket = Ticket,
            share = true,
            slots = slots,
            pucks = pucks,
        };
        yield return Post($"{BaseUrl}/api/reskins/equips", payload,
            $"Reskin equips saved ({slots.Count} slots, {pucks.Count} pucks)", "Reskin equips");
    }

    private static IEnumerator PostSettingsSnapshot()
    {
        var settings = BuildSettingsSnapshot();
        var payload = new SettingsSnapshotPayload
        {
            ticket = Ticket,
            share = true,
            settings = settings,
        };
        yield return Post($"{BaseUrl}/api/reskins/settings", payload,
            $"Settings snapshot saved ({settings.Count} keys)", "Settings");
    }

    private static IEnumerator PurgeCoroutine(string url)
    {
        yield return WaitForTicket();
        if (Ticket == null) yield break;
        yield return Post(url, new ShareOffPayload { ticket = Ticket, share = false },
            "Analytics data purged", "Analytics opt-out");
    }

    /// <summary>Serializes and POSTs a payload; logs success/failure with a short label.</summary>
    private static IEnumerator Post(string url, object payload, string okMessage, string errLabel)
    {
        string json = JsonConvert.SerializeObject(payload);
        using var request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
            Plugin.LogError($"[UsageAnalytics] {errLabel} POST failed: {request.error} - {request.downloadHandler?.text}");
        else
            Plugin.LogDebug($"[UsageAnalytics] {okMessage}");
    }

    /// <summary>
    /// Flattens the non-reskin mod settings (QoL SettingsConfig + ModSettings) into a
    /// flat key -> scalar map. Skips [JsonIgnore] members (the per-server credential dicts
    /// + internal tuning knobs), excluded noise keys, and any non-scalar value.
    ///
    /// NOTE: the puckstats settings endpoint hard-caps at ~300 distinct keys per user (and
    /// 200 chars per string value) and silently drops the overflow. TRL's real surface is
    /// ~60 QoL fields + a handful of ModSettings, so there's plenty of headroom, but if the
    /// settings surface ever balloons, raise the server cap in lock-step
    /// (util/reskins/settings.ts MAX_KEYS) so we don't silently truncate our own data.
    /// </summary>
    private static Dictionary<string, object> BuildSettingsSnapshot()
    {
        var dict = new Dictionary<string, object>();

        var cfg = core.Settings.Current;
        if (cfg != null)
        {
            foreach (var f in cfg.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.GetCustomAttribute<JsonIgnoreAttribute>() != null) continue;
                if (SettingsExclude.Contains(f.Name)) continue;
                var val = ScalarOrNull(f.GetValue(cfg));
                if (val != null) dict[f.Name] = val;
            }
        }

        var ms = Plugin.modSettings;
        if (ms != null)
        {
            foreach (var prop in ms.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead) continue;
                var val = ScalarOrNull(prop.GetValue(ms));
                if (val != null) dict[prop.Name] = val;
            }
        }

        return dict;
    }

    /// <summary>Returns the value if it's a JSON scalar we want to track, else null (skip).</summary>
    private static object ScalarOrNull(object v)
    {
        switch (v)
        {
            case bool b: return b;
            case int i: return i;
            case float f: return f;
            case double d: return d;
            case string s: return s;
            default: return null; // Dictionary / Color / complex -> skip
        }
    }

    // ==================== SERIALIZATION ====================

    [Serializable]
    private class ReskinEquipsPayload
    {
        public string ticket;
        public bool share;
        public List<ReskinProfileManager.EquipSlotInfo> slots;
        public List<ReskinProfileManager.PuckEntryInfo> pucks;
    }

    [Serializable]
    private class SettingsSnapshotPayload
    {
        public string ticket;
        public bool share;
        public Dictionary<string, object> settings;
    }

    [Serializable]
    private class ShareOffPayload
    {
        public string ticket;
        public bool share;
    }
}
