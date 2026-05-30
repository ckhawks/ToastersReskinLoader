using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using UnityEngine;

namespace ToasterReskinLoader.qol.serverbrowser;

/// Persists the last successful ServerPreviewData per endpoint so the
/// server browser can render rows immediately on open (with maxPlayers
/// known and cached ping used for initial sort order) instead of showing
/// a wave of "?/?" rows that shuffle as live pings trickle in.
///
/// Eviction is driven by ping failure (vanilla calling SetServerPreviewData
/// with null) — the master server's endpoint list is the source of truth
/// for "what exists," so dead-server cache entries simply stop being
/// referenced when the master stops returning them.
public static class ServerPreviewCache
{
    public sealed class CachedPreview
    {
        public string name { get; set; } = "";
        public int maxPlayers { get; set; }
        public bool isPasswordProtected { get; set; }
        public string[] clientRequiredModIds { get; set; } = Array.Empty<string>();
        public int lastPingMs { get; set; }
        public long lastSeenUnix { get; set; }
    }

    private static readonly string CacheDir = Path.Combine(
        Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "reskinprofiles");
    private static readonly string CachePath = Path.Combine(CacheDir, "server_previews.json");

    private static Dictionary<string, CachedPreview> _entries = new();
    private static readonly object _lock = new();
    private static bool _dirty;

    public static void Initialize()
    {
        TryLoadFromDisk();
        Plugin.Log($"ServerPreviewCache initialized (cached {_entries.Count} server(s))");
    }

    private static string Key(string ip, ushort port) => ip + ":" + port;
    public static string Key(EndPoint ep) => Key(ep.ipAddress, ep.port);

    public static int Count
    {
        get { lock (_lock) return _entries.Count; }
    }

    public static bool TryGet(EndPoint ep, out CachedPreview preview)
    {
        lock (_lock) return _entries.TryGetValue(Key(ep), out preview);
    }

    public static void Upsert(EndPoint ep, ServerPreviewData data)
    {
        if (data == null) return;
        lock (_lock)
        {
            _entries[Key(ep)] = new CachedPreview
            {
                name = data.name ?? "",
                maxPlayers = data.maxPlayers,
                isPasswordProtected = data.isPasswordProtected,
                clientRequiredModIds = data.clientRequiredModIds ?? Array.Empty<string>(),
                lastPingMs = data.ping,
                lastSeenUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
            _dirty = true;
        }
    }

    public static void Evict(EndPoint ep)
    {
        lock (_lock)
        {
            if (_entries.Remove(Key(ep))) _dirty = true;
        }
    }

    /// Drop any cached entry whose key is not present in <paramref name="keepKeys"/>.
    /// Used after each master-list refresh wave to garbage-collect servers
    /// that the master server is no longer advertising. Returns the number
    /// of entries removed.
    public static int RetainOnly(HashSet<string> keepKeys)
    {
        if (keepKeys == null) return 0;
        lock (_lock)
        {
            var toRemove = new List<string>();
            foreach (var k in _entries.Keys)
            {
                if (!keepKeys.Contains(k)) toRemove.Add(k);
            }
            foreach (var k in toRemove) _entries.Remove(k);
            if (toRemove.Count > 0) _dirty = true;
            return toRemove.Count;
        }
    }

    /// Write to disk if anything has changed since the last flush. Intended
    /// to be called once after a refresh wave settles, not per-server.
    public static void FlushIfDirty()
    {
        bool needWrite;
        Dictionary<string, CachedPreview> snapshot;
        lock (_lock)
        {
            needWrite = _dirty;
            if (!needWrite) return;
            snapshot = new Dictionary<string, CachedPreview>(_entries);
            _dirty = false;
        }
        TrySaveToDisk(snapshot);
    }

    private static void TryLoadFromDisk()
    {
        try
        {
            if (!File.Exists(CachePath)) return;
            var json = File.ReadAllText(CachePath);
            var loaded = JsonSerializer.Deserialize<Dictionary<string, CachedPreview>>(json);
            if (loaded != null) _entries = loaded;
        }
        catch (Exception e)
        {
            Plugin.LogError($"ServerPreviewCache load failed: {e.Message}");
        }
    }

    private static void TrySaveToDisk(Dictionary<string, CachedPreview> snapshot)
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
            var json = JsonSerializer.Serialize(snapshot);
            File.WriteAllText(CachePath, json);
        }
        catch (Exception e)
        {
            Plugin.LogError($"ServerPreviewCache save failed: {e.Message}");
        }
    }
}
