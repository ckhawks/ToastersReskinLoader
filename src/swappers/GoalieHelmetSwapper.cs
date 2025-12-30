using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ToasterReskinLoader.swappers
{
    public static class GoalieHelmetSwapper
    {
        private static Texture originalBlueHelmet;
        private static Texture originalRedHelmet;
        
        private static Dictionary<ulong, Texture> originalBlueHelmetTextures = new Dictionary<ulong, Texture>();
        private static Dictionary<ulong, Texture> originalRedHelmetTextures = new Dictionary<ulong, Texture>();


        private static Renderer GetHelmetRenderer(PlayerHead playerHead)
        {
            if (playerHead == null) return null;
            

            var renderers = playerHead.GetComponentsInChildren<Renderer>();
            
            foreach (var renderer in renderers)
            {
                if (renderer.name.ToLower().Contains("helmet") || 
                    renderer.name.ToLower().Contains("head") ||
                    renderer.name.ToLower().Contains("mask"))
                {
                    return renderer;
                }
            }
            
            return playerHead.GetComponent<Renderer>();
        }

        public static void SetHelmetForPlayer(Player player)
        {
            if (player == null || player.PlayerBody == null || player.PlayerBody.PlayerMesh == null)
            {
                Plugin.LogDebug($"Player is missing body parts for helmet.");
                return;
            }


            if (player.Role.Value != PlayerRole.Goalie)
            {
                return;
            }

            PlayerMesh playerMesh = player.PlayerBody.PlayerMesh;
            PlayerTeam team = player.Team.Value;

            if (team is not (PlayerTeam.Blue or PlayerTeam.Red))
            {
                return;
            }


            Renderer helmetRenderer = GetHelmetRenderer(playerMesh.PlayerHead);

            if (helmetRenderer == null)
            {
                Plugin.LogDebug($"Could not find helmet renderer for player {player.Username.Value}");
                return;
            }

            if (team == PlayerTeam.Blue)
            {
                if (originalBlueHelmet == null)
                {
                    originalBlueHelmet = helmetRenderer.material.mainTexture;
                }

                if (!originalBlueHelmetTextures.ContainsKey(player.OwnerClientId))
                {
                    originalBlueHelmetTextures[player.OwnerClientId] = helmetRenderer.material.mainTexture;
                }


                if (ReskinProfileManager.currentProfile.blueGoalieHelmet != null && 
                    ReskinProfileManager.currentProfile.blueGoalieHelmet.Path != null)
                {
                    helmetRenderer.material.SetTexture("_MainTex", 
                        TextureManager.GetTexture(ReskinProfileManager.currentProfile.blueGoalieHelmet));
                    helmetRenderer.material.SetTexture("_BaseMap", 
                        TextureManager.GetTexture(ReskinProfileManager.currentProfile.blueGoalieHelmet));
                }
                else
                {

                    if (originalBlueHelmetTextures.ContainsKey(player.OwnerClientId))
                    {
                        helmetRenderer.material.mainTexture = originalBlueHelmetTextures[player.OwnerClientId];
                    }
                }
            }
            else if (team == PlayerTeam.Red)
            {
                if (originalRedHelmet == null)
                {
                    originalRedHelmet = helmetRenderer.material.mainTexture;
                }

                if (!originalRedHelmetTextures.ContainsKey(player.OwnerClientId))
                {
                    originalRedHelmetTextures[player.OwnerClientId] = helmetRenderer.material.mainTexture;
                }


                if (ReskinProfileManager.currentProfile.redGoalieHelmet != null && 
                    ReskinProfileManager.currentProfile.redGoalieHelmet.Path != null)
                {
                    helmetRenderer.material.SetTexture("_MainTex", 
                        TextureManager.GetTexture(ReskinProfileManager.currentProfile.redGoalieHelmet));
                    helmetRenderer.material.SetTexture("_BaseMap", 
                        TextureManager.GetTexture(ReskinProfileManager.currentProfile.redGoalieHelmet));
                }
                else
                {

                    if (originalRedHelmetTextures.ContainsKey(player.OwnerClientId))
                    {
                        helmetRenderer.material.mainTexture = originalRedHelmetTextures[player.OwnerClientId];
                    }
                }
            }

            Plugin.LogDebug($"Set helmet for {player.Username.Value} ({team})");
        }

        public static void OnBlueHelmetsChanged()
        {
            List<Player> bluePlayers = PlayerManager.Instance.GetPlayersByTeam(PlayerTeam.Blue);
            foreach (Player player in bluePlayers)
            {
                if (player.Role.Value == PlayerRole.Goalie)
                {
                    SetHelmetForPlayer(player);
                }
            }
        }

        public static void OnRedHelmetsChanged()
        {
            List<Player> redPlayers = PlayerManager.Instance.GetPlayersByTeam(PlayerTeam.Red);
            foreach (Player player in redPlayers)
            {
                if (player.Role.Value == PlayerRole.Goalie)
                {
                    SetHelmetForPlayer(player);
                }
            }
        }
    }
}