using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ToasterReskinLoader.swappers;

public static class PuckSwapper
{
    private static Texture _originalTexture;
    private static Texture _originalBumpMap;
    private static string _puckBumpMapPath = "";
    private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
    private static System.Random _random = new System.Random();

    private static bool _loggedHierarchy;

    /// <summary>
    /// Resolves the transform carrying the puck's body MeshRenderer. The vanilla hierarchy is
    /// puck &gt; "puck" &gt; "Puck", but a game update can rename/restructure it, so we fall back
    /// to searching children for a MeshRenderer that has a MeshFilter and a "_BaseMap" material
    /// (the puck body — not FX quads, trails, or the elevation indicator). The actual hierarchy
    /// is logged once when the known path misses, so a broken layout is diagnosable passively.
    /// </summary>
    public static Transform ResolvePuckMeshTransform(Transform puckRoot)
    {
        if (puckRoot == null) return null;

        // Known vanilla path.
        var known = puckRoot.Find("puck")?.Find("Puck");
        if (known != null && known.GetComponent<MeshRenderer>() != null)
            return known;

        // Fallback: find the body renderer by shape (MeshFilter + textured material).
        Transform best = null;
        foreach (var mr in puckRoot.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || mr.sharedMaterial == null) continue;
            if (!mr.sharedMaterial.HasProperty("_BaseMap")) continue;
            best = mr.transform;
            if (string.Equals(mr.name, "Puck", StringComparison.OrdinalIgnoreCase))
                break; // exact-name match wins outright
        }

        if (!_loggedHierarchy && (known == null || best == null))
        {
            _loggedHierarchy = true;
            if (best == null)
                Plugin.LogWarning($"[Puck] Could not resolve puck body mesh under '{puckRoot.name}'. Hierarchy:\n{DescribeHierarchy(puckRoot, 0)}");
            else
                Plugin.LogWarning($"[Puck] Vanilla path puck/Puck missing; resolved body mesh via fallback: '{best.name}'. Hierarchy:\n{DescribeHierarchy(puckRoot, 0)}");
        }

        return best;
    }

    /// <summary>Indented dump of a transform subtree with renderer/filter markers, for diagnostics.</summary>
    private static string DescribeHierarchy(Transform t, int depth)
    {
        string indent = new string(' ', depth * 2);
        bool mr = t.GetComponent<MeshRenderer>() != null;
        bool mf = t.GetComponent<MeshFilter>() != null;
        string tags = (mr ? " [MeshRenderer]" : "") + (mf ? " [MeshFilter]" : "");
        var sb = new System.Text.StringBuilder();
        sb.Append(indent).Append(t.name).Append(tags).Append('\n');
        for (int i = 0; i < t.childCount; i++)
            sb.Append(DescribeHierarchy(t.GetChild(i), depth + 1));
        return sb.ToString();
    }

    // Set a specific Puck to a specific ReskinEntry (can be null)
    private static void SetPuckTexture(Puck puck, ReskinRegistry.ReskinEntry reskinEntry)
    {
        try
        {
            Transform meshTransform = ResolvePuckMeshTransform(puck.gameObject.transform);
            MeshRenderer puckMeshRenderer = meshTransform != null
                ? meshTransform.GetComponent<MeshRenderer>()
                : null;

            if (puckMeshRenderer == null)
            {
                Plugin.LogError("No MeshRenderer found for puck body.");
                return;
            }

            // these should only run on the first go around setting the puck from vanilla->custom
            if (_originalTexture == null)
            {
                _originalTexture = puckMeshRenderer.material.GetTexture("_BaseMap");
            }
            if (_originalBumpMap == null)
            {
                _originalBumpMap = puckMeshRenderer.material.GetTexture("_BumpMap");
            }

            if (reskinEntry == null || reskinEntry.Path == null)
            {
                // No entry or unchanged — restore the original puck
                puckMeshRenderer.material.SetTexture(BaseMap, _originalTexture);
                puckMeshRenderer.material.SetTexture("_BumpMap", _originalBumpMap);
            }
            else
            {
                // ReskinEntry has values, set puck to custom texture
                puckMeshRenderer.material.SetTexture(BaseMap, TextureManager.GetTexture(reskinEntry));
                puckMeshRenderer.material.SetTexture("_BumpMap", TextureManager.GetTextureFromFilePath(_puckBumpMapPath));
            }
        }
        catch (Exception ex)
        {
            Plugin.LogError($"Error while setting puck texture: {ex.Message}");
        }
    }

    public static void GetBumpMapPathAndLoad()
    {
        // string workshopModsRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(execPath)!, ".."));
        _puckBumpMapPath = Path.Combine(Path.GetFullPath(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)), "puck_normal.png");
        TextureManager.GetTextureFromFilePath(_puckBumpMapPath);
    }

    /// <summary>
    /// The clean puck normal map applied when a custom skin is active (the vanilla bump
    /// has embossed lettering). Exposed so the locker-room preview matches the in-game look.
    /// </summary>
    public static Texture GetCleanBumpMap()
    {
        if (string.IsNullOrEmpty(_puckBumpMapPath)) GetBumpMapPathAndLoad();
        return TextureManager.GetTextureFromFilePath(_puckBumpMapPath);
    }

    /// <summary>The vanilla puck base texture, captured the first time a puck is textured.</summary>
    public static Texture OriginalTexture => _originalTexture;
    /// <summary>The vanilla puck bump map, captured the first time a puck is textured.</summary>
    public static Texture OriginalBumpMap => _originalBumpMap;

    /// <summary>
    /// Gets a random puck from the randomizer list.
    /// If randomizer list is empty, returns null to use original/default texture.
    /// </summary>
    private static ReskinRegistry.ReskinEntry GetPuckForRandomizer()
    {
        var puckList = ReskinProfileManager.currentProfile.puckList;

        // If puck list has entries, pick a random one
        if (puckList != null && puckList.Count > 0)
        {
            int randomIndex = _random.Next(puckList.Count);
            return puckList[randomIndex];
        }

        // If list is empty, return null to use original/default texture
        return null;
    }

    // ── Viewmodel shrink ────────────────────────────────────────────────────────
    // Pulls the visible puck body mesh in by 1% so its surface stops clipping into
    // the stick at contact. Purely cosmetic: the collider/rigidbody live on the puck
    // root, not this body-mesh child, so physics and hitreg are untouched. The
    // silhouette and outline render this same mesh (both on the "Puck" layer), so they
    // shrink with it — that's fine, the target is the puck-vs-stick surface, not a
    // body-vs-silhouette gap. Gated on the profile toggle; default on.
    private const float ViewmodelShrinkFactor = 0.99f;
    private static Vector3? _originalPuckScale;

    /// <summary>Applies (or clears) the viewmodel shrink for one puck based on the toggle.</summary>
    public static void ApplyViewmodelScale(Puck puck)
    {
        try
        {
            if (puck == null) return;
            Transform meshTransform = ResolvePuckMeshTransform(puck.gameObject.transform);
            if (meshTransform == null) return;

            // Capture the prefab's original scale once (before any shrink is applied)
            // so toggling the feature off restores the exact vanilla size.
            _originalPuckScale ??= meshTransform.localScale;

            bool shrink = ReskinProfileManager.currentProfile.puckShrinkViewmodel;
            meshTransform.localScale = shrink
                ? _originalPuckScale.Value * ViewmodelShrinkFactor
                : _originalPuckScale.Value;

            Plugin.LogDebug($"[Puck] Viewmodel scale on '{meshTransform.name}': shrink={shrink}, " +
                            $"original={_originalPuckScale.Value:F4}, now={meshTransform.localScale:F4}");
        }
        catch (Exception ex)
        {
            Plugin.LogError($"Error applying puck viewmodel scale: {ex.Message}");
        }
    }

    /// <summary>Applies the viewmodel shrink to every active puck (settings change / startup).</summary>
    public static void ApplyViewmodelScaleAll()
    {
        try
        {
            foreach (Puck puck in PuckManager.Instance.GetPucks())
                ApplyViewmodelScale(puck);
        }
        catch (Exception ex)
        {
            Plugin.LogError($"Error applying puck viewmodel scale to all pucks: {ex.Message}");
        }
    }

    // Set all puck textures; called when Puck reskin settings are changed
    public static void SetAllPucksTextures()
    {
        List<Puck> pucks = PuckManager.Instance.GetPucks();
        foreach (Puck puck in pucks)
        {
            var puckTexture = GetPuckForRandomizer();
            SetPuckTexture(puck, puckTexture);
        }
        Plugin.LogDebug($"Updated all pucks to have correct texture.");
    }
    
    // Whenever a new puck spawns, set its texture 
    [HarmonyPatch(typeof(Puck), "OnNetworkPostSpawn")]
    public static class PuckOnNetworkPostSpawn
    {
        [HarmonyPostfix]
        public static void Postfix(Puck __instance)
        {
            // Backup source for the locker-room preview mesh (primary source is the loaded prefab).
            PuckPreview.TryCaptureAssets(__instance);
            var puckTexture = GetPuckForRandomizer();
            SetPuckTexture(__instance, puckTexture);
            ApplyViewmodelScale(__instance);
        }
    }
}