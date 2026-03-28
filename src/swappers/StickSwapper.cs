using System;
using System.Reflection;
using UnityEngine;

namespace ToasterReskinLoader.swappers;

public static class StickSwapper
{
    static readonly FieldInfo _stickMeshRendererField = typeof(StickMesh)
        .GetField("stickMeshRenderer",
            BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    /// Applies a custom texture to a StickMesh's renderer material.
    /// Used for both in-game sticks and locker room preview sticks.
    /// </summary>
    public static void SetStickMeshTexture(StickMesh stickMesh, ReskinRegistry.ReskinEntry reskin, PlayerRole role)
    {
        try
        {
            Plugin.LogDebug($"Trying to replace stick mesh texture");
            if (stickMesh == null)
            {
                Plugin.LogError($"stickMesh is null!");
                return;
            }

            MeshRenderer stickMeshRenderer = (MeshRenderer)_stickMeshRendererField.GetValue(stickMesh);
            if (stickMeshRenderer == null)
            {
                Plugin.LogError($"stickMeshRenderer is null!");
                return;
            }

            Texture2D texture2D = TextureManager.GetTexture(reskin);
            if (texture2D == null)
            {
                Plugin.LogError($"texture2D is null!");
                return;
            }

            Plugin.LogDebug($"Material: {stickMeshRenderer.material.name}");
            Plugin.LogDebug($"Shader: {stickMeshRenderer.material.shader.name}");

            ApplyTextureToStickMaterial(stickMeshRenderer.material, texture2D);
            Plugin.LogDebug("Texture applied to stick mesh!");
        }
        catch (Exception ex)
        {
            Plugin.LogError($"Error when setting stick mesh texture: {ex.Message}");
        }
    }

    /// <summary>
    /// Applies a custom texture to a Stick component (in-game player stick).
    /// </summary>
    public static void SetStickTexture(Stick stick, ReskinRegistry.ReskinEntry reskin)
    {
        try
        {
            Plugin.LogDebug($"Trying to replace stick texture");
            StickMesh stickMesh = stick.StickMesh;
            if (stickMesh == null)
            {
                Plugin.LogError($"stickMesh is null!");
                return;
            }

            MeshRenderer stickMeshRenderer = (MeshRenderer)_stickMeshRendererField.GetValue(stickMesh);
            if (stickMeshRenderer == null)
            {
                Plugin.LogError($"stickMeshRenderer is null!");
                return;
            }

            // Reset to normal skin
            if (reskin == null || reskin.Path == null)
            {
                stickMesh.SetSkinID(stick.Player.GetPlayerStickSkinID(), stick.Player.Team);
                return;
            }

            Texture2D texture2D = TextureManager.GetTexture(reskin);
            if (texture2D == null)
            {
                Plugin.LogError($"texture2D is null!");
                return;
            }

            Plugin.LogDebug($"Material: {stickMeshRenderer.material.name}");
            Plugin.LogDebug($"Shader: {stickMeshRenderer.material.shader.name}");

            ApplyTextureToStickMaterial(stickMeshRenderer.material, texture2D);
            Plugin.LogDebug("Texture applied to stick!");
        }
        catch (Exception ex)
        {
            Plugin.LogError($"Error when setting stick texture: {ex.Message}");
        }
    }

    /// <summary>
    /// Applies texture to a stick material, trying all known property names
    /// for b310's Shader Graphs/Stick Simple and other potential shaders.
    /// Also clears holographic/iridescent/emission layers that some vanilla skins use,
    /// so the custom texture displays cleanly without visual artifacts.
    /// </summary>
    private static void ApplyTextureToStickMaterial(Material material, Texture2D texture)
    {
        Plugin.LogDebug($"Swapping stick shader from '{material.shader.name}' to URP/Lit");

        // Swap the shader to URP Lit to completely remove holographic/iridescent
        // effects baked into the vanilla Shader Graph. The old code replaced the
        // entire material from stickMaterialMap; swapping the shader achieves the
        // same result without needing that dictionary.
        material.shader = Shader.Find("Universal Render Pipeline/Lit");

        // Apply the custom texture
        material.mainTexture = texture;
        material.SetTexture("_BaseMap", texture);
        material.SetTexture("_MainTex", texture);

        // White tint so the texture displays at full brightness
        material.color = Color.white;
        material.SetColor("_BaseColor", Color.white);

        // Clean material state
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Smoothness", 0.5f);
    }
}
