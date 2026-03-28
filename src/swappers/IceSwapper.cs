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

        return true;
    }
}