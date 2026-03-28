using System;
using System.Collections.Generic;
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
    // Colors are cached by (playerId, team, role, tapePart) since vanilla colors differ by team/role
    private static Dictionary<ulong, Shader> originalShaders =
        new Dictionary<ulong, Shader>();
    private static Dictionary<(ulong, PlayerTeam, PlayerRole, string), Color> originalColors =
        new Dictionary<(ulong, PlayerTeam, PlayerRole, string), Color>();

    // Called when scene changes out of level1(rink)
    public static void ClearTapeCache()
    {
        originalShaders.Clear();
        originalColors.Clear();
    }
    
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
            if (mat.HasProperty("baseColorTexture"))
                mat.SetTexture("baseColorTexture", texture);
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

    // Helper method to apply color to tape renderer
    private static void ApplyTapeColor(MeshRenderer renderer, Color color)
    {
        if (renderer == null) return;

        renderer.material.SetColor("_Color", color);
        renderer.material.color = color;
        Plugin.LogDebug($"Applied color {color} to tape material");
    }

    // Apply tape customization based on mode: "Unchanged" (restore original), "RGB" (solid color), or "Textured" (swap shader and apply texture)
    private static void ApplyTapeCustomization(
        MeshRenderer renderer,
        ulong playerId,
        PlayerTeam team,
        PlayerRole role,
        string tapePart,
        string mode,
        ReskinRegistry.ReskinEntry textureEntry,
        Color rgbColor)
    {
        if (renderer == null) return;

        var colorCacheKey = (playerId, team, role, tapePart);
        Material mat = renderer.material;

        // Cache original shader once per player (same for all tape parts)
        if (!originalShaders.ContainsKey(playerId))
        {
            originalShaders[playerId] = mat.shader;
            Plugin.LogDebug($"Cached original shader for player {playerId}: {mat.shader.name}");
        }

        // Cache original color per (player, team, role, tapePart) since vanilla colors differ
        if (!originalColors.ContainsKey(colorCacheKey))
        {
            originalColors[colorCacheKey] = mat.color;
            Plugin.LogDebug($"Cached original color for player {playerId} {team} {role} {tapePart}: {mat.color}");
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

            PlayerTeam team = stick.Player.Team;
            if (team is not (PlayerTeam.Blue or PlayerTeam.Red))
            {
                Plugin.LogDebug($"Player {stick.Player.Username.Value} is not on red or blue team, skipping tape customization.");
                return;
            }

            PlayerRole role = stick.Player.Role;
            StickMesh stickMesh = stick.StickMesh;

            // Get renderers via reflection
            MeshRenderer bladeTapeRenderer = (MeshRenderer) _bladeTapeMeshRendererField.GetValue(stickMesh);
            MeshRenderer shaftTapeRenderer = (MeshRenderer) _shaftTapeMeshRendererField.GetValue(stickMesh);

            // Determine which profile entries to use based on team + role
            if (team == PlayerTeam.Blue)
            {
                if (role == PlayerRole.Goalie)
                {
                    ApplyTapeCustomization(
                        bladeTapeRenderer,
                        stick.Player.OwnerClientId,
                        team,
                        role,
                        "blade",
                        ReskinProfileManager.currentProfile.blueGoalieBladeTapeMode ?? "Unchanged",
                        ReskinProfileManager.currentProfile.blueGoalieBladeTape,
                        ReskinProfileManager.currentProfile.blueGoalieBladeTapeColor
                    );

                    ApplyTapeCustomization(
                        shaftTapeRenderer,
                        stick.Player.OwnerClientId,
                        team,
                        role,
                        "shaft",
                        ReskinProfileManager.currentProfile.blueGoalieShaftTapeMode ?? "Unchanged",
                        ReskinProfileManager.currentProfile.blueGoalieShaftTape,
                        ReskinProfileManager.currentProfile.blueGoalieShaftTapeColor
                    );
                } else if (role == PlayerRole.Attacker) // Skater
                {
                    ApplyTapeCustomization(
                        bladeTapeRenderer,
                        stick.Player.OwnerClientId,
                        team,
                        role,
                        "blade",
                        ReskinProfileManager.currentProfile.blueSkaterBladeTapeMode ?? "Unchanged",
                        ReskinProfileManager.currentProfile.blueSkaterBladeTape,
                        ReskinProfileManager.currentProfile.blueSkaterBladeTapeColor
                    );

                    ApplyTapeCustomization(
                        shaftTapeRenderer,
                        stick.Player.OwnerClientId,
                        team,
                        role,
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
                        team,
                        role,
                        "blade",
                        ReskinProfileManager.currentProfile.redGoalieBladeTapeMode ?? "Unchanged",
                        ReskinProfileManager.currentProfile.redGoalieBladeTape,
                        ReskinProfileManager.currentProfile.redGoalieBladeTapeColor
                    );

                    ApplyTapeCustomization(
                        shaftTapeRenderer,
                        stick.Player.OwnerClientId,
                        team,
                        role,
                        "shaft",
                        ReskinProfileManager.currentProfile.redGoalieShaftTapeMode ?? "Unchanged",
                        ReskinProfileManager.currentProfile.redGoalieShaftTape,
                        ReskinProfileManager.currentProfile.redGoalieShaftTapeColor
                    );
                } else if (role == PlayerRole.Attacker) // Skater
                {
                    ApplyTapeCustomization(
                        bladeTapeRenderer,
                        stick.Player.OwnerClientId,
                        team,
                        role,
                        "blade",
                        ReskinProfileManager.currentProfile.redSkaterBladeTapeMode ?? "Unchanged",
                        ReskinProfileManager.currentProfile.redSkaterBladeTape,
                        ReskinProfileManager.currentProfile.redSkaterBladeTapeColor
                    );

                    ApplyTapeCustomization(
                        shaftTapeRenderer,
                        stick.Player.OwnerClientId,
                        team,
                        role,
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

    /// <summary>
    /// Applies tape customization to a standalone StickMesh (e.g. locker room preview).
    /// </summary>
    public static void ApplyTapeToStickMesh(StickMesh stickMesh, PlayerTeam team, PlayerRole role)
    {
        try
        {
            if (stickMesh == null) return;

            MeshRenderer bladeTapeRenderer = (MeshRenderer) _bladeTapeMeshRendererField.GetValue(stickMesh);
            MeshRenderer shaftTapeRenderer = (MeshRenderer) _shaftTapeMeshRendererField.GetValue(stickMesh);

            const ulong lockerRoomId = 99999UL;

            if (team == PlayerTeam.Blue)
            {
                if (role == PlayerRole.Goalie)
                {
                    ApplyTapeCustomization(bladeTapeRenderer, lockerRoomId, team, role, "blade",
                        ReskinProfileManager.currentProfile.blueGoalieBladeTapeMode ?? "Unchanged",
                        ReskinProfileManager.currentProfile.blueGoalieBladeTape,
                        ReskinProfileManager.currentProfile.blueGoalieBladeTapeColor);
                    ApplyTapeCustomization(shaftTapeRenderer, lockerRoomId, team, role, "shaft",
                        ReskinProfileManager.currentProfile.blueGoalieShaftTapeMode ?? "Unchanged",
                        ReskinProfileManager.currentProfile.blueGoalieShaftTape,
                        ReskinProfileManager.currentProfile.blueGoalieShaftTapeColor);
                }
                else
                {
                    ApplyTapeCustomization(bladeTapeRenderer, lockerRoomId, team, role, "blade",
                        ReskinProfileManager.currentProfile.blueSkaterBladeTapeMode ?? "Unchanged",
                        ReskinProfileManager.currentProfile.blueSkaterBladeTape,
                        ReskinProfileManager.currentProfile.blueSkaterBladeTapeColor);
                    ApplyTapeCustomization(shaftTapeRenderer, lockerRoomId, team, role, "shaft",
                        ReskinProfileManager.currentProfile.blueSkaterShaftTapeMode ?? "Unchanged",
                        ReskinProfileManager.currentProfile.blueSkaterShaftTape,
                        ReskinProfileManager.currentProfile.blueSkaterShaftTapeColor);
                }
            }
            else if (team == PlayerTeam.Red)
            {
                if (role == PlayerRole.Goalie)
                {
                    ApplyTapeCustomization(bladeTapeRenderer, lockerRoomId, team, role, "blade",
                        ReskinProfileManager.currentProfile.redGoalieBladeTapeMode ?? "Unchanged",
                        ReskinProfileManager.currentProfile.redGoalieBladeTape,
                        ReskinProfileManager.currentProfile.redGoalieBladeTapeColor);
                    ApplyTapeCustomization(shaftTapeRenderer, lockerRoomId, team, role, "shaft",
                        ReskinProfileManager.currentProfile.redGoalieShaftTapeMode ?? "Unchanged",
                        ReskinProfileManager.currentProfile.redGoalieShaftTape,
                        ReskinProfileManager.currentProfile.redGoalieShaftTapeColor);
                }
                else
                {
                    ApplyTapeCustomization(bladeTapeRenderer, lockerRoomId, team, role, "blade",
                        ReskinProfileManager.currentProfile.redSkaterBladeTapeMode ?? "Unchanged",
                        ReskinProfileManager.currentProfile.redSkaterBladeTape,
                        ReskinProfileManager.currentProfile.redSkaterBladeTapeColor);
                    ApplyTapeCustomization(shaftTapeRenderer, lockerRoomId, team, role, "shaft",
                        ReskinProfileManager.currentProfile.redSkaterShaftTapeMode ?? "Unchanged",
                        ReskinProfileManager.currentProfile.redSkaterShaftTape,
                        ReskinProfileManager.currentProfile.redSkaterShaftTapeColor);
                }
            }

            Plugin.LogDebug($"Applied locker room tape customization for {team} {role}");
        }
        catch (Exception ex)
        {
            Plugin.LogError($"Error applying tape to locker room stick: {ex.Message}");
        }
    }

    // Helper to get the local player's stick
    private static Stick GetLocalPlayerStick()
    {
        try
        {
            PlayerManager pm = PlayerManager.Instance;
            Player localPlayer = pm.GetLocalPlayer();

            if (localPlayer != null && localPlayer.Stick != null && localPlayer.PlayerBody != null && localPlayer.PlayerBody.PlayerMesh != null)
            {
                Plugin.LogDebug($"Found local player via GetLocalPlayer: {localPlayer.Username.Value}");
                return localPlayer.Stick;
            }
            
            // search using IsLocalPlayer and IsOwner
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
        if (stick != null && stick.Player.Team == PlayerTeam.Blue && stick.Player.Role == PlayerRole.Attacker)
        {
            SetStickTapeForPlayer(stick);
            Plugin.LogDebug("Applied blue skater tape changes");
        }
    }

    public static void OnRedSkaterTapeChanged()
    {
        var stick = GetLocalPlayerStick();
        if (stick != null && stick.Player.Team == PlayerTeam.Red && stick.Player.Role == PlayerRole.Attacker)
        {
            SetStickTapeForPlayer(stick);
            Plugin.LogDebug("Applied red skater tape changes");
        }
    }

    public static void OnBlueGoalieTapeChanged()
    {
        var stick = GetLocalPlayerStick();
        if (stick != null && stick.Player.Team == PlayerTeam.Blue && stick.Player.Role == PlayerRole.Goalie)
        {
            SetStickTapeForPlayer(stick);
            Plugin.LogDebug("Applied blue goalie tape changes");
        }
    }

    public static void OnRedGoalieTapeChanged()
    {
        var stick = GetLocalPlayerStick();
        if (stick != null && stick.Player.Team == PlayerTeam.Red && stick.Player.Role == PlayerRole.Goalie)
        {
            SetStickTapeForPlayer(stick);
            Plugin.LogDebug("Applied red goalie tape changes");
        }
    }
}
