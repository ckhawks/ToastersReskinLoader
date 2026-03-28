using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ToasterReskinLoader.swappers;

public static class GenderSwapper
{
    private static AssetBundle genderBundle;
    private static GameObject femaleTorsoPrefab;
    private static GameObject maleTorsoPrefab;
    private static GameObject femaleGroinPrefab;
    private static bool loadFailed;

    private const string BUNDLE_NAME = "gender";
    private const string FEMALE_TORSO_PATH = "assets/toaster's rink/torsofemale.fbx";
    private const string MALE_TORSO_PATH = "assets/toaster's rink/torsomale.fbx";
    private const string FEMALE_GROIN_PATH = "assets/toaster's rink/groinfemale.fbx";

    // Track spawned replacement objects per player (clientId -> torso/groin instances)
    private static Dictionary<ulong, GameObject> spawnedTorsos = new Dictionary<ulong, GameObject>();
    private static Dictionary<ulong, GameObject> spawnedGroins = new Dictionary<ulong, GameObject>();

    // Skin tone + hair color: track assigned colors per player
    private static Dictionary<ulong, Color> assignedSkinTones = new Dictionary<ulong, Color>();
    private static Dictionary<ulong, Color> assignedHairColors = new Dictionary<ulong, Color>();
    private static System.Random colorRng = new System.Random();

    public static readonly Color[] SKIN_TONES = new Color[]
    {
        new Color(1.00f, 0.87f, 0.75f), // Light / fair
        new Color(0.96f, 0.80f, 0.69f), // Light-medium
        new Color(0.87f, 0.72f, 0.53f), // Medium / olive
        new Color(0.76f, 0.57f, 0.38f), // Medium-dark / tan
        new Color(0.60f, 0.40f, 0.24f), // Dark brown
        new Color(0.44f, 0.27f, 0.14f), // Deep brown
    };

    public static readonly Color[] HAIR_COLORS = new Color[]
    {
        new Color(0.10f, 0.07f, 0.05f), // Black
        new Color(0.30f, 0.16f, 0.07f), // Dark brown
        new Color(0.55f, 0.32f, 0.14f), // Medium brown
        new Color(0.65f, 0.45f, 0.25f), // Light brown
        new Color(0.85f, 0.65f, 0.30f), // Dirty blonde
        new Color(0.95f, 0.80f, 0.45f), // Blonde
        new Color(0.62f, 0.22f, 0.10f), // Auburn / red
        new Color(0.75f, 0.75f, 0.75f), // Grey
    };

    // Toggle state for the alternation test
    private static bool useFemale = true;
    private static GenderAlternator alternatorInstance;
    private static int toggleCount = 0;

    public static void Initialize()
    {
        if (loadFailed) return;
        if (femaleTorsoPrefab != null) return;

        try
        {
            string bundlePath = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                BUNDLE_NAME);

            if (!File.Exists(bundlePath))
            {
                Plugin.LogWarning($"[Gender] Asset bundle not found at: {bundlePath}");
                loadFailed = true;
                return;
            }

            genderBundle = AssetBundle.LoadFromFile(bundlePath);
            if (genderBundle == null)
            {
                Plugin.LogError("[Gender] Failed to load asset bundle.");
                loadFailed = true;
                return;
            }

            string[] allAssets = genderBundle.GetAllAssetNames();
            Plugin.Log($"[Gender] Bundle contains {allAssets.Length} assets:");
            foreach (string asset in allAssets)
                Plugin.Log($"[Gender]   - {asset}");

            femaleTorsoPrefab = genderBundle.LoadAsset<GameObject>(FEMALE_TORSO_PATH);
            maleTorsoPrefab = genderBundle.LoadAsset<GameObject>(MALE_TORSO_PATH);
            femaleGroinPrefab = genderBundle.LoadAsset<GameObject>(FEMALE_GROIN_PATH);

            if (femaleTorsoPrefab == null || femaleGroinPrefab == null)
            {
                Plugin.LogError("[Gender] Failed to load one or more prefabs from bundle.");
                loadFailed = true;
                return;
            }

            Plugin.Log($"[Gender] Loaded prefabs - femaleTorso: {femaleTorsoPrefab.name}, " +
                        $"maleTorso: {maleTorsoPrefab?.name ?? "null"}, " +
                        $"femaleGroin: {femaleGroinPrefab.name}");
        }
        catch (Exception ex)
        {
            Plugin.LogError($"[Gender] Error loading asset bundle: {ex.Message}\n{ex.StackTrace}");
            loadFailed = true;
        }
    }

    public static void ApplyToPlayer(Player player)
    {
        if (femaleTorsoPrefab == null)
        {
            Initialize();
            if (femaleTorsoPrefab == null) return;
        }

        if (player?.PlayerBody?.PlayerMesh == null)
            return;

        if (player.IsLocalPlayer)
            return;

        PlayerTeam team = player.Team;
        if (team is not (PlayerTeam.Blue or PlayerTeam.Red))
            return;

        try
        {
            ApplyForPlayer(player, useFemale);
        }
        catch (Exception ex)
        {
            Plugin.LogError($"[Gender] Error applying to {player.Username.Value}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// Apply body model to locker room preview (no Player object, just PlayerMesh).
    /// </summary>
    public static void ApplyToPlayerMesh(PlayerMesh playerMesh, bool female)
    {
        if (femaleTorsoPrefab == null)
        {
            Initialize();
            if (femaleTorsoPrefab == null) return;
        }

        if (playerMesh == null) return;

        try
        {
            const ulong LOCKER_ROOM_KEY = 0;
            ApplyForMesh(playerMesh, female, LOCKER_ROOM_KEY);
            Plugin.LogDebug($"[Gender] Applied {(female ? "female" : "male")} model to locker room");
        }
        catch (Exception ex)
        {
            Plugin.LogError($"[Gender] Error applying to locker room: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private static void ApplyForPlayer(Player player, bool female)
    {
        ulong clientId = player.OwnerClientId;
        PlayerMesh playerMesh = player.PlayerBody.PlayerMesh;
        ApplyForMesh(playerMesh, female, clientId);

        // Apply randomized skin tone to head
        ApplySkinTone(player);
    }

    private static void ApplyForMesh(PlayerMesh playerMesh, bool female, ulong key)
    {
        Transform torsoChild = FindChildByName(playerMesh.PlayerTorso?.transform, "torso");
        Transform groinChild = FindChildByName(playerMesh.PlayerGroin?.transform, "groin");

        if (torsoChild == null || groinChild == null)
        {
            Plugin.LogWarning("[Gender] Could not find torso/groin children");
            return;
        }

        MeshRenderer origTorsoRenderer = torsoChild.GetComponent<MeshRenderer>();
        MeshRenderer origGroinRenderer = groinChild.GetComponent<MeshRenderer>();

        DestroyForPlayer(key);

        if (female)
        {
            GameObject newTorso = SpawnReplacement(femaleTorsoPrefab, torsoChild, origTorsoRenderer);
            GameObject newGroin = SpawnReplacement(femaleGroinPrefab, groinChild, origGroinRenderer);

            spawnedTorsos[key] = newTorso;
            spawnedGroins[key] = newGroin;

            if (origTorsoRenderer != null) origTorsoRenderer.enabled = false;
            if (origGroinRenderer != null) origGroinRenderer.enabled = false;
        }
        else
        {
            if (maleTorsoPrefab != null)
            {
                GameObject newTorso = SpawnReplacement(maleTorsoPrefab, torsoChild, origTorsoRenderer);
                spawnedTorsos[key] = newTorso;
                if (origTorsoRenderer != null) origTorsoRenderer.enabled = false;
            }
            else
            {
                if (origTorsoRenderer != null) origTorsoRenderer.enabled = true;
            }

            if (origGroinRenderer != null) origGroinRenderer.enabled = true;
        }
    }

    private static void ApplySkinTone(Player player)
    {
        if (player?.PlayerBody?.PlayerMesh?.PlayerHead == null) return;

        ulong clientId = player.OwnerClientId;

        // Randomize each time for testing
        assignedSkinTones[clientId] = SKIN_TONES[colorRng.Next(SKIN_TONES.Length)];
        assignedHairColors[clientId] = HAIR_COLORS[colorRng.Next(HAIR_COLORS.Length)];

        Color skin = assignedSkinTones[clientId];
        Color hair = assignedHairColors[clientId];

        Transform playerHead = player.PlayerBody.PlayerMesh.PlayerHead.transform;

        var renderers = playerHead.GetComponentsInChildren<MeshRenderer>(true);
        foreach (var renderer in renderers)
        {
            string name = renderer.name.ToLower();
            Color color;
            if (name == "head")
                color = skin;
            else if (name.Contains("beard") || name.Contains("mustache"))
                color = hair;
            else
                continue;

            foreach (var mat in renderer.materials)
            {
                mat.color = color;
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", color);
            }
        }

        Plugin.LogDebug($"[Gender] Applied skin=({skin.r:F2},{skin.g:F2},{skin.b:F2}), hair=({hair.r:F2},{hair.g:F2},{hair.b:F2}) to {player.Username.Value}");
    }

    private static GameObject SpawnReplacement(GameObject prefab, Transform originalChild, MeshRenderer originalRenderer)
    {
        // Parent to the same parent as the original (the bone)
        Transform parent = originalChild.parent;
        GameObject instance = UnityEngine.Object.Instantiate(prefab, parent);

        // Match the original's local transform, scaled up (FBX import scale)
        instance.transform.localPosition = originalChild.localPosition;
        instance.transform.localRotation = originalChild.localRotation * Quaternion.Euler(-90f, 180f, 0f);
        instance.transform.localScale = originalChild.localScale * 250f;

        Plugin.LogDebug($"[Gender] Spawned replacement: origChild localScale={originalChild.localScale}, lossyScale={originalChild.lossyScale}, instance localScale={instance.transform.localScale}, lossyScale={instance.transform.lossyScale}");

        // Copy materials from the original renderer to the new one
        MeshRenderer newRenderer = instance.GetComponent<MeshRenderer>();
        if (newRenderer == null)
            newRenderer = instance.GetComponentInChildren<MeshRenderer>();

        if (newRenderer != null && originalRenderer != null)
        {
            // Copy the original's materials array (preserves shader + textures)
            newRenderer.materials = originalRenderer.materials;

            Plugin.LogDebug($"[Gender] Copied {originalRenderer.materials.Length} material(s) from {originalRenderer.name} to {newRenderer.name}");
            foreach (var mat in newRenderer.materials)
            {
                Plugin.LogDebug($"[Gender]   Material: {mat.name}, Shader: {mat.shader.name}");
            }
        }

        // Set the layer to match the original
        instance.layer = originalChild.gameObject.layer;
        foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = originalChild.gameObject.layer;

        return instance;
    }

    private static void DestroyForPlayer(ulong clientId)
    {
        if (spawnedTorsos.TryGetValue(clientId, out GameObject torso))
        {
            if (torso != null) UnityEngine.Object.Destroy(torso);
            spawnedTorsos.Remove(clientId);
        }
        if (spawnedGroins.TryGetValue(clientId, out GameObject groin))
        {
            if (groin != null) UnityEngine.Object.Destroy(groin);
            spawnedGroins.Remove(clientId);
        }
    }

    private static Transform FindChildByName(Transform parent, string childName)
    {
        if (parent == null) return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name.Equals(childName, StringComparison.OrdinalIgnoreCase))
                return child;
        }
        return null;
    }

    /// <summary>
    /// Toggles between male and female models on all spawned players.
    /// </summary>
    public static void ToggleAll()
    {
        useFemale = !useFemale;
        toggleCount++;
        if (toggleCount <= 10)
            Plugin.Log($"[Gender] Toggling to {(useFemale ? "female" : "male")} (toggle #{toggleCount})");

        try
        {
            var bluePlayers = PlayerManager.Instance.GetSpawnedPlayersByTeam(PlayerTeam.Blue);
            var redPlayers = PlayerManager.Instance.GetSpawnedPlayersByTeam(PlayerTeam.Red);

            foreach (var player in bluePlayers)
                ApplyToExistingPlayer(player);
            foreach (var player in redPlayers)
                ApplyToExistingPlayer(player);
        }
        catch (Exception ex)
        {
            Plugin.LogError($"[Gender] Error during toggle: {ex.Message}");
        }
    }

    private static void ApplyToExistingPlayer(Player player)
    {
        if (player?.PlayerBody?.PlayerMesh == null) return;
        if (player.IsLocalPlayer) return;

        try
        {
            ApplyForPlayer(player, useFemale);
        }
        catch (Exception ex)
        {
            Plugin.LogError($"[Gender] Error applying to existing player: {ex.Message}");
        }
    }

    public static void StartAlternating()
    {
        if (alternatorInstance != null) return;

        GameObject go = new GameObject("GenderAlternator");
        UnityEngine.Object.DontDestroyOnLoad(go);
        alternatorInstance = go.AddComponent<GenderAlternator>();
        Plugin.Log("[Gender] Alternation test started (toggling every 1s)");
    }

    public static void StopAlternating()
    {
        if (alternatorInstance != null)
        {
            UnityEngine.Object.Destroy(alternatorInstance.gameObject);
            alternatorInstance = null;
            Plugin.Log("[Gender] Alternation test stopped");
        }
    }

    public static void ClearCache()
    {
        // Destroy all spawned replacements
        foreach (var torso in spawnedTorsos.Values)
            if (torso != null) UnityEngine.Object.Destroy(torso);
        foreach (var groin in spawnedGroins.Values)
            if (groin != null) UnityEngine.Object.Destroy(groin);
        spawnedTorsos.Clear();
        spawnedGroins.Clear();
        assignedSkinTones.Clear();
        assignedHairColors.Clear();
    }

    public static void Cleanup()
    {
        StopAlternating();
        ClearCache();
        if (genderBundle != null)
        {
            genderBundle.Unload(false);
            genderBundle = null;
        }
        femaleTorsoPrefab = null;
        maleTorsoPrefab = null;
        femaleGroinPrefab = null;
        loadFailed = false;
    }
}

/// <summary>
/// Simple MonoBehaviour that toggles gender meshes every second for alignment testing.
/// </summary>
public class GenderAlternator : MonoBehaviour
{
    private float timer;
    private const float INTERVAL = 1f;

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= INTERVAL)
        {
            timer = 0f;
            GenderSwapper.ToggleAll();
        }
    }
}
