using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ToasterReskinLoader.swappers
{
    public static class GoalieEquipmentSwapper
    {
        private static Texture originalBlueLegPadLeft;
        private static Texture originalBlueLegPadRight;
        private static Texture originalRedLegPadLeft;
        private static Texture originalRedLegPadRight;
        
        private static Dictionary<ulong, Texture> originalBlueLegPadLeftTextures = new Dictionary<ulong, Texture>();
        private static Dictionary<ulong, Texture> originalBlueLegPadRightTextures = new Dictionary<ulong, Texture>();
        private static Dictionary<ulong, Texture> originalRedLegPadLeftTextures = new Dictionary<ulong, Texture>();
        private static Dictionary<ulong, Texture> originalRedLegPadRightTextures = new Dictionary<ulong, Texture>();


        private static MeshRenderer GetLegPadRenderer(PlayerLegPad legPad)
        {
            if (legPad == null) return null;
            
            MeshRenderer renderer = legPad.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                renderer = legPad.GetComponentInChildren<MeshRenderer>();
            }
            
            return renderer;
        }

        public static void SetLegPadsForPlayer(Player player)
        {
            if (player == null || player.PlayerBody == null || player.PlayerBody.PlayerMesh == null)
            {
                Plugin.LogDebug($"Player is missing body parts for leg pads.");
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


            MeshRenderer leftLegPadRenderer = GetLegPadRenderer(playerMesh.PlayerLegPadLeft);
            MeshRenderer rightLegPadRenderer = GetLegPadRenderer(playerMesh.PlayerLegPadRight);

            if (leftLegPadRenderer == null || rightLegPadRenderer == null)
            {
                Plugin.LogDebug($"Could not find leg pad renderers for player {player.Username.Value}");
                return;
            }


            if (team == PlayerTeam.Blue)
            {
                if (originalBlueLegPadLeft == null)
                {
                    originalBlueLegPadLeft = leftLegPadRenderer.material.mainTexture;
                }
                if (originalBlueLegPadRight == null)
                {
                    originalBlueLegPadRight = rightLegPadRenderer.material.mainTexture;
                }

                if (!originalBlueLegPadLeftTextures.ContainsKey(player.OwnerClientId))
                {
                    originalBlueLegPadLeftTextures[player.OwnerClientId] = leftLegPadRenderer.material.mainTexture;
                }
                if (!originalBlueLegPadRightTextures.ContainsKey(player.OwnerClientId))
                {
                    originalBlueLegPadRightTextures[player.OwnerClientId] = rightLegPadRenderer.material.mainTexture;
                }


                if (ReskinProfileManager.currentProfile.blueLegPadLeft != null && 
                    ReskinProfileManager.currentProfile.blueLegPadLeft.Path != null)
                {
                    leftLegPadRenderer.material.SetTexture("_MainTex", 
                        TextureManager.GetTexture(ReskinProfileManager.currentProfile.blueLegPadLeft));
                    leftLegPadRenderer.material.SetTexture("_BaseMap", 
                        TextureManager.GetTexture(ReskinProfileManager.currentProfile.blueLegPadLeft));
                }
                else
                {

                    if (originalBlueLegPadLeftTextures.ContainsKey(player.OwnerClientId))
                    {
                        leftLegPadRenderer.material.mainTexture = originalBlueLegPadLeftTextures[player.OwnerClientId];
                    }
                }

                if (ReskinProfileManager.currentProfile.blueLegPadRight != null && 
                    ReskinProfileManager.currentProfile.blueLegPadRight.Path != null)
                {
                    rightLegPadRenderer.material.SetTexture("_MainTex", 
                        TextureManager.GetTexture(ReskinProfileManager.currentProfile.blueLegPadRight));
                    rightLegPadRenderer.material.SetTexture("_BaseMap", 
                        TextureManager.GetTexture(ReskinProfileManager.currentProfile.blueLegPadRight));
                }
                else
                {

                    if (originalBlueLegPadRightTextures.ContainsKey(player.OwnerClientId))
                    {
                        rightLegPadRenderer.material.mainTexture = originalBlueLegPadRightTextures[player.OwnerClientId];
                    }
                }
            }
            else if (team == PlayerTeam.Red)
            {
                if (originalRedLegPadLeft == null)
                {
                    originalRedLegPadLeft = leftLegPadRenderer.material.mainTexture;
                }
                if (originalRedLegPadRight == null)
                {
                    originalRedLegPadRight = rightLegPadRenderer.material.mainTexture;
                }

                if (!originalRedLegPadLeftTextures.ContainsKey(player.OwnerClientId))
                {
                    originalRedLegPadLeftTextures[player.OwnerClientId] = leftLegPadRenderer.material.mainTexture;
                }
                if (!originalRedLegPadRightTextures.ContainsKey(player.OwnerClientId))
                {
                    originalRedLegPadRightTextures[player.OwnerClientId] = rightLegPadRenderer.material.mainTexture;
                }


                if (ReskinProfileManager.currentProfile.redLegPadLeft != null && 
                    ReskinProfileManager.currentProfile.redLegPadLeft.Path != null)
                {
                    leftLegPadRenderer.material.SetTexture("_MainTex", 
                        TextureManager.GetTexture(ReskinProfileManager.currentProfile.redLegPadLeft));
                    leftLegPadRenderer.material.SetTexture("_BaseMap", 
                        TextureManager.GetTexture(ReskinProfileManager.currentProfile.redLegPadLeft));
                }
                else
                {

                    if (originalRedLegPadLeftTextures.ContainsKey(player.OwnerClientId))
                    {
                        leftLegPadRenderer.material.mainTexture = originalRedLegPadLeftTextures[player.OwnerClientId];
                    }
                }

                if (ReskinProfileManager.currentProfile.redLegPadRight != null && 
                    ReskinProfileManager.currentProfile.redLegPadRight.Path != null)
                {
                    rightLegPadRenderer.material.SetTexture("_MainTex", 
                        TextureManager.GetTexture(ReskinProfileManager.currentProfile.redLegPadRight));
                    rightLegPadRenderer.material.SetTexture("_BaseMap", 
                        TextureManager.GetTexture(ReskinProfileManager.currentProfile.redLegPadRight));
                }
                else
                {

                    if (originalRedLegPadRightTextures.ContainsKey(player.OwnerClientId))
                    {
                        rightLegPadRenderer.material.mainTexture = originalRedLegPadRightTextures[player.OwnerClientId];
                    }
                }
            }

            Plugin.LogDebug($"Set leg pads for {player.Username.Value} ({team})");
        }

        public static void OnBlueLegPadsChanged()
        {
            List<Player> bluePlayers = PlayerManager.Instance.GetPlayersByTeam(PlayerTeam.Blue);
            foreach (Player player in bluePlayers)
            {
                if (player.Role.Value == PlayerRole.Goalie)
                {
                    SetLegPadsForPlayer(player);
                }
            }
        }

        public static void OnRedLegPadsChanged()
        {
            List<Player> redPlayers = PlayerManager.Instance.GetPlayersByTeam(PlayerTeam.Red);
            foreach (Player player in redPlayers)
            {
                if (player.Role.Value == PlayerRole.Goalie)
                {
                    SetLegPadsForPlayer(player);
                }
            }
        }
    }
}