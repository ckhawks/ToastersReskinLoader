using System;
using UnityEngine;

namespace ToasterReskinLoader.swappers;

public static class SkyboxSwapper
{
    private static Material _originalSkybox;
    private static Material _skyboxInstance;

    public static void UpdateSkybox()
    {
        try
        {
            if (RenderSettings.skybox == null)
            {
                Plugin.LogWarning("No skybox material found in RenderSettings.");
                return;
            }

            // Cache the original skybox on first call so we always clone from it
            if (_originalSkybox == null)
            {
                _originalSkybox = RenderSettings.skybox;
            }

            // Destroy previous instance to avoid material leak
            if (_skyboxInstance != null)
            {
                UnityEngine.Object.Destroy(_skyboxInstance);
            }

            // Create a fresh instance from the original
            _skyboxInstance = new Material(_originalSkybox);
            RenderSettings.skybox = _skyboxInstance;

            _skyboxInstance.SetFloat("_AtmosphereThickness", ReskinProfileManager.currentProfile.skyboxAtmosphereThickness);
            _skyboxInstance.SetFloat("_Exposure", ReskinProfileManager.currentProfile.skyboxExposure);
            _skyboxInstance.SetFloat("_SunDisk", ReskinProfileManager.currentProfile.skyboxSunDisk);
            _skyboxInstance.SetFloat("_SunSize", ReskinProfileManager.currentProfile.skyboxSunSize);
            _skyboxInstance.SetFloat("_SunSizeConvergence", ReskinProfileManager.currentProfile.skyboxSunSizeConvergence);

            _skyboxInstance.SetColor("_GroundColor", ReskinProfileManager.currentProfile.skyboxGroundColor);
            _skyboxInstance.SetColor("_SkyTint", ReskinProfileManager.currentProfile.skyboxSkyTint);
        }
        catch (Exception e)
        {
            Plugin.LogError($"Error when updating skybox: {e.Message}");
        }
    }
}
