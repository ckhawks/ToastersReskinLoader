using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ToasterReskinLoader.swappers;

public static class JerseySwapper
{
    private static Texture originalBlueGroin;
    private static Texture originalRedGroin;
    private static Dictionary<ulong, Texture> originalBlueTorsoTextures = new Dictionary<ulong, Texture>();
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

    public static void ClearJerseyCache()
    {
        originalBlueTorsoTextures.Clear();
        originalRedTorsoTextures.Clear();
    }

    // A "real" jersey texture is anything other than null or Unity's built-in
    // white placeholder (seen when PlayerTorso hasn't been assigned a jerseyID yet,
    // e.g. AI goalies spawned with jerseyID=0 on a remote server).
    private static bool IsRealTexture(Texture tex)
    {
        if (tex == null) return false;
        string n = tex.name;
        if (string.IsNullOrEmpty(n)) return false;
        return n != "UnityWhite" && !n.StartsWith("Default-");
    }

    private static void TryCacheOriginal(Dictionary<ulong, Texture> cache, ulong id, MeshRenderer renderer)
    {
        if (cache.ContainsKey(id)) return;
        Texture current = renderer.material.mainTexture;
        if (IsRealTexture(current))
            cache[id] = current;
    }
    
    // Helper method to consolidate texture application logic.
    // If no reskin is configured and we have no trusted original cached, leave the
    // material alone so the game's own texture (which may load async) is preserved.
    private static void ApplyJerseyTexture(MeshRenderer meshRenderer, ReskinRegistry.ReskinEntry reskinEntry, Texture originalTexture)
    {
        if (reskinEntry == null || reskinEntry.Path == null)
        {
            if (originalTexture != null)
                meshRenderer.material.mainTexture = originalTexture;
        }
        else
        {
            var texture = TextureManager.GetTexture(reskinEntry);
            if (texture != null)
            {
                SwapperUtils.ApplyTextureToMaterial(meshRenderer.material, texture);
            }
            else
            {
                Plugin.LogError($"Failed to load jersey texture for '{reskinEntry.Name}', keeping original.");
                if (originalTexture != null)
                    meshRenderer.material.mainTexture = originalTexture;
            }
        }
    }

    public static void SetJerseyForPlayer(Player player)
    {
        Plugin.LogDebug($"Setting jersey for {player.Username.Value} {player.Team} isReplay {player.IsReplay.Value}");
        PlayerTeam team = player.Team;

        if (team is not (PlayerTeam.Blue or PlayerTeam.Red))
        {
            Plugin.LogDebug($"Player {player.Username.Value} is not on red or blue team, not swapping jersey.");
            return;
        }

        if (player == null || player.PlayerBody == null || player.PlayerBody.PlayerMesh == null ||
            player.PlayerBody.PlayerMesh.PlayerTorso == null)
        {
            Plugin.LogDebug($"Player {player.Username.Value} is missing body parts, will retry on ApplyCustomizations.");
            return;
        }
        
        MeshRendererTexturer torsoMeshRendererTexturer =
            (MeshRendererTexturer) _meshRendererTexturerTorsoField.GetValue(player.PlayerBody.PlayerMesh.PlayerTorso);
        MeshRendererTexturer groinMeshRendererTexturer =
            (MeshRendererTexturer) _meshRendererTexturerGroinField.GetValue(player.PlayerBody.PlayerMesh.PlayerGroin);
        
        // can call torsoMeshRendererTexturer.SetTexture(Texture);
        MeshRenderer torsoMeshRenderer = (MeshRenderer) _meshRendererField.GetValue(torsoMeshRendererTexturer);
        MeshRenderer groinMeshRenderer = (MeshRenderer) _meshRendererField.GetValue(groinMeshRendererTexturer);

        if (team == PlayerTeam.Blue)
        {
            TryCacheOriginal(originalBlueTorsoTextures, player.OwnerClientId, torsoMeshRenderer);
            if (originalBlueGroin == null && IsRealTexture(groinMeshRenderer.material.mainTexture))
                originalBlueGroin = groinMeshRenderer.material.mainTexture;

            Texture torsoOrig = originalBlueTorsoTextures.TryGetValue(player.OwnerClientId, out var bt) ? bt : null;

            if (player.Role == PlayerRole.Goalie)
            {
                ApplyJerseyTexture(torsoMeshRenderer, ReskinProfileManager.currentProfile.blueGoalieTorso, torsoOrig);
                ApplyJerseyTexture(groinMeshRenderer, ReskinProfileManager.currentProfile.blueGoalieGroin, originalBlueGroin);
            }
            else
            {
                Plugin.LogDebug($"Setting blue skater torso to {ReskinProfileManager.currentProfile.blueSkaterTorso?.Name ?? "original"}");
                ApplyJerseyTexture(torsoMeshRenderer, ReskinProfileManager.currentProfile.blueSkaterTorso, torsoOrig);
                ApplyJerseyTexture(groinMeshRenderer, ReskinProfileManager.currentProfile.blueSkaterGroin, originalBlueGroin);
            }
        } else if (team == PlayerTeam.Red)
        {
            TryCacheOriginal(originalRedTorsoTextures, player.OwnerClientId, torsoMeshRenderer);
            if (originalRedGroin == null && IsRealTexture(groinMeshRenderer.material.mainTexture))
                originalRedGroin = groinMeshRenderer.material.mainTexture;

            Texture torsoOrig = originalRedTorsoTextures.TryGetValue(player.OwnerClientId, out var rt) ? rt : null;

            if (player.Role == PlayerRole.Goalie)
            {
                ApplyJerseyTexture(torsoMeshRenderer, ReskinProfileManager.currentProfile.redGoalieTorso, torsoOrig);
                ApplyJerseyTexture(groinMeshRenderer, ReskinProfileManager.currentProfile.redGoalieGroin, originalRedGroin);
            }
            else
            {
                ApplyJerseyTexture(torsoMeshRenderer, ReskinProfileManager.currentProfile.redSkaterTorso, torsoOrig);
                ApplyJerseyTexture(groinMeshRenderer, ReskinProfileManager.currentProfile.redSkaterGroin, originalRedGroin);
            }
        }
        Plugin.LogDebug($"Set jersey for {player.Username.Value.ToString()}");

        // Prototype: Test player text color customization
        PlayerTextSwapper.SetPlayerTextColors(player);
    }

}