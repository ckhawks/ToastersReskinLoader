using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using Linework.SoftOutline;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace ToasterReskinLoader.swappers;

public static class PuckFXSwapper
{
    // Puck trails are disabled on PHL Official/Pickup servers to comply with league rules
    public static bool IsPHLServer = false;

    static readonly FieldInfo _lineRendererField = typeof(PuckElevationIndicator)
        .GetField("lineRenderer",
            BindingFlags.Instance | BindingFlags.NonPublic);

    static readonly FieldInfo _puckElevationIndicatorMaterialField = typeof(PuckElevationIndicator)
        .GetField("material",
            BindingFlags.Instance | BindingFlags.NonPublic);

    static readonly FieldInfo _puckOutlineSettingsField = typeof(PostProcessing)
        .GetField("puckOutlineSettings",
            BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    /// Applies the puck outline color and kernel size.
    /// Can be called at startup and whenever the user changes outline settings.
    /// </summary>
    public static void SetupPuckOutline()
    {
        try
        {
            PostProcessing ppm = Object.FindFirstObjectByType<PostProcessing>();
            if (ppm == null)
            {
                Plugin.LogWarning("PostProcessing not found, cannot set puck outline.");
                return;
            }

            // Documentation: https://linework.ameye.dev/soft-outline/
            SoftOutlineSettings puckOutlineSettings =
                (SoftOutlineSettings)_puckOutlineSettingsField.GetValue(ppm);

            if (puckOutlineSettings == null)
            {
                Plugin.LogWarning("puckOutlineSettings is null, cannot set puck outline.");
                return;
            }

            var profile = ReskinProfileManager.currentProfile;
            puckOutlineSettings.sharedColor = new Color(
                profile.puckFXOutlineColor.r,
                profile.puckFXOutlineColor.g,
                profile.puckFXOutlineColor.b,
                1f);
            puckOutlineSettings.kernelSize = profile.puckFXOutlineKernelSize;

            _puckOutlineSettingsField.SetValue(ppm, puckOutlineSettings);

            // Experimental: these properties exist on SoftOutlineSettings but did not appear
            // to have a visible effect in testing. They may work in future Linework versions
            // or with different configurations:
            //   puckOutlineSettings.blurSpread  (float) - Gaussian kernel spread
            //   puckOutlineSettings.intensity   (float) - Outline intensity multiplier
            //   puckOutlineSettings.gap         (float) - Gap between outline and object
        }
        catch (Exception e)
        {
            Plugin.LogError($"Error setting up puck outline: {e.Message}");
        }
    }

    /// <summary>
    /// Applies verticality line and elevation indicator settings to a PuckElevationIndicatorController.
    /// Called from Harmony postfix on PuckElevationIndicatorController.Start().
    /// </summary>
    public static void UpdateVerticalityLine(PuckElevationIndicatorController instance)
    {
        try
        {
            var profile = ReskinProfileManager.currentProfile;

            Color lineColor = new Color(
                profile.puckFXVerticalityLineColor.r,
                profile.puckFXVerticalityLineColor.g,
                profile.puckFXVerticalityLineColor.b,
                profile.puckFXVerticalityLineColor.a);

            PuckElevationIndicator puckElevationIndicator =
                instance.GetComponent<PuckElevationIndicator>();

            if (_lineRendererField == null)
            {
                Plugin.LogError("PuckFX: FieldInfo for lineRenderer is null!");
                return;
            }

            if (_puckElevationIndicatorMaterialField == null)
            {
                Plugin.LogError("PuckFX: FieldInfo for puckElevationIndicator material is null!");
                return;
            }

            Material puckElevationIndicatorMaterial =
                (Material)_puckElevationIndicatorMaterialField.GetValue(puckElevationIndicator);

            if (puckElevationIndicatorMaterial == null)
            {
                Plugin.LogError("PuckFX: puckElevationIndicatorMaterial is null!");
                return;
            }

            // Set the elevation indicator (outer ring) color
            Color elevationIndicatorColor = profile.puckFXElevationIndicatorColor;
            puckElevationIndicatorMaterial.SetColor("_Outer_Color", elevationIndicatorColor);

            // Set the verticality line color and alpha gradient
            LineRenderer lineRenderer =
                (LineRenderer)_lineRendererField.GetValue(puckElevationIndicator);

            lineRenderer.material.color = lineColor;
            var gradient = new Gradient();
            gradient.colorKeys = new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            };
            gradient.alphaKeys = new[]
            {
                new GradientAlphaKey(profile.puckFXVerticalityLineStartAlpha, 0f),
                new GradientAlphaKey(profile.puckFXVerticalityLineEndAlpha, 1f)
            };
            lineRenderer.colorGradient = gradient;

            // Experimental: LineRenderer has additional properties that could be exposed
            // but were not tested or did not produce visible changes:
            //   lineRenderer.startWidth / endWidth  - line thickness
            //   lineRenderer.widthMultiplier        - overall width scaling
            //   lineRenderer.numCapVertices          - rounded endpoints
        }
        catch (Exception e)
        {
            Plugin.LogError($"Error updating verticality line: {e.Message}");
        }
    }

    private const string TrailChildName = "ToasterPuckTrail";
    // One shared, unlit, vertex-colored material for every puck trail — the colour comes
    // from each trail's colorGradient, so the material just carries a transparent shader.
    private static Material _trailMaterial;

    /// <summary>
    /// Puck trail customization. Called from a Harmony postfix on PuckManager.AddPuck()
    /// and from ApplyAll() for existing pucks.
    ///
    /// B1117 removed the puck's vanilla trail (no "Trail" child / TrailRenderer on the
    /// puck prefab), so we attach and drive our OWN TrailRenderer, configured from the
    /// puckFXTrail* profile fields. The trail lives on a child GameObject of the puck so
    /// it's isolated from the puck's own components and is torn down with the puck.
    /// </summary>
    public static void UpdatePuckTrail(Puck puck)
    {
        try
        {
            var profile = ReskinProfileManager.currentProfile;
            if (puck == null) return;

            var puckRoot = puck.gameObject;
            var existing = puckRoot.transform.Find(TrailChildName)?.GetComponent<TrailRenderer>();

            // Off (feature disabled, or a PHL server) — silence an existing trail; don't
            // create one just to disable it.
            bool on = profile.puckFXTrailEnabled && !IsPHLServer;
            if (!on)
            {
                if (existing != null) { existing.emitting = false; existing.enabled = false; }
                return;
            }

            var trail = existing ?? CreatePuckTrail(puckRoot);
            if (trail == null) return;

            trail.enabled = true;
            trail.emitting = true;
            trail.time = profile.puckFXTrailLifetime;
            trail.startWidth = profile.puckFXTrailStartWidth;
            trail.endWidth = profile.puckFXTrailEndWidth;

            var c = profile.puckFXTrailColor;
            var rgb = new Color(c.r, c.g, c.b);
            trail.colorGradient = new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(rgb, 0f),
                    new GradientColorKey(rgb, 1f),
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(profile.puckFXTrailStartAlpha, 0f),
                    new GradientAlphaKey(profile.puckFXTrailEndAlpha, 1f),
                },
            };
        }
        catch (Exception e) { Plugin.LogError($"Error updating puck trail: {e.Message}"); }
    }

    private static TrailRenderer CreatePuckTrail(GameObject puckRoot)
    {
        var go = new GameObject(TrailChildName);
        go.transform.SetParent(puckRoot.transform, worldPositionStays: false);
        go.transform.localPosition = Vector3.zero;

        var tr = go.AddComponent<TrailRenderer>();
        tr.material = GetTrailMaterial();
        tr.alignment = LineAlignment.View;
        tr.textureMode = LineTextureMode.Stretch;
        tr.numCapVertices = 4;
        tr.numCornerVertices = 2;
        tr.minVertexDistance = 0.02f;
        tr.autodestruct = false;
        tr.receiveShadows = false;
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        return tr;
    }

    // Which URP shaders survive build stripping varies, so fall through a few names.
    private static Material GetTrailMaterial()
    {
        if (_trailMaterial != null) return _trailMaterial;
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Sprites/Default")
                     ?? Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Color");
        if (shader == null)
            Plugin.LogWarning("PuckFX: no trail shader found; the puck trail will be invisible.");
        _trailMaterial = shader != null ? new Material(shader) : null;
        return _trailMaterial;
    }

    /// <summary>
    /// Applies all Puck FX settings to currently existing objects in the scene.
    /// Call this when the user changes settings in the menu for instant feedback.
    /// </summary>
    public static void ApplyAll()
    {
        SetupPuckOutline();

        // Apply verticality line + elevation indicator to all active controllers
        try
        {
            var controllers = Object.FindObjectsByType<PuckElevationIndicatorController>(FindObjectsSortMode.None);
            foreach (var controller in controllers)
            {
                UpdateVerticalityLine(controller);
            }
        }
        catch (Exception e)
        {
            Plugin.LogError($"Error applying verticality line to existing pucks: {e.Message}");
        }

        // Apply trail settings to all active pucks (skipped on PHL servers)
        try
        {
            if (IsPHLServer && ReskinProfileManager.currentProfile.puckFXTrailEnabled)
            {
                Plugin.Log("Puck trail is disabled on PHL Official/Pickup servers.");
            }
            else
            {
                List<Puck> pucks = PuckManager.Instance.GetPucks();
                foreach (Puck puck in pucks)
                {
                    UpdatePuckTrail(puck);
                }
            }
        }
        catch (Exception e)
        {
            Plugin.LogError($"Error applying trail to existing pucks: {e.Message}");
        }

        // Apply silhouette color
        UpdatePuckSilhouetteColor();
    }

    static readonly FieldInfo _universalRendererDataField = typeof(PostProcessing)
        .GetField("universalRendererData",
            BindingFlags.Instance | BindingFlags.NonPublic);

    // RenderObjects' "settings" / "overrideMaterial" fields are resolved
    // reflectively because RenderObjects lives in com.unity.render-pipelines.universal
    // and we don't take a hard dep on that assembly. Cache by Type so settings-slider
    // drags don't pay the GetField cost on every color tick.
    private static FieldInfo _settingsField;
    private static System.Type _settingsFieldOwnerType;
    private static FieldInfo _overrideMaterialField;
    private static System.Type _overrideMaterialFieldOwnerType;

    /// <summary>
    /// Applies the puck silhouette (obstructed puck) color.
    /// The "Obstructed Puck" is a RenderObjects feature whose overrideMaterial
    /// uses the URP/Unlit shader with a _BaseColor property.
    /// </summary>
    public static void UpdatePuckSilhouetteColor()
    {
        try
        {
            PostProcessing ppm = Object.FindFirstObjectByType<PostProcessing>();
            if (ppm == null)
            {
                Plugin.LogWarning("PostProcessing not found, cannot set puck silhouette color.");
                return;
            }

            var rendererData = (UniversalRendererData)_universalRendererDataField.GetValue(ppm);
            if (rendererData == null)
            {
                Plugin.LogWarning("universalRendererData is null, cannot set puck silhouette color.");
                return;
            }

            var feature = rendererData.rendererFeatures.Find(x => x.name == "Puck Silhouette");
            if (feature == null)
            {
                Plugin.LogWarning("'Puck Silhouette' renderer feature not found.");
                return;
            }

            // The feature is a RenderObjects; get its settings.overrideMaterial
            var featureType = feature.GetType();
            if (_settingsField == null || _settingsFieldOwnerType != featureType)
            {
                _settingsField = featureType.GetField("settings",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                _settingsFieldOwnerType = featureType;
            }
            if (_settingsField == null)
            {
                Plugin.LogWarning("Could not find 'settings' field on RenderObjects feature.");
                return;
            }

            var settings = _settingsField.GetValue(feature);
            var settingsType = settings.GetType();
            if (_overrideMaterialField == null || _overrideMaterialFieldOwnerType != settingsType)
            {
                _overrideMaterialField = settingsType.GetField("overrideMaterial",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                _overrideMaterialFieldOwnerType = settingsType;
            }
            if (_overrideMaterialField == null)
            {
                Plugin.LogWarning("Could not find 'overrideMaterial' field on RenderObjects settings.");
                return;
            }

            Material mat = (Material)_overrideMaterialField.GetValue(settings);
            if (mat == null)
            {
                Plugin.LogWarning("Obstructed Puck override material is null.");
                return;
            }

            var profile = ReskinProfileManager.currentProfile;
            Color color = profile.puckFXSilhouetteColor;
            mat.SetColor("_BaseColor", color);
            mat.color = color;
        }
        catch (Exception e)
        {
            Plugin.LogError($"Error updating puck silhouette color: {e.Message}");
        }
    }

    // Harmony patch: apply verticality line + elevation indicator settings when the indicator starts
    [HarmonyPatch(typeof(PuckElevationIndicatorController), "Start")]
    public static class PuckElevationIndicatorStartPatch
    {
        [HarmonyPostfix]
        public static void Postfix(PuckElevationIndicatorController __instance)
        {
            UpdateVerticalityLine(__instance);
        }
    }

    // Harmony patch: apply trail settings when a puck is added
    [HarmonyPatch(typeof(PuckManager), nameof(PuckManager.AddPuck))]
    public static class PuckManagerAddPuckPatch
    {
        [HarmonyPostfix]
        public static void Postfix(PuckManager __instance, Puck puck)
        {
            UpdatePuckTrail(puck);
        }
    }

    // Harmony patch: detect PHL servers when server config changes (NetworkVariable sync)
    [HarmonyPatch(typeof(ServerManager), "OnServerChanged")]
    public static class ServerConfigurationPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Server oldServer, Server newServer)
        {
            string serverName = newServer.Name.ToString();
            string plainName = Regex.Replace(serverName, "<.*?>", "");
            Plugin.LogDebug("Server configuration changed: " + serverName);
            IsPHLServer = plainName.StartsWith("PHL Official") || plainName.StartsWith("PHL Pickup");

            // Re-apply trail state to any pucks that already spawned before this RPC
            try
            {
                List<Puck> pucks = PuckManager.Instance.GetPucks();
                foreach (Puck puck in pucks)
                {
                    UpdatePuckTrail(puck);
                }
            }
            catch (Exception e)
            {
                Plugin.LogDebug($"Could not update trails on existing pucks: {e.Message}");
            }

            if (IsPHLServer && ReskinProfileManager.currentProfile.puckFXTrailEnabled)
            {
                MonoBehaviourSingleton<UIManager>.Instance.ToastManager.ShowToast(
                    "Puck Trail Disabled",
                    "Puck trail is disabled on PHL Official/Pickup servers.",
                    5f);
            }
        }
    }
}
