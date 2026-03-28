using UnityEngine;

namespace ToasterReskinLoader.swappers;

public static class SwapperUtils
{
    /// <summary>
    /// Applies a texture to a renderer's material using all known texture property names.
    /// Sets material color to white so the texture displays without tinting.
    /// This is the single source of truth for texture application across all swappers.
    /// </summary>
    public static void ApplyTextureToMaterial(Material material, Texture2D texture)
    {
        material.mainTexture = texture;
        material.SetTexture("_MainTex", texture);
        material.SetTexture("_BaseMap", texture);
        material.SetTexture("baseColorTexture", texture);
        material.SetTexture("_Albedo", texture);
        material.color = Color.white;
        material.SetColor("_BaseColor", Color.white);
        material.SetColor("_Color", Color.white);
    }

    /// <summary>
    /// Restores a renderer's material to its original texture with a tint color.
    /// Used when resetting a swapped texture back to vanilla.
    /// </summary>
    public static void RestoreOriginalTexture(Material material, Texture originalTexture, Color tintColor)
    {
        material.mainTexture = originalTexture;
        material.color = tintColor;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", tintColor);
    }
}
