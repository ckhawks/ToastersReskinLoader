using System;
using System.Collections.Generic;
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

    // Cache original shader (same for all tape parts on a player) and colors for restoration
    private static Dictionary<ulong, Shader> originalShaders =
        new Dictionary<ulong, Shader>();
    private static Dictionary<(ulong, string), Color> originalColors =
        new Dictionary<(ulong, string), Color>();

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

    // Apply tape customization based on mode: Unchanged (restore original), RGB (color), or Textured (swap shader and apply texture)
    private static void ApplyTapeCustomization(
        MeshRenderer renderer,
        ulong playerId,
        string tapePart,
        string mode,
        ReskinRegistry.ReskinEntry textureEntry,
        Color rgbColor)
    {
        if (renderer == null) return;

        var colorCacheKey = (playerId, tapePart);
        Material mat = renderer.material;

        // Cache original shader once per player (same for all tape parts)
        if (!originalShaders.ContainsKey(playerId))
        {
            originalShaders[playerId] = mat.shader;
            Plugin.LogDebug($"Cached original shader for player {playerId}: {mat.shader.name}");
        }

        // Cache original color per tape part
        if (!originalColors.ContainsKey(colorCacheKey))
        {
            originalColors[colorCacheKey] = mat.color;
            Plugin.LogDebug($"Cached original color for player {playerId} {tapePart}: {mat.color}");
        }

        // Apply based on mode
        switch (mode)
        {
            case "Unchanged":
                // Restore original shader and color (vanilla tapes don't have textures)
                mat.shader = originalShaders[playerId];
                mat.color = originalColors[colorCacheKey];
                Plugin.LogDebug($"Restored original tape for {tapePart}");
                break;

            case "RGB":
                // Keep original shader, apply custom color
                mat.shader = originalShaders[playerId];
                mat.color = rgbColor;
                Plugin.LogDebug($"Applied RGB color {rgbColor} to {tapePart}");
                break;

            case "Textured":
                // Apply texture with existing ApplyTapeTexture logic
                if (textureEntry?.Path != null)
                {
                    ApplyTapeTexture(renderer, TextureManager.GetTexture(textureEntry));
                    Plugin.LogDebug($"Applied texture {textureEntry.Name} to {tapePart}");
                }
                else
                {
                    Plugin.LogError($"Textured mode selected but no texture entry provided for {tapePart}");
                }
                break;

            default:
                Plugin.LogError($"Unknown tape mode: {mode}");
                break;
        }
    }

    // Set tape customization for personal player's stick only
    public static void SetStickTapeForPlayer(Stick stick)
    {
        try
        {
            // Validation
            if (stick?.StickMesh == null)
            {
                Plugin.LogError("Stick or StickMesh is null");
                return;
            }

            if (stick.Player == null)
            {
                Plugin.LogError("Stick player is null");
                return;
            }

            PlayerTeam team = stick.Player.Team.Value;
            if (team is not (PlayerTeam.Blue or PlayerTeam.Red))
            {
                Plugin.LogDebug($"Player {stick.Player.Username.Value} is not on red or blue team, skipping tape customization.");
                return;
            }

            PlayerRole role = stick.Player.Role.Value;
            StickMesh stickMesh = stick.StickMesh;

            // Get renderers via reflection
            var bladeTapeRenderer = (MeshRenderer)_bladeTapeMeshRendererField.GetValue(stickMesh);
            var shaftTapeRenderer = (MeshRenderer)_shaftTapeMeshRendererField.GetValue(stickMesh);

            // Determine which profile entries to use based on team + role
            if (team == PlayerTeam.Blue)
            {
                if (role == PlayerRole.Goalie)
                {
                    ApplyTapeCustomization(
                        bladeTapeRenderer,
                        stick.Player.OwnerClientId,
                        "blade",
                        ReskinProfileManager.currentProfile.blueGoalieBladeTapeMode ?? "Unchanged",
                        ReskinProfileManager.currentProfile.blueGoalieBladeTape,
                        ReskinProfileManager.currentProfile.blueGoalieBladeTapeColor
                    );

                    ApplyTapeCustomization(
                        shaftTapeRenderer,
                        stick.Player.OwnerClientId,
                        "shaft",
                        ReskinProfileManager.currentProfile.blueGoalieShaftTapeMode ?? "Unchanged",
                        ReskinProfileManager.currentProfile.blueGoalieShaftTape,
                        ReskinProfileManager.currentProfile.blueGoalieShaftTapeColor
                    );
                }
                else // Skater
                {
                    ApplyTapeCustomization(
                        bladeTapeRenderer,
                        stick.Player.OwnerClientId,
                        "blade",
                        ReskinProfileManager.currentProfile.blueSkaterBladeTapeMode ?? "Unchanged",
                        ReskinProfileManager.currentProfile.blueSkaterBladeTape,
                        ReskinProfileManager.currentProfile.blueSkaterBladeTapeColor
                    );

                    ApplyTapeCustomization(
                        shaftTapeRenderer,
                        stick.Player.OwnerClientId,
                        "shaft",
                        ReskinProfileManager.currentProfile.blueSkaterShaftTapeMode ?? "Unchanged",
                        ReskinProfileManager.currentProfile.blueSkaterShaftTape,
                        ReskinProfileManager.currentProfile.blueSkaterShaftTapeColor
                    );
                }
            }
            else // Red team
            {
                if (role == PlayerRole.Goalie)
                {
                    ApplyTapeCustomization(
                        bladeTapeRenderer,
                        stick.Player.OwnerClientId,
                        "blade",
                        ReskinProfileManager.currentProfile.redGoalieBladeTapeMode ?? "Unchanged",
                        ReskinProfileManager.currentProfile.redGoalieBladeTape,
                        ReskinProfileManager.currentProfile.redGoalieBladeTapeColor
                    );

                    ApplyTapeCustomization(
                        shaftTapeRenderer,
                        stick.Player.OwnerClientId,
                        "shaft",
                        ReskinProfileManager.currentProfile.redGoalieShaftTapeMode ?? "Unchanged",
                        ReskinProfileManager.currentProfile.redGoalieShaftTape,
                        ReskinProfileManager.currentProfile.redGoalieShaftTapeColor
                    );
                }
                else // Skater
                {
                    ApplyTapeCustomization(
                        bladeTapeRenderer,
                        stick.Player.OwnerClientId,
                        "blade",
                        ReskinProfileManager.currentProfile.redSkaterBladeTapeMode ?? "Unchanged",
                        ReskinProfileManager.currentProfile.redSkaterBladeTape,
                        ReskinProfileManager.currentProfile.redSkaterBladeTapeColor
                    );

                    ApplyTapeCustomization(
                        shaftTapeRenderer,
                        stick.Player.OwnerClientId,
                        "shaft",
                        ReskinProfileManager.currentProfile.redSkaterShaftTapeMode ?? "Unchanged",
                        ReskinProfileManager.currentProfile.redSkaterShaftTape,
                        ReskinProfileManager.currentProfile.redSkaterShaftTapeColor
                    );
                }
            }

            Plugin.LogDebug($"Applied tape customization for {stick.Player.Username.Value}");
        }
        catch (Exception ex)
        {
            Plugin.LogError($"Error setting stick tape: {ex.Message}");
        }
    }

    // Legacy method for backwards compatibility - redirects to new method
    public static void SetStickTapeColors(Stick stick)
    {
        SetStickTapeForPlayer(stick);
    }

    // Helper to get the local player's stick
    private static Stick GetLocalPlayerStick()
    {
        try
        {
            var players = UnityEngine.Object.FindObjectsByType<Player>(FindObjectsSortMode.None);

            // Try to find the local player using various methods
            foreach (var player in players)
            {
                if (player?.Stick == null) continue;
                if (player.PlayerBody == null || player.PlayerBody.PlayerMesh == null) continue;

                // Check if this player has an IsLocalPlayer or IsOwner property
                var playerType = player.GetType();
                var isLocalPlayerProp = playerType.GetProperty("IsLocalPlayer", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                var isOwnerProp = playerType.GetProperty("IsOwner", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (isLocalPlayerProp != null)
                {
                    var isLocal = (bool)isLocalPlayerProp.GetValue(player);
                    if (isLocal)
                    {
                        Plugin.LogDebug($"Found local player via IsLocalPlayer: {player.Username.Value}");
                        return player.Stick;
                    }
                }

                if (isOwnerProp != null)
                {
                    var isOwner = (bool)isOwnerProp.GetValue(player);
                    if (isOwner)
                    {
                        Plugin.LogDebug($"Found local player via IsOwner: {player.Username.Value}");
                        return player.Stick;
                    }
                }
            }

            // Fallback: if no player was marked as local, just return the first valid stick
            // This handles single-player or test scenarios
            foreach (var player in players)
            {
                if (player?.Stick != null && player.PlayerBody != null && player.PlayerBody.PlayerMesh != null)
                {
                    Plugin.LogDebug($"No IsLocalPlayer/IsOwner found, using first available player: {player.Username.Value}");
                    return player.Stick;
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.LogDebug($"Error getting local player stick: {ex.Message}");
        }

        return null;
    }

    // Update handlers for UI callbacks - apply changes to local player's stick if in game
    public static void OnBlueSkaterTapeChanged()
    {
        var stick = GetLocalPlayerStick();
        if (stick != null && stick.Player.Team.Value == PlayerTeam.Blue && stick.Player.Role.Value == PlayerRole.Attacker)
        {
            SetStickTapeForPlayer(stick);
            Plugin.LogDebug("Applied blue skater tape changes");
        }
    }

    public static void OnRedSkaterTapeChanged()
    {
        var stick = GetLocalPlayerStick();
        if (stick != null && stick.Player.Team.Value == PlayerTeam.Red && stick.Player.Role.Value == PlayerRole.Attacker)
        {
            SetStickTapeForPlayer(stick);
            Plugin.LogDebug("Applied red skater tape changes");
        }
    }

    public static void OnBlueGoalieTapeChanged()
    {
        var stick = GetLocalPlayerStick();
        if (stick != null && stick.Player.Team.Value == PlayerTeam.Blue && stick.Player.Role.Value == PlayerRole.Goalie)
        {
            SetStickTapeForPlayer(stick);
            Plugin.LogDebug("Applied blue goalie tape changes");
        }
    }

    public static void OnRedGoalieTapeChanged()
    {
        var stick = GetLocalPlayerStick();
        if (stick != null && stick.Player.Team.Value == PlayerTeam.Red && stick.Player.Role.Value == PlayerRole.Goalie)
        {
            SetStickTapeForPlayer(stick);
            Plugin.LogDebug("Applied red goalie tape changes");
        }
    }
}
