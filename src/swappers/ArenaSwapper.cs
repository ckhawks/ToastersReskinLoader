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
        "Scoreboard (1)",
        "Red Score",
        "Blue Score",
        "Period",
        "Minute",
        "Second"
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
                if (!hiddenCrowdObjects.Contains(gameObject))
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
                if (!hiddenOutdoorObjects.Contains(gameObject))
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
                    Scoreboard scoreboard = gameObject.GetComponent<Scoreboard>();
                    scoreboard.TurnOff();
                }
                gameObject.SetActive(false);

                // Disable all renderers including children (score digits, etc.)
                foreach (var r in gameObject.GetComponentsInChildren<Renderer>(true))
                {
                    r.enabled = false;
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

            // Re-enable all renderers including children (score digits, etc.)
            foreach (var r in obj.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = true;
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
                if (!hiddenGlassObjects.Contains(gameObject))
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

    [HarmonyPatch(typeof(SpectatorManager), nameof(SpectatorManager.RegisterSpectatorPosition))]
    public static class SpectatorManagerRegisterSpectatorPosition
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
                Plugin.LogError("No MeshRenderer found on GameObject Barrier.");
                return;
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
                Plugin.LogError("No MeshRenderer found on GameObject Barrier Top Border.");
                return;
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
                Plugin.LogError("No MeshRenderer found on GameObject Barrier Bottom Border.");
                return;
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
                Plugin.LogError("No MeshRenderer found on GameObject Glass.");
                return;
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
                Plugin.LogError("No MeshRenderer found on GameObject Pillars.");
                return;
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

    static readonly FieldInfo _spectatorMapField = typeof(SpectatorManager)
        .GetField("spectatorPositionSpectatorMap",
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

            var spectatorManager = SpectatorManager.Instance;

            // Update density
            _spectatorDensityField.SetValue(spectatorManager,
                ReskinProfileManager.currentProfile.spectatorDensity);

            // Get all SpectatorPosition objects and re-register them
            // First, unregister all existing ones
            SpectatorPosition[] positions = Object.FindObjectsByType<SpectatorPosition>(FindObjectsSortMode.None);
            foreach (var pos in positions)
            {
                spectatorManager.UnregisterSpectatorPosition(pos);
            }

            // Then re-register if crowd is enabled (density filtering happens in RegisterSpectatorPosition)
            if (ReskinProfileManager.currentProfile.crowdEnabled)
            {
                foreach (var pos in positions)
                {
                    spectatorManager.RegisterSpectatorPosition(pos);
                }
            }

            Plugin.LogDebug($"Update spectators complete.");
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

    private static Color? _originalBlueGoalFrameColor;
    private static Color? _originalRedGoalFrameColor;

    public static void UpdateGoalFrameColors()
    {
        try
        {
            var goals = Object.FindObjectsByType(typeof(Goal), FindObjectsSortMode.None);
            var profile = ReskinProfileManager.currentProfile;

            foreach (Object obj in goals)
            {
                Goal goal = (Goal)obj;
                Transform frameTransform = goal.transform.Find("Frame");
                if (frameTransform == null) continue;

                MeshRenderer mr = frameTransform.GetComponent<MeshRenderer>();
                if (mr == null) continue;

                bool isBlue = goal.gameObject.name.Contains("Blue");

                // Cache original colors
                if (isBlue && _originalBlueGoalFrameColor == null)
                    _originalBlueGoalFrameColor = mr.material.GetColor("_BaseColor");
                else if (!isBlue && _originalRedGoalFrameColor == null)
                    _originalRedGoalFrameColor = mr.material.GetColor("_BaseColor");

                if (profile.teamColorsEnabled)
                {
                    Color color = isBlue ? profile.blueTeamColor : profile.redTeamColor;
                    mr.material.SetColor("_BaseColor", color);
                    mr.material.SetColor("_Color", color);
                }
                else
                {
                    Color original = isBlue
                        ? _originalBlueGoalFrameColor ?? Color.white
                        : _originalRedGoalFrameColor ?? Color.white;
                    mr.material.SetColor("_BaseColor", original);
                    mr.material.SetColor("_Color", original);
                }
            }
        }
        catch (Exception e)
        {
            Plugin.LogError($"Failed to update goal frame colors: {e.Message}");
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