using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using ToasterReskinLoader.api;
using ToasterReskinLoader.swappers;

namespace ToasterReskinLoader.swappers;

public static class SwapperManager
{
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

        // Replay players have their OwnerClientId offset by 1337 from the original player
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
            // Hat + body type + skin/hair color are handled by AppearanceAPI based on server data
            AppearanceAPI.OnPlayerSpawned(__instance.Player);
        }
    }

    // Clear cached appearance when a player leaves so we re-fetch if they rejoin
    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.RemovePlayer))]
    public static class PlayerManagerRemovePlayerPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Player player)
        {
            if (player == null || player.IsLocalPlayer) return;
            string steamId = player.SteamId.Value.ToString();
            AppearanceAPI.OnPlayerLeft(steamId);
        }
    }

    // Track player input for XP heartbeats (time is tracked by AppearanceAPI coroutine)
    [HarmonyPatch(typeof(PlayerInput), "Update")]
    public static class PlayerInputUpdatePatch
    {
        [HarmonyPostfix]
        public static void Postfix(PlayerInput __instance)
        {
            if (__instance.Player == null || !__instance.Player.IsLocalPlayer) return;

            if (__instance.MoveInput.ClientValue.sqrMagnitude > 0.01f ||
                __instance.StickRaycastOriginAngleInput.ClientValue.sqrMagnitude > 0.01f)
            {
                AppearanceAPI.TrackInput();
            }
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
            // Apply stick tape for local player and their replay counterpart
            bool isReplayLocal = __instance.Player.IsReplay.Value &&
                PlayerManager.Instance.GetLocalPlayer()?.OwnerClientId == __instance.Player.OwnerClientId - 1337UL;
            if (__instance.Player.IsLocalPlayer || isReplayLocal)
                StickTapeSwapper.SetStickTapeForPlayer(__instance.Player.Stick);

            // Attach stick-based apparel (e.g. Deltapoint) now that the stick exists
            AppearanceAPI.OnStickReady(__instance.Player);
        }
    }

    public static void Setup()
    {
        global::UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        FullArenaSwapper.Initialize();
        HatSwapper.Initialize();
        GenderSwapper.Initialize();
        TeamIndicatorSwapper.Setup();
    }

    public static void Destroy()
    {
        global::UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        HatSwapper.Cleanup();
        GenderSwapper.Cleanup();
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
            HatSwapper.ClearHats();
            GenderSwapper.ClearCache();
            AppearanceAPI.ClearCache();
            ui.sections.UISection.ApplyChatBackground(false);
            Plugin.Log($"Local player caches reset from switching to locker room");
        }
        else
        {
            // Entering a game scene — fetch appearances for all players on the server
            AppearanceAPI.FetchAllPlayersOnServer();
            ui.sections.UISection.ApplyChatBackground(ReskinProfileManager.currentProfile.chatBackground);
            MinimapSwapper.ApplyRefreshRate();
        }

        SetAll();
    }

    public static void OnBlueJerseyChanged() => OnJerseyChanged(PlayerTeam.Blue);
    public static void OnRedJerseyChanged() => OnJerseyChanged(PlayerTeam.Red);

    private static void OnJerseyChanged(PlayerTeam team)
    {
        List<Player> players = PlayerManager.Instance.GetPlayersByTeam(team);
        foreach (Player player in players)
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
        MinimapSwapper.RefreshAll();
    }

    // ── Matchmaking queue info overlay ──────────────────────────────

    public static void SetupMatchmakingListeners()
    {
        // No setup needed — the postfix reads stats directly from BackendManager
    }

    [HarmonyPatch(typeof(UIMatchmaking), nameof(UIMatchmaking.SetMatchingPhaseText))]
    public static class UIMatchmakingPhasePatch
    {
        [HarmonyPostfix]
        public static void Postfix(UIMatchmaking __instance, string text)
        {
            if (text != "LOOKING FOR A MATCH...") return;

            try
            {
                var stats = BackendManager.PlayerState.PlayerStatistics;
                if (stats?.matchmakingManager?.pools == null) return;

                int total = 0;
                foreach (var pool in stats.matchmakingManager.pools)
                    total += pool.groupPlayers;

                if (total <= 0) return;

                var labelField = typeof(UIMatchmaking).GetField("matchingPhaseLabel",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var label = labelField?.GetValue(__instance) as Label;
                if (label != null)
                    label.text = $"LOOKING FOR A MATCH...  ({total} in queue)";
            }
            catch (System.Exception e)
            {
                Plugin.LogDebug($"MatchmakingPhasePatch error: {e.Message}");
            }
        }
    }
}