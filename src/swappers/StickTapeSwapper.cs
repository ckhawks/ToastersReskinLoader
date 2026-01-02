using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ToasterReskinLoader.swappers;

public static class StickTapeSwapper
{
    static readonly FieldInfo _bladeTapeMeshRendererField = typeof(StickMesh)
        .GetField("bladeTapeMeshRenderer",
            BindingFlags.Instance | BindingFlags.NonPublic);

    static readonly FieldInfo _shaftTapeMeshRendererField = typeof(StickMesh)
        .GetField("shaftTapeMeshRenderer",
            BindingFlags.Instance | BindingFlags.NonPublic);

    // Helper method to apply texture to tape renderer (swaps to URP Lit and preserves detail maps)
    private static void ApplyTapeTexture(MeshRenderer renderer, Texture texture)
    {
        if (renderer == null || texture == null) return;

        Material mat = renderer.material;

        Plugin.LogDebug($"=== ApplyTapeTexture START ===");
        Plugin.LogDebug($"Original shader: {mat.shader.name}");

        // Extract existing textures and values from current material BEFORE shader swap
        Texture normalMap = mat.GetTexture("_Normal");
        Texture heightMap = mat.GetTexture("_Height");
        Texture maskMap = mat.GetTexture("_Mask");

        Plugin.LogDebug($"Extracted textures - Normal: {(normalMap != null ? normalMap.name : "null")}, Height: {(heightMap != null ? heightMap.name : "null")}, Mask: {(maskMap != null ? maskMap.name : "null")}");

        float normalStrength = mat.HasProperty("_Normal_Strength") ? mat.GetFloat("_Normal_Strength") : 1.0f;
        float heightStrength = mat.HasProperty("_Height_Strength") ? mat.GetFloat("_Height_Strength") : 0.1f;
        float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0.0f;
        float smoothness = mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") : 0.5f;

        Plugin.LogDebug($"Extracted values - Metallic: {metallic}, Smoothness: {smoothness}");

        // Swap to URP Lit shader
        mat.shader = Shader.Find("Universal Render Pipeline/Lit");
        mat.color = new Color(1f, 1f, 1f, 1f); // Keep white but preserve texture alpha

        Plugin.LogDebug($"Swapped to URP Lit shader: {mat.shader.name}");

        // Enable alpha support for transparent textures
        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1); // Transparent
            Plugin.LogDebug($"✓ Set _Surface to 1 (Transparent)");
        }

        if (mat.HasProperty("_AlphaClip"))
        {
            mat.SetFloat("_AlphaClip", 0); // Disable hard cutoff for smooth transparency
            Plugin.LogDebug($"✓ Set _AlphaClip to 0 (smooth alpha)");
        }

        if (mat.HasProperty("_Blend"))
        {
            mat.SetFloat("_Blend", 0); // Alpha blend
            Plugin.LogDebug($"✓ Set _Blend to 0 (Alpha blend)");
        }

        // Apply new albedo texture (skip compositing for now due to resize issues)
        if (mat.HasProperty("_BaseMap"))
        {
            mat.SetTexture("_BaseMap", texture);
            Plugin.LogDebug($"✓ Set _BaseMap to {texture.name}");
        }
        else
        {
            Plugin.LogDebug($"✗ _BaseMap property not found on URP Lit");
        }

        // Copy normal map
        if (normalMap != null)
        {
            if (mat.HasProperty("_NormalMap"))
            {
                mat.SetTexture("_NormalMap", normalMap);
                Plugin.LogDebug($"✓ Set _NormalMap to {normalMap.name}");
            }
            else
            {
                Plugin.LogDebug($"✗ _NormalMap property not found");
            }

            if (mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", normalMap);
                Plugin.LogDebug($"✓ Set _BumpMap to {normalMap.name}");
            }
            else
            {
                Plugin.LogDebug($"✗ _BumpMap property not found");
            }

            // Set normal map strength (URP Lit uses _BumpScale, multiply by 10 to make more visible)
            if (mat.HasProperty("_BumpScale"))
            {
                float bumpScale = normalStrength * 10.0f;
                mat.SetFloat("_BumpScale", bumpScale);
                Plugin.LogDebug($"✓ Set _BumpScale to {bumpScale} (original: {normalStrength})");
            }
            else
            {
                Plugin.LogDebug($"✗ _BumpScale property not found");
            }
        }
        else
        {
            Plugin.LogDebug($"- No normal map to copy");
        }

        // Copy metallic and smoothness
        if (mat.HasProperty("_Metallic"))
        {
            mat.SetFloat("_Metallic", metallic);
            Plugin.LogDebug($"✓ Set _Metallic to {metallic}");
        }
        else
        {
            Plugin.LogDebug($"✗ _Metallic property not found");
        }

        if (mat.HasProperty("_Smoothness"))
        {
            mat.SetFloat("_Smoothness", smoothness);
            Plugin.LogDebug($"✓ Set _Smoothness to {smoothness}");
        }
        else
        {
            Plugin.LogDebug($"✗ _Smoothness property not found");
        }

        // Skip mask occlusion map since we're using texture's built-in alpha transparency
        Plugin.LogDebug($"- Skipping occlusion map (using texture's alpha transparency)");

        // Copy height as parallax (if URP Lit supports it)
        if (heightMap != null)
        {
            if (mat.HasProperty("_ParallaxMap"))
            {
                mat.SetTexture("_ParallaxMap", heightMap);
                Plugin.LogDebug($"✓ Set _ParallaxMap to {heightMap.name}");
            }
            else
            {
                Plugin.LogDebug($"✗ _ParallaxMap property not found");
            }

            // Set height/parallax map strength (URP Lit uses _Parallax, multiply by 20 to make more visible)
            if (mat.HasProperty("_Parallax"))
            {
                float parallaxValue = heightStrength * 20.0f;
                mat.SetFloat("_Parallax", parallaxValue);
                Plugin.LogDebug($"✓ Set _Parallax to {parallaxValue} (original: {heightStrength})");
            }
            else
            {
                Plugin.LogDebug($"✗ _Parallax property not found");
            }
        }
        else
        {
            Plugin.LogDebug($"- No height map to copy as parallax");
        }

        Plugin.LogDebug($"=== ApplyTapeTexture COMPLETE ===");
    }

    // Helper method to read texture pixels using RenderTexture (works with non-readable textures)
    private static Texture2D ReadTexturePixels(Texture texture)
    {
        try
        {
            RenderTexture rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(texture, rt);

            RenderTexture prevRT = RenderTexture.active;
            RenderTexture.active = rt;

            Texture2D readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            readable.Apply();

            RenderTexture.active = prevRT;
            RenderTexture.ReleaseTemporary(rt);

            return readable;
        }
        catch (Exception ex)
        {
            Plugin.LogError($"Failed to read texture pixels: {ex.Message}");
            return null;
        }
    }

    // Helper method to composite two textures: RGB from source, A from mask
    private static Texture2D CompositeTextureWithAlpha(Texture sourceTexture, Texture maskTexture)
    {
        if (sourceTexture == null || maskTexture == null) return null;

        try
        {
            // Read pixels from both textures using RenderTexture (works with non-readable textures)
            Texture2D sourceReadable = ReadTexturePixels(sourceTexture);
            Texture2D maskReadable = ReadTexturePixels(maskTexture);

            if (sourceReadable == null || maskReadable == null)
            {
                if (sourceReadable != null) UnityEngine.Object.Destroy(sourceReadable);
                if (maskReadable != null) UnityEngine.Object.Destroy(maskReadable);
                return null;
            }

            int width = sourceReadable.width;
            int height = sourceReadable.height;

            Plugin.LogDebug($"Read textures - Source: {width}x{height}, Mask: {maskReadable.width}x{maskReadable.height}");

            Color[] sourcePixels = sourceReadable.GetPixels();
            Color[] maskPixels = maskReadable.GetPixels();

            Plugin.LogDebug($"Sample source pixel [0]: {sourcePixels[0]}, Sample mask pixel [0]: {maskPixels[0]}");

            // Log multiple sample mask pixels to see if it's uniform
            int maskSampleCount = Mathf.Min(5, maskPixels.Length);
            for (int s = 0; s < maskSampleCount; s++)
            {
                int idx = (s * maskPixels.Length) / maskSampleCount;
                Plugin.LogDebug($"  Mask pixel [{idx}]: {maskPixels[idx]} (grayscale: {maskPixels[idx].grayscale})");
            }

            // Scale mask to match source dimensions if needed
            if (maskReadable.width != width || maskReadable.height != height)
            {
                // Use Resize to properly scale while preserving data
                maskReadable.Resize(width, height, TextureFormat.RGBA32, false);
                maskPixels = maskReadable.GetPixels();
                Plugin.LogDebug($"Resized mask to {width}x{height}");

                // Log scaled mask samples
                for (int s = 0; s < maskSampleCount; s++)
                {
                    int idx = (s * maskPixels.Length) / maskSampleCount;
                    Plugin.LogDebug($"  Resized mask pixel [{idx}]: {maskPixels[idx]} (grayscale: {maskPixels[idx].grayscale})");
                }
            }

            // Create output texture
            Texture2D composited = new Texture2D(width, height, TextureFormat.RGBA32, false);

            // Composite: RGB from source, A from mask's grayscale value (brightness)
            Color[] compositedPixels = new Color[sourcePixels.Length];
            for (int i = 0; i < sourcePixels.Length; i++)
            {
                compositedPixels[i] = new Color(
                    sourcePixels[i].r,
                    sourcePixels[i].g,
                    sourcePixels[i].b,
                    maskPixels[i].grayscale  // Use mask's brightness as alpha
                );
            }

            Plugin.LogDebug($"Sample composited pixel [0]: {compositedPixels[0]}");

            composited.SetPixels(compositedPixels);
            composited.Apply();

            // Clean up
            UnityEngine.Object.Destroy(sourceReadable);
            UnityEngine.Object.Destroy(maskReadable);

            Plugin.LogDebug($"✓ Composited textures: {width}x{height} (RGB from source, A from mask)");
            return composited;
        }
        catch (Exception ex)
        {
            Plugin.LogError($"✗ Failed to composite textures: {ex.Message}");
            return null;
        }
    }

    // Helper method to apply color to tape renderer
    private static void ApplyTapeColor(MeshRenderer renderer, Color color)
    {
        if (renderer == null) return;

        renderer.material.SetColor("_Color", color);
        renderer.material.color = color;
        Plugin.LogDebug($"Applied color {color} to tape material");
    }

    // Helper method to discover all texture properties on a material dynamically using reflection
    private static void LogAllTextureProperties(MeshRenderer renderer, string rendererName)
    {
        if (renderer == null) return;

        try
        {
            Material mat = renderer.material;
            Shader shader = mat.shader;

            Plugin.LogDebug($"{rendererName} - Attempting dynamic property discovery:");

            // Try to get shader properties via reflection
            var shaderType = shader.GetType();
            var fieldsInfo = shaderType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (fieldsInfo.Length > 0)
            {
                Plugin.LogDebug($"  Found {fieldsInfo.Length} fields on Shader");
                foreach (var field in fieldsInfo)
                {
                    Plugin.LogDebug($"    - {field.Name} ({field.FieldType})");
                }
            }

            // Try Material's internal data
            var matType = mat.GetType();
            var matFields = matType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (matFields.Length > 0)
            {
                Plugin.LogDebug($"  Found {matFields.Length} fields on Material");
                foreach (var field in matFields.Take(10)) // Limit output
                {
                    if (field.Name.Contains("Property") || field.Name.Contains("Shader"))
                    {
                        Plugin.LogDebug($"    - {field.Name} ({field.FieldType})");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.LogError($"Error discovering properties dynamically: {ex.Message}");
        }
    }

    public static void SetStickTapeColors(Stick stick)
    {
        try
        {
            if (stick?.StickMesh == null)
            {
                Plugin.LogError("Stick or StickMesh is null");
                return;
            }

            StickMesh stickMesh = stick.StickMesh;

            // Get tape mesh renderers via reflection
            var bladeTapeMeshRenderer = (MeshRenderer)_bladeTapeMeshRendererField.GetValue(stickMesh);
            var shaftTapeMeshRenderer = (MeshRenderer)_shaftTapeMeshRendererField.GetValue(stickMesh);

            // Log shader information and discover texture properties dynamically
            if (bladeTapeMeshRenderer != null)
            {
                Plugin.LogDebug($"Blade tape mesh renderer shader: {bladeTapeMeshRenderer.material.shader.name}");
                LogAllTextureProperties(bladeTapeMeshRenderer, "Blade tape");
            }

            if (shaftTapeMeshRenderer != null)
            {
                Plugin.LogDebug($"Shaft tape mesh renderer shader: {shaftTapeMeshRenderer.material.shader.name}");
                LogAllTextureProperties(shaftTapeMeshRenderer, "Shaft tape");
            }

            if (bladeTapeMeshRenderer != null)
            {
                // Proof of concept: Apply a texture to blade tape using a stick reskin
                var stickReskins = ReskinRegistry.GetReskinEntriesByType("tape_attacker_blade");
                if (stickReskins.Count > 0)
                {
                    var stickTexture = TextureManager.GetTexture(stickReskins[0]);
                    ApplyTapeTexture(bladeTapeMeshRenderer, stickTexture);
                    Plugin.LogDebug($"Applied stick texture to blade tape for {stick.Player.Username.Value}");
                }
            }

            if (shaftTapeMeshRenderer != null)
            {
                // Proof of concept: Apply a color to shaft tape
                // ApplyTapeColor(shaftTapeMeshRenderer, Color.blue);
                // Proof of concept: Apply a texture to blade tape using a stick reskin
                var stickReskins = ReskinRegistry.GetReskinEntriesByType("tape_attacker_shaft");
                if (stickReskins.Count > 0)
                {
                    var stickTexture = TextureManager.GetTexture(stickReskins[0]);
                    ApplyTapeTexture(shaftTapeMeshRenderer, stickTexture);
                    Plugin.LogDebug($"Applied stick texture to shaft tape for {stick.Player.Username.Value}");
                }
                Plugin.LogDebug($"Applied blue color to shaft tape for {stick.Player.Username.Value}");
            }
        }
        catch (Exception ex)
        {
            Plugin.LogError($"Error setting stick tape colors: {ex.Message}");
        }
    }
}
