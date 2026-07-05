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

    // B1117 rewrote MeshRendererTexturer to apply the jersey as a per-renderer
    // MaterialPropertyBlock override on `texturePropertyName` (default "_BaseMap")
    // at `materialIndex` (default 0), instead of instantiating a material. A property
    // block overrides anything written to material.mainTexture, so we must both read
    // the vanilla jersey from — and write our reskin into — that same block, via the
    // game's SetTexture(). We reflect the block coordinates to read the exact slot.
    static readonly FieldInfo _meshRendererField = typeof(MeshRendererTexturer)
        .GetField("meshRenderer",
            BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo _texturePropertyNameField = typeof(MeshRendererTexturer)
        .GetField("texturePropertyName",
            BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo _materialIndexField = typeof(MeshRendererTexturer)
        .GetField("materialIndex",
            BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly MaterialPropertyBlock _readBlock = new MaterialPropertyBlock();

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

    // Reads the jersey texture the game currently has applied via the texturer's
    // MaterialPropertyBlock (not material.mainTexture, which the block overrides).
    private static Texture ReadJerseyTexture(MeshRendererTexturer texturer)
    {
        if (texturer == null) return null;
        var renderer = (MeshRenderer) _meshRendererField.GetValue(texturer);
        if (renderer == null) return null;

        int idx = _materialIndexField != null ? (int) _materialIndexField.GetValue(texturer) : 0;
        string prop = _texturePropertyNameField?.GetValue(texturer) as string ?? "_BaseMap";

        renderer.GetPropertyBlock(_readBlock, idx);
        return _readBlock.GetTexture(prop);
    }

    private static void TryCacheOriginal(Dictionary<ulong, Texture> cache, ulong id, MeshRendererTexturer texturer)
    {
        if (cache.ContainsKey(id)) return;
        Texture current = ReadJerseyTexture(texturer);
        if (IsRealTexture(current))
            cache[id] = current;
    }

    // Helper method to consolidate texture application logic.
    // If no reskin is configured and we have no trusted original cached, leave the
    // texturer alone so the game's own texture (which may load async) is preserved.
    private static void ApplyJerseyTexture(MeshRendererTexturer texturer, ReskinRegistry.ReskinEntry reskinEntry, Texture originalTexture)
    {
        if (texturer == null) return;

        if (reskinEntry == null || reskinEntry.Path == null)
        {
            // Restore: push the cached vanilla jersey back into the property block.
            if (originalTexture != null)
                texturer.SetTexture(originalTexture);
        }
        else
        {
            var texture = TextureManager.GetTexture(reskinEntry);
            if (texture != null)
            {
                texturer.SetTexture(texture);
            }
            else
            {
                Plugin.LogError($"Failed to load jersey texture for '{reskinEntry.Name}', keeping original.");
                if (originalTexture != null)
                    texturer.SetTexture(originalTexture);
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

        if (team == PlayerTeam.Blue)
        {
            TryCacheOriginal(originalBlueTorsoTextures, player.OwnerClientId, torsoMeshRendererTexturer);
            if (originalBlueGroin == null)
            {
                var g = ReadJerseyTexture(groinMeshRendererTexturer);
                if (IsRealTexture(g)) originalBlueGroin = g;
            }

            Texture torsoOrig = originalBlueTorsoTextures.TryGetValue(player.OwnerClientId, out var bt) ? bt : null;

            if (player.Role == PlayerRole.Goalie)
            {
                ApplyJerseyTexture(torsoMeshRendererTexturer, ReskinProfileManager.currentProfile.blueGoalieTorso, torsoOrig);
                ApplyJerseyTexture(groinMeshRendererTexturer, ReskinProfileManager.currentProfile.blueGoalieGroin, originalBlueGroin);
            }
            else
            {
                Plugin.LogDebug($"Setting blue skater torso to {ReskinProfileManager.currentProfile.blueSkaterTorso?.Name ?? "original"}");
                ApplyJerseyTexture(torsoMeshRendererTexturer, ReskinProfileManager.currentProfile.blueSkaterTorso, torsoOrig);
                ApplyJerseyTexture(groinMeshRendererTexturer, ReskinProfileManager.currentProfile.blueSkaterGroin, originalBlueGroin);
            }
        } else if (team == PlayerTeam.Red)
        {
            TryCacheOriginal(originalRedTorsoTextures, player.OwnerClientId, torsoMeshRendererTexturer);
            if (originalRedGroin == null)
            {
                var g = ReadJerseyTexture(groinMeshRendererTexturer);
                if (IsRealTexture(g)) originalRedGroin = g;
            }

            Texture torsoOrig = originalRedTorsoTextures.TryGetValue(player.OwnerClientId, out var rt) ? rt : null;

            if (player.Role == PlayerRole.Goalie)
            {
                ApplyJerseyTexture(torsoMeshRendererTexturer, ReskinProfileManager.currentProfile.redGoalieTorso, torsoOrig);
                ApplyJerseyTexture(groinMeshRendererTexturer, ReskinProfileManager.currentProfile.redGoalieGroin, originalRedGroin);
            }
            else
            {
                ApplyJerseyTexture(torsoMeshRendererTexturer, ReskinProfileManager.currentProfile.redSkaterTorso, torsoOrig);
                ApplyJerseyTexture(groinMeshRendererTexturer, ReskinProfileManager.currentProfile.redSkaterGroin, originalRedGroin);
            }
        }
        Plugin.LogDebug($"Set jersey for {player.Username.Value.ToString()}");

        // Prototype: Test player text color customization
        PlayerTextSwapper.SetPlayerTextColors(player);
    }

}
