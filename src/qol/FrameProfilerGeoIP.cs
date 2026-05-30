using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace ToasterReskinLoader.qol;

// GeoIP lookup for the currently-connected server endpoint. Uses
// ip-api.com (free, HTTP-only, 45 req/min limit). We only query once
// per unique IP per session and cache the result, so the rate limit is
// effectively a non-issue for normal play.
//
// Display goal: turn the bare "Server: 1.2.3.4:9999" into something like
// "Server: 1.2.3.4:9999 (Los Angeles, US — Comcast Cable)".
public static class FrameProfilerGeoIP
{
    public struct Info
    {
        public bool Valid;          // populated (success OR known-local fallback)
        public bool LookupFailed;   // network request errored
        public string City;
        public string Region;
        public string Country;      // 2-letter code
        public string Isp;
        public string Org;
    }

    static readonly Dictionary<string, Info> cache = new Dictionary<string, Info>();
    static readonly HashSet<string> inflight = new HashSet<string>();

    public static bool TryGet(string ip, out Info info)
    {
        return cache.TryGetValue(ip ?? "", out info);
    }

    // Returns a coroutine the caller (MonoBehaviour) can StartCoroutine on.
    // No-op if the IP is already cached or already being fetched.
    public static IEnumerator FetchAsync(string ip)
    {
        if (string.IsNullOrEmpty(ip)) yield break;
        if (cache.ContainsKey(ip)) yield break;
        if (inflight.Contains(ip)) yield break;

        if (IsPrivateOrLoopback(ip))
        {
            cache[ip] = new Info { Valid = true, City = "local", Country = "--", Isp = "(LAN)" };
            yield break;
        }

        inflight.Add(ip);
        Plugin.Log($"[FrameProfiler][GEOIP] starting fetch for {ip}");
        // ipapi.co supports HTTPS on the free tier and doesn't need an API
        // key. Returns JSON with city, region, country_code, org fields.
        string url = $"https://ipapi.co/{ip}/json/";

        UnityWebRequest req = UnityWebRequest.Get(url);
        req.timeout = 5;
        req.SetRequestHeader("User-Agent", "ToasterReskinLoader/2.x (FrameProfiler)");
        // SendWebRequest() returns a yieldable op; yield outside try to
        // satisfy iterator rules.
        yield return req.SendWebRequest();
        inflight.Remove(ip);

        string raw = null;
        long code = req.responseCode;
        string err = req.error;
        try { raw = req.downloadHandler != null ? req.downloadHandler.text : null; } catch { }
        try { req.Dispose(); } catch { }

        Plugin.Log($"[FrameProfiler][GEOIP] {ip} → http {code} err='{err}' bodyLen={(raw == null ? 0 : raw.Length)}");

        bool ok = string.IsNullOrEmpty(err) && code >= 200 && code < 300 && !string.IsNullOrEmpty(raw);
        if (!ok)
        {
            cache[ip] = new Info { Valid = true, LookupFailed = true };
            yield break;
        }

        IpapiCoResponse parsed = null;
        try { parsed = JsonConvert.DeserializeObject<IpapiCoResponse>(raw); }
        catch (Exception ex)
        {
            Plugin.LogError($"[FrameProfiler][GEOIP] {ip} → json parse failed: {ex.Message}");
            cache[ip] = new Info { Valid = true, LookupFailed = true };
            yield break;
        }
        if (parsed == null || parsed.error == true)
        {
            cache[ip] = new Info { Valid = true, LookupFailed = true };
            Plugin.Log($"[FrameProfiler][GEOIP] {ip} → API error='{parsed?.reason}' raw='{raw}'");
            yield break;
        }

        cache[ip] = new Info
        {
            Valid = true,
            City = parsed.city,
            Region = parsed.region,
            Country = parsed.country_code,
            Isp = parsed.org,
            Org = parsed.org,
        };
        Plugin.Log($"[FrameProfiler][GEOIP] {ip} → {parsed.city}, {parsed.region}, {parsed.country_code} ({parsed.org})");
    }

    static bool IsPrivateOrLoopback(string ip)
    {
        if (string.IsNullOrEmpty(ip)) return false;
        if (ip.StartsWith("127.") || ip == "::1") return true;
        if (ip.StartsWith("10.")) return true;
        if (ip.StartsWith("192.168.")) return true;
        // 172.16.0.0 – 172.31.255.255
        if (ip.StartsWith("172."))
        {
            var second = ip.Substring(4);
            int dot = second.IndexOf('.');
            if (dot > 0 && int.TryParse(second.Substring(0, dot), out int o) && o >= 16 && o <= 31) return true;
        }
        return false;
    }

    public static void ClearCache()
    {
        cache.Clear();
        inflight.Clear();
    }

    // ipapi.co schema. Fields are snake_case in the API. On rate-limit /
    // bad-request the API returns { "error": true, "reason": "..." }.
    // Fields are assigned by the JSON deserializer via reflection, so CS0649
    // ("never assigned") is a false positive here.
#pragma warning disable CS0649
    [Serializable]
    class IpapiCoResponse
    {
        public bool? error;
        public string reason;
        public string ip;
        public string city;
        public string region;
        public string country_code;
        public string org;
    }
#pragma warning restore CS0649
}
