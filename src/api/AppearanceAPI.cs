using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Steamworks;
using ToasterReskinLoader.swappers;
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

        // Fetch the local player's saved appearance from the server
        FetchLocalPlayerAppearance();
    }

    public static void Cleanup()
    {
        ticketCallback?.Dispose();
        ticketCallback = null;
        coroutineRunner = null;
        cachedTicket = null;
        ticketRequested = false;
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
            onComplete?.Invoke(result);
            yield break;
        }

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
