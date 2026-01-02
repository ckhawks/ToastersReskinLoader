using System;
using System.Collections.Generic;
using UnityEngine;

namespace ToasterReskinLoader.swappers
{
    // Handles leg pad texture swapping for goalies.
    // Caches original textures per player to allow resetting.
    public static class GoalieEquipmentSwapper
    {
        // Cache of original leg pad textures: key is (team, playerId, side)
        private static Dictionary<(PlayerTeam, ulong, string), Texture> originalTextures =
            new Dictionary<(PlayerTeam, ulong, string), Texture>();

        // Gets the MeshRenderer for a leg pad, checking both component and children
        private static MeshRenderer GetLegPadRenderer(PlayerLegPad legPad)
        {
            if (legPad == null) return null;

            MeshRenderer renderer = legPad.GetComponent<MeshRenderer>();
            return renderer ?? legPad.GetComponentInChildren<MeshRenderer>();
        }

        // Applies leg pad texture to a single side (left or right)
        private static void ApplyLegPadTexture(MeshRenderer renderer, ulong playerId, PlayerTeam team,
            string side, ReskinRegistry.ReskinEntry textureEntry, Color defaultColor)
        {
            if (renderer == null) return;

            var cacheKey = (team, playerId, side);

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
                renderer.material.SetTexture("_Albedo", texture);
                renderer.material.color = Color.white;
            }
            else
            {
                // Reset to original with the configured default color
                renderer.material.mainTexture = originalTextures[cacheKey];
                renderer.material.color = defaultColor;
            }
        }

        // Sets leg pads for a player (only if goalie)
        public static void SetLegPadsForPlayer(Player player)
        {
            // Validate player
            if (player?.PlayerBody?.PlayerMesh == null)
            {
                Plugin.LogDebug("Player is missing body parts for leg pads.");
                return;
            }

            // Only apply to goalies
            if (player.Role.Value != PlayerRole.Goalie)
                return;

            // Only blue/red teams
            PlayerTeam team = player.Team.Value;
            if (team is not (PlayerTeam.Blue or PlayerTeam.Red))
                return;

            // Get renderers
            PlayerMesh playerMesh = player.PlayerBody.PlayerMesh;
            MeshRenderer leftRenderer = GetLegPadRenderer(playerMesh.PlayerLegPadLeft);
            MeshRenderer rightRenderer = GetLegPadRenderer(playerMesh.PlayerLegPadRight);

            if (leftRenderer == null || rightRenderer == null)
            {
                Plugin.LogDebug($"Could not find leg pad renderers for {player.Username.Value}");
                return;
            }

            // Apply textures based on team
            if (team == PlayerTeam.Blue)
            {
                ApplyLegPadTexture(leftRenderer, player.OwnerClientId, team, "left",
                    ReskinProfileManager.currentProfile.blueLegPadLeft,
                    ReskinProfileManager.currentProfile.blueLegPadDefaultColor);
                ApplyLegPadTexture(rightRenderer, player.OwnerClientId, team, "right",
                    ReskinProfileManager.currentProfile.blueLegPadRight,
                    ReskinProfileManager.currentProfile.blueLegPadDefaultColor);
            }
            else // Red
            {
                ApplyLegPadTexture(leftRenderer, player.OwnerClientId, team, "left",
                    ReskinProfileManager.currentProfile.redLegPadLeft,
                    ReskinProfileManager.currentProfile.redLegPadDefaultColor);
                ApplyLegPadTexture(rightRenderer, player.OwnerClientId, team, "right",
                    ReskinProfileManager.currentProfile.redLegPadRight,
                    ReskinProfileManager.currentProfile.redLegPadDefaultColor);
            }

            Plugin.LogDebug($"Set leg pads for {player.Username.Value} ({team})");
        }

        // Updates leg pads for all players on a team
        private static void UpdateTeamLegPads(PlayerTeam team)
        {
            var players = PlayerManager.Instance.GetPlayersByTeam(team);
            foreach (Player player in players)
            {
                if (player.Role.Value == PlayerRole.Goalie)
                {
                    SetLegPadsForPlayer(player);
                }
            }
        }

        public static void OnBlueLegPadsChanged() => UpdateTeamLegPads(PlayerTeam.Blue);
        public static void OnRedLegPadsChanged() => UpdateTeamLegPads(PlayerTeam.Red);

        public static void OnBlueLegPadColorChanged() => UpdateTeamLegPads(PlayerTeam.Blue);
        public static void OnRedLegPadColorChanged() => UpdateTeamLegPads(PlayerTeam.Red);

        // Resets both blue and red leg pad colors to default
        public static void ResetLegPadColorsToDefault()
        {
            ReskinProfileManager.currentProfile.blueLegPadDefaultColor = new Color(0.151f, 0.151f, 0.151f, 1f);
            ReskinProfileManager.currentProfile.redLegPadDefaultColor = new Color(0.151f, 0.151f, 0.151f, 1f);
            ReskinProfileManager.SaveProfile();

            UpdateTeamLegPads(PlayerTeam.Blue);
            UpdateTeamLegPads(PlayerTeam.Red);
        }
    }
}