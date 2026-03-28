using System;
using System.Collections.Generic;
using UnityEngine;

namespace ToasterReskinLoader.swappers
{
    // Handles goalie helmet, mask, and cage texture/color swapping.
    // Caches original textures per player to allow resetting.
    public static class GoalieHelmetSwapper
    {
        // Cache of original textures: key is (team, playerId, part)
        private static Dictionary<(PlayerTeam, ulong, string), Texture> originalTextures =
            new Dictionary<(PlayerTeam, ulong, string), Texture>();

        public static void ClearHelmetCache() => originalTextures.Clear();
        
        // Gets the Renderer for a specific part of goalie headgear
        private static Renderer GetHeadgearRenderer(PlayerHead playerHead, string part)
        {
            if (playerHead == null) return null;

            var renderers = playerHead.GetComponentsInChildren<Renderer>();

            Plugin.LogDebug($"[GoalieHelmet] Searching for '{part}' among {renderers.Length} renderers on PlayerHead:");
            foreach (var r in renderers)
            {
                Plugin.LogDebug($"  - Renderer: '{r.name}' (type: {r.GetType().Name}, shader: {r.material?.shader?.name ?? "null"})");
            }

            // Actual goalie head renderers (as observed in-game):
            // - "Helmet Cage & Neck Guard (Goalie)" = the main painted goalie mask shell
            // - "Cage" = the metal wire cage in front of the face
            // - "Neck Guard" = throat/neck protector
            // - "Flag", "Head", "Eyes", "Mustache Sheriff" = decorative
            //
            // The helmet shell is a SINGLE renderer with "helmet", "cage", AND "neck" in
            // its name, so filters must match it specifically before the individual parts.

            foreach (var renderer in renderers)
            {
                string rendererNameLower = renderer.name.ToLower();
                switch (part)
                {
                    case "helmet":
                        // The main goalie mask shell: "Helmet Cage & Neck Guard (Goalie)"
                        // This contains "helmet", "cage", AND "neck" — match the composite name
                        if (rendererNameLower.Contains("helmet") && rendererNameLower.Contains("goalie"))
                        {
                            Plugin.LogDebug($"[GoalieHelmet] Matched '{part}' → renderer '{renderer.name}'");
                            return renderer;
                        }
                        break;
                    case "mask":
                        // The standalone neck guard: "Neck Guard" (NOT the composite helmet piece)
                        if (rendererNameLower == "neck guard")
                        {
                            Plugin.LogDebug($"[GoalieHelmet] Matched '{part}' → renderer '{renderer.name}'");
                            return renderer;
                        }
                        break;
                    case "cage":
                        // The standalone cage: "Cage" (NOT the composite helmet piece)
                        if (rendererNameLower == "cage")
                        {
                            Plugin.LogDebug($"[GoalieHelmet] Matched '{part}' → renderer '{renderer.name}'");
                            return renderer;
                        }
                        break;
                }
            }

            Plugin.Log($"[GoalieHelmet] No renderer matched for part '{part}'");
            return null;
        }

        // Helper to apply texture/color to a headgear part
        private static void ApplyHeadgearTexture(Renderer renderer, ulong playerId, PlayerTeam team,
            string part, ReskinRegistry.ReskinEntry textureEntry, Color defaultColor)
        {
            if (renderer == null) return;

            var cacheKey = (team, playerId, part);

            // Store original if not cached yet
            if (!originalTextures.ContainsKey(cacheKey))
            {
                originalTextures[cacheKey] = renderer.material.mainTexture;
            }

            // Apply custom texture or revert to original
            if (textureEntry?.Path != null)
            {
                var texture = TextureManager.GetTexture(textureEntry);
                if (texture == null)
                {
                    Plugin.LogError($"[GoalieHelmet] Failed to load texture for {textureEntry.Name}");
                    return;
                }

                SwapperUtils.ApplyTextureToMaterial(renderer.material, texture);
                Plugin.LogDebug($"[GoalieHelmet] Applied texture '{textureEntry.Name}' to {part} (shader: {renderer.material.shader.name})");
            }
            else
            {
                SwapperUtils.RestoreOriginalTexture(renderer.material, originalTextures[cacheKey], defaultColor);
                Plugin.LogDebug($"[GoalieHelmet] Restored original for {part} with color {defaultColor}");
            }
        }

        // Sets headgear for a player (only if goalie)
        public static void SetHeadgearForPlayer(Player player)
        {
            // Validate player
            if (player?.PlayerBody?.PlayerMesh == null)
            {
                Plugin.LogDebug("Player is missing body parts for headgear.");
                return;
            }

            // Only apply to goalies
            if (player.Role != PlayerRole.Goalie)
                return;

            // Only blue/red teams
            PlayerTeam team = player.Team;
            if (team is not (PlayerTeam.Blue or PlayerTeam.Red))
                return;

            SetHelmetForPlayer(player);
            SetMaskForPlayer(player);
            SetCageForPlayer(player);
        }

        // Sets helmet for a goalie player
        private static void SetHelmetForPlayer(Player player)
        {
            PlayerTeam team = player.Team;
            Renderer helmetRenderer = GetHeadgearRenderer(player.PlayerBody.PlayerMesh.PlayerHead, "helmet");

            if (helmetRenderer == null)
            {
                Plugin.LogDebug($"Could not find helmet renderer for {player.Username.Value}");
                return;
            }

            if (team == PlayerTeam.Blue)
            {
                ApplyHeadgearTexture(helmetRenderer, player.OwnerClientId, team, "helmet",
                    ReskinProfileManager.currentProfile.blueGoalieHelmet,
                    ReskinProfileManager.currentProfile.blueGoalieHelmetColor);
            }
            else // Red
            {
                ApplyHeadgearTexture(helmetRenderer, player.OwnerClientId, team, "helmet",
                    ReskinProfileManager.currentProfile.redGoalieHelmet,
                    ReskinProfileManager.currentProfile.redGoalieHelmetColor);
            }
        }

        // Sets mask (neck shield) for a goalie player
        private static void SetMaskForPlayer(Player player)
        {
            PlayerTeam team = player.Team;
            Renderer maskRenderer = GetHeadgearRenderer(player.PlayerBody.PlayerMesh.PlayerHead, "mask");

            if (maskRenderer == null)
            {
                Plugin.LogDebug($"Could not find mask renderer for {player.Username.Value}");
                return;
            }

            if (team == PlayerTeam.Blue)
            {
                ApplyHeadgearTexture(maskRenderer, player.OwnerClientId, team, "mask",
                    ReskinProfileManager.currentProfile.blueGoalieMask,
                    ReskinProfileManager.currentProfile.blueGoalieMaskColor);
            }
            else // Red
            {
                ApplyHeadgearTexture(maskRenderer, player.OwnerClientId, team, "mask",
                    ReskinProfileManager.currentProfile.redGoalieMask,
                    ReskinProfileManager.currentProfile.redGoalieMaskColor);
            }
        }

        // Sets cage for a goalie player (color only)
        private static void SetCageForPlayer(Player player)
        {
            PlayerTeam team = player.Team;
            Renderer cageRenderer = GetHeadgearRenderer(player.PlayerBody.PlayerMesh.PlayerHead, "cage");

            if (cageRenderer == null)
            {
                Plugin.LogDebug($"Could not find cage renderer for {player.Username.Value}");
                return;
            }

            // Cage only has color, no texture
            if (team == PlayerTeam.Blue)
            {
                Color currentColor = cageRenderer.material.color;
                Color newColor = ReskinProfileManager.currentProfile.blueGoalieCageColor;
                Plugin.Log($"Changing blue cage color from {currentColor} to {newColor}");
                cageRenderer.material.color = newColor;
            }
            else // Red
            {
                Color currentColor = cageRenderer.material.color;
                Color newColor = ReskinProfileManager.currentProfile.redGoalieCageColor;
                Plugin.Log($"Changing red cage color from {currentColor} to {newColor}");
                cageRenderer.material.color = newColor;
            }
        }

        // Updates headgear for all players on a team
        private static void UpdateTeamHeadgear(PlayerTeam team)
        {
            var players = PlayerManager.Instance.GetPlayersByTeam(team);
            foreach (Player player in players)
            {
                if (player.Role == PlayerRole.Goalie)
                {
                    SetHeadgearForPlayer(player);
                }
            }
        }

        public static void OnBlueHelmetsChanged() => UpdateTeamHeadgear(PlayerTeam.Blue);
        public static void OnRedHelmetsChanged() => UpdateTeamHeadgear(PlayerTeam.Red);
        public static void OnBlueMasksChanged() => UpdateTeamHeadgear(PlayerTeam.Blue);
        public static void OnRedMasksChanged() => UpdateTeamHeadgear(PlayerTeam.Red);
        public static void OnBlueHelmetColorChanged() => UpdateTeamHeadgear(PlayerTeam.Blue);
        public static void OnRedHelmetColorChanged() => UpdateTeamHeadgear(PlayerTeam.Red);
        public static void OnBlueMaskColorChanged() => UpdateTeamHeadgear(PlayerTeam.Blue);
        public static void OnRedMaskColorChanged() => UpdateTeamHeadgear(PlayerTeam.Red);
        public static void OnBlueCageColorChanged() => UpdateTeamHeadgear(PlayerTeam.Blue);
        public static void OnRedCageColorChanged() => UpdateTeamHeadgear(PlayerTeam.Red);

        // Resets all headgear colors to default
        public static void ResetHeadgearColorsToDefault()
        {
            ReskinProfileManager.currentProfile.blueGoalieHelmetColor = Color.black;
            ReskinProfileManager.currentProfile.redGoalieHelmetColor = Color.black;
            ReskinProfileManager.currentProfile.blueGoalieMaskColor = Color.black;
            ReskinProfileManager.currentProfile.redGoalieMaskColor = Color.black;
            ReskinProfileManager.currentProfile.blueGoalieCageColor = new Color(0.708f, 0.708f, 0.708f, 1f);
            ReskinProfileManager.currentProfile.redGoalieCageColor = new Color(0.708f, 0.708f, 0.708f, 1f);
            ReskinProfileManager.SaveProfile();

            UpdateTeamHeadgear(PlayerTeam.Blue);
            UpdateTeamHeadgear(PlayerTeam.Red);
        }
    }
}