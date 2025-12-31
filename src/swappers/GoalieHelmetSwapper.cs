using System;
using System.Collections.Generic;
using UnityEngine;

namespace ToasterReskinLoader.swappers
{
    // Handles goalie helmet texture swapping.
    // Caches original textures per player to allow resetting.
    public static class GoalieHelmetSwapper
    {
        // Cache of original helmet textures: key is (team, playerId)
        private static Dictionary<(PlayerTeam, ulong), Texture> originalTextures =
            new Dictionary<(PlayerTeam, ulong), Texture>();

        // Gets the Renderer for a goalie helmet (searches children for "helmet" or "mask")
        private static Renderer GetHelmetRenderer(PlayerHead playerHead)
        {
            if (playerHead == null) return null;

            var renderers = playerHead.GetComponentsInChildren<Renderer>();

            // 2025-12-30 18:56:06 [ToasterReskinLoader] PlayerHead renderer: Cage
            // 2025-12-30 18:56:06 [ToasterReskinLoader] PlayerHead renderer: Eyes
            // 2025-12-30 18:56:06 [ToasterReskinLoader] PlayerHead renderer: Head
            // 2025-12-30 18:56:06 [ToasterReskinLoader] PlayerHead renderer: Helmet
            // 2025-12-30 18:56:06 [ToasterReskinLoader] PlayerHead renderer: Flag
            // 2025-12-30 18:56:06 [ToasterReskinLoader] PlayerHead renderer: Mustache Chevron
            // 2025-12-30 18:56:06 [ToasterReskinLoader] PlayerHead renderer: Neck Shield

            // helmet = top stuff, same as skater
            // cage = metal grate thing in front
            // neck shield = lower part of goalie helmet
            
            // Search for helmet/mask renderer
            foreach (var renderer in renderers)
            {
                if (renderer.name.ToLower().Contains("helmet") || renderer.name.ToLower().Contains("mask"))
                {
                    return renderer;
                }
            }

            return playerHead.GetComponent<Renderer>();
        }

        // Applies helmet texture to a player
        private static void ApplyHelmetTexture(Renderer renderer, ulong playerId, PlayerTeam team,
            ReskinRegistry.ReskinEntry textureEntry)
        {
            if (renderer == null) return;

            var cacheKey = (team, playerId);

            // Store original if not cached yet
            if (!originalTextures.ContainsKey(cacheKey))
            {
                originalTextures[cacheKey] = renderer.material.mainTexture;
            }

            // Apply custom texture or revert to original
            if (textureEntry?.Path != null)
            {
                var texture = TextureManager.GetTexture(textureEntry);
                renderer.material.SetTexture("_MainTex", texture);
                renderer.material.SetTexture("_BaseMap", texture);
                renderer.material.color = Color.white;
            }
            else
            {
                // Reset to original
                renderer.material.mainTexture = originalTextures[cacheKey];
                renderer.material.color = Color.black;
            }
        }

        // Sets helmet for a player (only if goalie)
        public static void SetHelmetForPlayer(Player player)
        {
            // Validate player
            if (player?.PlayerBody?.PlayerMesh == null)
            {
                Plugin.LogDebug("Player is missing body parts for helmet.");
                return;
            }

            // Only apply to goalies
            if (player.Role.Value != PlayerRole.Goalie)
                return;

            // Only blue/red teams
            PlayerTeam team = player.Team.Value;
            if (team is not (PlayerTeam.Blue or PlayerTeam.Red))
                return;

            // Get helmet renderer
            Renderer helmetRenderer = GetHelmetRenderer(player.PlayerBody.PlayerMesh.PlayerHead);
            if (helmetRenderer == null)
            {
                Plugin.LogDebug($"Could not find helmet renderer for {player.Username.Value}");
                return;
            }

            // Apply texture based on team
            if (team == PlayerTeam.Blue)
            {
                ApplyHelmetTexture(helmetRenderer, player.OwnerClientId, team,
                    ReskinProfileManager.currentProfile.blueGoalieHelmet);
            }
            else // Red
            {
                ApplyHelmetTexture(helmetRenderer, player.OwnerClientId, team,
                    ReskinProfileManager.currentProfile.redGoalieHelmet);
            }

            Plugin.LogDebug($"Set helmet for {player.Username.Value} ({team})");
        }

        // Updates helmets for all players on a team
        private static void UpdateTeamHelmets(PlayerTeam team)
        {
            var players = PlayerManager.Instance.GetPlayersByTeam(team);
            foreach (Player player in players)
            {
                if (player.Role.Value == PlayerRole.Goalie)
                {
                    SetHelmetForPlayer(player);
                }
            }
        }

        public static void OnBlueHelmetsChanged() => UpdateTeamHelmets(PlayerTeam.Blue);
        public static void OnRedHelmetsChanged() => UpdateTeamHelmets(PlayerTeam.Red);
    }
}