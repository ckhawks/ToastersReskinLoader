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

    // Set a specific Puck to a specific ReskinEntry (can be null)
    private static void SetPuckTexture(Puck puck, ReskinRegistry.ReskinEntry reskinEntry)
    {
        try
        {
            // Get puck's MeshRenderer from its class
            // TODO fix this
            MeshRenderer puckMeshRenderer =
                puck.gameObject.transform.Find("puck").Find("Puck").GetComponent<MeshRenderer>();

            if (puckMeshRenderer == null)
            {
                Debug.LogError("No MeshRenderer found on GameObject Puck.");
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
            
            if (reskinEntry.Path == null)
            {
                // If ReskinEntry is null, make it the original puck again
                puckMeshRenderer.material.SetTexture(BaseMap, _originalTexture);
                puckMeshRenderer.material.SetTexture("_BumpMap", _originalBumpMap);
                // Plugin.Log("Original texture applied to property: _BaseMap");
            }
            else
            {
                // ReskinEntry has values, set puck to custom texture
                puckMeshRenderer.material.SetTexture(BaseMap, TextureManager.GetTexture(reskinEntry));
                puckMeshRenderer.material.SetTexture("_BumpMap", TextureManager.GetTextureFromFilePath(_puckBumpMapPath));
                // Plugin.Log("Texture applied to property: _BaseMap");
            }

            // Plugin.Log($"Set the puck texture to {reskinEntry.Name} {reskinEntry.Path}");
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
            var puckTexture = GetPuckForRandomizer();
            SetPuckTexture(__instance, puckTexture);
        }
    }
}