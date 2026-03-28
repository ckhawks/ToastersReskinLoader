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

    /// <summary>
    /// Finds all GameObjects matching the given names, adds them to the tracking list,
    /// deactivates them, and optionally runs an extra action on each (e.g. disable renderers).
    /// </summary>
    private static void HideObjectsByName(string[] names, List<GameObject> trackingList,
        Action<GameObject> onHide = null)
    {
        UnityEngine.Object[] allObjects =
            UnityEngine.Object.FindObjectsByType(typeof(GameObject), FindObjectsSortMode.None);

        foreach (Object obj in allObjects)
        {
            GameObject gameObject = (GameObject)obj;
            if (gameObject == null || gameObject.transform == null)
                continue;

            if (names.Contains(gameObject.name))
            {
                if (!trackingList.Contains(gameObject))
                    trackingList.Add(gameObject);
                onHide?.Invoke(gameObject);
                gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Re-activates all objects in the tracking list and optionally runs an extra action
    /// on each (e.g. re-enable renderers), then clears the list.
    /// </summary>
    private static void ShowTrackedObjects(List<GameObject> trackingList,
        Action<GameObject> onShow = null)
    {
        foreach (GameObject obj in trackingList)
        {
            if (obj == null || obj.transform == null) continue;
            obj.SetActive(true);
            onShow?.Invoke(obj);
        }

        trackingList.Clear();
    }

    private static void SetMeshRendererEnabled(GameObject go, bool enabled)
    {
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (mr != null) mr.enabled = enabled;
    }

    private static void SetAllRenderersEnabled(GameObject go, bool enabled)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            r.enabled = enabled;
    }

    private static void HideCrowdObjects() =>
        HideObjectsByName(namesOfCrowdObjects, hiddenCrowdObjects);

    private static void ShowCrowdObjects() =>
        ShowTrackedObjects(hiddenCrowdObjects);

    public static void HideOutdoorObjects() =>
        HideObjectsByName(namesOfOutdoorObjects, hiddenOutdoorObjects,
            go => SetMeshRendererEnabled(go, false));

    public static void ShowOutdoorObjects() =>
        ShowTrackedObjects(hiddenOutdoorObjects,
            go => SetMeshRendererEnabled(go, true));

    public static void HideScoreboardObjects() =>
        HideObjectsByName(namesOfScoreboardObjects, hiddenScoreboardObjects, go =>
        {
            go.GetComponent<Scoreboard>()?.TurnOff();
            SetAllRenderersEnabled(go, false);
        });

    public static void ShowScoreboardObjects() =>
        ShowTrackedObjects(hiddenScoreboardObjects, go =>
        {
            SetAllRenderersEnabled(go, true);
            go.GetComponent<Scoreboard>()?.TurnOn();
        });

    public static void HideGlassObjects() =>
        HideObjectsByName(namesOfGlassObjects, hiddenGlassObjects,
            go => SetMeshRendererEnabled(go, false));

    public static void ShowGlassObjects() =>
        ShowTrackedObjects(hiddenGlassObjects,
            go => SetMeshRendererEnabled(go, true));

    [HarmonyPatch(typeof(SpectatorManager), nameof(SpectatorManager.RegisterSpectatorPosition))]
    public static class SpectatorManagerRegisterSpectatorPosition
    {
        [HarmonyPostfix]
        public static void Postfix(SpectatorManager __instance)
        {
            UpdateCrowdState();
        }
    }

    private static void SetGameObjectColor(string gameObjectName, Color color)
    {
        GameObject go = GameObject.Find(gameObjectName);
        if (go == null)
        {
            Plugin.LogError($"Could not locate {gameObjectName} GameObject.");
            return;
        }

        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (mr == null)
        {
            Plugin.LogError($"No MeshRenderer found on GameObject {gameObjectName}.");
            return;
        }

        mr.material.SetColor("_BaseColor", color);
        mr.material.SetColor("_Color", color);
    }

    public static void UpdateBoards()
    {
        try
        {
            SetGameObjectColor("Barrier", ReskinProfileManager.currentProfile.boardsMiddleColor);
            SetGameObjectColor("Barrier Top Border", ReskinProfileManager.currentProfile.boardsBorderTopColor);
            SetGameObjectColor("Barrier Bottom Border", ReskinProfileManager.currentProfile.boardsBorderBottomColor);
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
                    SkinnedMeshRenderer netMeshRenderer = gameObject.GetComponent<SkinnedMeshRenderer>();
                    if (netMeshRenderer == null)
                    {
                        Debug.LogError("No SkinnedMeshRenderer found on GameObject Net.");
                        return;
                    }

                    if (_netOriginalTexture == null)
                    {
                        _netOriginalTexture = netMeshRenderer.material.GetTexture("_BaseMap");
                    }

                    // If setting to unchanged,
                    if (reskinEntry == null || reskinEntry.Path == null)
                    {
                        netMeshRenderer.material.SetTexture("_BaseMap", _netOriginalTexture);
                    }
                    else
                    {
                        netMeshRenderer.material.SetTexture("_BaseMap", TextureManager.GetTexture(reskinEntry));
                    }

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