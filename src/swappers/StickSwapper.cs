using System;
using System.Collections.Generic;
using System.Reflection;
using AYellowpaper.SerializedCollections;
using UnityEngine;

namespace ToasterReskinLoader.swappers;

public static class StickSwapper
{
    static readonly FieldInfo _stickMeshRendererField = typeof(StickMesh)
        .GetField("stickMeshRenderer", 
            BindingFlags.Instance | BindingFlags.NonPublic);
    
    static readonly FieldInfo _stickMaterialMapField = typeof(StickMesh)
        .GetField("stickMaterialMap", 
            BindingFlags.Instance | BindingFlags.NonPublic);
    
    // private static Dictionary<ulong, Texture> originalTextures = new Dictionary<ulong, Texture>();

    // Helper method to apply texture to stick based on role
    private static void ApplyStickTexture(MeshRenderer stickMeshRenderer, SerializedDictionary<string, Material> stickMaterialMap, Texture2D texture, PlayerRole role)
    {
        // Select material based on role
        if (role == PlayerRole.Attacker)
            stickMeshRenderer.material = stickMaterialMap["red_beta_attacker"];
        else if (role == PlayerRole.Goalie)
            stickMeshRenderer.material = stickMaterialMap["red_beta_goalie"];

        stickMeshRenderer.material.SetTexture("_Texture", texture);
        Plugin.LogDebug("Texture applied to property: _Texture");

        // Ensure the renderer is enabled
        if (!stickMeshRenderer.enabled)
        {
            Plugin.LogError("stickMeshRenderer is disabled. Enabling it.");
            stickMeshRenderer.enabled = true;
        }
    }

    // This is only used for the changing room stick because it will only be used to set reskins, not the original skins
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
            
            // Plugin.Log($"file path: {textureFilePath}");
            Texture2D texture2D = TextureManager.GetTexture(reskin);
            if (texture2D == null)
            {
                Plugin.LogError($"texture2D is null!");
                return;
            }

            // Debugging: Log material and shader
            Plugin.LogDebug($"Material: {stickMeshRenderer.material.name}");
            Plugin.LogDebug($"Shader: {stickMeshRenderer.material.shader.name}");

            SerializedDictionary<string, Material> stickMaterialMap =
                (SerializedDictionary<string, Material>)_stickMaterialMapField.GetValue(stickMesh);

            ApplyStickTexture(stickMeshRenderer, stickMaterialMap, texture2D, role);

            Plugin.LogDebug("Texture applied to stick GameObject!");
            return;
        }
        catch (Exception ex)
        {
            Plugin.LogError($"Error when setting stick mesh texture: {ex.Message}");
        }
    }
    
    public static void SetStickTexture(Stick stick, ReskinRegistry.ReskinEntry reskin)
    {
        try
        {
            Plugin.LogDebug($"Trying to replace stick texture, postspawn");
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
                stickMesh.SetSkin(stick.Player.Team.Value, stick.Player.GetPlayerStickSkin().ToString());
                return;
            }

            // Plugin.Log($"file path: {textureFilePath}");
            Texture2D texture2D = TextureManager.GetTexture(reskin);
            if (texture2D == null)
            {
                Plugin.LogError($"texture2D is null!");
                return;
            }

            // Debugging: Log material and shader
            Plugin.LogDebug($"Material: {stickMeshRenderer.material.name}");
            Plugin.LogDebug($"Shader: {stickMeshRenderer.material.shader.name}");

            SerializedDictionary<string, Material> stickMaterialMap =
                (SerializedDictionary<string, Material>)_stickMaterialMapField.GetValue(stickMesh);
            // foreach (KeyValuePair<string, Material> pair in stickMaterialMap)
            // {
            //     Plugin.Log($"Material key: {pair.Key}");
            //     Plugin.Log($"Material value: {pair.Value.name} - {pair.Value}");
            //     // if (pair.Key.Contains(search))
            // }

            ApplyStickTexture(stickMeshRenderer, stickMaterialMap, texture2D, stick.Player.Role.Value);

            Plugin.LogDebug("Texture applied to stick GameObject!");
            return;
        }
        catch (Exception ex)
        {
            Plugin.LogError($"Error when setting stick texture: {ex.Message}");
        }
    }
    
    public static Material FindMaterialByRendererScan(string materialNamePartial)
    {
        // Get all active Renderers in the current scene
        Renderer[] allRenderers = GameObject.FindObjectsOfType<Renderer>();

        foreach (Renderer renderer in allRenderers)
        {
            // Iterate through all shared materials on this renderer
            // An object might have multiple sub-meshes, each with a different material
            foreach (Material mat in renderer.sharedMaterials)
            {
                if (mat != null && mat.name.Contains(materialNamePartial))
                {
                    Plugin.Log($"Found material '{mat.name}' on object '{renderer.gameObject.name}'");
                    return mat; // Return the first match
                }
            }
        }

        Debug.LogWarning($"Material '{materialNamePartial}' not found on any active renderer in the scene.");
        return null;
    }
}