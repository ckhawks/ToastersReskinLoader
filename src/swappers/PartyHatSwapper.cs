using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ToasterReskinLoader.swappers
{
    public static class PartyHatSwapper
    {
        private static AssetBundle partyHatBundle;
        private static GameObject partyHatPrefab;
        private static bool loadFailed;

        // Track spawned hats so we don't double-spawn and can clean up
        private static Dictionary<ulong, GameObject> spawnedHats = new Dictionary<ulong, GameObject>();

        private const string BUNDLE_NAME = "partyhat";
        private const string PREFAB_PATH = "assets/toaster's rink/partyhat.prefab";
        private const float HAT_Y_OFFSET = 3f;
        private const float HAT_SCALE = 20f;
        private const float BIG_HEAD_SCALE = 2f;

        public static void Initialize()
        {
            if (loadFailed) return;
            if (partyHatPrefab != null) return;

            try
            {
                string bundlePath = Path.Combine(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    BUNDLE_NAME);

                if (!File.Exists(bundlePath))
                {
                    Plugin.LogWarning($"[PartyHat] Asset bundle not found at: {bundlePath}");
                    loadFailed = true;
                    return;
                }

                partyHatBundle = AssetBundle.LoadFromFile(bundlePath);
                if (partyHatBundle == null)
                {
                    Plugin.LogError("[PartyHat] Failed to load asset bundle.");
                    loadFailed = true;
                    return;
                }

                partyHatPrefab = partyHatBundle.LoadAsset<GameObject>(PREFAB_PATH);
                if (partyHatPrefab == null)
                {
                    Plugin.LogError($"[PartyHat] Prefab '{PREFAB_PATH}' not found in bundle.");
                    loadFailed = true;
                    return;
                }

                Plugin.Log("[PartyHat] Asset bundle loaded successfully.");
            }
            catch (Exception ex)
            {
                Plugin.LogError($"[PartyHat] Error loading asset bundle: {ex.Message}");
                loadFailed = true;
            }
        }

        public static void AttachToPlayer(Player player)
        {
            if (partyHatPrefab == null)
            {
                Initialize();
                if (partyHatPrefab == null) return;
            }

            if (player?.PlayerBody?.PlayerMesh?.PlayerHead == null)
                return;

            if (player.IsLocalPlayer)
                return;

            PlayerTeam team = player.Team;
            if (team is not (PlayerTeam.Blue or PlayerTeam.Red))
                return;

            ulong clientId = player.OwnerClientId;

            RemoveFromPlayer(clientId);

            try
            {
                // Find the helmet renderer to use as attachment point
                Transform helmetTransform = FindHelmetTransform(player.PlayerBody.PlayerMesh.PlayerHead);
                if (helmetTransform == null)
                {
                    Plugin.LogDebug($"[PartyHat] No helmet transform found for {player.Username.Value}");
                    return;
                }

                if (Plugin.modSettings.BigHeadsEnabled)
                    ScaleHead(player.PlayerBody.PlayerMesh.PlayerHead.transform);

                GameObject hat = SpawnHat(helmetTransform);
                spawnedHats[clientId] = hat;
                Plugin.LogDebug($"[PartyHat] Attached to {player.Username.Value}");
            }
            catch (Exception ex)
            {
                Plugin.LogError($"[PartyHat] Error attaching to {player.Username.Value}: {ex.Message}");
            }
        }

        private const ulong LOCKER_ROOM_KEY = 0;

        public static void RemoveFromPlayerMesh()
        {
            RemoveFromPlayer(LOCKER_ROOM_KEY);
        }

        public static void RemoveFromPlayer(ulong clientId)
        {
            if (spawnedHats.TryGetValue(clientId, out GameObject existingHat))
            {
                if (existingHat != null)
                    UnityEngine.Object.Destroy(existingHat);
                spawnedHats.Remove(clientId);
            }
        }

        public static void AttachToPlayerMesh(PlayerMesh playerMesh)
        {
            if (partyHatPrefab == null)
            {
                Initialize();
                if (partyHatPrefab == null) return;
            }

            if (playerMesh?.PlayerHead == null)
                return;

            RemoveFromPlayer(LOCKER_ROOM_KEY);

            try
            {
                Transform helmetTransform = FindHelmetTransform(playerMesh.PlayerHead);
                if (helmetTransform == null) return;

                if (Plugin.modSettings.BigHeadsEnabled)
                    ScaleHead(playerMesh.PlayerHead.transform);

                GameObject hat = SpawnHat(helmetTransform);
                spawnedHats[LOCKER_ROOM_KEY] = hat;
            }
            catch (Exception ex)
            {
                Plugin.LogError($"[PartyHat] Error attaching to locker room player: {ex.Message}");
            }
        }

        private static Dictionary<Transform, Vector3> originalHeadScales = new Dictionary<Transform, Vector3>();

        private static void ScaleHead(Transform headTransform)
        {
            if (headTransform == null) return;
            if (!originalHeadScales.ContainsKey(headTransform))
                originalHeadScales[headTransform] = headTransform.localScale;
            headTransform.localScale = originalHeadScales[headTransform] * BIG_HEAD_SCALE;
        }

        private static GameObject SpawnHat(Transform helmetTransform)
        {
            GameObject hat = UnityEngine.Object.Instantiate(partyHatPrefab, helmetTransform);
            hat.transform.localPosition = new Vector3(0f, HAT_Y_OFFSET, 0f);
            hat.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            hat.transform.localScale = InverseScale(helmetTransform.lossyScale) * HAT_SCALE;

            // Fix materials: the prefab uses built-in pipeline shaders but the game uses URP.
            // Grab the URP shader from the helmet's own material and apply it to the hat.
            FixMaterials(hat, helmetTransform);

            return hat;
        }

        private static Shader cachedUrpLitShader;

        private static Shader GetUrpLitShader(Transform helmetTransform)
        {
            if (cachedUrpLitShader != null) return cachedUrpLitShader;

            // Try to find URP Lit shader by name
            cachedUrpLitShader = Shader.Find("Universal Render Pipeline/Lit");
            if (cachedUrpLitShader != null) return cachedUrpLitShader;

            // Fallback: try Simple Lit
            cachedUrpLitShader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (cachedUrpLitShader != null) return cachedUrpLitShader;

            // Last resort: grab from the helmet renderer
            var helmetRenderer = helmetTransform.GetComponent<Renderer>();
            if (helmetRenderer != null && helmetRenderer.material != null)
                cachedUrpLitShader = helmetRenderer.material.shader;

            return cachedUrpLitShader;
        }

        private static void FixMaterials(GameObject hat, Transform helmetTransform)
        {
            Shader urpShader = GetUrpLitShader(helmetTransform);
            if (urpShader == null)
            {
                Plugin.LogError("[PartyHat] Could not find a URP shader.");
                return;
            }
            Plugin.Log($"[PartyHat] Using shader: {urpShader.name}");

            var hatRenderers = hat.GetComponentsInChildren<Renderer>(true);
            foreach (var r in hatRenderers)
            {
                // Fix ALL materials on each renderer
                var mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;

                    Texture mainTex = mats[i].mainTexture;
                    Color color = mats[i].color;

                    mats[i].shader = urpShader;

                    if (mainTex != null)
                    {
                        mats[i].SetTexture("_BaseMap", mainTex);
                        mats[i].SetTexture("_MainTex", mainTex);
                    }
                    mats[i].SetColor("_BaseColor", color);
                    mats[i].color = color;

                    Plugin.LogDebug($"[PartyHat] Fixed material {i} on {r.name}: shader={urpShader.name}");
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

        private static Transform FindHelmetTransform(PlayerHead playerHead)
        {
            if (playerHead == null) return null;

            var renderers = playerHead.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                string name = renderer.name.ToLower();
                if (name.Contains("helmet") && !name.Contains("cage") && !name.Contains("neck"))
                {
                    return renderer.transform;
                }
            }

            // Fallback to the head itself
            return playerHead.transform;
        }

        public static void ResetHeadScales()
        {
            foreach (var kvp in originalHeadScales)
            {
                if (kvp.Key != null)
                    kvp.Key.localScale = kvp.Value;
            }
        }

        public static void ReapplyAll()
        {
            // Re-trigger for all spawned players
            var players = PlayerManager.Instance.GetSpawnedPlayersByTeam(PlayerTeam.Blue);
            players.AddRange(PlayerManager.Instance.GetSpawnedPlayersByTeam(PlayerTeam.Red));
            foreach (var player in players)
            {
                AttachToPlayer(player);
            }

            // Re-trigger for locker room if applicable
            if (ChangingRoomHelper.IsInMainMenu())
            {
                var playerMesh = ChangingRoomHelper.GetPlayerMesh();
                if (playerMesh != null)
                    AttachToPlayerMesh(playerMesh);
            }
        }

        public static void ClearHats()
        {
            foreach (var hat in spawnedHats.Values)
            {
                if (hat != null)
                    UnityEngine.Object.Destroy(hat);
            }
            spawnedHats.Clear();
        }

        public static void Cleanup()
        {
            ClearHats();
            if (partyHatBundle != null)
            {
                partyHatBundle.Unload(true);
                partyHatBundle = null;
            }
            partyHatPrefab = null;
            loadFailed = false;
        }
    }
}
