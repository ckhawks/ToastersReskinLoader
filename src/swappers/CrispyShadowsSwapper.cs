using System;
using System.Reflection;
using ToasterReskinLoader.qol;
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
            var cfg = QoLRunner.Instance?.Config;
            if (cfg == null)
            {
                Plugin.LogDebug("CrispyShadows: QoL config not ready, skipping.");
                return;
            }

            if (!cfg.crispyShadowsEnabled)
            {
                Plugin.LogDebug("CrispyShadows: Disabled, skipping.");
                return;
            }

            PostProcessing pp = UnityEngine.Object.FindFirstObjectByType<PostProcessing>();
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

            rpAsset.shadowCascadeCount = cfg.shadowCascadeCount;
            rpAsset.shadowDistance = cfg.shadowDistance;
            rpAsset.mainLightShadowmapResolution = cfg.shadowResolution;

            var softShadowField = typeof(UniversalRenderPipelineAsset)
                .GetField("m_SoftShadowsSupported", BindingFlags.Instance | BindingFlags.NonPublic);
            softShadowField?.SetValue(rpAsset, cfg.shadowSoftShadows);

            Plugin.LogDebug($"CrispyShadows: Applied (res={cfg.shadowResolution}, dist={cfg.shadowDistance}, cascades={cfg.shadowCascadeCount}).");
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
