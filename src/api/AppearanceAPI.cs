using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Steamworks;
using ToasterReskinLoader.swappers;
using ToasterReskinLoader.ui.sections;
using UnityEngine;
using UnityEngine.Networking;

namespace ToasterReskinLoader.api;

/// <summary>
/// HTTP client for the puckstats player appearance API.
/// Handles Steam ticket auth for POSTs and batch GETs for other players.
/// </summary>
public static class AppearanceAPI
{
    private const string BASE_URL = "https://puckstats.io";
    private const float DEBOUNCE_DELAY = 2f;

    private static MonoBehaviour coroutineRunner;
    private static Coroutine pendingPost;
    private static AppearancePayload pendingPayload;
    private static Coroutine heartbeatCoroutine;
    private static Coroutine timeTrackingCoroutine;

    /// <summary>
    /// True once we've gotten at least one successful response from the backend.
    /// Starts null (unknown), set to true/false after the first fetch attempt completes.
    /// </summary>
    public static bool? BackendReachable { get; private set; }

    // Steam ticket — fetched once at init, reused for all POSTs.
    // Requesting a ticket fires a global Steamworks callback that the game's
    // BackendManagerController also listens on, causing a re-auth + UI reset.
    // By caching the ticket we only trigger that once.
    private static string cachedTicket;
    private static bool ticketRequested;
    private static Callback<GetTicketForWebApiResponse_t> ticketCallback;

    public static void Initialize(MonoBehaviour runner)
    {
        coroutineRunner = runner;
        ticketCallback = Callback<GetTicketForWebApiResponse_t>.Create(OnGetTicketForWebApiResponse);

        // Request the ticket immediately so it's ready before the first POST
        RequestTicket();

        // Fetch the local player's saved appearance and unlocks from the server
        FetchLocalPlayerAppearance();
        FetchLocalPlayerUnlocks();

        // Start the XP heartbeat loop and time tracking
        heartbeatCoroutine = coroutineRunner.StartCoroutine(HeartbeatLoop());
        timeTrackingCoroutine = coroutineRunner.StartCoroutine(TimeTrackingLoop());
    }

    public static void Cleanup()
    {
        if (heartbeatCoroutine != null && coroutineRunner != null)
            coroutineRunner.StopCoroutine(heartbeatCoroutine);
        if (timeTrackingCoroutine != null && coroutineRunner != null)
            coroutineRunner.StopCoroutine(timeTrackingCoroutine);
        heartbeatCoroutine = null;
        timeTrackingCoroutine = null;
        ticketCallback?.Dispose();
        ticketCallback = null;
        coroutineRunner = null;
        cachedTicket = null;
        ticketRequested = false;
        BackendReachable = null;
    }

    /// <summary>
    /// Queue a debounced POST of the player's appearance.
    /// Resets the timer each time it's called — only fires after DEBOUNCE_DELAY seconds of no changes.
    /// </summary>
    public static void QueuePostAppearance(int bodyType, Color skinTone, Color hairColor, int hatId, int hairId)
    {
        if (coroutineRunner == null) return;

        pendingPayload = new AppearancePayload
        {
            body_type = bodyType,
            skin_tone = new ColorPayload { r = skinTone.r, g = skinTone.g, b = skinTone.b },
            hair_color = new ColorPayload { r = hairColor.r, g = hairColor.g, b = hairColor.b },
            hat_id = hatId,
            hair_id = hairId,
        };

        // Reset debounce timer
        if (pendingPost != null)
            coroutineRunner.StopCoroutine(pendingPost);

        pendingPost = coroutineRunner.StartCoroutine(DebouncedPost());
    }

    private static IEnumerator DebouncedPost()
    {
        yield return new WaitForSeconds(DEBOUNCE_DELAY);

        var payload = pendingPayload;
        pendingPost = null;

        if (payload == null) yield break;

        // Wait for cached ticket if it hasn't arrived yet (max 10 seconds)
        float elapsed = 0f;
        while (cachedTicket == null && elapsed < 10f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (cachedTicket == null)
        {
            Plugin.LogError("[AppearanceAPI] No Steam ticket available");
            yield break;
        }

        payload.ticket = cachedTicket;
        string json = JsonConvert.SerializeObject(payload);

        using var request = new UnityWebRequest($"{BASE_URL}/api/appearance", "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            if (request.responseCode == 403)
                Plugin.LogWarning("[AppearanceAPI] POST rejected: hat not unlocked");
            else
                Plugin.LogError($"[AppearanceAPI] POST failed: {request.error} - {request.downloadHandler?.text}");
        }
        else
        {
            Plugin.LogDebug("[AppearanceAPI] Appearance saved successfully");
        }
    }

    /// <summary>
    /// Batch-GET appearances for a list of steam IDs.
    /// Calls onComplete with a dictionary of steamId -> AppearanceData (null = use defaults).
    /// </summary>
    public static void GetAppearances(List<string> steamIds, Action<Dictionary<string, AppearanceData>> onComplete)
    {
        if (coroutineRunner == null || steamIds == null || steamIds.Count == 0)
        {
            onComplete?.Invoke(new Dictionary<string, AppearanceData>());
            return;
        }

        coroutineRunner.StartCoroutine(GetAppearancesCoroutine(steamIds, onComplete));
    }

    private static IEnumerator GetAppearancesCoroutine(List<string> steamIds, Action<Dictionary<string, AppearanceData>> onComplete)
    {
        string ids = string.Join(",", steamIds);
        string url = $"{BASE_URL}/api/appearance?steamIds={ids}";

        using var request = UnityWebRequest.Get(url);
        yield return request.SendWebRequest();

        var result = new Dictionary<string, AppearanceData>();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Plugin.LogError($"[AppearanceAPI] GET failed: {request.error}");
            if (BackendReachable == null) BackendReachable = false;
            onComplete?.Invoke(result);
            yield break;
        }

        BackendReachable = true;

        try
        {
            string json = request.downloadHandler.text;
            var parsed = JObject.Parse(json);

            foreach (var kvp in parsed)
            {
                if (kvp.Value == null || kvp.Value.Type == JTokenType.Null)
                {
                    result[kvp.Key] = null;
                    continue;
                }

                var obj = (JObject)kvp.Value;
                var skinTone = (JObject)obj["skin_tone"];
                var hairColor = (JObject)obj["hair_color"];

                result[kvp.Key] = new AppearanceData
                {
                    bodyType = (int)obj["body_type"],
                    skinTone = new Color(
                        (float)skinTone["r"],
                        (float)skinTone["g"],
                        (float)skinTone["b"]
                    ),
                    hairColor = new Color(
                        (float)hairColor["r"],
                        (float)hairColor["g"],
                        (float)hairColor["b"]
                    ),
                    hatId = (int)obj["hat_id"],
                    hairId = (int)obj["hair_id"],
                };
            }
        }
        catch (Exception e)
        {
            Plugin.LogError($"[AppearanceAPI] Failed to parse response: {e.Message}");
        }

        onComplete?.Invoke(result);
    }

    private static void RequestTicket()
    {
        if (ticketRequested || cachedTicket != null) return;
        if (!SteamManager.IsInitialized) return;

        ticketRequested = true;
        Plugin.LogDebug("[AppearanceAPI] Requesting Steam ticket...");
        SteamUser.GetAuthTicketForWebApi("puckstats");
    }

    private static void OnGetTicketForWebApiResponse(GetTicketForWebApiResponse_t response)
    {
        // Only process if we're the ones who requested it
        if (cachedTicket != null) return;

        string ticket = BitConverter.ToString(response.m_rgubTicket, 0, (int)response.m_cubTicket)
            .Replace("-", string.Empty);

        cachedTicket = ticket;
        Plugin.LogDebug("[AppearanceAPI] Steam ticket cached");
    }

    // ==================== LOCAL PLAYER APPEARANCE ====================

    /// <summary>
    /// Fetches the local player's saved appearance from the server on startup.
    /// Updates PlayerCustomizationSection state and the locker room preview.
    /// </summary>
    private static void FetchLocalPlayerAppearance()
    {
        if (!SteamManager.IsInitialized) return;

        string localSteamId = SteamUser.GetSteamID().ToString();
        if (string.IsNullOrEmpty(localSteamId)) return;

        Plugin.Log($"[AppearanceAPI] Fetching local player appearance ({localSteamId})...");

        GetAppearances(new List<string> { localSteamId }, results =>
        {
            if (results.TryGetValue(localSteamId, out var data) && data != null)
            {
                Plugin.Log("[AppearanceAPI] Loaded saved appearance from server");
                OnLocalAppearanceLoaded?.Invoke(data);
            }
            else
            {
                Plugin.Log("[AppearanceAPI] No saved appearance found, using defaults");
            }
        });
    }

    /// <summary>
    /// Event fired when the local player's appearance is loaded from the server.
    /// PlayerCustomizationSection subscribes to this to update its state.
    /// </summary>
    public static event Action<AppearanceData> OnLocalAppearanceLoaded;

    // ==================== CLIENT-SIDE APPEARANCE CACHE ====================

    // Cache of fetched appearances by steam ID (persists across spawns within a session)
    private static readonly Dictionary<string, AppearanceData> appearanceCache = new();
    // Steam IDs we've already requested (to avoid duplicate fetches)
    private static readonly HashSet<string> requestedIds = new();
    // Whether we've done the initial bulk fetch for the current server
    private static bool initialFetchDone;

    /// <summary>
    /// Called when joining a server. Collects all player steam IDs and fetches their appearances.
    /// </summary>
    public static void FetchAllPlayersOnServer()
    {
        if (coroutineRunner == null) return;

        var steamIds = CollectAllPlayerSteamIds();
        if (steamIds.Count == 0) return;

        Plugin.Log($"[AppearanceAPI] Fetching appearances for {steamIds.Count} players...");
        initialFetchDone = true;

        // Mark all as requested
        foreach (var id in steamIds)
            requestedIds.Add(id);

        GetAppearances(steamIds, results =>
        {
            foreach (var kvp in results)
            {
                if (kvp.Value != null)
                    appearanceCache[kvp.Key] = kvp.Value;
            }
            Plugin.Log($"[AppearanceAPI] Cached {appearanceCache.Count} player appearances");

            // Apply to any already-spawned players
            ReapplyAllAppearances();
        });
    }

    /// <summary>
    /// Called when a player disconnects. Clears their cached appearance so it
    /// will be re-fetched if they rejoin (they may have changed it while away).
    /// </summary>
    public static void OnPlayerLeft(string steamId)
    {
        if (string.IsNullOrEmpty(steamId)) return;
        appearanceCache.Remove(steamId);
        requestedIds.Remove(steamId);
    }

    /// <summary>
    /// Called when a new player spawns. If we don't have their appearance cached, fetch it.
    /// </summary>
    /// <summary>
    /// Called when a player's stick is ready. Attaches stick-based apparel
    /// if the player has a cached appearance with a stick item.
    /// </summary>
    public static void OnStickReady(Player player)
    {
        if (player == null) return;

        // For the local player, use their selected hat
        if (player.IsLocalPlayer)
        {
            int localHatId = PlayerCustomizationSection.SelectedHatId;
            if (localHatId > 0)
            {
                var hatDef = HatSwapper.AllHats.Find(h => h.Id == localHatId);
                if (hatDef.AttachToStick)
                    HatSwapper.AttachToPlayer(player, localHatId);
            }
            return;
        }

        string steamId = player.SteamId.Value.ToString();
        if (appearanceCache.TryGetValue(steamId, out var data) && data != null)
        {
            if (data.hatId > 0 && Plugin.modSettings.ShowPersonalization && Plugin.modSettings.ShowOtherPlayersHats)
            {
                var hatDef = HatSwapper.AllHats.Find(h => h.Id == data.hatId);
                if (hatDef.AttachToStick)
                    HatSwapper.AttachToPlayer(player, data.hatId);
            }
        }
    }

    public static void OnPlayerSpawned(Player player)
    {
        if (player == null || player.IsLocalPlayer) return;

        string steamId = player.SteamId.Value.ToString();
        if (string.IsNullOrEmpty(steamId)) return;

        // If already cached, apply immediately
        if (appearanceCache.TryGetValue(steamId, out var cached))
        {
            ApplyAppearanceToPlayer(player, cached);
            return;
        }

        // If already requested, skip
        if (!requestedIds.Add(steamId)) return;

        // Fetch from API
        Plugin.LogDebug($"[AppearanceAPI] Fetching appearance for new player {steamId}");
        GetAppearances(new List<string> { steamId }, results =>
        {
            if (results.TryGetValue(steamId, out var data) && data != null)
            {
                appearanceCache[steamId] = data;
                // Re-find the player (they might have despawned during the fetch)
                ApplyToPlayerBySteamId(steamId);
            }
        });
    }

    /// <summary>
    /// Get cached appearance for a player. Returns null if not cached.
    /// </summary>
    public static AppearanceData GetCachedAppearance(string steamId)
    {
        appearanceCache.TryGetValue(steamId, out var data);
        return data;
    }

    /// <summary>
    /// Clear cache when returning to locker room.
    /// </summary>
    public static void ClearCache()
    {
        appearanceCache.Clear();
        requestedIds.Clear();
        initialFetchDone = false;
    }

    private static List<string> CollectAllPlayerSteamIds()
    {
        var ids = new List<string>();
        try
        {
            foreach (var team in new[] { PlayerTeam.Blue, PlayerTeam.Red })
            {
                var players = PlayerManager.Instance.GetSpawnedPlayersByTeam(team);
                foreach (var player in players)
                {
                    if (player.IsLocalPlayer) continue;
                    string id = player.SteamId.Value.ToString();
                    if (!string.IsNullOrEmpty(id))
                        ids.Add(id);
                }
            }
        }
        catch (Exception e)
        {
            Plugin.LogError($"[AppearanceAPI] Error collecting steam IDs: {e.Message}");
        }
        return ids;
    }

    /// <summary>
    /// Re-applies cached appearances to all spawned players.
    /// Call when display settings change (e.g. show/hide hats or skin tones).
    /// </summary>
    public static void ReapplyAllAppearances()
    {
        try
        {
            foreach (var team in new[] { PlayerTeam.Blue, PlayerTeam.Red })
            {
                var players = PlayerManager.Instance.GetSpawnedPlayersByTeam(team);
                foreach (var player in players)
                {
                    if (player.IsLocalPlayer) continue;
                    string steamId = player.SteamId.Value.ToString();
                    if (appearanceCache.TryGetValue(steamId, out var data))
                        ApplyAppearanceToPlayer(player, data);
                }
            }
        }
        catch (Exception e)
        {
            Plugin.LogError($"[AppearanceAPI] Error applying to spawned players: {e.Message}");
        }
    }

    private static void ApplyToPlayerBySteamId(string steamId)
    {
        try
        {
            foreach (var team in new[] { PlayerTeam.Blue, PlayerTeam.Red })
            {
                var players = PlayerManager.Instance.GetSpawnedPlayersByTeam(team);
                foreach (var player in players)
                {
                    if (player.SteamId.Value.ToString() == steamId)
                    {
                        if (appearanceCache.TryGetValue(steamId, out var data))
                            ApplyAppearanceToPlayer(player, data);
                        return;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Plugin.LogError($"[AppearanceAPI] Error applying to player {steamId}: {e.Message}");
        }
    }

    private static void ApplyAppearanceToPlayer(Player player, AppearanceData data)
    {
        if (player?.PlayerBody?.PlayerMesh == null) return;

        try
        {
            if (!Plugin.modSettings.ShowPersonalization)
            {
                ResetPlayerToDefaults(player);
                return;
            }

            GenderSwapper.ApplyToPlayer(player, data.bodyType == 1);

            Color skinTone = data.skinTone;
            if (!Plugin.modSettings.ShowNonNaturalSkinTones && !IsNaturalSkinTone(skinTone))
                skinTone = GetRandomNaturalTone(player.SteamId.Value.ToString());
            GenderSwapper.ApplyHeadColors(player.PlayerBody.PlayerMesh.PlayerHead, skinTone, data.hairColor);

            if (Plugin.modSettings.ShowOtherPlayersHats)
                HatSwapper.AttachToPlayer(player, data.hatId);

            Plugin.LogDebug($"[AppearanceAPI] Applied appearance to {player.Username.Value}: body={data.bodyType}, hat={data.hatId}");
        }
        catch (Exception e)
        {
            Plugin.LogError($"[AppearanceAPI] Error applying appearance: {e.Message}");
        }
    }

    // The game's default head/facial hair color (vanilla untinted white)
    private static readonly Color VANILLA_SKIN_COLOR = Color.white;
    private static readonly Color VANILLA_HAIR_COLOR = Color.white;

    private static void ResetPlayerToDefaults(Player player)
    {
        if (player?.PlayerBody?.PlayerMesh == null) return;

        // Reset to male body
        GenderSwapper.ApplyToPlayer(player, false);
        // Reset skin + facial hair to vanilla colors
        GenderSwapper.ApplyHeadColors(player.PlayerBody.PlayerMesh.PlayerHead, VANILLA_SKIN_COLOR, VANILLA_HAIR_COLOR);
        // Remove hat
        HatSwapper.RemoveFromPlayer(player.OwnerClientId);
    }

    /// <summary>
    /// Returns a consistent random natural skin tone for a given steam ID,
    /// so the same player always gets the same fallback tone within a session.
    /// </summary>
    private static Color GetRandomNaturalTone(string steamId)
    {
        int hash = steamId.GetHashCode();
        int index = ((hash % GenderSwapper.SKIN_TONES.Length) + GenderSwapper.SKIN_TONES.Length) % GenderSwapper.SKIN_TONES.Length;
        return GenderSwapper.SKIN_TONES[index];
    }

    private static bool IsNaturalSkinTone(Color c)
    {
        foreach (var natural in GenderSwapper.SKIN_TONES)
        {
            if (Mathf.Abs(c.r - natural.r) < 0.05f &&
                Mathf.Abs(c.g - natural.g) < 0.05f &&
                Mathf.Abs(c.b - natural.b) < 0.05f)
                return true;
        }
        return false;
    }

    // ==================== HEARTBEAT (XP) ====================

    private const float HEARTBEAT_INTERVAL = 300f; // 5 minutes

    // Accumulated time counters (reset after each heartbeat send)
    private static float inGameSeconds;
    private static float inWarmupSeconds;
    private static float inMenuSeconds;
    private static int inputCount;

    /// <summary>Call when the player provides meaningful input (stick move, shot, etc).</summary>
    public static void TrackInput()
    {
        inputCount++;
    }

    /// <summary>
    /// Runs every frame to accumulate time by game state.
    /// Unlike the PlayerInput patch (which only exists when spawned in-game),
    /// this coroutine runs in all scenes including the menu.
    /// </summary>
    private static IEnumerator TimeTrackingLoop()
    {
        while (true)
        {
            yield return null;
            float dt = Time.deltaTime;

            bool inGame = false;
            bool inWarmup = false;

            try
            {
                string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                if (scene != "locker_room")
                {
                    var gm = NetworkBehaviourSingleton<GameManager>.Instance;
                    if (gm != null)
                    {
                        var phase = gm.Phase;
                        inWarmup = phase == GamePhase.Warmup;
                        inGame = phase == GamePhase.Play || phase == GamePhase.FaceOff ||
                                 phase == GamePhase.BlueScore || phase == GamePhase.RedScore ||
                                 phase == GamePhase.Replay || phase == GamePhase.Intermission;
                    }
                }
            }
            catch { /* GameManager not available */ }

            if (inGame) inGameSeconds += dt;
            else if (inWarmup) inWarmupSeconds += dt;
            else inMenuSeconds += dt;
        }
    }

    private static IEnumerator HeartbeatLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(HEARTBEAT_INTERVAL);
            yield return SendHeartbeat();
        }
    }

    private static IEnumerator SendHeartbeat()
    {
        // Wait for ticket
        float elapsed = 0f;
        while (cachedTicket == null && elapsed < 10f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (cachedTicket == null) yield break;

        var payload = new HeartbeatPayload
        {
            ticket = cachedTicket,
            in_game_seconds = (int)inGameSeconds,
            in_warmup_seconds = (int)inWarmupSeconds,
            in_menu_seconds = (int)inMenuSeconds,
            input_count = inputCount,
            mod_version = Plugin.MOD_VERSION,
        };

        // Reset accumulators
        inGameSeconds = 0f;
        inWarmupSeconds = 0f;
        inMenuSeconds = 0f;
        inputCount = 0;

        string json = JsonConvert.SerializeObject(payload);
        using var request = new UnityWebRequest($"{BASE_URL}/api/appearance/xp", "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Plugin.LogError($"[AppearanceAPI] Heartbeat failed: {request.error}");
            yield break;
        }

        try
        {
            var resp = JObject.Parse(request.downloadHandler.text);
            int xp = (int)resp["xp"];
            int level = (int)resp["level"];
            int xpToNext = (int)(resp["xp_to_next_level"] ?? 0);
            bool leveledUp = (bool)(resp["leveled_up"] ?? false);

            UpdateXpState(xp, level, xpToNext);
            Plugin.LogDebug($"[AppearanceAPI] Heartbeat OK: xp={xp}, level={level}, leveled_up={leveledUp}");

            var newHats = resp["new_hats"] as JArray;
            if (newHats != null && newHats.Count > 0)
            {
                foreach (var hat in newHats)
                {
                    int hatId = (int)hat["id"];
                    string hatName = (string)hat["name"];
                    UnlockedHatIds.Add(hatId);
                    Plugin.Log($"[AppearanceAPI] New hat unlocked: {hatName} (id={hatId})");
                }

                string hatList = string.Join(", ", newHats.Select(h => (string)h["name"]));
                string body = $"TRL: Level {level} — You unlocked: {hatList}";
                MonoBehaviourSingleton<UIManager>.Instance?.ToastManager?.ShowToast(
                    "TRL", body, 5f);
            }

            OnUnlocksChanged?.Invoke();
        }
        catch (Exception e)
        {
            Plugin.LogError($"[AppearanceAPI] Failed to parse heartbeat response: {e.Message}");
        }
    }

    // ==================== UNLOCKS ====================

    /// <summary>Set of hat IDs the local player has unlocked. Hat 0 ("None") is always allowed.</summary>
    public static readonly HashSet<int> UnlockedHatIds = new() { 0 };
    public static int PlayerXP { get; private set; }
    public static int PlayerLevel { get; private set; }
    public static int XpToNextLevel { get; private set; }
    /// <summary>Total XP span of the current level. Used to compute progress bar fill.</summary>
    public static int LevelXpTotal { get; private set; }

    private static void UpdateXpState(int xp, int level, int xpToNext)
    {
        PlayerXP = xp;
        PlayerLevel = level;
        XpToNextLevel = xpToNext;
        // Level N→N+1 costs 50 + N*50 XP (matches server formula in xp.ts)
        LevelXpTotal = 50 + level * 50;
    }

    /// <summary>Fired when the unlock set changes (initial fetch, heartbeat, or code redeem).</summary>
    public static event Action OnUnlocksChanged;

    /// <summary>Manually fire the unlocks-changed event (e.g. after debug commands).</summary>
    public static void NotifyUnlocksChanged() => OnUnlocksChanged?.Invoke();

    /// <summary>Returns true if the local player owns the given hat.</summary>
    public static bool IsHatUnlocked(int hatId) => hatId == 0 || UnlockedHatIds.Contains(hatId);

    private static void FetchLocalPlayerUnlocks()
    {
        if (!SteamManager.IsInitialized || coroutineRunner == null) return;
        string steamId = SteamUser.GetSteamID().ToString();
        coroutineRunner.StartCoroutine(FetchUnlocksCoroutine(steamId));
    }

    private static IEnumerator FetchUnlocksCoroutine(string steamId)
    {
        string url = $"{BASE_URL}/api/appearance/unlocks?steamId={steamId}";
        using var request = UnityWebRequest.Get(url);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Plugin.LogError($"[AppearanceAPI] Unlocks fetch failed: {request.error}");
            if (BackendReachable == null) BackendReachable = false;
            yield break;
        }

        BackendReachable = true;

        try
        {
            var resp = JObject.Parse(request.downloadHandler.text);
            UpdateXpState((int)resp["xp"], (int)resp["level"], (int)(resp["xp_to_next_level"] ?? 0));

            var hats = resp["unlocked_hats"] as JArray;
            if (hats != null)
            {
                foreach (var hat in hats)
                    UnlockedHatIds.Add((int)hat["hat_id"]);
            }

            Plugin.Log($"[AppearanceAPI] Unlocks loaded: level={PlayerLevel}, xp={PlayerXP}, hats={UnlockedHatIds.Count}");
            OnUnlocksChanged?.Invoke();
        }
        catch (Exception e)
        {
            Plugin.LogError($"[AppearanceAPI] Failed to parse unlocks: {e.Message}");
        }
    }

    // ==================== CODE REDEMPTION ====================

    /// <summary>
    /// Redeems a code via the server API. Calls onResult with (success, message).
    /// </summary>
    public static void RedeemCode(string code, Action<bool, string> onResult)
    {
        if (coroutineRunner == null)
        {
            onResult?.Invoke(false, "Not initialized");
            return;
        }
        coroutineRunner.StartCoroutine(RedeemCodeCoroutine(code, onResult));
    }

    private static IEnumerator RedeemCodeCoroutine(string code, Action<bool, string> onResult)
    {
        // Wait for ticket
        float elapsed = 0f;
        while (cachedTicket == null && elapsed < 10f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (cachedTicket == null)
        {
            onResult?.Invoke(false, "No Steam ticket available");
            yield break;
        }

        var payload = new RedeemPayload { ticket = cachedTicket, code = code };
        string json = JsonConvert.SerializeObject(payload);

        using var request = new UnityWebRequest($"{BASE_URL}/api/appearance/redeem", "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        string body = request.downloadHandler?.text ?? "";

        if (request.responseCode == 400)
        {
            onResult?.Invoke(false, "Invalid code.");
            yield break;
        }
        if (request.responseCode == 409)
        {
            onResult?.Invoke(false, "Already unlocked.");
            yield break;
        }
        if (request.result != UnityWebRequest.Result.Success)
        {
            onResult?.Invoke(false, $"Error: {request.error}");
            yield break;
        }

        try
        {
            var resp = JObject.Parse(body);
            int hatId = (int)resp["hat_id"];
            string hatName = (string)resp["name"];
            UnlockedHatIds.Add(hatId);
            OnUnlocksChanged?.Invoke();
            onResult?.Invoke(true, $"Unlocked: {hatName}!");
        }
        catch (Exception e)
        {
            Plugin.LogError($"[AppearanceAPI] Failed to parse redeem response: {e.Message}");
            onResult?.Invoke(false, "Unexpected response");
        }
    }

    // ==================== SERIALIZATION ====================

    [Serializable]
    private class AppearancePayload
    {
        public string ticket;
        public int body_type;
        public ColorPayload skin_tone;
        public ColorPayload hair_color;
        public int hat_id;
        public int hair_id;
    }

    [Serializable]
    private class ColorPayload
    {
        public float r;
        public float g;
        public float b;
    }

    [Serializable]
    private class HeartbeatPayload
    {
        public string ticket;
        public int in_game_seconds;
        public int in_warmup_seconds;
        public int in_menu_seconds;
        public int input_count;
        public string mod_version;
    }

    [Serializable]
    private class RedeemPayload
    {
        public string ticket;
        public string code;
    }

    /// <summary>
    /// Appearance data received from the API for a player.
    /// Null from GetAppearances means the player has no saved appearance (use defaults).
    /// </summary>
    public class AppearanceData
    {
        public int bodyType;
        public Color skinTone;
        public Color hairColor;
        public int hatId;
        public int hairId;
    }
}
