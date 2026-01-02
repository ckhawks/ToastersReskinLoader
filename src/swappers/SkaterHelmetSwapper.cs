using System;
using System.Collections.Generic;
using UnityEngine;

namespace ToasterReskinLoader.swappers
{
    // Handles skater helmet texture/color swapping.
    // Caches original textures per player to allow resetting.
    public static class SkaterHelmetSwapper
    {
        // Cache of original helmet textures: key is (team, playerId)
        private static Dictionary<(PlayerTeam, ulong), Texture> originalTextures =
            new Dictionary<(PlayerTeam, ulong), Texture>();

        // Gets the Renderer for a skater helmet
        private static Renderer GetHelmetRenderer(PlayerHead playerHead)
        {
            if (playerHead == null) return null;

            var renderers = playerHead.GetComponentsInChildren<Renderer>();

            // Search for helmet renderer (skaters have a helmet similar to the goalie helmet top)
            foreach (var renderer in renderers)
            {
                string rendererNameLower = renderer.name.ToLower();
                if (rendererNameLower.Contains("helmet") && !rendererNameLower.Contains("cage") && !rendererNameLower.Contains("neck"))
                {
                    return renderer;
                }
            }

            return null;
        }

        // Helper to apply texture/color to helmet
        private static void ApplyHelmetTexture(Renderer renderer, ulong playerId, PlayerTeam team,
            ReskinRegistry.ReskinEntry textureEntry, Color defaultColor)
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
                // Reset to original with the configured default color
                renderer.material.mainTexture = originalTextures[cacheKey];
                renderer.material.color = defaultColor;
            }
        }

        // Sets helmet for a skater player
        public static void SetHelmetForPlayer(Player player)
        {
            // Validate player
            if (player?.PlayerBody?.PlayerMesh == null)
            {
                Plugin.LogDebug("Player is missing body parts for helmet.");
                return;
            }

            // Only apply to skaters (non-goalies)
            if (player.Role.Value == PlayerRole.Goalie)
                return;

            // Only blue/red teams
            PlayerTeam team = player.Team.Value;
            if (team is not (PlayerTeam.Blue or PlayerTeam.Red))
                return;

            Renderer helmetRenderer = GetHelmetRenderer(player.PlayerBody.PlayerMesh.PlayerHead);
            if (helmetRenderer == null)
            {
                Plugin.LogDebug($"Could not find helmet renderer for {player.Username.Value}");
                return;
            }

            if (team == PlayerTeam.Blue)
            {
                ApplyHelmetTexture(helmetRenderer, player.OwnerClientId, team,
                    ReskinProfileManager.currentProfile.blueSkaterHelmet,
                    ReskinProfileManager.currentProfile.blueSkaterHelmetColor);
            }
            else // Red
            {
                ApplyHelmetTexture(helmetRenderer, player.OwnerClientId, team,
                    ReskinProfileManager.currentProfile.redSkaterHelmet,
                    ReskinProfileManager.currentProfile.redSkaterHelmetColor);
            }

            Plugin.LogDebug($"Set helmet for {player.Username.Value} ({team})");
        }

        // Updates helmets for all skaters on a team
        private static void UpdateTeamSkaterHelmets(PlayerTeam team)
        {
            var players = PlayerManager.Instance.GetPlayersByTeam(team);
            foreach (Player player in players)
            {
                if (player.Role.Value != PlayerRole.Goalie)
                {
                    SetHelmetForPlayer(player);
                }
            }
        }

        public static void OnBlueHelmetsChanged() => UpdateTeamSkaterHelmets(PlayerTeam.Blue);
        public static void OnRedHelmetsChanged() => UpdateTeamSkaterHelmets(PlayerTeam.Red);
        public static void OnBlueHelmetColorChanged() => UpdateTeamSkaterHelmets(PlayerTeam.Blue);
        public static void OnRedHelmetColorChanged() => UpdateTeamSkaterHelmets(PlayerTeam.Red);

        // Resets skater helmet colors to default
        public static void ResetHelmetColorsToDefault()
        {
            ReskinProfileManager.currentProfile.blueSkaterHelmetColor = Color.black;
            ReskinProfileManager.currentProfile.redSkaterHelmetColor = Color.black;
            ReskinProfileManager.SaveProfile();

            UpdateTeamSkaterHelmets(PlayerTeam.Blue);
            UpdateTeamSkaterHelmets(PlayerTeam.Red);
        }
    }
}
