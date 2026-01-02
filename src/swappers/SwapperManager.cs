using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine.SceneManagement;
using ToasterReskinLoader.swappers;


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

        Plugin.LogDebug($"player.Team {player.Team.Value.ToString()}");
        Plugin.LogDebug($"player.Role {player.Role.Value.ToString()}");

        bool isReplayLocalPlayer = player.IsReplay.Value &&
                                   PlayerManager.Instance.GetLocalPlayer().OwnerClientId == player.OwnerClientId - 1337UL;

        switch (player.Team.Value)
        {
            case PlayerTeam.Blue when player.IsLocalPlayer || isReplayLocalPlayer:
                StickSwapper.SetStickTexture(player.Stick,
                    player.Role.Value == PlayerRole.Attacker
                        ? ReskinProfileManager.currentProfile.stickAttackerBluePersonal
                        : ReskinProfileManager.currentProfile.stickGoalieBluePersonal);

                return;
            case PlayerTeam.Blue:
                StickSwapper.SetStickTexture(player.Stick,
                    player.Role.Value == PlayerRole.Attacker
                        ? ReskinProfileManager.currentProfile.stickAttackerBlue
                        : ReskinProfileManager.currentProfile.stickGoalieBlue);

                return;
            case PlayerTeam.Red when player.IsLocalPlayer || isReplayLocalPlayer:
                StickSwapper.SetStickTexture(player.Stick,
                    player.Role.Value == PlayerRole.Attacker
                        ? ReskinProfileManager.currentProfile.stickAttackerRedPersonal
                        : ReskinProfileManager.currentProfile.stickGoalieRedPersonal);
                return;
            case PlayerTeam.Red:
                StickSwapper.SetStickTexture(player.Stick,
                    player.Role.Value == PlayerRole.Attacker
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

    // [HarmonyPatch(typeof(Stick), "OnNetworkPostSpawn")]
    // public static class StickOnNetworkPostSpawn
    // {
    //     [HarmonyPostfix]
    //     public static void Postfix(Stick __instance)
    //     {
    //         Plugin.LogDebug($"Stick.OnNetworkPostSpawn");
    //         Player player = __instance.PlayerBody.Player;
    //
    //         SetStickReskinForPlayer(player);
    //         JerseySwapper.SetJerseyForPlayer(player);
    //     }
    // }

    // [HarmonyPatch(typeof(Player), "OnPlayerRoleChanged")]
    // public static class PlayerOnPlayerRoleChanged
    // {
    //     [HarmonyPostfix]
    //     public static void Postfix(Player __instance, PlayerRole newRole)
    //     {
    //         Plugin.LogDebug($"Player.OnPlayerRoleChanged");
    //
    //         if (newRole != null && newRole != PlayerRole.None)
    //         {
    //             SetStickReskinForPlayer(__instance);
    //             JerseySwapper.SetJerseyForPlayer(__instance);
    //         }
    //     }
    // }

    // [HarmonyPatch(typeof(Player), "OnNetworkPostSpawn")]
    // public static class PlayerOnNetworkPostSpawn
    // {
    //     [HarmonyPostfix]
    //     public static void Postfix(Player __instance)
    //     {
    //         Plugin.LogDebug($"Player.OnNetworkPostSpawn");
    //         OnPersonalStickChanged();
    //         OnBlueTeamStickChanged();
    //         OnRedTeamStickChanged();
    //         // OnBlueJerseyChanged();
    //         // OnRedJerseyChanged();
    //     }
    // }

    // This patch makes the jersey change when a player spawns
    [HarmonyPatch(typeof(PlayerBodyV2), nameof(PlayerBodyV2.UpdateMesh))]
    public static class PlayerBodyV2UpdateMesh
    {
        [HarmonyPostfix]
        public static void Postfix(PlayerBodyV2 __instance)
        {
            JerseySwapper.SetJerseyForPlayer(__instance.Player);
            GoalieEquipmentSwapper.SetLegPadsForPlayer(__instance.Player);
            GoalieHelmetSwapper.SetHeadgearForPlayer(__instance.Player);
            SkaterHelmetSwapper.SetHelmetForPlayer(__instance.Player);
        }
    }

    // This patch makes the stick change when a player spawns
    [HarmonyPatch(typeof(Stick), nameof(Stick.UpdateStick))]
    public static class StickUpdateStickPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Stick __instance)
        {
            Plugin.LogDebug($"Stick.UpdateStick");
            SetStickReskinForPlayer(__instance.Player);
        }
    }

    public static void Setup()
    {
        global::UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        FullArenaSwapper.Initialize();
        // // We register patches for Changing Room
        // var harmony = new Harmony("com.toaster.reskinloader");
        // harmony.PatchAll(typeof(ChangingRoomPatcher));
    }

    public static void Destroy()
    {
        global::UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    public static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Plugin.Log($"OnSceneLoaded: {scene.name}");
        if (scene.name.Equals("changing_room"))
        {
        }
        else
        {
            SetAll();
        }
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
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "changing_room")
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
            OnBlueJerseyChanged();
            OnRedJerseyChanged();
            OnBlueLegPadsChanged();
            OnRedLegPadsChanged();   
            OnBlueHelmetsChanged();
            OnRedHelmetsChanged();        
            FullArenaSwapper.ApplyFromProfile();
            SkyboxSwapper.UpdateSkybox();
        }
    }
}