using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
// when the server allows it. Sets are fetched on connect, for each player whenever they join
// (so rejoining picks up changes), and lazily for any sender not seen yet. Sheets are cached on disk by their content-hash filename,
// so an emote is downloaded once, ever.
public class EmoteFeed : MonoBehaviour
{
    const string BASE_URL = "https://puckstats.io";
    const float BATCH_DELAY_S = 0.3f;
    const int MAX_BATCH = 64;
    const float ENDPOINT_WAIT_S = 15f;
    // Free unused emotes before adding more once the atlas is this full.
    const float COMPACT_AT_FILL = 0.8f;
    // A join can fire more than once in quick succession; one fetch covers it.
    const float REFRESH_MIN_INTERVAL_S = 10f;
    // Decoding sheets runs on the main thread; spread it over frames past this much per frame.
    const double DECODE_BUDGET_MS = 3.0;
    const int MAX_PARALLEL_LOADS = 4;
    // Loads landing within this window share one redraw.
    const float REDRAW_DELAY_S = 0.1f;

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
    readonly Dictionary<string, float> requestedAt = new Dictionary<string, float>();
    readonly HashSet<string> pendingBatch = new HashSet<string>();
    bool batchScheduled;
    readonly Dictionary<string, Texture2D> sheets = new Dictionary<string, Texture2D>();
    // Which sheet each emote in the atlas came from, so freeing it can release the sheet.
    readonly Dictionary<string, string> sheetOf = new Dictionary<string, string>();

    readonly Queue<RemoteEmote> loadQueue = new Queue<RemoteEmote>();
    // Emote ids queued or loading.
    readonly HashSet<string> queuedIds = new HashSet<string>();
    readonly HashSet<string> loadingSheets = new HashSet<string>();
    // Sheets that failed to download or decode; not retried until the next connection.
    readonly HashSet<string> failedSheets = new HashSet<string>();
    int activeLoaders;
    int budgetFrame = -1;
    double budgetUsedMs;

    readonly HashSet<string> redrawOwners = new HashSet<string>();
    bool redrawAll;
    bool redrawScheduled;
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

        feed.onSteamIdChanged = msg => feed.Refresh(msg.TryGetValue("newSteamId", out object id) ? id?.ToString() : null);
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
            return SlotOrLoad(serverEmote);
        }
        if (!AllowPersonal || senderSteamId == null)
        {
            return null;
        }
        Request(senderSteamId);
        return personal.TryGetValue(senderSteamId, out var set) && set.TryGetValue(name, out RemoteEmote emote)
            ? SlotOrLoad(emote)
            : null;
    }

    // The emote's slot, or null while it loads; the line is redrawn when it arrives.
    EmoteSlot SlotOrLoad(RemoteEmote emote)
    {
        EmoteSlot slot = atlas.Get(emote.Id);
        if (slot == null)
        {
            EnsureLoaded(emote);
        }
        return slot;
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

    // The local player's set as fetched, loaded into the atlas or not. False until it arrives.
    public bool TryGetLocalSet(out ICollection<RemoteEmote> emotes)
    {
        emotes = null;
        if (LocalSteamId == null || !personal.TryGetValue(LocalSteamId, out var set))
        {
            return false;
        }
        emotes = set.Values;
        return true;
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
        requestedAt.Clear();
        // Loads in flight finish on their own; drop what hasn't started.
        loadQueue.Clear();
        queuedIds.Clear();
        failedSheets.Clear();
        Compact();
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
        Compact();
        SetsChanged?.Invoke(null);
    }

    // Frees every emote nothing can show any more: not in a live chat line, the server set,
    // your own set, or the set of a player still here. Sets of players who left are dropped
    // too, so they are fetched fresh if the player comes back. Freed cells get reused.
    public void Compact()
    {
        var keep = new HashSet<string>();
        EmoteChat.CollectReferenced(keep);
        foreach (RemoteEmote emote in serverSet.Values)
        {
            keep.Add(emote.Id);
        }

        var present = new HashSet<string>();
        if (LocalSteamId != null)
        {
            present.Add(LocalSteamId);
        }
        try
        {
            foreach (Player player in PlayerManager.Instance?.GetPlayers() ?? new List<Player>())
            {
                present.Add(player.SteamId.Value.ToString());
            }
        }
        catch (Exception)
        {
            // Not connected: only the local player is present.
        }
        foreach (string steamId in personal.Keys.ToList())
        {
            if (present.Contains(steamId))
            {
                foreach (RemoteEmote emote in personal[steamId].Values)
                {
                    keep.Add(emote.Id);
                }
            }
            else
            {
                personal.Remove(steamId);
                requested.Remove(steamId);
                requestedAt.Remove(steamId);
            }
        }

        List<string> drop = atlas.Ids.Where(id => !keep.Contains(id)).ToList();
        foreach (string id in drop)
        {
            atlas.Free(id);
            animator?.Remove(id);
            if (sheetOf.TryGetValue(id, out string path))
            {
                sheetOf.Remove(id);
                if (!sheetOf.ContainsValue(path) && sheets.TryGetValue(path, out Texture2D sheet))
                {
                    sheets.Remove(path);
                    UnityEngine.Object.Destroy(sheet);
                }
            }
        }
        if (drop.Count > 0)
        {
            atlas.CommitFrees();
            Plugin.Log($"[Emotes] Freed {drop.Count} unused emotes; {atlas.Count} still loaded.");
        }
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

        // Server emotes are in the picker, so they load now. Lines redraw as each one lands;
        // this redraw covers allowPersonal changing.
        foreach (RemoteEmote emote in emotes)
        {
            EnsureLoaded(emote);
        }
        SetsChanged?.Invoke(null);
    }

    // ---- personal sets ----

    // Fetches the player's set even if it was fetched before. Called when a player joins, so
    // someone who changed their emotes picks them up by rejoining, and everyone else here
    // sees the change too.
    public void Refresh(string steamId)
    {
        if (steamId == null)
        {
            return;
        }
        if (requestedAt.TryGetValue(steamId, out float at) && Time.unscaledTime - at < REFRESH_MIN_INTERVAL_S)
        {
            return;
        }
        requested.Remove(steamId);
        Request(steamId);
    }

    // Fetches the player's set unless it was already fetched this connection.
    public void Request(string steamId)
    {
        if (steamId == null || !steamIdPattern.IsMatch(steamId) || !requested.Add(steamId))
        {
            return;
        }
        requestedAt[steamId] = Time.unscaledTime;
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
                        }
                    }
                    // Only your own emotes load up front (they're in your picker). Other
                    // players' load the first time one shows up in chat, via Resolve.
                    if (id == LocalSteamId)
                    {
                        foreach (RemoteEmote emote in set.Values)
                        {
                            EnsureLoaded(emote);
                        }
                    }
                    // A refresh that emptied the set still needs that player's lines redrawn.
                    bool hadEmotes = personal.TryGetValue(id, out var previous) && previous.Count > 0;
                    personal[id] = set;
                    if (set.Count > 0 || hadEmotes)
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

            // Redrawing these players' lines resolves their :name: codes, which queues the
            // emotes those lines actually use.
            if (changed.Count > 0)
            {
                SetsChanged?.Invoke(changed);
            }
        }
    }

    // ---- sheets ----

    // Queues the emote's sheet to load into the atlas, unless it's there, queued, or its
    // sheet already failed this connection. A few loaders run at once so a server's worth of
    // first-time downloads don't line up one behind another.
    void EnsureLoaded(RemoteEmote emote)
    {
        if (atlas.Get(emote.Id) != null || failedSheets.Contains(emote.Sheet) || !queuedIds.Add(emote.Id))
        {
            return;
        }
        loadQueue.Enqueue(emote);
        while (activeLoaders < MAX_PARALLEL_LOADS && activeLoaders < loadQueue.Count)
        {
            activeLoaders++;
            StartCoroutine(Loader());
        }
    }

    IEnumerator Loader()
    {
        while (loadQueue.Count > 0)
        {
            RemoteEmote emote = loadQueue.Dequeue();
            yield return LoadEmote(emote);
            queuedIds.Remove(emote.Id);
        }
        activeLoaders--;
    }

    IEnumerator LoadEmote(RemoteEmote emote)
    {
        if (atlas.Get(emote.Id) != null)
        {
            yield break;
        }
        if (!sheets.TryGetValue(emote.Sheet, out Texture2D sheet))
        {
            // Another loader may already be fetching this sheet; wait for it instead.
            if (loadingSheets.Contains(emote.Sheet))
            {
                while (loadingSheets.Contains(emote.Sheet))
                {
                    yield return null;
                }
            }
            else
            {
                loadingSheets.Add(emote.Sheet);
                yield return LoadSheet(emote.Sheet);
                loadingSheets.Remove(emote.Sheet);
                if (!sheets.ContainsKey(emote.Sheet))
                {
                    failedSheets.Add(emote.Sheet);
                }
            }
            if (!sheets.TryGetValue(emote.Sheet, out sheet))
            {
                yield break;
            }
        }
        int rows = (emote.Frames + emote.Columns - 1) / emote.Columns;
        if (sheet.width < emote.Columns * emote.FrameWidth || sheet.height < rows * emote.FrameHeight)
        {
            Plugin.LogWarning($"[Emotes] Sheet for :{emote.Name}: is smaller than its layout; skipped.");
            yield break;
        }
        if (atlas.FillRatio >= COMPACT_AT_FILL)
        {
            Compact();
        }
        EmoteSlot slot = atlas.Add(emote, sheet);
        if (slot != null)
        {
            sheetOf[emote.Id] = emote.Sheet;
            animator?.Add(emote, sheet, slot);
            MarkLoaded(emote.Id);
        }
    }

    // Collects who is affected by newly loaded emotes and redraws once for the lot, so a
    // burst of loads is one redraw rather than one per emote.
    void MarkLoaded(string id)
    {
        if (serverSet.Values.Any(e => e.Id == id))
        {
            redrawAll = true;
        }
        foreach (var pair in personal)
        {
            if (pair.Value.Values.Any(e => e.Id == id))
            {
                redrawOwners.Add(pair.Key);
            }
        }
        if (!redrawScheduled && (redrawAll || redrawOwners.Count > 0))
        {
            redrawScheduled = true;
            StartCoroutine(FlushRedraw());
        }
    }

    IEnumerator FlushRedraw()
    {
        yield return new WaitForSecondsRealtime(REDRAW_DELAY_S);
        redrawScheduled = false;
        if (redrawAll)
        {
            redrawAll = false;
            redrawOwners.Clear();
            SetsChanged?.Invoke(null);
        }
        else if (redrawOwners.Count > 0)
        {
            List<string> owners = redrawOwners.ToList();
            redrawOwners.Clear();
            SetsChanged?.Invoke(owners);
        }
    }

    // Decoding is main-thread work; loaders share this per-frame allowance.
    bool DecodeBudgetLeft()
    {
        if (Time.frameCount != budgetFrame)
        {
            budgetFrame = Time.frameCount;
            budgetUsedMs = 0;
        }
        return budgetUsedMs < DECODE_BUDGET_MS;
    }

    IEnumerator LoadSheet(string path)
    {
        string file = path.Substring(path.LastIndexOf('/') + 1);
        if (!sheetFilePattern.IsMatch(file))
        {
            yield break;
        }
        string cachePath = Path.Combine(cacheDir, file);

        // Disk reads and writes run on the thread pool so a slow disk can't stall a frame.
        byte[] bytes = null;
        if (File.Exists(cachePath))
        {
            Task<byte[]> read = Task.Run(() => File.ReadAllBytes(cachePath));
            while (!read.IsCompleted)
            {
                yield return null;
            }
            if (read.IsFaulted)
            {
                Plugin.LogWarning($"[Emotes] Cache read failed for {file}: {read.Exception?.GetBaseException().Message}");
            }
            else
            {
                bytes = read.Result;
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
            byte[] toWrite = bytes;
            string dir = cacheDir;
            Task.Run(() =>
            {
                // Write then rename, so a crash mid-write never leaves a truncated sheet that a
                // later read would take as cached. The temp name is unique because two
                // downloads of the same sheet can finish at once.
                string temp = cachePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    Directory.CreateDirectory(dir);
                    File.WriteAllBytes(temp, toWrite);
                    if (!File.Exists(cachePath))
                    {
                        File.Move(temp, cachePath);
                    }
                }
                catch (Exception e)
                {
                    Plugin.LogWarning($"[Emotes] Cache write failed for {file}: {e.Message}");
                }
                finally
                {
                    try
                    {
                        File.Delete(temp);
                    }
                    catch (Exception)
                    {
                        // Already moved into place, or never written.
                    }
                }
            });
        }

        while (!DecodeBudgetLeft())
        {
            yield return null;
        }
        var decodeTime = System.Diagnostics.Stopwatch.StartNew();
        Texture2D sheet = DecodeSheet(bytes);
        budgetUsedMs += decodeTime.Elapsed.TotalMilliseconds;
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
