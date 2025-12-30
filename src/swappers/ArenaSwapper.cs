using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ToasterReskinLoader.swappers;

public static class ArenaSwapper
{
    private static List<GameObject> hiddenOutdoorObjects = new List<GameObject>();
    private static List<GameObject> hiddenCrowdObjects = new List<GameObject>();
    private static List<GameObject> hiddenScoreboardObjects = new List<GameObject>();
    private static List<GameObject> hiddenGlassObjects = new List<GameObject>();

    public static void UpdateCrowdState()
    {
        try
        {
            if (ReskinProfileManager.currentProfile.crowdEnabled)
            {
                ShowCrowdObjects();
            }
            else
            {
                HideCrowdObjects();
            }
        }
        catch (Exception e)
        {
            Plugin.LogError($"Error when updating crowd state: {e.Message}");
        }
    }

    public static void UpdateHangarState()
    {
        try
        {
            if (ReskinProfileManager.currentProfile.hangarEnabled)
            {
                ShowOutdoorObjects();
            }
            else
            {
                HideOutdoorObjects();
            }
        }
        catch (Exception e)
        {
            Plugin.LogError($"Error when updating hanger state: {e.Message}");
        }
    }

    public static void UpdateScoreboardState()
    {
        try
        {
            if (ReskinProfileManager.currentProfile.scoreboardEnabled)
            {
                ShowScoreboardObjects();
            }
            else
            {
                HideScoreboardObjects();
            }
        }
        catch (Exception e)
        {
            Plugin.LogError($"Error when updating scoreboard state: {e.Message}");
        }
    }

    public static void UpdateGlassState()
    {
        try
        {
            if (ReskinProfileManager.currentProfile.glassEnabled)
            {
                ShowGlassObjects();
            }
            else
            {
                HideGlassObjects();
            }
        }
        catch (Exception e)
        {
            Plugin.LogError($"Error when updating glass state: {e.Message}");
        }
    }

    private static string[] namesOfOutdoorObjects = new[]
    {
        "hangar",
        "Rafter",
        "Rafter Edge",

        "Doors",
        // "Light Row",
        // "Light Row.001",
        // "Light Row.002",
        // "Light Row.003",
        "Small Roof Rafters",
        "Small Side Rafters",
        "Window Borders",
        "Windows",

        "Side Rafter Ties",
        "Hangar"
    };

    private static string[] namesOfGlassObjects = new[]
    {
        "Pillars",
        "Glass",
    };

    private static string[] namesOfScoreboardObjects = new[]
    {
        "scoreboard",
        "Scoreboard",
        "Scoreboard (1)"
    };

    private static string[] namesOfCrowdObjects = new[]
    {
        "Spectator",
        "Spectator(Clone)",
        "spectator_booth"
    };

    private static void HideCrowdObjects()
    {
        // Find all GameObjects in the scene
        UnityEngine.Object[] allObjects =
            UnityEngine.Object.FindObjectsByType(typeof(GameObject), FindObjectsSortMode.None);

        // Iterate through all objects
        foreach (Object obj in allObjects)
        {
            // Try to cast the object to a GameObject
            GameObject gameObject = (GameObject)obj;
            if (gameObject == null || gameObject.transform == null)
            {
                continue;
            }

            if (namesOfCrowdObjects.Contains(gameObject.name))
            {
                hiddenCrowdObjects.Add(gameObject);
                gameObject.SetActive(false);
            }
        }
    }

    private static void ShowCrowdObjects()
    {
        foreach (GameObject obj in hiddenCrowdObjects)
        {
            obj.SetActive(true);
        }

        hiddenCrowdObjects.Clear();
    }

    public static void HideOutdoorObjects()
    {
        // Find all GameObjects in the scene
        UnityEngine.Object[] allObjects =
            UnityEngine.Object.FindObjectsByType(typeof(GameObject), FindObjectsSortMode.None);

        // Iterate through all objects
        foreach (Object obj in allObjects)
        {
            // Try to cast the object to a GameObject
            GameObject gameObject = (GameObject)obj;
            if (gameObject == null || gameObject.transform == null)
            {
                continue;
            }

            if (namesOfOutdoorObjects.Contains(gameObject.name))
            {
                hiddenOutdoorObjects.Add(gameObject);
                gameObject.SetActive(false);

                MeshRenderer mr = gameObject.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.enabled = false;
                }
            }
        }
    }

    public static void ShowOutdoorObjects()
    {
        foreach (GameObject obj in hiddenOutdoorObjects)
        {
            if (obj == null || obj.transform == null) continue;
            obj.SetActive(true);
            MeshRenderer mr = obj.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.enabled = true;
            }
        }

        hiddenOutdoorObjects.Clear();
    }

    public static void HideScoreboardObjects()
    {
        // Find all GameObjects in the scene
        UnityEngine.Object[] allObjects =
            UnityEngine.Object.FindObjectsByType(typeof(GameObject), FindObjectsSortMode.None);

        // Iterate through all objects
        foreach (Object obj in allObjects)
        {
            // Try to cast the object to a GameObject
            GameObject gameObject = (GameObject)obj;
            if (gameObject == null || gameObject.transform == null)
            {
                continue;
            }

            if (namesOfScoreboardObjects.Contains(gameObject.name))
            {
                if (!hiddenScoreboardObjects.Contains(gameObject))
                    hiddenScoreboardObjects.Add(gameObject);
                if (gameObject.GetComponent<Scoreboard>() != null)
                {
                    // Plugin.Log($"turning off scoreboard {gameObject.name}");
                    Scoreboard scoreboard = gameObject.GetComponent<Scoreboard>();
                    scoreboard.TurnOff();
                }
                gameObject.SetActive(false);

                MeshRenderer mr = gameObject.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.enabled = false;
                }
            }
        }
    }

    public static void ShowScoreboardObjects()
    {
        foreach (GameObject obj in hiddenScoreboardObjects)
        {
            if (obj == null || obj.transform == null) continue;
            obj.SetActive(true);
            MeshRenderer mr = obj.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.enabled = true;
            }
            if (obj.GetComponent<Scoreboard>() != null)
            {
                Scoreboard scoreboard = obj.GetComponent<Scoreboard>();
                scoreboard.TurnOn();
            }
        }

        hiddenScoreboardObjects.Clear();
    }

    public static void HideGlassObjects()
    {
        // Find all GameObjects in the scene
        UnityEngine.Object[] allObjects =
            UnityEngine.Object.FindObjectsByType(typeof(GameObject), FindObjectsSortMode.None);

        // Iterate through all objects
        foreach (Object obj in allObjects)
        {
            // Try to cast the object to a GameObject
            GameObject gameObject = (GameObject)obj;
            if (gameObject == null || gameObject.transform == null)
            {
                continue;
            }

            if (namesOfGlassObjects.Contains(gameObject.name))
            {
                hiddenGlassObjects.Add(gameObject);
                gameObject.SetActive(false);

                MeshRenderer mr = gameObject.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.enabled = false;
                }
            }
        }
    }

    public static void ShowGlassObjects()
    {
        foreach (GameObject obj in hiddenGlassObjects)
        {
            if (obj == null || obj.transform == null) continue;
            obj.SetActive(true);
            MeshRenderer mr = obj.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.enabled = true;
            }
        }

        hiddenGlassObjects.Clear();
    }

    [HarmonyPatch(typeof(SpectatorManager), nameof(SpectatorManager.SpawnSpectators))]
    public static class SpectatorManagerSpawnSpectators
    {
        [HarmonyPostfix]
        public static void Postfix(SpectatorManager __instance)
        {
            UpdateCrowdState();
        }
    }

    public static void UpdateBoards()
    {
        try
        {
            // middle
            GameObject barrierGameObject = GameObject.Find("Barrier");

            if (barrierGameObject == null)
            {
                Plugin.LogError($"Could not locate Barrier GameObject.");
                return;
            }

            MeshRenderer barrierMeshRenderer = barrierGameObject.GetComponent<MeshRenderer>();

            if (barrierMeshRenderer == null)
            {
                Debug.LogError("No MeshRenderer found on GameObject Barrier.");
            }

            barrierMeshRenderer.material.SetColor("_BaseColor", ReskinProfileManager.currentProfile.boardsMiddleColor);
            barrierMeshRenderer.material.SetColor("_Color", ReskinProfileManager.currentProfile.boardsMiddleColor);

            // top
            GameObject barrierBorderTopGameObject = GameObject.Find("Barrier Top Border");

            if (barrierBorderTopGameObject == null)
            {
                Plugin.LogError($"Could not locate Barrier Top Border GameObject.");
                return;
            }

            MeshRenderer barrierBorderTopMeshRenderer = barrierBorderTopGameObject.GetComponent<MeshRenderer>();

            if (barrierBorderTopMeshRenderer == null)
            {
                Debug.LogError("No MeshRenderer found on GameObject Barrier Top Border.");
            }

            barrierBorderTopMeshRenderer.material.SetColor("_BaseColor",
                ReskinProfileManager.currentProfile.boardsBorderTopColor);
            barrierBorderTopMeshRenderer.material.SetColor("_Color",
                ReskinProfileManager.currentProfile.boardsBorderTopColor);

            // bottom
            GameObject barrierBorderBottomGameObject = GameObject.Find("Barrier Bottom Border");

            if (barrierBorderBottomGameObject == null)
            {
                Plugin.LogError($"Could not locate Barrier Bottom Border GameObject.");
                return;
            }

            MeshRenderer barrierBorderBottomMeshRenderer = barrierBorderBottomGameObject.GetComponent<MeshRenderer>();

            if (barrierBorderBottomMeshRenderer == null)
            {
                Debug.LogError("No MeshRenderer found on GameObject Barrier Bottom Border.");
            }

            barrierBorderBottomMeshRenderer.material.SetColor("_BaseColor",
                ReskinProfileManager.currentProfile.boardsBorderBottomColor);
            barrierBorderBottomMeshRenderer.material.SetColor("_Color",
                ReskinProfileManager.currentProfile.boardsBorderBottomColor);
            return;
        }
        catch (Exception e)
        {
            Plugin.LogError($"Error when updating boards: {e.Message}");
        }
    }

    public static void UpdateGlassAndPillars()
    {
        try
        {
            GameObject glassGameObject = GameObject.Find("Glass");
            // Plugin.Log($"GlassGameObject name: {glassGameObject.name}");
            if (glassGameObject == null)
            {
                Plugin.LogError($"Could not locate Glass GameObject.");
                return;
            }

            MeshRenderer glassMeshRenderer = glassGameObject.GetComponent<MeshRenderer>();

            if (glassMeshRenderer == null)
            {
                Debug.LogError("No MeshRenderer found on GameObject Glass");
            }

            // Plugin.Log($"glassMeshRenderer name: {glassMeshRenderer.name}");
            // Plugin.Log($"glassMeshRenderer.material name: {glassMeshRenderer.material.name}");
            glassMeshRenderer.material.SetFloat("_Smoothness", ReskinProfileManager.currentProfile.glassSmoothness);

            GameObject pillarsGameObject = GameObject.Find("Pillars");
            if (pillarsGameObject == null)
            {
                Plugin.LogError($"Could not locate Pillars GameObject.");
                return;
            }

            MeshRenderer pillarsMeshRenderer = pillarsGameObject.GetComponent<MeshRenderer>();

            if (pillarsMeshRenderer == null)
            {
                Debug.LogError("No MeshRenderer found on GameObject Pillars");
            }

            pillarsMeshRenderer.material.SetColor("_BaseColor", ReskinProfileManager.currentProfile.pillarsColor);
            pillarsMeshRenderer.material.SetColor("_Color", ReskinProfileManager.currentProfile.pillarsColor);
            return;
        }
        catch (Exception e)
        {
            Plugin.LogError($"Error when updating glass and pillars: {e.Message}");
        }
    }

    static readonly FieldInfo _spectatorDensityField = typeof(SpectatorManager)
        .GetField("spectatorDensity",
            BindingFlags.Instance | BindingFlags.NonPublic);

    public static void UpdateSpectators()
    {
        try
        {
            if (_spectatorDensityField == null)
            {
                Plugin.LogError($"Could not locate _spectatorDensityField");
                return;
            }

            _spectatorDensityField.SetValue(SpectatorManager.Instance,
                ReskinProfileManager.currentProfile.spectatorDensity);
            SpectatorManager.Instance.ClearSpectators();
            if (ReskinProfileManager.currentProfile.crowdEnabled)
            {
                SpectatorManager.Instance.SpawnSpectators();
            }

            Plugin.LogDebug($"Update spectators complete.");

            return;
        }
        catch (Exception e)
        {
            Plugin.LogError($"Error when updating spectators: {e.Message}");
        }
    }

    private static Texture _netOriginalTexture;

    public static void SetNetTexture()
    {
        try
        {
            // Find all GameObjects in the scene
            UnityEngine.Object[] allObjects =
                UnityEngine.Object.FindObjectsByType(typeof(GameObject), FindObjectsSortMode.None);

            ReskinRegistry.ReskinEntry reskinEntry = ReskinProfileManager.currentProfile.net;

            int netCount = 0;
            // Iterate through all objects
            foreach (Object obj in allObjects)
            {
                if (netCount == 2) return; // stop checking objects
                // Try to cast the object to a GameObject
                GameObject gameObject = (GameObject)obj;
                if (gameObject == null || gameObject.transform == null)
                {
                    continue;
                }

                if (gameObject.name.Equals("Net"))
                {
                    // Plugin.LogDebug($"net: {gameObject.name} {gameObject.transform.position.ToString()}");
                    SkinnedMeshRenderer netMeshRenderer = gameObject.GetComponent<SkinnedMeshRenderer>();

                    // MeshRenderer netMeshRenderer2 = (MeshRenderer) gameObject.GetComponentsInChildren<Component>(true).First(component => component.GetType() == typeof(MeshRenderer));
                    //
                    // Plugin.Log($"netMeshRenderer2: {netMeshRenderer.name}");
                    // Plugin.Log($"netMeshRenderer23: {netMeshRenderer.gameObject.name}");
                    // Plugin.Log($"netMeshRenderer24: {netMeshRenderer.gameObject.transform.parent.name}");
                    if (netMeshRenderer == null)
                    {
                        Debug.LogError("No SkinnedMeshRenderer found on GameObject Net.");
                        return;
                    }

                    // string texturePropertyName = SwapperUtils.FindTextureProperty(iceBottomMeshRenderer.material);
                    // if (texturePropertyName == null)
                    // {
                    //     Plugin.LogError("No texture property found in the shader.");
                    //     return false;
                    // }

                    if (_netOriginalTexture == null)
                    {
                        _netOriginalTexture = netMeshRenderer.material.GetTexture("_BaseMap");
                    }

                    // If setting to unchanged,
                    if (reskinEntry == null || reskinEntry.Path == null)
                    {
                        netMeshRenderer.material.SetTexture("_BaseMap", _netOriginalTexture);
                        // Plugin.Log("Texture applied to property: _BaseMap");
                    }
                    else
                    {
                        netMeshRenderer.material.SetTexture("_BaseMap", TextureManager.GetTexture(reskinEntry));
                        // Plugin.Log("Texture applied to property: _BaseMap");
                    }

                    // Plugin.Log($"Set the Net texture to {reskinEntry.Name} {reskinEntry.Path}");
                    netCount++;
                }
            }
        }
        catch (Exception e)
        {
            Plugin.LogError($"Failed to set net texture: {e}");
        }
    }

    [HarmonyPatch(typeof(Scoreboard), nameof(Scoreboard.TurnOn))]
    public static class ScoreboardTurnOnPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Scoreboard __instance)
        {
            if (!ReskinProfileManager.currentProfile.scoreboardEnabled)
            {
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Scoreboard), nameof(Scoreboard.TurnOff))]
    public static class ScoreboardTurnOffPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Scoreboard __instance)
        {
            if (!ReskinProfileManager.currentProfile.scoreboardEnabled)
            {
                return false;
            }

            return true;
        }
    }
}