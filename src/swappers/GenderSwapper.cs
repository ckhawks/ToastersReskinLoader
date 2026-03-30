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
            Plugin.LogDebug($"[Gender] Bundle contains {allAssets.Length} assets");
            foreach (string asset in allAssets)
                Plugin.LogDebug($"[Gender]   - {asset}");

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

    /// <summary>
    /// Apply body model to locker room preview (no Player object, just PlayerMesh).
    /// </summary>
    public static void ApplyToPlayerMesh(PlayerMesh playerMesh, bool female)
    {
        ApplyToPlayerMesh(playerMesh, female, 0);
    }

    /// <summary>
    /// Apply body model to a PlayerMesh with a specific tracking key.
    /// Key 0 is used for the local player's locker room model.
    /// </summary>
    public static void ApplyToPlayerMesh(PlayerMesh playerMesh, bool female, ulong key)
    {
        if (femaleTorsoPrefab == null)
        {
            Initialize();
            if (femaleTorsoPrefab == null) return;
        }

        if (playerMesh == null) return;

        try
        {
            ApplyForMesh(playerMesh, female, key);
            Plugin.LogDebug($"[Gender] Applied {(female ? "female" : "male")} model (key={key})");
        }
        catch (Exception ex)
        {
            Plugin.LogError($"[Gender] Error applying to PlayerMesh (key={key}): {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// Remove tracked gender swap objects for a specific key.
    /// </summary>
    public static void RemoveForKey(ulong key)
    {
        if (spawnedTorsos.TryGetValue(key, out var torso))
        {
            if (torso != null) UnityEngine.Object.Destroy(torso);
            spawnedTorsos.Remove(key);
        }
        if (spawnedGroins.TryGetValue(key, out var groin))
        {
            if (groin != null) UnityEngine.Object.Destroy(groin);
            spawnedGroins.Remove(key);
        }
    }

    /// <summary>
    /// Apply body model to an in-game player (uses their clientId as key for tracking spawned objects).
    /// </summary>
    public static void ApplyToPlayer(Player player, bool female)
    {
        if (femaleTorsoPrefab == null)
        {
            Initialize();
            if (femaleTorsoPrefab == null) return;
        }

        if (player?.PlayerBody?.PlayerMesh == null) return;

        try
        {
            ulong clientId = player.OwnerClientId;
            ApplyForMesh(player.PlayerBody.PlayerMesh, female, clientId);
            Plugin.LogDebug($"[Gender] Applied {(female ? "female" : "male")} model to {player.Username.Value}");
        }
        catch (Exception ex)
        {
            Plugin.LogError($"[Gender] Error applying to player: {ex.Message}\n{ex.StackTrace}");
        }
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

    /// <summary>
    /// Applies skin tone and facial hair color to a player's head renderers.
    /// Shared by both the locker room preview and in-game appearance application.
    /// </summary>
    public static void ApplyHeadColors(PlayerHead playerHead, Color skinTone, Color hairColor)
    {
        if (playerHead == null) return;

        var renderers = playerHead.GetComponentsInChildren<MeshRenderer>(true);
        foreach (var renderer in renderers)
        {
            string name = renderer.name.ToLower();
            Color color;
            if (name == "head")
                color = skinTone;
            else if (name.Contains("beard") || name.Contains("mustache"))
                color = hairColor;
            else
                continue;

            foreach (var mat in renderer.materials)
            {
                mat.color = color;
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("baseColorFactor"))
                    mat.SetColor("baseColorFactor", color);
            }
        }
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

    public static void ClearCache()
    {
        // Destroy all spawned replacements
        foreach (var torso in spawnedTorsos.Values)
            if (torso != null) UnityEngine.Object.Destroy(torso);
        foreach (var groin in spawnedGroins.Values)
            if (groin != null) UnityEngine.Object.Destroy(groin);
        spawnedTorsos.Clear();
        spawnedGroins.Clear();
    }

    public static void Cleanup()
    {
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
