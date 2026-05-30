using UnityEngine;

namespace ToasterReskinLoader.swappers;

public static class SwapperUtils
{
    private static readonly string[] TextureSlots = { "_MainTex", "_BaseMap", "baseColorTexture", "_Albedo" };

    /// <summary>
    /// Snapshot of a material's pre-swap texture state, captured per-slot so each slot
    /// can be restored to its own original value (not a single cached mainTexture).
    /// </summary>
    public class MaterialSnapshot
    {
        public Texture MainTexture;
        public Texture MainTex;
        public Texture BaseMap;
        public Texture BaseColorTexture;
        public Texture Albedo;

        public static MaterialSnapshot Capture(Material material)
        {
            return new MaterialSnapshot
            {
                MainTexture = material.mainTexture,
                MainTex = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null,
                BaseMap = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null,
                BaseColorTexture = material.HasProperty("baseColorTexture") ? material.GetTexture("baseColorTexture") : null,
                Albedo = material.HasProperty("_Albedo") ? material.GetTexture("_Albedo") : null,
            };
        }
    }

    /// <summary>
    /// Applies a texture to a renderer's material using all known texture property names.
    /// Sets material color to white so the texture displays without tinting.
    /// This is the single source of truth for texture application across all swappers.
    /// </summary>
    public static void ApplyTextureToMaterial(Material material, Texture2D texture)
    {
        material.mainTexture = texture;
        foreach (var slot in TextureSlots)
        {
            if (material.HasProperty(slot))
                material.SetTexture(slot, texture);
        }
        material.color = Color.white;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);
    }

    /// <summary>
    /// Restores a renderer's material to its original per-slot texture state with a tint color.
    /// Each texture slot is restored independently so we don't pollute slots that were null
    /// pre-swap (e.g. a vanilla material that was just an albedo color with no texture).
    /// </summary>
    public static void RestoreOriginalTexture(Material material, MaterialSnapshot snapshot, Color tintColor)
    {
        material.mainTexture = snapshot.MainTexture;
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", snapshot.MainTex);
        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", snapshot.BaseMap);
        if (material.HasProperty("baseColorTexture"))
            material.SetTexture("baseColorTexture", snapshot.BaseColorTexture);
        if (material.HasProperty("_Albedo"))
            material.SetTexture("_Albedo", snapshot.Albedo);
        material.color = tintColor;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", tintColor);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", tintColor);
    }
}
