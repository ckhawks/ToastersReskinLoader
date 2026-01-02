using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ToasterReskinLoader.swappers;

public static class JerseySwapper
{
    private static Texture originalBlueGroin;
    private static Texture originalRedGroin;
    private static Dictionary<ulong, Texture> originalBlueTorsoTextures = new Dictionary<ulong, Texture>(); // TODO clear this when leave server
    private static Dictionary<ulong, Texture> originalRedTorsoTextures = new Dictionary<ulong, Texture>();
    
    static readonly FieldInfo _meshRendererTexturerTorsoField = typeof(PlayerTorso)
        .GetField("meshRendererTexturer", 
            BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo _meshRendererTexturerGroinField = typeof(PlayerGroin)
        .GetField("meshRendererTexturer", 
            BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo _meshRendererField = typeof(MeshRendererTexturer)
        .GetField("meshRenderer",
            BindingFlags.Instance | BindingFlags.NonPublic);

    // Helper method to consolidate texture application logic
    private static void ApplyJerseyTexture(MeshRenderer meshRenderer, ReskinRegistry.ReskinEntry reskinEntry, Texture originalTexture)
    {
        if (reskinEntry == null || reskinEntry.Path == null)
        {
            // Restore original texture
            meshRenderer.material.mainTexture = originalTexture;
        }
        else
        {
            // Apply custom texture
            var texture = TextureManager.GetTexture(reskinEntry);
            meshRenderer.material.SetTexture("_MainTex", texture);
            meshRenderer.material.SetTexture("_BaseMap", texture);
        }
    }

    public static void SetJerseyForPlayer(Player player)
    {
        Plugin.LogDebug($"Setting jersey for {player.Username.Value} {player.Team.Value} isReplay {player.IsReplay.Value}");
        PlayerTeam team = player.Team.Value;

        if (team is not (PlayerTeam.Blue or PlayerTeam.Red))
        {
            Plugin.LogDebug($"Player {player.Username.Value} is not on red or blue team, not swapping jersey.");
            return;
        }

        if (player == null || player.PlayerBody == null || player.PlayerBody.PlayerMesh == null ||
            player.PlayerBody.PlayerMesh.PlayerTorso == null)
        {
            Plugin.LogError($"Player {player.Username.Value} is missing body parts.");
            return;
        }
        
        MeshRendererTexturer torsoMeshRendererTexturer =
            (MeshRendererTexturer) _meshRendererTexturerTorsoField.GetValue(player.PlayerBody.PlayerMesh.PlayerTorso);
        MeshRendererTexturer groinMeshRendererTexturer =
            (MeshRendererTexturer) _meshRendererTexturerGroinField.GetValue(player.PlayerBody.PlayerMesh.PlayerGroin);
        
        // can call torsoMeshRendererTexturer.SetTexture(Texture);
        MeshRenderer torsoMeshRenderer = (MeshRenderer) _meshRendererField.GetValue(torsoMeshRendererTexturer);
        MeshRenderer groinMeshRenderer = (MeshRenderer) _meshRendererField.GetValue(groinMeshRendererTexturer);

        // SwapperUtils.FindTextureProperties(torsoMeshRenderer.material);
        // Plugin.Log($"Texture torso property: {SwapperUtils.FindTextureProperty(torsoMeshRenderer.material)}");
        // Plugin.Log($"Texture groin property: {SwapperUtils.FindTextureProperty(groinMeshRenderer.material)}");
        
        // player.PlayerBody.PlayerMesh.SetJersey(player.Team.Value, player.GetPlayerJerseySkin().ToString());
        
        if (team == PlayerTeam.Blue)
        {
            if (originalBlueGroin == null)
            {
                originalBlueGroin = groinMeshRenderer.material.mainTexture;
            }

            if (!originalBlueTorsoTextures.ContainsKey(player.OwnerClientId))
            {
                if (torsoMeshRenderer.material.mainTexture.name.Contains("blue_"))
                    originalBlueTorsoTextures.Add(player.OwnerClientId, torsoMeshRenderer.material.mainTexture);
            }

            if (player.Role.Value == PlayerRole.Goalie)
            {
                //  Apply blue goalie torso
                if (originalBlueTorsoTextures.ContainsKey(player.OwnerClientId))
                    ApplyJerseyTexture(torsoMeshRenderer, ReskinProfileManager.currentProfile.blueGoalieTorso, originalBlueTorsoTextures[player.OwnerClientId]);

                //  Apply blue goalie groin
                ApplyJerseyTexture(groinMeshRenderer, ReskinProfileManager.currentProfile.blueGoalieGroin, originalBlueGroin);
            }
            else
            {
                //  Apply blue skater torso
                if (originalBlueTorsoTextures.ContainsKey(player.OwnerClientId))
                {
                    Plugin.LogDebug($"Setting blue skater torso to {ReskinProfileManager.currentProfile.blueSkaterTorso?.Name ?? "original"}");
                    ApplyJerseyTexture(torsoMeshRenderer, ReskinProfileManager.currentProfile.blueSkaterTorso, originalBlueTorsoTextures[player.OwnerClientId]);
                }

                //  Apply blue skater groin
                ApplyJerseyTexture(groinMeshRenderer, ReskinProfileManager.currentProfile.blueSkaterGroin, originalBlueGroin);
            }
        } else if (team == PlayerTeam.Red)
        {
            if (originalRedGroin == null)
            {
                originalRedGroin = groinMeshRenderer.material.mainTexture;
            }
            
            if (!originalRedTorsoTextures.ContainsKey(player.OwnerClientId))
            {
                if (torsoMeshRenderer.material.mainTexture.name.Contains("red_"))
                {
                    originalRedTorsoTextures.Add(player.OwnerClientId, torsoMeshRenderer.material.mainTexture);
                }
            }
            
            if (player.Role.Value == PlayerRole.Goalie)
            {
                //  Apply red goalie torso
                if (originalRedTorsoTextures.ContainsKey(player.OwnerClientId))
                    ApplyJerseyTexture(torsoMeshRenderer, ReskinProfileManager.currentProfile.redGoalieTorso, originalRedTorsoTextures[player.OwnerClientId]);

                //  Apply red goalie groin
                ApplyJerseyTexture(groinMeshRenderer, ReskinProfileManager.currentProfile.redGoalieGroin, originalRedGroin);
            }
            else
            {
                //  Apply red skater torso
                if (originalRedTorsoTextures.ContainsKey(player.OwnerClientId))
                    ApplyJerseyTexture(torsoMeshRenderer, ReskinProfileManager.currentProfile.redSkaterTorso, originalRedTorsoTextures[player.OwnerClientId]);

                //  Apply red skater groin
                ApplyJerseyTexture(groinMeshRenderer, ReskinProfileManager.currentProfile.redSkaterGroin, originalRedGroin);
            }
        }
        Plugin.LogDebug($"Set jersey for {player.Username.Value.ToString()}");
    }

    // [HarmonyPatch(typeof(PlayerMesh), nameof(PlayerMesh.SetJersey))]
    // public static class PlayerMeshSetJersey
    // {
    //     [HarmonyPrefix]
    //     public static void Prefix(PlayerMesh __instance)
    //     {
    //         Player player = __instance.
    //     }
    // }
    
}