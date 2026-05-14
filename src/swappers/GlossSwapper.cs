using System.Collections.Generic;
using UnityEngine;

namespace ToasterReskinLoader.swappers;

/// <summary>
/// Adjusts material smoothness / roughness on URP Lit and glTF-PBR materials in the scene
/// so the user can tone down or eliminate environment-reflection gloss on sticks, players,
/// and pucks. After recent game updates, surfaces pick up a strong reflection from the ice
/// at grazing angles — this swapper lets the user dial that back.
///
/// Materials are tracked once seen so the slider/toggle UI can re-apply current settings
/// or restore originals without rescanning the whole scene every change.
/// </summary>
public static class GlossSwapper
{
    private class Tracked
    {
        public Material material;
        public ObjectCategory category;
        public float originalSmoothness;
        public float originalRoughness;
        public float originalSpecular;
        public float originalEnvRefl;
        public float originalGlossy;
        public bool hadSpecHighlightsOn;
        public bool hadEnvReflOn;
        public bool hadMetallicGlossMap;
        public bool hadSpecGlossMap;
        public bool hadSmoothnessAlbedoAlpha;
    }

    public enum ObjectCategory { Other, Stick, Player, Puck }

    private static readonly Dictionary<int, Tracked> _tracked = new Dictionary<int, Tracked>();
    private static readonly HashSet<int> _trackedRendererIds = new HashSet<int>();
    private static readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();

    /// <summary>
    /// Scans every active mesh / skinned-mesh renderer in the scene and applies the
    /// configured gloss settings to materials in enabled categories. Already-processed
    /// renderers are skipped so steady-state cost is essentially zero.
    /// </summary>
    public static void Scan()
    {
        if (!ReskinProfileManager.currentProfile.glossRemoverEnabled) return;

        var meshes = Object.FindObjectsOfType<MeshRenderer>();
        foreach (var r in meshes) ProcessRenderer(r);

        var skinned = Object.FindObjectsOfType<SkinnedMeshRenderer>();
        foreach (var r in skinned) ProcessRenderer(r);
    }

    /// <summary>
    /// Re-applies current settings to all tracked materials. Use when the user changes
    /// the slider or a category toggle so the visual updates without a full rescan.
    /// </summary>
    public static void ReapplyAll()
    {
        var dead = new List<int>();
        foreach (var kv in _tracked)
            if (kv.Value.material == null) dead.Add(kv.Key);
        foreach (var id in dead) _tracked.Remove(id);

        foreach (var t in _tracked.Values) ApplyOrRestoreMaterial(t);

        // Force a rescan so newly enabled categories pick up renderers we previously skipped
        _trackedRendererIds.Clear();
        Scan();
    }

    /// <summary>
    /// Restores every tracked material to its original gloss settings. Used when the
    /// feature is disabled entirely.
    /// </summary>
    public static void RestoreAll()
    {
        foreach (var t in _tracked.Values) Restore(t);
        _tracked.Clear();
        _trackedRendererIds.Clear();
    }

    private static void ProcessRenderer(Renderer r)
    {
        if (r == null) return;
        int rid = r.GetInstanceID();
        if (_trackedRendererIds.Contains(rid)) return;

        var mats = r.sharedMaterials;
        if (mats == null || mats.Length == 0) return;

        ObjectCategory cat = Categorize(r, mats);
        bool enabled = CategoryEnabled(cat);

        bool anyHandled = false;
        foreach (var mat in mats)
        {
            if (mat == null || mat.shader == null) continue;
            if (!HasGlossControl(mat)) continue;

            int mid = mat.GetInstanceID();
            if (!_tracked.TryGetValue(mid, out Tracked t))
            {
                t = new Tracked
                {
                    material = mat,
                    category = cat,
                    originalSmoothness = mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") :
                                         mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.5f,
                    originalRoughness = mat.HasProperty("roughnessFactor") ? mat.GetFloat("roughnessFactor") : 0.5f,
                    originalSpecular = mat.HasProperty("_SpecularHighlights") ? mat.GetFloat("_SpecularHighlights") : 1f,
                    originalEnvRefl = mat.HasProperty("_EnvironmentReflections") ? mat.GetFloat("_EnvironmentReflections") : 1f,
                    originalGlossy = mat.HasProperty("_GlossyReflections") ? mat.GetFloat("_GlossyReflections") : 1f,
                    hadSpecHighlightsOn = mat.IsKeywordEnabled("_SPECULARHIGHLIGHTS_ON") || !mat.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF"),
                    hadEnvReflOn = mat.IsKeywordEnabled("_ENVIRONMENTREFLECTIONS_ON") || !mat.IsKeywordEnabled("_ENVIRONMENTREFLECTIONS_OFF"),
                    hadMetallicGlossMap = mat.IsKeywordEnabled("_METALLICSPECGLOSSMAP") || mat.IsKeywordEnabled("_METALLICGLOSSMAP"),
                    hadSpecGlossMap = mat.IsKeywordEnabled("_SPECGLOSSMAP"),
                    hadSmoothnessAlbedoAlpha = mat.IsKeywordEnabled("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A"),
                };
                _tracked[mid] = t;
            }
            ApplyOrRestoreMaterial(t);
            anyHandled = true;
        }

        if (anyHandled)
        {
            if (enabled) WritePropertyBlock(r);
            _trackedRendererIds.Add(rid);
        }
    }

    private static void WritePropertyBlock(Renderer r)
    {
        float s = Mathf.Clamp01(ReskinProfileManager.currentProfile.glossSmoothness);
        r.GetPropertyBlock(_block);
        _block.SetFloat("_Smoothness", s);
        _block.SetFloat("_Glossiness", s);
        _block.SetFloat("roughnessFactor", 1f - s);
        _block.SetFloat("_SpecularHighlights", s <= 0.01f ? 0f : 1f);
        _block.SetFloat("_EnvironmentReflections", s <= 0.01f ? 0f : 1f);
        r.SetPropertyBlock(_block);
    }

    private static bool HasGlossControl(Material mat)
    {
        return mat.HasProperty("_Smoothness")
            || mat.HasProperty("_Glossiness")
            || mat.HasProperty("roughnessFactor");
    }

    private static ObjectCategory Categorize(Renderer r, Material[] mats)
    {
        // Walk up to 8 parents; most categorization wins are within 2–3 levels.
        Transform t = r.transform;
        for (int d = 0; d < 8 && t != null; d++, t = t.parent)
        {
            var n = t.gameObject != null ? t.gameObject.name.ToLowerInvariant() : "";
            if (n.Length == 0) continue;

            if (n.Contains("stick") || n.Contains("blade")) return ObjectCategory.Stick;
            if (n == "puck" || n.StartsWith("puck_") || n.EndsWith("_puck") ||
                n.Contains("puckmesh") || n.Contains("puck ("))
                return ObjectCategory.Puck;
            if (n.Contains("body") || n.Contains("head") || n.Contains("hair") ||
                n.Contains("jersey") || n.Contains("helmet") || n.Contains("pant") ||
                n.Contains("skate") || n.Contains("glove") || n.Contains("legpad") ||
                n.Contains("mustache") || n.Contains("beard") || n.Contains("face") ||
                n.Contains("visor") || n.Contains("chestpad") || n.Contains("shoulder") ||
                n.Contains("strap"))
                return ObjectCategory.Player;
        }

        // Fallback to material name (cheap, only first material)
        if (mats.Length > 0 && mats[0] != null && !string.IsNullOrEmpty(mats[0].name))
        {
            var n = mats[0].name.ToLowerInvariant();
            if (n.Contains("stick") || n.Contains("blade")) return ObjectCategory.Stick;
            if (n.Contains("puck")) return ObjectCategory.Puck;
            if (n.Contains("helmet") || n.Contains("jersey") || n.Contains("body") || n.Contains("pad"))
                return ObjectCategory.Player;
        }

        return ObjectCategory.Other;
    }

    private static bool CategoryEnabled(ObjectCategory cat)
    {
        var p = ReskinProfileManager.currentProfile;
        switch (cat)
        {
            case ObjectCategory.Stick: return p.glossAffectSticks;
            case ObjectCategory.Player: return p.glossAffectPlayers;
            case ObjectCategory.Puck: return p.glossAffectPucks;
            default: return false;
        }
    }

    private static void ApplyOrRestoreMaterial(Tracked t)
    {
        if (t.material == null) return;
        if (CategoryEnabled(t.category)) ApplyGloss(t.material);
        else Restore(t);
    }

    private static void ApplyGloss(Material mat)
    {
        float s = Mathf.Clamp01(ReskinProfileManager.currentProfile.glossSmoothness);
        float roughness = 1f - s;

        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", s);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", s);
        if (mat.HasProperty("roughnessFactor")) mat.SetFloat("roughnessFactor", roughness);

        // URP ignores _Smoothness when a gloss-map texture is bound — disable those
        // keywords so the shader falls back to the float value we set.
        mat.DisableKeyword("_METALLICSPECGLOSSMAP");
        mat.DisableKeyword("_SPECGLOSSMAP");
        mat.DisableKeyword("_METALLICGLOSSMAP");
        mat.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");

        bool off = s <= 0.01f;
        if (off)
        {
            mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.DisableKeyword("_SPECULARHIGHLIGHTS_ON");
            mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            mat.DisableKeyword("_ENVIRONMENTREFLECTIONS_ON");
            if (mat.HasProperty("_SpecularHighlights")) mat.SetFloat("_SpecularHighlights", 0f);
            if (mat.HasProperty("_EnvironmentReflections")) mat.SetFloat("_EnvironmentReflections", 0f);
            if (mat.HasProperty("_GlossyReflections")) mat.SetFloat("_GlossyReflections", 0f);
        }
        else
        {
            mat.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.EnableKeyword("_SPECULARHIGHLIGHTS_ON");
            mat.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_ON");
            if (mat.HasProperty("_SpecularHighlights")) mat.SetFloat("_SpecularHighlights", 1f);
            if (mat.HasProperty("_EnvironmentReflections")) mat.SetFloat("_EnvironmentReflections", 1f);
            if (mat.HasProperty("_GlossyReflections")) mat.SetFloat("_GlossyReflections", 1f);
        }
    }

    private static void Restore(Tracked t)
    {
        var mat = t.material;
        if (mat == null) return;

        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", t.originalSmoothness);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", t.originalSmoothness);
        if (mat.HasProperty("roughnessFactor")) mat.SetFloat("roughnessFactor", t.originalRoughness);
        if (mat.HasProperty("_SpecularHighlights")) mat.SetFloat("_SpecularHighlights", t.originalSpecular);
        if (mat.HasProperty("_EnvironmentReflections")) mat.SetFloat("_EnvironmentReflections", t.originalEnvRefl);
        if (mat.HasProperty("_GlossyReflections")) mat.SetFloat("_GlossyReflections", t.originalGlossy);

        if (t.hadSpecHighlightsOn) { mat.EnableKeyword("_SPECULARHIGHLIGHTS_ON"); mat.DisableKeyword("_SPECULARHIGHLIGHTS_OFF"); }
        else { mat.DisableKeyword("_SPECULARHIGHLIGHTS_ON"); mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); }

        if (t.hadEnvReflOn) { mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_ON"); mat.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF"); }
        else { mat.DisableKeyword("_ENVIRONMENTREFLECTIONS_ON"); mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF"); }

        if (t.hadMetallicGlossMap) { mat.EnableKeyword("_METALLICSPECGLOSSMAP"); mat.EnableKeyword("_METALLICGLOSSMAP"); }
        if (t.hadSpecGlossMap) mat.EnableKeyword("_SPECGLOSSMAP");
        if (t.hadSmoothnessAlbedoAlpha) mat.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
    }
}
