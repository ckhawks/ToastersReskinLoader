using System;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace ToasterReskinLoader.swappers
{
    public static class ChangingRoomPatcher
    {

        [HarmonyPatch(typeof(ChangingRoomPlayer), nameof(ChangingRoomPlayer.UpdatePlayerMesh))]
        public static class ChangingRoomPlayerUpdatePlayerMeshPatch
        {
            [HarmonyPostfix]
            public static void Postfix(ChangingRoomPlayer __instance)
            {
                try
                {
                    if (__instance == null || __instance.PlayerMesh == null) return;
                    
                    Plugin.LogDebug($"ChangingRoomPlayer.UpdatePlayerMesh called for role: {__instance.Role}, team: {__instance.Team}");
                    

                    ApplyCustomSkins(__instance);
                }
                catch (Exception e)
                {
                    Plugin.LogError($"Error in ChangingRoomPlayerUpdatePlayerMeshPatch: {e}");
                }
            }
        }
        
        private static void ApplyCustomSkins(ChangingRoomPlayer changingRoomPlayer)
        {
            var playerMesh = changingRoomPlayer.PlayerMesh;
            var role = changingRoomPlayer.Role;
            var team = changingRoomPlayer.Team;
            

            ApplyJerseyToPlayerMesh(playerMesh, team, role);
            

            if (role == PlayerRole.Goalie)
            {
                ApplyLegPadsToPlayerMesh(playerMesh, team);
                ApplyHelmetToPlayerMesh(playerMesh, team);
            }
        }
        
        private static void ApplyLegPadsToPlayerMesh(PlayerMesh playerMesh, PlayerTeam team)
        {
            if (playerMesh == null || playerMesh.PlayerLegPadLeft == null || playerMesh.PlayerLegPadRight == null)
                return;
                
            try
            {

                ApplyLegPadTextureToRenderer(playerMesh.PlayerLegPadLeft, team, "left");
                ApplyLegPadTextureToRenderer(playerMesh.PlayerLegPadRight, team, "right");
            }
            catch (Exception e)
            {
                Plugin.LogError($"Error applying leg pads: {e}");
            }
        }
        
        private static void ApplyLegPadTextureToRenderer(PlayerLegPad legPad, PlayerTeam team, string side)
        {
            if (legPad == null) return;
            

            MeshRenderer renderer = legPad.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                renderer = legPad.GetComponentInChildren<MeshRenderer>(true);
            }
            
            if (renderer == null)
            {
                Plugin.LogDebug($"Could not find renderer for {side} leg pad");
                return;
            }
            

            Texture2D texture = null;
            if (team == PlayerTeam.Blue)
            {
                var entry = side == "left" 
                    ? ReskinProfileManager.currentProfile.blueLegPadLeft 
                    : ReskinProfileManager.currentProfile.blueLegPadRight;
                    
                if (entry != null && entry.Path != null)
                {
                    texture = TextureManager.GetTexture(entry);
                }
            }
            else if (team == PlayerTeam.Red)
            {
                var entry = side == "left"
                    ? ReskinProfileManager.currentProfile.redLegPadLeft
                    : ReskinProfileManager.currentProfile.redLegPadRight;
                    
                if (entry != null && entry.Path != null)
                {
                    texture = TextureManager.GetTexture(entry);
                }
            }

            if (texture != null)
            {
                renderer.material.mainTexture = texture;
                renderer.material.SetTexture("_MainTex", texture);
                renderer.material.SetTexture("_BaseMap", texture);
                Plugin.LogDebug($"Applied {side} leg pad texture for {team}");
            }
        }
        
        private static void ApplyHelmetToPlayerMesh(PlayerMesh playerMesh, PlayerTeam team)
        {
            if (playerMesh == null || playerMesh.PlayerHead == null) return;
            
            try
            {

                var renderers = playerMesh.PlayerHead.GetComponentsInChildren<Renderer>(true);
                
                foreach (var renderer in renderers)
                {
                    if (renderer == null) continue;
                    
                    string name = renderer.gameObject.name.ToLower();
                    

                    bool isHelmetPart = name.Contains("helmet") || 
                                       name.Contains("mask") ||
                                       name.Contains("visor") ||
                                       name.Contains("cage") ||
                                       name.Contains("goalie");
                    
                    bool isFace = name.Contains("face") || 
                                 name.Contains("eye") ||
                                 name.Contains("mouth") ||
                                 name.Contains("nose");
                    
                    if (isHelmetPart && !isFace)
                    {

                        Texture2D texture = null;
                        var entry = team == PlayerTeam.Blue 
                            ? ReskinProfileManager.currentProfile.blueGoalieHelmet 
                            : ReskinProfileManager.currentProfile.redGoalieHelmet;
                            
                        if (entry != null && entry.Path != null)
                        {
                            texture = TextureManager.GetTexture(entry);
                        }
                        
                        if (texture != null)
                        {
                            renderer.material.mainTexture = texture;
                            renderer.material.SetTexture("_MainTex", texture);
                            renderer.material.SetTexture("_BaseMap", texture);
                            Plugin.LogDebug($"Applied helmet texture to {renderer.gameObject.name}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.LogError($"Error applying helmet: {e}");
            }
        }
        
        private static void ApplyJerseyToPlayerMesh(PlayerMesh playerMesh, PlayerTeam team, PlayerRole role)
        {
            if (playerMesh == null) return;
            
            try
            {

                ReskinRegistry.ReskinEntry torsoEntry = null;
                ReskinRegistry.ReskinEntry groinEntry = null;
                
                if (team == PlayerTeam.Blue)
                {
                    torsoEntry = role == PlayerRole.Goalie 
                        ? ReskinProfileManager.currentProfile.blueGoalieTorso 
                        : ReskinProfileManager.currentProfile.blueSkaterTorso;
                        
                    groinEntry = role == PlayerRole.Goalie
                        ? ReskinProfileManager.currentProfile.blueGoalieGroin
                        : ReskinProfileManager.currentProfile.blueSkaterGroin;
                }
                else if (team == PlayerTeam.Red)
                {
                    torsoEntry = role == PlayerRole.Goalie
                        ? ReskinProfileManager.currentProfile.redGoalieTorso
                        : ReskinProfileManager.currentProfile.redSkaterTorso;
                        
                    groinEntry = role == PlayerRole.Goalie
                        ? ReskinProfileManager.currentProfile.redGoalieGroin
                        : ReskinProfileManager.currentProfile.redSkaterGroin;
                }
                

                if (torsoEntry != null && torsoEntry.Path != null && playerMesh.PlayerTorso != null)
                {
                    var texture = TextureManager.GetTexture(torsoEntry);
                    if (texture != null)
                    {
                        ApplyTextureToPlayerTorso(playerMesh.PlayerTorso, texture);
                    }
                }
                

                if (groinEntry != null && groinEntry.Path != null && playerMesh.PlayerGroin != null)
                {
                    var texture = TextureManager.GetTexture(groinEntry);
                    if (texture != null)
                    {
                        ApplyTextureToPlayerGroin(playerMesh.PlayerGroin, texture);
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.LogError($"Error applying jersey: {e}");
            }
        }
        
        private static void ApplyTextureToPlayerTorso(PlayerTorso playerTorso, Texture2D texture)
        {

            var meshRendererTexturer = playerTorso.GetComponent<MeshRendererTexturer>();
            if (meshRendererTexturer != null)
            {

                var setTextureMethod = typeof(MeshRendererTexturer).GetMethod("SetTexture");
                if (setTextureMethod != null)
                {
                    setTextureMethod.Invoke(meshRendererTexturer, new object[] { texture });
                }
            }
            

            var renderer = playerTorso.GetComponent<Renderer>();
            if (renderer != null && renderer.material != null)
            {
                renderer.material.mainTexture = texture;
                renderer.material.SetTexture("_MainTex", texture);
                renderer.material.SetTexture("_BaseMap", texture);
            }
        }
        
        private static void ApplyTextureToPlayerGroin(PlayerGroin playerGroin, Texture2D texture)
        {

            var meshRendererTexturer = playerGroin.GetComponent<MeshRendererTexturer>();
            if (meshRendererTexturer != null)
            {
                var setTextureMethod = typeof(MeshRendererTexturer).GetMethod("SetTexture");
                if (setTextureMethod != null)
                {
                    setTextureMethod.Invoke(meshRendererTexturer, new object[] { texture });
                }
            }
            
            var renderer = playerGroin.GetComponent<Renderer>();
            if (renderer != null && renderer.material != null)
            {
                renderer.material.mainTexture = texture;
                renderer.material.SetTexture("_MainTex", texture);
                renderer.material.SetTexture("_BaseMap", texture);
            }
        }
    }
}