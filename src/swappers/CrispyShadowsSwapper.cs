using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ToasterReskinLoader.swappers;

public static class CrispyShadowsSwapper
{
    static readonly FieldInfo _rpField = typeof(PostProcessing)
        .GetField("renderPipelineAsset",
            BindingFlags.Instance | BindingFlags.NonPublic);

    public static void Apply()
    {
        try
        {
            var profile = ReskinProfileManager.currentProfile;

            if (!profile.crispyShadowsEnabled)
            {
                Plugin.LogDebug("CrispyShadows: Disabled, skipping.");
                return;
            }

            PostProcessing pp = UnityEngine.Object.FindObjectOfType<PostProcessing>();
            if (pp == null)
            {
                Plugin.LogWarning("CrispyShadows: PostProcessing is null.");
                return;
            }

            if (_rpField == null)
            {
                Plugin.LogError("CrispyShadows: FieldInfo for renderPipelineAsset is null.");
                return;
            }

            var rpAsset = (UniversalRenderPipelineAsset)_rpField.GetValue(pp);
            if (rpAsset == null)
            {
                Plugin.LogError("CrispyShadows: renderPipelineAsset came back null.");
                return;
            }

            rpAsset.shadowCascadeCount = profile.shadowCascadeCount;
            rpAsset.shadowDistance = profile.shadowDistance;
            rpAsset.mainLightShadowmapResolution = profile.shadowResolution;

            var softShadowField = typeof(UniversalRenderPipelineAsset)
                .GetField("m_SoftShadowsSupported", BindingFlags.Instance | BindingFlags.NonPublic);
            softShadowField?.SetValue(rpAsset, profile.shadowSoftShadows);

            Plugin.LogDebug($"CrispyShadows: Applied (res={profile.shadowResolution}, dist={profile.shadowDistance}, cascades={profile.shadowCascadeCount}).");
        }
        catch (Exception e)
        {
            Plugin.LogError($"CrispyShadows: Error applying shadow settings: {e.Message}");
        }
    }

    /// <summary>
    /// Estimates VRAM usage for a shadow map at the given resolution.
    /// Shadow maps are typically 32-bit depth textures.
    /// </summary>
    public static string EstimateVRAM(int resolution)
    {
        long bytes = (long)resolution * resolution * 4;
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024f:F0} KB";
        return $"{bytes / (1024f * 1024f):F0} MB";
    }
}
