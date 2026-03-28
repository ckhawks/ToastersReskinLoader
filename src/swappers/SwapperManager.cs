using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine.SceneManagement;
using ToasterReskinLoader.swappers;
using ToasterReskinLoader.ui.sections;

namespace ToasterReskinLoader.swappers;

public static class SwapperManager
{
    // TODO this class should call the various swappers to trigger them to set textures/reskins whenever game events happen.
    // TODO we also need like a SettingsManager or something that handles the state of which reskins are selected, and handles saving/loading profiles

    // TODO all selected skins' Texture2D's should be loaded into memory.
    // TODO when someone selects a new skin we can load that one into memory

    // TODO we need to save each player's vanilla setup (stick, jersey) before applying anything

    // Intended to be called whenever we need to update the local player's stick
    public static void OnPersonalStickChanged()
    {
        SetStickReskinForPlayer(PlayerManager.Instance.GetLocalPlayer());
    }

    public static void OnBlueTeamStickChanged()
    {
        List<Player> bluePlayers = PlayerManager.Instance.GetSpawnedPlayersByTeam(PlayerTeam.Blue);
        foreach (Player bluePlayer in bluePlayers)
        {
            if (!bluePlayer.IsLocalPlayer)
                SetStickReskinForPlayer(bluePlayer);
        }
    }

    public static void OnRedTeamStickChanged()
    {
        List<Player> redPlayers = PlayerManager.Instance.GetSpawnedPlayersByTeam(PlayerTeam.Red);
        foreach (Player redPlayer in redPlayers)
        {
            if (!redPlayer.IsLocalPlayer)
                SetStickReskinForPlayer(redPlayer);
        }
    }
    public static void OnBlueHelmetsChanged()
    {
        GoalieHelmetSwapper.OnBlueHelmetsChanged();
    }

    public static void OnRedHelmetsChanged()
    {
        GoalieHelmetSwapper.OnRedHelmetsChanged();
    }
    
    private static void SetStickReskinForPlayer(Player player)
    {
        // If we are missing a part of the player, player body, or stick
        if (player == null || player.PlayerBody == null || player.Stick == null)
            return;

        Plugin.LogDebug($"player.Team {player.Team.ToString()}");
        Plugin.LogDebug($"player.Role {player.Role.ToString()}");

        bool isReplayLocalPlayer = player.IsReplay.Value &&
                                   PlayerManager.Instance.GetLocalPlayer()?.OwnerClientId == player.OwnerClientId - 1337UL;

        switch (player.Team)
        {
            case PlayerTeam.Blue when player.IsLocalPlayer || isReplayLocalPlayer:
                StickSwapper.SetStickTexture(player.Stick,
                    player.Role == PlayerRole.Attacker
                        ? ReskinProfileManager.currentProfile.stickAttackerBluePersonal
                        : ReskinProfileManager.currentProfile.stickGoalieBluePersonal);

                return;
            case PlayerTeam.Blue:
                StickSwapper.SetStickTexture(player.Stick,
                    player.Role == PlayerRole.Attacker
                        ? ReskinProfileManager.currentProfile.stickAttackerBlue
                        : ReskinProfileManager.currentProfile.stickGoalieBlue);

                return;
            case PlayerTeam.Red when player.IsLocalPlayer || isReplayLocalPlayer:
                StickSwapper.SetStickTexture(player.Stick,
                    player.Role == PlayerRole.Attacker
                        ? ReskinProfileManager.currentProfile.stickAttackerRedPersonal
                        : ReskinProfileManager.currentProfile.stickGoalieRedPersonal);
                return;
            case PlayerTeam.Red:
                StickSwapper.SetStickTexture(player.Stick,
                    player.Role == PlayerRole.Attacker
                        ? ReskinProfileManager.currentProfile.stickAttackerRed
                        : ReskinProfileManager.currentProfile.stickGoalieRed);
                break;
            case PlayerTeam.None:
            case PlayerTeam.Spectator:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    // This patch makes the jersey change when a player spawns
    [HarmonyPatch(typeof(PlayerBody), nameof(PlayerBody.ApplyCustomizations))]
    public static class PlayerBodyApplyCustomizations
    {
        [HarmonyPostfix]
        public static void Postfix(PlayerBody __instance)
        {
            JerseySwapper.SetJerseyForPlayer(__instance.Player);
            GoalieEquipmentSwapper.SetLegPadsForPlayer(__instance.Player);
            GoalieHelmetSwapper.SetHeadgearForPlayer(__instance.Player);
            SkaterHelmetSwapper.SetHelmetForPlayer(__instance.Player);
            PartyHatSwapper.AttachToPlayer(__instance.Player);
        }
    }

    // This patch makes the stick change when a player spawns
    [HarmonyPatch(typeof(Stick), nameof(Stick.ApplyCustomizations))]
    public static class StickApplyCustomizationsPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Stick __instance)
        {
            Plugin.LogDebug($"Stick.ApplyCustomizations");
            SetStickReskinForPlayer(__instance.Player);
            if (__instance.Player.IsLocalPlayer)
                StickTapeSwapper.SetStickTapeForPlayer(__instance.Player.Stick);
        }
    }

    public static void Setup()
    {
        global::UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        FullArenaSwapper.Initialize();
        PartyHatSwapper.Initialize();
        TeamIndicatorSwapper.Setup();
        // // We register patches for Changing Room
        // var harmony = new Harmony("com.toaster.reskinloader");
        // harmony.PatchAll(typeof(ChangingRoomPatcher));
    }

    public static void Destroy()
    {
        global::UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        PartyHatSwapper.Cleanup();
        TeamIndicatorSwapper.Cleanup();
    }

    public static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Plugin.Log($"OnSceneLoaded: {scene.name}");
        if (scene.name.Equals("locker_room"))
        {
            StickTapeSwapper.ClearTapeCache();
            JerseySwapper.ClearJerseyCache();
            GoalieEquipmentSwapper.ClearEquipmentCache();
            GoalieHelmetSwapper.ClearHelmetCache();
            SkaterHelmetSwapper.ClearHelmetCache();
            PartyHatSwapper.ClearHats();
            Plugin.Log($"Local player caches reset from switching to locker room");
        }

        SetAll();
    }

    // Update each jersey texture (torso and groin) for all blue team players
    public static void OnBlueJerseyChanged()
    {
        List<Player> bluePlayers = PlayerManager.Instance.GetPlayersByTeam(PlayerTeam.Blue);
        foreach (Player player in bluePlayers)
        {
            try
            {
                JerseySwapper.SetJerseyForPlayer(player);
            }
            catch (Exception e)
            {
                Plugin.LogError($"Error when setting jersey for {player.Username.Value}: {e.Message}");
            }
        }
    }

    // Update each jersey texture (torso and groin) for all red team players
    public static void OnRedJerseyChanged()
    {
        List<Player> redPlayers = PlayerManager.Instance.GetPlayersByTeam(PlayerTeam.Red);
        foreach (Player player in redPlayers)
        {
            try
            {
                JerseySwapper.SetJerseyForPlayer(player);
            }
            catch (Exception e)
            {
                Plugin.LogError($"Error when setting jersey for {player.Username.Value}: {e.Message}");
            }
        }
    }
    // TODO add to when players spawn to call these

    public static void OnBlueLegPadsChanged()
    {
        GoalieEquipmentSwapper.OnBlueLegPadsChanged();
    }

    public static void OnRedLegPadsChanged()
    {
        GoalieEquipmentSwapper.OnRedLegPadsChanged();
    }

    public static void SetAll()
    {
        {
            IceSwapper.SetIceTexture();
            IceSwapper.UpdateIceSmoothness();
            ArenaSwapper.UpdateCrowdState();
            ArenaSwapper.UpdateHangarState();
            ArenaSwapper.UpdateScoreboardState();
            ArenaSwapper.UpdateGlassState();
            ArenaSwapper.UpdateBoards();
            ArenaSwapper.UpdateGlassAndPillars();
            ArenaSwapper.UpdateSpectators();
            ArenaSwapper.SetNetTexture();
            ArenaSwapper.UpdateGoalFrameColors();
            OnBlueJerseyChanged();
            OnRedJerseyChanged();
            OnBlueLegPadsChanged();
            OnRedLegPadsChanged();
            OnBlueHelmetsChanged();
            OnRedHelmetsChanged();
            SkaterHelmetSwapper.OnBlueHelmetsChanged();
            SkaterHelmetSwapper.OnRedHelmetsChanged();
            FullArenaSwapper.ApplyFromProfile();
            SkyboxSwapper.UpdateSkybox();
            CrispyShadowsSwapper.Apply();
            TeamIndicatorSwapper.Setup();
            TeamIndicatorSwapper.UpdateVisibility();
            PuckFXSwapper.ApplyAll();
        }
    }
}