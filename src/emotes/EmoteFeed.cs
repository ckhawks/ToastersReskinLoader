using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Steamworks;
using UnityEngine;
using UnityEngine.Networking;

namespace ToasterReskinLoader.emotes;

// Emote sets from the puckstats game feed, and the sprite sheets behind them.
//
//   GET /api/emotes/server?ip=&port=   the server's community set + whether personal emotes work
//   GET /api/emotes/sets?steamIds=     players' personal sets (approved emotes only)
//
// A :name: in a message from player X resolves to the server set first, then X's personal set
// when the server allows it. Sets are fetched on connect, for each player as they appear, and
// lazily for any sender not seen yet. Sheets are cached on disk by their content-hash filename,
// so an emote is downloaded once, ever.
public class EmoteFeed : MonoBehaviour
{
    const string BASE_URL = "https://puckstats.io";
    const float BATCH_DELAY_S = 0.3f;
    const int MAX_BATCH = 64;
    const float ENDPOINT_WAIT_S = 15f;

    static readonly Regex steamIdPattern = new Regex(@"^\d{17}$");
    static readonly Regex sheetFilePattern = new Regex(@"^[0-9a-f]{64}\.png$");

    public static EmoteFeed Instance { get; private set; }

    // Raised after sets change and their sheets are in the atlas. The argument lists the steam
    // ids whose personal sets changed, or is null when the server set changed (affects everyone).
    public static event Action<ICollection<string>> SetsChanged;

    public string LocalSteamId { get; private set; }
    public bool AllowPersonal { get; private set; } = true;
    public string CommunityName { get; private set; }

    EmoteAtlas atlas;
    EmoteAnimator animator;
    string cacheDir;

    Dictionary<string, RemoteEmote> serverSet = NewNameMap();
    readonly Dictionary<string, Dictionary<string, RemoteEmote>> personal = new Dictionary<string, Dictionary<string, RemoteEmote>>();
    readonly HashSet<string> requested = new HashSet<string>();
    readonly HashSet<string> pendingBatch = new HashSet<string>();
    bool batchScheduled;
    readonly Dictionary<string, Texture2D> sheets = new Dictionary<string, Texture2D>();
    int connectionGeneration;

    Action<Dictionary<string, object>> onSteamIdChanged;
    Action<Dictionary<string, object>> onClientStarted;
    Action<Dictionary<string, object>> onClientStopped;

    static Dictionary<string, RemoteEmote> NewNameMap()
    {
        return new Dictionary<string, RemoteEmote>(StringComparer.OrdinalIgnoreCase);
    }

    public static EmoteFeed Create(EmoteAtlas atlas, EmoteAnimator animator)
    {
        var go = new GameObject("TRLEmotesFeed");
        DontDestroyOnLoad(go);
        var feed = go.AddComponent<EmoteFeed>();
        Instance = feed;
        feed.atlas = atlas;
        feed.animator = animator;
        feed.cacheDir = Path.Combine(PathManager.GameRootFolder, "config", "ToasterReskinLoader", "emote-cache");
        try
        {
            feed.LocalSteamId = SteamUser.GetSteamID().ToString();
        }
        catch (Exception e)
        {
            Plugin.LogError($"[Emotes] No local steam id: {e.Message}");
        }

        feed.onSteamIdChanged = msg => feed.Request(msg.TryGetValue("newSteamId", out object id) ? id?.ToString() : null);
        feed.onClientStarted = _ => feed.OnClientStarted();
        feed.onClientStopped = _ => feed.OnClientStopped();
        EventManager.AddEventListener("Event_Everyone_OnPlayerSteamIdChanged", feed.onSteamIdChanged);
        EventManager.AddEventListener("Event_OnClientStarted", feed.onClientStarted);
        EventManager.AddEventListener("Event_OnClientStopped", feed.onClientStopped);

        feed.Request(feed.LocalSteamId);
        // Enabled mid-match: pick up the current server too.
        if (GlobalStateManager.ConnectionState.Connection != null)
        {
            feed.OnClientStarted();
        }
        return feed;
    }

    public void Destroy()
    {
        EventManager.RemoveEventListener("Event_Everyone_OnPlayerSteamIdChanged", onSteamIdChanged);
        EventManager.RemoveEventListener("Event_OnClientStarted", onClientStarted);
        EventManager.RemoveEventListener("Event_OnClientStopped", onClientStopped);
        foreach (Texture2D sheet in sheets.Values)
        {
            UnityEngine.Object.Destroy(sheet);
        }
        sheets.Clear();
        Instance = null;
        UnityEngine.Object.Destroy(gameObject);
    }

    // ---- resolution ----

    // The slot :name: from this sender shows, or null to leave it as text.
    public EmoteSlot Resolve(string senderSteamId, string name)
    {
        if (serverSet.TryGetValue(name, out RemoteEmote serverEmote))
        {
            return atlas.Get(serverEmote.Id);
        }
        if (!AllowPersonal || senderSteamId == null)
        {
            return null;
        }
        Request(senderSteamId);
        return personal.TryGetValue(senderSteamId, out var set) && set.TryGetValue(name, out RemoteEmote emote)
            ? atlas.Get(emote.Id)
            : null;
    }

    public IEnumerable<(string name, EmoteSlot slot)> ServerEmotes()
    {
        return Loaded(serverSet);
    }

    public IEnumerable<(string name, EmoteSlot slot)> LocalPersonalEmotes()
    {
        return LocalSteamId != null && personal.TryGetValue(LocalSteamId, out var set)
            ? Loaded(set)
            : Enumerable.Empty<(string, EmoteSlot)>();
    }

    IEnumerable<(string, EmoteSlot)> Loaded(Dictionary<string, RemoteEmote> set)
    {
        foreach (var pair in set)
        {
            EmoteSlot slot = atlas.Get(pair.Value.Id);
            if (slot != null)
            {
                yield return (pair.Value.Name, slot);
            }
        }
    }

    // ---- connection ----

    void OnClientStarted()
    {
        connectionGeneration++;
        // Sets can change between matches; start fresh but keep the loaded sheets.
        serverSet = NewNameMap();
        AllowPersonal = true;
        CommunityName = null;
        personal.Clear();
        requested.Clear();
        Request(LocalSteamId);
        StartCoroutine(FetchServerSet(connectionGeneration));
        SetsChanged?.Invoke(null);
    }

    void OnClientStopped()
    {
        connectionGeneration++;
        serverSet = NewNameMap();
        AllowPersonal = true;
        CommunityName = null;
        SetsChanged?.Invoke(null);
    }

    IEnumerator FetchServerSet(int generation)
    {
        float waited = 0f;
        EndPoint endPoint = null;
        while (waited < ENDPOINT_WAIT_S)
        {
            endPoint = GlobalStateManager.ConnectionState.Connection?.EndPoint;
            if (endPoint != null && !string.IsNullOrEmpty(endPoint.ipAddress))
            {
                break;
            }
            waited += 0.25f;
            yield return new WaitForSecondsRealtime(0.25f);
        }
        if (endPoint == null || string.IsNullOrEmpty(endPoint.ipAddress))
        {
            yield break;
        }

        string url = $"{BASE_URL}/api/emotes/server?ip={UnityWebRequest.EscapeURL(endPoint.ipAddress)}&port={endPoint.port}";
        using var request = UnityWebRequest.Get(url);
        request.timeout = 15;
        yield return request.SendWebRequest();
        if (generation != connectionGeneration)
        {
            yield break;
        }
        if (request.result != UnityWebRequest.Result.Success)
        {
            Plugin.LogWarning($"[Emotes] Server set request failed: {request.error}");
            yield break;
        }

        var emotes = new List<RemoteEmote>();
        try
        {
            JObject body = JObject.Parse(request.downloadHandler.text);
            AllowPersonal = (bool?)body["allowPersonal"] ?? true;
            CommunityName = (string)body["community"]?["name"];
            var set = NewNameMap();
            foreach (JToken token in body["emotes"] as JArray ?? new JArray())
            {
                RemoteEmote emote = RemoteEmote.FromJson(token);
                if (emote != null)
                {
                    set[emote.Name] = emote;
                    emotes.Add(emote);
                }
            }
            serverSet = set;
            Plugin.Log($"[Emotes] Server set: {set.Count} emotes from {CommunityName ?? "no community"}, personal emotes {(AllowPersonal ? "allowed" : "off")}.");
        }
        catch (Exception e)
        {
            Plugin.LogWarning($"[Emotes] Bad server set response: {e.Message}");
            yield break;
        }

        // Prefetch everyone already here; later arrivals come through the steam id event.
        try
        {
            foreach (Player player in PlayerManager.Instance.GetPlayers())
            {
                Request(player.SteamId.Value.ToString());
            }
        }
        catch (Exception e)
        {
            Plugin.LogWarning($"[Emotes] Could not list players: {e.Message}");
        }

        yield return LoadSheets(emotes);
        if (generation == connectionGeneration)
        {
            SetsChanged?.Invoke(null);
        }
    }

    // ---- personal sets ----

    public void Request(string steamId)
    {
        if (steamId == null || !steamIdPattern.IsMatch(steamId) || !requested.Add(steamId))
        {
            return;
        }
        pendingBatch.Add(steamId);
        if (!batchScheduled)
        {
            batchScheduled = true;
            StartCoroutine(SendBatch());
        }
    }

    IEnumerator SendBatch()
    {
        yield return new WaitForSecondsRealtime(BATCH_DELAY_S);
        batchScheduled = false;
        int generation = connectionGeneration;
        List<string> ids = pendingBatch.ToList();
        pendingBatch.Clear();

        for (int start = 0; start < ids.Count; start += MAX_BATCH)
        {
            List<string> chunk = ids.Skip(start).Take(MAX_BATCH).ToList();
            using var request = UnityWebRequest.Get($"{BASE_URL}/api/emotes/sets?steamIds={string.Join(",", chunk)}");
            request.timeout = 15;
            yield return request.SendWebRequest();
            if (generation != connectionGeneration)
            {
                yield break;
            }
            if (request.result != UnityWebRequest.Result.Success)
            {
                Plugin.LogWarning($"[Emotes] Player sets request failed: {request.error}");
                // Let a later message try again.
                foreach (string id in chunk)
                {
                    requested.Remove(id);
                }
                continue;
            }

            var emotes = new List<RemoteEmote>();
            var changed = new List<string>();
            try
            {
                JObject players = JObject.Parse(request.downloadHandler.text)["players"] as JObject ?? new JObject();
                foreach (string id in chunk)
                {
                    var set = NewNameMap();
                    foreach (JToken token in players[id]?["emotes"] as JArray ?? new JArray())
                    {
                        RemoteEmote emote = RemoteEmote.FromJson(token);
                        if (emote != null)
                        {
                            set[emote.Name] = emote;
                            emotes.Add(emote);
                        }
                    }
                    personal[id] = set;
                    if (set.Count > 0)
                    {
                        changed.Add(id);
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.LogWarning($"[Emotes] Bad player sets response: {e.Message}");
                continue;
            }

            yield return LoadSheets(emotes);
            if (generation == connectionGeneration && changed.Count > 0)
            {
                SetsChanged?.Invoke(changed);
            }
        }
    }

    // ---- sheets ----

    IEnumerator LoadSheets(List<RemoteEmote> emotes)
    {
        foreach (RemoteEmote emote in emotes)
        {
            if (atlas.Get(emote.Id) != null)
            {
                continue;
            }
            if (!sheets.TryGetValue(emote.Sheet, out Texture2D sheet))
            {
                yield return LoadSheet(emote.Sheet);
                if (!sheets.TryGetValue(emote.Sheet, out sheet))
                {
                    continue;
                }
            }
            int rows = (emote.Frames + emote.Columns - 1) / emote.Columns;
            if (sheet.width < emote.Columns * emote.FrameWidth || sheet.height < rows * emote.FrameHeight)
            {
                Plugin.LogWarning($"[Emotes] Sheet for :{emote.Name}: is smaller than its layout; skipped.");
                continue;
            }
            EmoteSlot slot = atlas.Add(emote, sheet);
            if (slot != null)
            {
                animator?.Add(emote, sheet, slot);
            }
        }
    }

    IEnumerator LoadSheet(string path)
    {
        string file = path.Substring(path.LastIndexOf('/') + 1);
        if (!sheetFilePattern.IsMatch(file))
        {
            yield break;
        }
        string cachePath = Path.Combine(cacheDir, file);

        byte[] bytes = null;
        if (File.Exists(cachePath))
        {
            try
            {
                bytes = File.ReadAllBytes(cachePath);
            }
            catch (Exception e)
            {
                Plugin.LogWarning($"[Emotes] Cache read failed for {file}: {e.Message}");
            }
        }
        if (bytes == null)
        {
            using var request = UnityWebRequest.Get(BASE_URL + path);
            request.timeout = 15;
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Plugin.LogWarning($"[Emotes] Sheet download failed for {file}: {request.error}");
                yield break;
            }
            bytes = request.downloadHandler.data;
            try
            {
                Directory.CreateDirectory(cacheDir);
                File.WriteAllBytes(cachePath, bytes);
            }
            catch (Exception e)
            {
                Plugin.LogWarning($"[Emotes] Cache write failed for {file}: {e.Message}");
            }
        }

        Texture2D sheet = DecodeSheet(bytes);
        if (sheet == null)
        {
            Plugin.LogWarning($"[Emotes] Could not decode sheet {file}.");
            yield break;
        }
        sheet.name = file;
        sheets[path] = sheet;
    }

    // RGBA32 so frames can be copied straight into the atlas with Graphics.CopyTexture.
    static Texture2D DecodeSheet(byte[] bytes)
    {
        var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!decoded.LoadImage(bytes))
        {
            UnityEngine.Object.Destroy(decoded);
            return null;
        }
        Texture2D sheet = decoded;
        if (decoded.format != TextureFormat.RGBA32)
        {
            sheet = new Texture2D(decoded.width, decoded.height, TextureFormat.RGBA32, false);
            sheet.SetPixels32(decoded.GetPixels32());
            UnityEngine.Object.Destroy(decoded);
        }
        sheet.filterMode = FilterMode.Bilinear;
        sheet.wrapMode = TextureWrapMode.Clamp;
        sheet.Apply(false, true);
        return sheet;
    }
}
