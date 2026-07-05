using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using UnityEngine;

using ToasterReskinLoader.core;

namespace ToasterReskinLoader.social.probe;

/// Fetches the matchmaking probe list on demand via the game's
/// "playerGetProbesRequest" / "playerGetProbesResponse" websocket round-trip and
/// persists it to disk so the panel can render on a cold start.
///
/// Successor to BeaconCache: the game migrated Edgegap Beacons -> Probes, so the
/// old passive "playerBeaconRttRequest" capture is replaced by an active request.
public static class ProbeCache
{
    private static readonly string CacheDir = Path.Combine(
        Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "reskinprofiles");
    private static readonly string CachePath = Path.Combine(CacheDir, "probes.json");

    private static Probe[] _probes = Array.Empty<Probe>();
    private static readonly object _lock = new object();

    public static event Action OnProbesUpdated;

    public static Probe[] GetProbes()
    {
        lock (_lock) return (Probe[])_probes.Clone();
    }

    public static bool HasProbes
    {
        get { lock (_lock) return _probes.Length > 0; }
    }

    public static void Initialize()
    {
        TryLoadFromDisk();
        WebSocketManager.AddMessageListener("playerGetProbesResponse", OnPlayerGetProbesResponse);
        Plugin.Log($"ProbeCache initialized (cached {_probes.Length} probe(s))");
    }

    public static void Shutdown()
    {
        WebSocketManager.RemoveMessageListener("playerGetProbesResponse", OnPlayerGetProbesResponse);
    }

    /// Ask the backend for the current probe catalog. The response arrives on
    /// OnPlayerGetProbesResponse, which caches it and raises OnProbesUpdated.
    public static void RequestProbes()
    {
        try
        {
            WebSocketManager.Emit("playerGetProbesRequest", null, "playerGetProbesResponse");
        }
        catch (Exception e)
        {
            Plugin.LogError($"ProbeCache failed to emit playerGetProbesRequest: {e.Message}");
        }
    }

    private static void OnPlayerGetProbesResponse(Dictionary<string, object> message)
    {
        try
        {
            var inMessage = (InMessage)message["inMessage"];
            var response = inMessage.GetData<PlayerGetProbesResponse>();
            var probes = response?.data?.probes;
            if (probes == null || probes.Length == 0) return;

            lock (_lock) _probes = probes;
            Plugin.LogDebug($"Captured {probes.Length} probe(s) from playerGetProbesResponse");

            TrySaveToDisk(probes);
            OnProbesUpdated?.Invoke();
        }
        catch (Exception e)
        {
            Plugin.LogError($"ProbeCache failed to parse playerGetProbesResponse: {e.Message}");
        }
    }

    private static void TryLoadFromDisk()
    {
        try
        {
            if (!File.Exists(CachePath)) return;
            var json = File.ReadAllText(CachePath);
            var probes = JsonSerializer.Deserialize<Probe[]>(json);
            if (probes != null) _probes = probes;
        }
        catch (Exception e)
        {
            Plugin.LogError($"ProbeCache load failed: {e.Message}");
        }
    }

    private static void TrySaveToDisk(Probe[] probes)
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
            var json = JsonSerializer.Serialize(probes);
            File.WriteAllText(CachePath, json);
        }
        catch (Exception e)
        {
            Plugin.LogError($"ProbeCache save failed: {e.Message}");
        }
    }
}
