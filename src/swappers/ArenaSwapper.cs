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
    private static List<GameObject> hiddenScoreboardObjects = new List<GameObject>();

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

        "Doors",
        "Small Roof Rafters",
        "Small Side Rafters",
        "Window Frames",
        "Windows",

        "Side Rafter Ties",
        "Hangar"
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

    private static readonly List<GameObject> hiddenCrowdMembers = new List<GameObject>();
    private static readonly List<MeshRenderer> hiddenBoothRenderers = new List<MeshRenderer>();

    // Bleacher / booth (stands) materials — from the crowd_booth model. The booth
    // nodes aren't reliably named, so we hide them by material instead.
    private static readonly string[] namesOfBoothMaterials =
        { "Base", "Railing", "Seat", "Step Trim", "Wood Cover" };

    private static void HideCrowdObjects()
    {
        // Crowd people via the CrowdManager/CrowdMember system.
        foreach (var member in Object.FindObjectsByType<CrowdMember>(FindObjectsSortMode.None))
        {
            if (member != null && member.gameObject.activeSelf)
            {
                member.gameObject.SetActive(false);
                hiddenCrowdMembers.Add(member.gameObject);
            }
        }
        // Bleachers / booth located by their distinctive materials.
        foreach (var mr in FindRenderersByMaterial(namesOfBoothMaterials))
        {
            if (mr.enabled) { mr.enabled = false; hiddenBoothRenderers.Add(mr); }
        }
    }

    private static void ShowCrowdObjects()
    {
        foreach (var go in hiddenCrowdMembers) if (go != null) go.SetActive(true);
        hiddenCrowdMembers.Clear();
        foreach (var mr in hiddenBoothRenderers) if (mr != null) mr.enabled = true;
        hiddenBoothRenderers.Clear();
    }

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

    private static readonly List<MeshRenderer> hiddenGlassRenderers = new List<MeshRenderer>();

    public static void HideGlassObjects()
    {
        // The "hide glass" toggle removes the barrier glass and its pillars.
        foreach (var mr in FindRenderersByMaterial("Glass", "Pillars"))
        {
            if (mr.enabled) { mr.enabled = false; hiddenGlassRenderers.Add(mr); }
        }
    }

    public static void ShowGlassObjects()
    {
        foreach (var mr in hiddenGlassRenderers)
            if (mr != null) mr.enabled = true;
        hiddenGlassRenderers.Clear();
    }

    [HarmonyPatch(typeof(CrowdManager), nameof(CrowdManager.RegisterCrowdPosition))]
    public static class CrowdManagerRegisterCrowdPosition
    {
        [HarmonyPostfix]
        public static void Postfix(CrowdManager __instance)
        {
            UpdateCrowdState();
        }
    }

    // ── Material-based arena lookup ──────────────────────────────────────
    //
    // B1117 rebuilt the rink model (the barrier glass is generated dynamically),
    // renaming/nesting the mesh nodes so GameObject.Find("Glass"/"Pillars"/…) no
    // longer resolves. The MATERIAL names are stable (Glass, Pillars, Barrier,
    // Barrier Top, Barrier Bottom, Ice Bottom/Top), so we locate arena surfaces by
    // material instead — resilient to the node restructure.

    private static List<MeshRenderer> FindRenderersByMaterial(params string[] materialNames)
    {
        var result = new List<MeshRenderer>();
        foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (mr == null) continue;
            var mats = mr.sharedMaterials;
            if (mats == null) continue;
            foreach (var m in mats)
            {
                if (m != null && MaterialNameMatchesAny(m.name, materialNames)) { result.Add(mr); break; }
            }
        }
        return result;
    }

    private static bool MaterialNameMatchesAny(string actual, string[] targets)
    {
        if (string.IsNullOrEmpty(actual)) return false;
        int idx = actual.IndexOf(" (Instance)", StringComparison.Ordinal);
        if (idx >= 0) actual = actual.Substring(0, idx);
        actual = actual.Trim();
        foreach (var t in targets)
            if (actual.Equals(t, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static void SetSurfaceColorByMaterial(string materialName, Color color)
    {
        var renderers = FindRenderersByMaterial(materialName);
        if (renderers.Count == 0)
        {
            Plugin.LogWarning($"Arena: no renderer found with material '{materialName}'.");
            return;
        }
        foreach (var mr in renderers)
        {
            var mat = mr.material; // instanced so we don't tint the shared asset globally
            if (mat == null) continue;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        }
    }

    public static void UpdateBoards()
    {
        try
        {
            var p = ReskinProfileManager.currentProfile;
            SetSurfaceColorByMaterial("Barrier", p.boardsMiddleColor);
            SetSurfaceColorByMaterial("Barrier Top", p.boardsBorderTopColor);
            SetSurfaceColorByMaterial("Barrier Bottom", p.boardsBorderBottomColor);
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
            var p = ReskinProfileManager.currentProfile;

            // Glass smoothness — every renderer using the Glass material.
            foreach (var mr in FindRenderersByMaterial("Glass"))
            {
                var mat = mr.material;
                if (mat != null && mat.HasProperty("_Smoothness"))
                    mat.SetFloat("_Smoothness", p.glassSmoothness);
            }

            // Pillars color.
            SetSurfaceColorByMaterial("Pillars", p.pillarsColor);
        }
        catch (Exception e)
        {
            Plugin.LogError($"Error when updating glass and pillars: {e.Message}");
        }
    }

    static readonly FieldInfo _crowdDensityField = typeof(CrowdManager)
        .GetField("crowdDensity",
            BindingFlags.Instance | BindingFlags.NonPublic);

    public static void UpdateSpectators()
    {
        try
        {
            if (_crowdDensityField == null)
            {
                Plugin.LogError($"Could not locate _crowdDensityField");
                return;
            }

            var crowdManager = CrowdManager.Instance;

            // Update density
            _crowdDensityField.SetValue(crowdManager,
                ReskinProfileManager.currentProfile.spectatorDensity);

            // Get all CrowdPosition objects and re-register them
            // First, unregister all existing ones
            CrowdPosition[] positions = Object.FindObjectsByType<CrowdPosition>(FindObjectsSortMode.None);
            foreach (var pos in positions)
            {
                crowdManager.UnregisterCrowdPosition(pos);
            }

            // Then re-register if crowd is enabled (density filtering happens in RegisterCrowdPosition)
            if (ReskinProfileManager.currentProfile.crowdEnabled)
            {
                foreach (var pos in positions)
                {
                    crowdManager.RegisterCrowdPosition(pos);
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

                if (gameObject.name.Equals("Net Cloth"))
                {
                    SkinnedMeshRenderer netMeshRenderer = gameObject.GetComponent<SkinnedMeshRenderer>();
                    if (netMeshRenderer == null)
                    {
                        Debug.LogError("No SkinnedMeshRenderer found on GameObject Net Cloth.");
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

                // B1117 renamed/restructured the goal frame node (was a direct "Frame"
                // child). Locate the frame renderer by its team-colored material instead
                // ("Blue Goal" / "Red Goal") so we're resilient to the node name.
                MeshRenderer mr = goal.GetComponentsInChildren<MeshRenderer>(true)
                    .FirstOrDefault(r => r.sharedMaterial != null && r.sharedMaterial.name.Contains("Goal"));
                if (mr == null) continue;

                bool isBlue = goal.gameObject.name.Contains("Blue");

                // Cache original colors
                if (isBlue && _originalBlueGoalFrameColor == null)
                    _originalBlueGoalFrameColor = mr.material.GetColor("_BaseColor");
                else if (!isBlue && _originalRedGoalFrameColor == null)
                    _originalRedGoalFrameColor = mr.material.GetColor("_BaseColor");

                bool teamEnabled = isBlue
                    ? TeamColorSwapper.IsEnabled(PlayerTeam.Blue)
                    : TeamColorSwapper.IsEnabled(PlayerTeam.Red);
                if (teamEnabled)
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