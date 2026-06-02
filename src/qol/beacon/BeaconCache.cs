using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using UnityEngine;

using ToasterReskinLoader.core;

namespace ToasterReskinLoader.qol.beacon;

/// Caches the most recent Beacon[] observed from the game's "playerBeaconRttRequest"
/// websocket message and persists it to disk so the panel can ping on cold start.
public static class BeaconCache
{
    private static readonly string CacheDir = Path.Combine(
        Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "reskinprofiles");
    private static readonly string CachePath = Path.Combine(CacheDir, "beacons.json");

    private static Beacon[] _beacons = Array.Empty<Beacon>();
    private static readonly object _lock = new object();

    public static event Action OnBeaconsUpdated;

    public static Beacon[] GetBeacons()
    {
        lock (_lock) return (Beacon[])_beacons.Clone();
    }

    public static bool HasBeacons
    {
        get { lock (_lock) return _beacons.Length > 0; }
    }

    public static void Initialize()
    {
        TryLoadFromDisk();
        WebSocketManager.AddMessageListener("playerBeaconRttRequest", OnPlayerBeaconRttRequest);
        Plugin.Log($"BeaconCache initialized (cached {_beacons.Length} beacon(s))");
    }

    public static void Shutdown()
    {
        WebSocketManager.RemoveMessageListener("playerBeaconRttRequest", OnPlayerBeaconRttRequest);
    }

    private static void OnPlayerBeaconRttRequest(Dictionary<string, object> message)
    {
        try
        {
            var inMessage = (InMessage)message["inMessage"];
            var beacons = inMessage.GetData<Beacon[]>();
            if (beacons == null || beacons.Length == 0) return;

            lock (_lock) _beacons = beacons;
            Plugin.LogDebug($"Captured {beacons.Length} beacon(s) from playerBeaconRttRequest");

            TrySaveToDisk(beacons);
            OnBeaconsUpdated?.Invoke();
        }
        catch (Exception e)
        {
            Plugin.LogError($"BeaconCache failed to parse playerBeaconRttRequest: {e.Message}");
        }
    }

    private static void TryLoadFromDisk()
    {
        try
        {
            if (!File.Exists(CachePath)) return;
            var json = File.ReadAllText(CachePath);
            var beacons = JsonSerializer.Deserialize<Beacon[]>(json);
            if (beacons != null) _beacons = beacons;
        }
        catch (Exception e)
        {
            Plugin.LogError($"BeaconCache load failed: {e.Message}");
        }
    }

    private static void TrySaveToDisk(Beacon[] beacons)
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
            var json = JsonSerializer.Serialize(beacons);
            File.WriteAllText(CachePath, json);
        }
        catch (Exception e)
        {
            Plugin.LogError($"BeaconCache save failed: {e.Message}");
        }
    }
}
