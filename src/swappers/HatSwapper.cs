using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ToasterReskinLoader.swappers
{
    public static class HatSwapper
    {
        private static AssetBundle hatsBundle;
        private static bool loadFailed;

        // All available hats, keyed by hat ID. ID 0 is reserved for "None".
        private static readonly Dictionary<int, HatDefinition> hatDefinitions = new();
        private static readonly Dictionary<int, GameObject> loadedPrefabs = new();
        private static readonly FieldInfo BladeHandleField = typeof(Stick)
            .GetField("bladeHandle", BindingFlags.Instance | BindingFlags.NonPublic);

        // Track spawned hats per player (clientId -> spawned instance)
        private static readonly Dictionary<ulong, GameObject> spawnedHats = new();

        private const string BUNDLE_NAME = "hats";
        private const string ASSET_PREFIX = "assets/toaster's rink/hats/";
        private const float HAT_Y_OFFSET = 3f;       // for prefab hats (party hat)
        private const float FBX_Y_OFFSET = 3.7f;     // for FBX hats
        private const float DEFAULT_FBX_SCALE = 100f;
        private const float BIG_HEAD_SCALE = 2f;
        private const ulong LOCKER_ROOM_KEY = 0;

        public struct HatDefinition
        {
            public int Id;
            public string Name;
            public string AssetPath;
            public bool AttachToTorso;
            public bool AttachToStick;
            public bool IsPrefab;  // prefab vs raw FBX (affects default scale/offset/rotation)
            public float Scale;    // 0 = use default for type
            public float YOffset;  // 0 = use default for type
            public float ZOffset;  // extra Z offset (closer/further from body)
            public bool Emissive;  // enable emission using the material's base color
        }

        // Ordered list for UI dropdown.
        // The party hat prefab has baked-in scale from Unity, so it uses the old 20f.
        // Raw FBX files need a larger base scale.
        public static readonly List<HatDefinition> AllHats = new()
        {
            new HatDefinition { Id = 0,  Name = "None",           AssetPath = null },
            new HatDefinition { Id = 1,  Name = "Party Hat",      AssetPath = ASSET_PREFIX + "partyhat.prefab", IsPrefab = true, Scale = 20f },
            new HatDefinition { Id = 2,  Name = "Headphones",     AssetPath = ASSET_PREFIX + "headphones.fbx" },
            new HatDefinition { Id = 3,  Name = "Mini Head",      AssetPath = ASSET_PREFIX + "minihead.fbx" },
            new HatDefinition { Id = 4,  Name = "Crown",          AssetPath = ASSET_PREFIX + "crown.fbx" },
            new HatDefinition { Id = 5,  Name = "Bucket",         AssetPath = ASSET_PREFIX + "bucket.fbx" },
            new HatDefinition { Id = 6,  Name = "Toaster",        AssetPath = ASSET_PREFIX + "toaster.fbx" },
            new HatDefinition { Id = 7,  Name = "Halo",           AssetPath = ASSET_PREFIX + "halo.fbx", Emissive = true },
            new HatDefinition { Id = 8,  Name = "Sunglasses",     AssetPath = ASSET_PREFIX + "sunglasses.fbx" },
            new HatDefinition { Id = 9,  Name = "Melvin",         AssetPath = ASSET_PREFIX + "melvin.fbx" },
            new HatDefinition { Id = 10, Name = "Cone",           AssetPath = ASSET_PREFIX + "cone.fbx" },
            new HatDefinition { Id = 11, Name = "Plunger",        AssetPath = ASSET_PREFIX + "plunger.fbx" },
            new HatDefinition { Id = 12, Name = "Ears",           AssetPath = ASSET_PREFIX + "ears1.fbx" },
            new HatDefinition { Id = 13, Name = "Extra Helmet",   AssetPath = ASSET_PREFIX + "extra_helmet.fbx" },
            new HatDefinition { Id = 14, Name = "Backup Stick",   AssetPath = ASSET_PREFIX + "backup_stick_torso.fbx", AttachToTorso = true, Scale = 120f, YOffset = 1.7f, ZOffset = -0.1f },
            new HatDefinition { Id = 15, Name = "Deltapoint",     AssetPath = ASSET_PREFIX + "deltapoint.fbx", AttachToStick = true, Scale = 100f, YOffset = -0.25f, ZOffset = 0f },
        };

        public static void Initialize()
        {
            if (loadFailed) return;
            if (hatsBundle != null) return;

            try
            {
                string bundlePath = Path.Combine(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    BUNDLE_NAME);

                if (!File.Exists(bundlePath))
                {
                    Plugin.LogWarning($"[Hats] Asset bundle not found at: {bundlePath}");
                    loadFailed = true;
                    return;
                }

                hatsBundle = AssetBundle.LoadFromFile(bundlePath);
                if (hatsBundle == null)
                {
                    Plugin.LogError("[Hats] Failed to load asset bundle.");
                    loadFailed = true;
                    return;
                }

                // Build ID lookup and preload all prefabs
                foreach (var hat in AllHats)
                {
                    if (hat.AssetPath == null) continue;
                    hatDefinitions[hat.Id] = hat;

                    var prefab = hatsBundle.LoadAsset<GameObject>(hat.AssetPath);
                    if (prefab != null)
                    {
                        loadedPrefabs[hat.Id] = prefab;
                    }
                    else
                    {
                        Plugin.LogWarning($"[Hats] Failed to load prefab for '{hat.Name}' at '{hat.AssetPath}'");
                    }
                }

                Plugin.Log($"[Hats] Loaded {loadedPrefabs.Count}/{AllHats.Count - 1} hat prefabs.");
            }
            catch (Exception ex)
            {
                Plugin.LogError($"[Hats] Error loading asset bundle: {ex.Message}");
                loadFailed = true;
            }
        }

        /// <summary>
        /// Attach a hat to an in-game player by hat ID. Pass 0 or negative to remove.
        /// </summary>
        public static void AttachToPlayer(Player player, int hatId)
        {
            if (player?.PlayerBody?.PlayerMesh == null) return;
            // Skip local player for body/head items (camera clips), but allow stick items
            hatDefinitions.TryGetValue(hatId, out var defCheck);
            if (player.IsLocalPlayer && !defCheck.AttachToStick) return;
            if (player.Team is not (PlayerTeam.Blue or PlayerTeam.Red)) return;

            ulong clientId = player.OwnerClientId;
            RemoveFromPlayer(clientId);

            if (hatId <= 0) return;
            if (!EnsureInitialized()) return;
            if (!loadedPrefabs.TryGetValue(hatId, out var prefab)) return;
            hatDefinitions.TryGetValue(hatId, out var def);

            try
            {
                Transform attachPoint;
                if (def.AttachToStick)
                {
                    attachPoint = player.Stick?.StickMesh?.transform;
                }
                else if (def.AttachToTorso)
                {
                    attachPoint = player.PlayerBody.PlayerMesh.PlayerTorso?.transform;
                }
                else
                {
                    attachPoint = FindHelmetTransform(player.PlayerBody.PlayerMesh.PlayerHead);
                    if (Plugin.modSettings.BigHeadsEnabled)
                        ScaleHead(player.PlayerBody.PlayerMesh.PlayerHead.transform);
                }

                if (attachPoint == null) return;

                var hat = SpawnHatFromDef(prefab, attachPoint, def, player.Role);
                spawnedHats[clientId] = hat;
                Plugin.LogDebug($"[Hats] Attached '{GetHatName(hatId)}' to {player.Username.Value}");
            }
            catch (Exception ex)
            {
                Plugin.LogError($"[Hats] Error attaching to {player.Username.Value}: {ex.Message}");
            }
        }

        /// <summary>
        /// Attach a hat to the locker room model by hat ID. Pass 0 or negative to remove.
        /// </summary>
        public static void AttachToPlayerMesh(PlayerMesh playerMesh, int hatId)
        {
            RemoveFromPlayer(LOCKER_ROOM_KEY);

            if (hatId <= 0) return;
            if (playerMesh?.PlayerHead == null) return;
            if (!EnsureInitialized()) return;
            if (!loadedPrefabs.TryGetValue(hatId, out var prefab)) return;
            hatDefinitions.TryGetValue(hatId, out var def);

            try
            {
                Transform attachPoint;
                if (def.AttachToStick)
                {
                    // In locker room, find the StickMesh's blade collider as attach point
                    attachPoint = FindLockerRoomBlade();
                }
                else if (def.AttachToTorso)
                {
                    attachPoint = playerMesh.PlayerTorso?.transform;
                }
                else
                {
                    attachPoint = FindHelmetTransform(playerMesh.PlayerHead);
                    if (Plugin.modSettings.BigHeadsEnabled)
                        ScaleHead(playerMesh.PlayerHead.transform);
                }

                if (attachPoint == null) return;

                var hat = SpawnHatFromDef(prefab, attachPoint, def, SettingsManager.Role);
                spawnedHats[LOCKER_ROOM_KEY] = hat;
            }
            catch (Exception ex)
            {
                Plugin.LogError($"[Hats] Error attaching to locker room: {ex.Message}");
            }
        }

        /// <summary>
        /// Attach a hat to a PlayerMesh with a specific tracking key.
        /// Key 0 is used for the local player's locker room model.
        /// </summary>
        public static void AttachToPlayerMesh(PlayerMesh playerMesh, int hatId, ulong key)
        {
            RemoveFromPlayer(key);

            if (hatId <= 0) return;
            if (playerMesh?.PlayerHead == null) return;
            if (!EnsureInitialized()) return;
            if (!loadedPrefabs.TryGetValue(hatId, out var prefab)) return;
            hatDefinitions.TryGetValue(hatId, out var def);

            try
            {
                // For non-locker-room keys, skip stick-attached hats (they need a real stick reference)
                if (def.AttachToStick) return;

                Transform attachPoint;
                if (def.AttachToTorso)
                    attachPoint = playerMesh.PlayerTorso?.transform;
                else
                    attachPoint = FindHelmetTransform(playerMesh.PlayerHead);

                if (attachPoint == null) return;

                var hat = SpawnHatFromDef(prefab, attachPoint, def, PlayerRole.Attacker);
                spawnedHats[key] = hat;
            }
            catch (Exception ex)
            {
                Plugin.LogError($"[Hats] Error attaching hat (key={key}): {ex.Message}");
            }
        }

        public static void RemoveFromPlayerMesh()
        {
            RemoveFromPlayer(LOCKER_ROOM_KEY);
        }

        public static void RemoveFromPlayer(ulong clientId)
        {
            if (spawnedHats.TryGetValue(clientId, out var hat))
            {
                if (hat != null) UnityEngine.Object.Destroy(hat);
                spawnedHats.Remove(clientId);
            }
        }

        public static string GetHatName(int hatId)
        {
            foreach (var hat in AllHats)
                if (hat.Id == hatId) return hat.Name;
            return "None";
        }

        // ==================== INTERNAL ====================

        private static bool EnsureInitialized()
        {
            if (hatsBundle == null) Initialize();
            return hatsBundle != null && !loadFailed;
        }

        private static GameObject SpawnHatFromDef(GameObject prefab, Transform parent, HatDefinition def, PlayerRole role = PlayerRole.Attacker)
        {
            float scale = def.Scale > 0 ? def.Scale : DEFAULT_FBX_SCALE;
            float yOffset;
            if (def.AttachToStick)
                yOffset = role == PlayerRole.Goalie ? def.YOffset * 2f : def.YOffset;
            else
                yOffset = def.YOffset != 0 ? def.YOffset : (def.IsPrefab ? HAT_Y_OFFSET : FBX_Y_OFFSET);
            float yRot = def.IsPrefab ? 0f : 180f;
            Quaternion rotation;
            if (def.AttachToStick)
            {
                bool isLockerRoom = ChangingRoomHelper.IsInMainMenu();
                rotation = isLockerRoom
                    ? Quaternion.Euler(-90f, 0f, 0f)
                    : Quaternion.Euler(0f, 0f, 0f);
            }
            else
            {
                rotation = Quaternion.Euler(-90f, yRot, 0f);
            }

            var hat = UnityEngine.Object.Instantiate(prefab, parent);
            if (def.AttachToStick && !ChangingRoomHelper.IsInMainMenu())
                hat.transform.localPosition = new Vector3(0f, 0f, yOffset); // in-game: Z axis runs along shaft
            else
                hat.transform.localPosition = new Vector3(0f, yOffset, def.ZOffset);
            hat.transform.localRotation = rotation;
            hat.transform.localScale = InverseScale(parent.lossyScale) * scale;

            Plugin.LogDebug($"[Hats] Spawned '{def.Name}' at parent={parent.name} lossyScale={parent.lossyScale} localScale={hat.transform.localScale} pos={hat.transform.localPosition}");

            FixMaterials(hat, parent, def.Emissive);

            return hat;
        }

        private static Shader cachedUrpLitShader;

        private static void FixMaterials(GameObject hat, Transform referenceTransform, bool emissive = false)
        {
            if (cachedUrpLitShader == null)
            {
                cachedUrpLitShader = Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Universal Render Pipeline/Simple Lit");

                if (cachedUrpLitShader == null)
                {
                    var refRenderer = referenceTransform.GetComponent<Renderer>();
                    if (refRenderer?.material != null)
                        cachedUrpLitShader = refRenderer.material.shader;
                }
            }

            if (cachedUrpLitShader == null) return;

            foreach (var r in hat.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;

                    Texture mainTex = mats[i].mainTexture;
                    Color color = mats[i].color;

                    mats[i].shader = cachedUrpLitShader;

                    if (mainTex != null)
                    {
                        mats[i].SetTexture("_BaseMap", mainTex);
                        mats[i].SetTexture("_MainTex", mainTex);
                    }
                    mats[i].SetColor("_BaseColor", color);
                    mats[i].color = color;

                    if (emissive)
                    {
                        mats[i].EnableKeyword("_EMISSION");
                        mats[i].EnableKeyword("_EMISSIVE_COLOR_MAP");
                        Color emissionColor = color * 3f;
                        mats[i].SetColor("_EmissionColor", emissionColor);
                        mats[i].SetColor("_EmissiveColor", emissionColor);
                        if (mats[i].HasProperty("_EmissionMap"))
                            mats[i].SetTexture("_EmissionMap", null);
                        mats[i].globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    }
                }
                r.materials = mats;
            }
        }

        private static Vector3 InverseScale(Vector3 lossyScale)
        {
            return new Vector3(
                lossyScale.x != 0 ? 1f / lossyScale.x : 1f,
                lossyScale.y != 0 ? 1f / lossyScale.y : 1f,
                lossyScale.z != 0 ? 1f / lossyScale.z : 1f);
        }

        private static readonly Dictionary<Transform, Vector3> originalHeadScales = new();

        private static void ScaleHead(Transform headTransform)
        {
            if (headTransform == null) return;
            if (!originalHeadScales.ContainsKey(headTransform))
                originalHeadScales[headTransform] = headTransform.localScale;
            headTransform.localScale = originalHeadScales[headTransform] * BIG_HEAD_SCALE;
        }

        private static Transform FindBladeHandle(Stick stick)
        {
            if (stick == null || BladeHandleField == null) return null;
            var bladeHandle = BladeHandleField.GetValue(stick) as GameObject;
            return bladeHandle?.transform;
        }

        private static Transform FindLockerRoomBlade()
        {
            var stickMeshes = UnityEngine.Object.FindObjectsByType<StickMesh>(FindObjectsSortMode.None);
            foreach (var sm in stickMeshes)
            {
                if (sm.gameObject.activeInHierarchy && sm.BladeCollider != null)
                    return sm.BladeCollider.transform;
            }
            return null;
        }

        private static Transform FindHelmetTransform(PlayerHead playerHead)
        {
            if (playerHead == null) return null;

            foreach (var renderer in playerHead.GetComponentsInChildren<Renderer>())
            {
                string name = renderer.name.ToLower();
                if (name.Contains("helmet") && !name.Contains("cage") && !name.Contains("neck"))
                    return renderer.transform;
            }

            return playerHead.transform;
        }

        public static void ResetHeadScales()
        {
            foreach (var kvp in originalHeadScales)
                if (kvp.Key != null)
                    kvp.Key.localScale = kvp.Value;
        }

        public static void ClearHats()
        {
            foreach (var hat in spawnedHats.Values)
                if (hat != null) UnityEngine.Object.Destroy(hat);
            spawnedHats.Clear();
        }

        public static void Cleanup()
        {
            ClearHats();
            loadedPrefabs.Clear();
            hatDefinitions.Clear();
            if (hatsBundle != null)
            {
                hatsBundle.Unload(true);
                hatsBundle = null;
            }
            loadFailed = false;
        }
    }
}
