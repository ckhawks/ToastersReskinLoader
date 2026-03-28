using System;
using System.Reflection;
using HarmonyLib;
using ToasterReskinLoader.swappers;
using ToasterReskinLoader.ui.sections;
using UnityEngine;

namespace ToasterReskinLoader
{
    public static class ChangingRoomPatcher
    {
        private static readonly FieldInfo _playerMeshField = typeof(LockerRoomPlayer)
            .GetField("playerMesh", BindingFlags.Instance | BindingFlags.NonPublic);

        private static PlayerMesh GetPlayerMesh(LockerRoomPlayer instance)
        {
            return (PlayerMesh)_playerMeshField?.GetValue(instance);
        }

        // Patch LockerRoomPlayer.SetJerseyID to apply custom jersey/leg pad textures
        [HarmonyPatch(typeof(LockerRoomPlayer), nameof(LockerRoomPlayer.SetJerseyID))]
        public static class LockerRoomPlayerSetJerseyIDPatch
        {
            [HarmonyPostfix]
            public static void Postfix(LockerRoomPlayer __instance, int jerseyID, PlayerTeam team)
            {
                try
                {
                    var playerMesh = GetPlayerMesh(__instance);
                    if (playerMesh == null) return;

                    var role = SettingsManager.Role;
                    Plugin.LogDebug($"LockerRoomPlayer.SetJerseyID called for team: {team}, role: {role}");

                    // Apply jerseys for both roles since the user may have customizations for either
                    ApplyJerseyToPlayerMesh(playerMesh, team, role);

                    // Always try leg pads - they'll just not find a renderer if the player isn't a goalie
                    ApplyLegPadsToPlayerMesh(playerMesh, team);

                    // Apply body model swap to locker room preview
                    GenderSwapper.ApplyToPlayerMesh(playerMesh, PlayerCustomizationSection.IsFemaleBodyType);
                }
                catch (Exception e)
                {
                    Plugin.LogError($"Error in LockerRoomPlayerSetJerseyIDPatch: {e}");
                }
            }
        }

        // Patch LockerRoomPlayer.SetHeadgearID to apply custom helmet textures
        [HarmonyPatch(typeof(LockerRoomPlayer), nameof(LockerRoomPlayer.SetHeadgearID))]
        public static class LockerRoomPlayerSetHeadgearIDPatch
        {
            [HarmonyPostfix]
            public static void Postfix(LockerRoomPlayer __instance, int headgearID, PlayerRole role)
            {
                try
                {
                    var playerMesh = GetPlayerMesh(__instance);
                    if (playerMesh == null) return;

                    var team = SettingsManager.Team;
                    Plugin.LogDebug($"LockerRoomPlayer.SetHeadgearID called for role: {role}, team: {team}");

                    if (role == PlayerRole.Goalie)
                    {
                        ApplyHelmetToPlayerMesh(playerMesh, team);
                    }
                    else if (role == PlayerRole.Attacker)
                    {
                        ApplySkaterHelmetToPlayerMesh(playerMesh, team);
                    }

                    HatSwapper.AttachToPlayerMesh(playerMesh, PlayerCustomizationSection.SelectedHatId);
                }
                catch (Exception e)
                {
                    Plugin.LogError($"Error in LockerRoomPlayerSetHeadgearIDPatch: {e}");
                }
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
                SwapperUtils.ApplyTextureToMaterial(renderer.material, texture);
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

                    // Main goalie mask shell: "Helmet Cage & Neck Guard (Goalie)"
                    if (name.Contains("helmet") && name.Contains("goalie"))
                    {
                        var entry = team == PlayerTeam.Blue
                            ? ReskinProfileManager.currentProfile.blueGoalieHelmet
                            : ReskinProfileManager.currentProfile.redGoalieHelmet;
                        if (entry?.Path != null)
                        {
                            var tex = TextureManager.GetTexture(entry);
                            if (tex != null) ApplyTextureToRenderer(renderer, tex);
                        }
                    }
                    // Standalone neck guard texture
                    else if (name == "neck guard")
                    {
                        var entry = team == PlayerTeam.Blue
                            ? ReskinProfileManager.currentProfile.blueGoalieMask
                            : ReskinProfileManager.currentProfile.redGoalieMask;
                        if (entry?.Path != null)
                        {
                            var tex = TextureManager.GetTexture(entry);
                            if (tex != null) ApplyTextureToRenderer(renderer, tex);
                        }
                    }
                    // Standalone cage - no texture, colors handled by ChangingRoomHelper.ApplyHelmetColors
                }
            }
            catch (Exception e)
            {
                Plugin.LogError($"Error applying goalie helmet: {e}");
            }
        }

        private static void ApplyTextureToRenderer(Renderer renderer, Texture2D texture)
        {
            SwapperUtils.ApplyTextureToMaterial(renderer.material, texture);
            Plugin.LogDebug($"Applied texture to {renderer.gameObject.name}");
        }

        private static void ApplySkaterHelmetToPlayerMesh(PlayerMesh playerMesh, PlayerTeam team)
        {
            if (playerMesh == null || playerMesh.PlayerHead == null) return;

            try
            {
                var renderers = playerMesh.PlayerHead.GetComponentsInChildren<Renderer>(true);

                foreach (var renderer in renderers)
                {
                    if (renderer == null) continue;

                    string name = renderer.gameObject.name.ToLower();

                    // Skater helmets: texture only (colors handled by ChangingRoomHelper.ApplyHelmetColors)
                    if (name.Contains("helmet") && !name.Contains("cage") && !name.Contains("neck"))
                    {
                        ReskinRegistry.ReskinEntry entry = team == PlayerTeam.Blue
                            ? ReskinProfileManager.currentProfile.blueSkaterHelmet
                            : ReskinProfileManager.currentProfile.redSkaterHelmet;

                        if (entry?.Path != null)
                        {
                            Texture2D texture = TextureManager.GetTexture(entry);
                            if (texture != null) ApplyTextureToRenderer(renderer, texture);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.LogError($"Error applying skater helmet: {e}");
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
                        ApplyTextureToComponent(playerMesh.PlayerTorso, texture);
                    }
                }

                if (groinEntry != null && groinEntry.Path != null && playerMesh.PlayerGroin != null)
                {
                    var texture = TextureManager.GetTexture(groinEntry);
                    if (texture != null)
                    {
                        ApplyTextureToComponent(playerMesh.PlayerGroin, texture);
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.LogError($"Error applying jersey: {e}");
            }
        }

        private static void ApplyTextureToComponent(Component component, Texture2D texture)
        {
            var meshRendererTexturer = component.GetComponent<MeshRendererTexturer>();
            if (meshRendererTexturer != null)
                meshRendererTexturer.SetTexture(texture);

            var renderer = component.GetComponent<Renderer>();
            if (renderer != null && renderer.material != null)
                SwapperUtils.ApplyTextureToMaterial(renderer.material, texture);
        }
    }
}
