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
            }

            // The game update flipped the ice mesh's UV orientation, so custom ice
            // textures read backwards. Rotate the mapping 180 degrees to restore it.
            Vector2 flipScale = new Vector2(-1f, -1f);
            Vector2 flipOffset = new Vector2(1f, 1f);
            iceBottomMeshRenderer.material.SetTextureScale("_BaseMap", flipScale);
            iceBottomMeshRenderer.material.SetTextureOffset("_BaseMap", flipOffset);
            iceBottomMeshRenderer.material.SetTextureScale("baseColorTexture", flipScale);
            iceBottomMeshRenderer.material.SetTextureOffset("baseColorTexture", flipOffset);

            return;
        }
        catch (Exception e)
        {
            Plugin.LogError($"Error when setting ice texture: {e.Message}");
        }
    }

    public static bool UpdateIceSmoothness()
    {
        // The rink model was rebuilt (B1117) — GameObject.Find("Ice Top") no longer
        // reliably resolves. Locate by material name instead, like the rest of the
        // arena code.
        var renderers = ArenaSwapper.FindRenderersByMaterialName("Ice Top");
        if (renderers.Count == 0)
        {
            Plugin.LogWarning("Could not locate any Ice Top material renderer.");
            return false;
        }

        float smoothness = ReskinProfileManager.currentProfile.iceSmoothness;
        float roughness = 1f - smoothness;

        foreach (var renderer in renderers)
        {
            var mat = renderer.material; // instanced
            if (mat == null) continue;

            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("roughnessFactor")) mat.SetFloat("roughnessFactor", roughness);

            // URP ignores _Smoothness when a gloss-map texture is bound — force the
            // float through.
            mat.DisableKeyword("_METALLICSPECGLOSSMAP");
            mat.DisableKeyword("_SPECGLOSSMAP");
            mat.DisableKeyword("_METALLICGLOSSMAP");
            mat.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
        }

        return true;
    }
}