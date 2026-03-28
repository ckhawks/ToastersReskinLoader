using System;
using UnityEngine;

namespace ToasterReskinLoader.swappers;

public static class IceSwapper
{
    private static Texture originalTexture;

    public static void SetIceTexture()
    {
        try
        {
            ReskinRegistry.ReskinEntry reskinEntry = ReskinProfileManager.currentProfile.ice;

            // var (go, renderer, material, originalTexture2) = FindUsageOfTexture();
            // Plugin.Log($"go: {go.name}, renderer: {renderer.name}, material: {material.name}, originalTexture: {originalTexture2.name}");
        
            GameObject iceBottomGameObject = GameObject.Find("Ice Bottom");

            if (iceBottomGameObject == null)
            {
                Plugin.LogError($"Could not locate Ice Bottom GameObject.");
                return;
            }
        
            MeshRenderer iceBottomMeshRenderer = iceBottomGameObject.GetComponent<MeshRenderer>();

            if (iceBottomMeshRenderer == null)
            {
                Plugin.LogError("No MeshRenderer found on GameObject Ice Bottom.");
                return;
            }
        
            // string texturePropertyName = SwapperUtils.FindTextureProperty(iceBottomMeshRenderer.material);
            // if (texturePropertyName == null)
            // {
            //     Plugin.LogError("No texture property found in the shader.");
            //     return;
            // }
        
            if (originalTexture == null)
            {
                originalTexture = iceBottomMeshRenderer.material.GetTexture("_BaseMap");
            }
        
            // If setting to unchanged,
            if (reskinEntry == null || reskinEntry.Path == null)
            {
                iceBottomMeshRenderer.material.SetTexture("_BaseMap", originalTexture);
                iceBottomMeshRenderer.material.SetTexture("baseColorTexture", originalTexture);
            }
            else
            {
                Texture2D texture2D = TextureManager.GetTexture(reskinEntry);
                iceBottomMeshRenderer.material.SetTexture("_BaseMap", texture2D);
                iceBottomMeshRenderer.material.SetTexture("baseColorTexture", texture2D);
                // Plugin.Log("Texture applied to property: _BaseMap");
            }
        
            // Plugin.Log($"Set the Ice Bottom texture to {reskinEntry.Name} {reskinEntry.Path}");
            return;
        }
        catch (Exception e)
        {
            Plugin.LogError($"Error when setting ice texture: {e.Message}");
        }
    }

    public static bool UpdateIceSmoothness()
    {
        GameObject iceTopGameObject = GameObject.Find("Ice Top");

        if (iceTopGameObject == null)
        {
            Plugin.LogError($"Could not locate Ice Top GameObject.");
            return false;
        }
        
        MeshRenderer iceTopMeshRenderer = iceTopGameObject.GetComponent<MeshRenderer>();

        if (iceTopMeshRenderer == null)
        {
            Plugin.LogError("No MeshRenderer found on GameObject Ice Top.");
            return false;
        }
        
        iceTopMeshRenderer.material.SetFloat("_Smoothness", ReskinProfileManager.currentProfile.iceSmoothness);
        
        // Plugin.Log($"Set the Ice Top smoothness to {ReskinProfileManager.currentProfile.iceSmoothness}");
        return true;
    }
    
    private const string TargetTextureName = "hockey_rink";
    
    /// <summary>
    /// Scans the scene to find a GameObject, MeshRenderer, and Material that uses the
    /// specified texture. This should be called sparingly due to performance.
    /// </summary>
    /// <returns>
    /// A tuple containing:
    /// - GameObject: The GameObject found, or null.
    /// - MeshRenderer (or other Renderer): The renderer found, or null.
    /// - Material: The material found, or null.
    /// - Texture2D: The original texture that was found, or null.
    /// </returns>
    public static (GameObject go, Renderer renderer, Material material, Texture2D texture)
        FindUsageOfTexture()
    {
        // Find all active Renderers in the current scene.
        // This is a slow operation, avoid calling in Update.
        Renderer[] allRenderers = GameObject.FindObjectsOfType<Renderer>();

        foreach (Renderer renderer in allRenderers)
        {
            if (renderer == null || renderer.sharedMaterials == null)
            {
                continue;
            }

            foreach (Material mat in renderer.sharedMaterials)
            {
                if (mat == null)
                {
                    continue;
                }

                // Check the mainTexture property first (common for many shaders)
                Texture mainTex = mat.mainTexture;
                if (mainTex != null && mainTex.name == TargetTextureName)
                {
                    Plugin.LogDebug($"Found '{TargetTextureName}' on material '{mat.name}' " +
                              $"on renderer '{renderer.gameObject.name}'. (Main Texture)");
                    return (renderer.gameObject, renderer, mat, mainTex as Texture2D);
                }

                // --- ADVANCED: Check other common texture properties by name ---
                // You might need to know the specific shader properties if mainTexture isn't it.
                // Examples: _BaseMap (URP), _Albedo (Standard), _MainTex (Legacy/Custom)
                foreach (string propName in new string[] { "_MainTex", "_BaseMap", "_Albedo" }) // Add more as needed
                {
                    if (mat.HasProperty(propName))
                    {
                        Texture otherTex = mat.GetTexture(propName);
                        if (otherTex != null && otherTex.name == TargetTextureName)
                        {
                            Plugin.LogDebug($"Found '{TargetTextureName}' on material '{mat.name}' " +
                                            $"on renderer '{renderer.gameObject.name}'. (Property: {propName})");
                            return (renderer.gameObject, renderer, mat, otherTex as Texture2D);
                        }
                    }
                }
                // --- END ADVANCED ---
            }
        }

        Debug.LogWarning($"Texture '{TargetTextureName}' not found on any active renderer in the scene.");
        return (null, null, null, null); // Not found
    }
}