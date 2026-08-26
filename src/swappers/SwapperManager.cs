using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using ToasterReskinLoader.api;
using ToasterReskinLoader.swappers;

using ToasterReskinLoader.display;

using ToasterReskinLoader.hud;

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
            // Must not throw. On a join this postfix runs via
            // PlayerBody.OnNetworkPostSpawn -> HandlePlayerReference ->
            // ApplyCustomizations, i.e. inside Netcode's client-synchronization
            // spawn loop, which does not guard against exceptions. An escape there
            // stops the client sending SynchronizeComplete, so the server times it
            // out and the player sees "server unreachable" — a cosmetic swapper
            // failure presenting as a network failure. (Patches reached through the
            // game's EventManager are already safe: TriggerEvent wraps every
            // listener in try/catch. This path is not one of those.)
            try
            {
                Player player = __instance.Player;
                JerseySwapper.SetJerseyForPlayer(player);
                GoalieEquipmentSwapper.SetLegPadsForPlayer(player);
                GoalieHelmetSwapper.SetHeadgearForPlayer(player);
                SkaterHelmetSwapper.SetHelmetForPlayer(player);
                // Hat + body type + skin/hair color are handled by AppearanceAPI based on server data
                AppearanceAPI.OnPlayerSpawned(player);
                // Pick up any newly spawned renderers on this player for gloss removal
                GlossSwapper.RequestScan();
            }
            catch (Exception e)
            {
                Plugin.LogError($"PlayerBody.ApplyCustomizations postfix failed: {e}");
            }
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

    // Track player input for XP heartbeats (time is tracked by AppearanceAPI coroutine).
    // input_count feeds an "active vs idle" signal on a 5-minute window, so a 1-in-N
    // stride is plenty of resolution — no need to sample every frame for every
    // PlayerInput in the scene.
    [HarmonyPatch(typeof(PlayerInput), "Update")]
    public static class PlayerInputUpdatePatch
    {
        private const int SampleStride = 10;
        private static int _frameCounter;

        [HarmonyPostfix]
        public static void Postfix(PlayerInput __instance)
        {
            if (__instance.Player == null || !__instance.Player.IsLocalPlayer) return;
            if ((++_frameCounter % SampleStride) != 0) return;

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
            // Guarded: Stick.ApplyCustomizations is reached from HandlePlayerReference
            // during Netcode spawn, which is not exception-guarded. See the note on
            // PlayerBodyApplyCustomizations above.
            try
            {
                Plugin.LogDebug($"Stick.ApplyCustomizations");

                Player player = __instance.Player;
                // SetStickReskinForPlayer null-checks this itself, so it can be null
                // here — everything after it must not assume otherwise.
                SetStickReskinForPlayer(player);
                if (player == null) return;

                // Apply stick tape for local player and their replay counterpart
                bool isReplayLocal = player.IsReplay.Value &&
                    PlayerManager.Instance.GetLocalPlayer()?.OwnerClientId == player.OwnerClientId - 1337UL;
                if (player.IsLocalPlayer || isReplayLocal)
                    StickTapeSwapper.SetStickTapeForPlayer(player.Stick);

                // Attach stick-based apparel (e.g. Deltapoint) now that the stick exists
                AppearanceAPI.OnStickReady(player);
                // Pick up the new stick renderer for gloss removal
                GlossSwapper.RequestScan();
            }
            catch (Exception e)
            {
                Plugin.LogError($"Stick.ApplyCustomizations postfix failed: {e}");
            }
        }
    }

    public static void Setup()
    {
        global::UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        FullArenaSwapper.Initialize();
        HatSwapper.Initialize();
        GenderSwapper.Initialize();
        TeamIndicatorSwapper.Refresh();
    }

    public static void Destroy()
    {
        global::UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        HatSwapper.Cleanup();
        GenderSwapper.Cleanup();
        TeamIndicatorSwapper.Cleanup();
    }

    // Unity's SceneManager.sceneLoaded is a plain multicast delegate: an exception
    // from one subscriber stops the rest of the invocation list, so an escape here
    // would silently kill the scene hook of every mod registered after us. Keep the
    // whole body guarded.
    public static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        try { OnSceneLoadedCore(scene); }
        catch (Exception e) { Plugin.LogError($"OnSceneLoaded({scene.name}) failed: {e}"); }
    }

    private static void OnSceneLoadedCore(Scene scene)
    {
        Plugin.Log($"OnSceneLoaded: {scene.name}");
        ToasterReskinLoader.display.ArenaVisualsToggle.InvalidateCache();
        ToasterReskinLoader.display.PatchMinimapRotation.ResetTracking();
        ToasterReskinLoader.hud.PatchPlayerUsernameColors.ResetTracking();
        GlossSwapper.ResetScanScheduled();
        // Reflection-probe intensity, per-renderer probe usage, and the default
        // reflection all reset per scene — clear stale tracking and re-apply.
        GlossSwapper.ResetReflectionTracking();
        GlossSwapper.ApplyReflectionIntensity();
        GlossSwapper.ApplyReflectionKill();
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
            ui.sections.ChatSection.ApplyChatBackground(false);
            Plugin.Log($"Local player caches reset from switching to locker room");
        }
        else
        {

            // Entering a game scene — fetch appearances for all players on the server
            AppearanceAPI.FetchAllPlayersOnServer();
            ui.sections.ChatSection.ApplyChatBackground(core.Settings.Current?.chatBackground ?? false);
            MinimapSwapper.ApplyRefreshRate();
        }

        // Rebuild or destroy party lineup based on scene
        PartyLineup.OnSceneChanged(scene.name, MonoBehaviourSingleton<UIManager>.Instance);

        // The apply pass now runs across frames, so the locker-room appearance
        // re-apply is chained onto its completion rather than started alongside it.
        // It has to land after the pass (both touch goalie headgear), which the old
        // inline SetAll() guaranteed for free by finishing before the coroutine's
        // first resume.
        SetAllDeferred(scene.name.Equals("locker_room") ? ReapplyLocalAppearance : null);
    }

    // Re-apply the local player's saved appearance (skin tone, hair, body, hat)
    // when entering the locker room. Without this, the head color only applies
    // once the user opens the Appearance tab.
    private static void ReapplyLocalAppearance()
    {
        ui.sections.PlayerCustomizationSection.ReapplyLocalAppearanceToLockerRoom();
        // Re-apply helmet/mask/cage colors + textures to the locker room preview.
        // Without this, returning from a server leaves the goalie headgear at the
        // game's default (black) until the TRL menu is opened and goalie is toggled.
        ChangingRoomHelper.ApplyInitialCustomizations();
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

    // The full apply pass, as an ordered list so it can be run either in one shot
    // (SetAll) or spread across frames (SetAllDeferred). Order is load-bearing —
    // do not reorder without checking the dependent swappers.
    private static readonly (string Name, Action Run)[] ApplySteps =
    {
        ("IceTexture",        IceSwapper.SetIceTexture),
        // Returns bool; the old inline SetAll ignored it, so keep ignoring it.
        ("IceSmoothness",     () => IceSwapper.UpdateIceSmoothness()),
        ("Crowd",             ArenaSwapper.UpdateCrowdState),
        ("Hangar",            ArenaSwapper.UpdateHangarState),
        ("Scoreboard",        ArenaSwapper.UpdateScoreboardState),
        ("Glass",             ArenaSwapper.UpdateGlassState),
        ("Boards",            ArenaSwapper.UpdateBoards),
        ("GlassAndPillars",   ArenaSwapper.UpdateGlassAndPillars),
        ("Spectators",        ArenaSwapper.UpdateSpectators),
        ("NetTexture",        ArenaSwapper.SetNetTexture),
        ("GoalFrameColors",   ArenaSwapper.UpdateGoalFrameColors),
        ("BlueJersey",        OnBlueJerseyChanged),
        ("RedJersey",         OnRedJerseyChanged),
        // Sticks and pucks are normally textured at spawn time via Harmony patches,
        // so they must be re-applied here too. Otherwise a re-apply that doesn't
        // respawn them (e.g. the Reload button, which destroys the texture cache
        // first) leaves their materials pointing at destroyed textures — invisible
        // stick, black pucks. All four no-op safely when nothing is spawned.
        ("PersonalStick",     OnPersonalStickChanged),
        ("BlueTeamStick",     OnBlueTeamStickChanged),
        ("RedTeamStick",      OnRedTeamStickChanged),
        ("PuckTextures",      PuckSwapper.SetAllPucksTextures),
        ("BlueLegPads",       OnBlueLegPadsChanged),
        ("RedLegPads",        OnRedLegPadsChanged),
        ("BlueGoalieHelmets", OnBlueHelmetsChanged),
        ("RedGoalieHelmets",  OnRedHelmetsChanged),
        ("BlueSkaterHelmets", SkaterHelmetSwapper.OnBlueHelmetsChanged),
        ("RedSkaterHelmets",  SkaterHelmetSwapper.OnRedHelmetsChanged),
        ("FullArena",         FullArenaSwapper.ApplyFromProfile),
        ("Skybox",            SkyboxSwapper.UpdateSkybox),
        ("TeamIndicator",     TeamIndicatorSwapper.Refresh),
        ("PuckFX",            PuckFXSwapper.ApplyAll),
        ("Minimap",           MinimapSwapper.RefreshAll),
        ("GlossScan",         GlossSwapper.RequestScan),
    };

    private static void RunStep(in (string Name, Action Run) step)
    {
        try { step.Run(); }
        catch (Exception e) { Plugin.LogError($"[Apply] step '{step.Name}' failed: {e}"); }
    }

    /// <summary>
    /// Runs the whole apply pass inline. Use from user-paced callers (menu changes,
    /// the Reload button, the StartupLoader completion) where a single hitch is
    /// acceptable and the result must be visible immediately. On a scene change use
    /// <see cref="SetAllDeferred"/> instead — see the note there.
    /// </summary>
    public static void SetAll()
    {
        foreach (var step in ApplySteps) RunStep(step);
    }

    // ── Frame-budgeted apply ────────────────────────────────────────────
    // Running the full pass inline from the scene-loaded hook blocks the main
    // thread for as long as it takes. That is not just a hitch: the client loads
    // the arena scene *during* Netcode's client synchronization, so a long enough
    // block stops the transport being pumped, the connection times out, and the
    // player is dropped mid-join. Launch already had this problem and was fixed by
    // frame-chunking (see core/StartupLoader) — this is the same treatment for
    // scene changes.
    //
    // Same budget as StartupLoader. A single step can overrun it (we can't
    // subdivide one swapper), but we always yield afterwards so no frame stacks
    // two heavy steps.
    private const double ApplyFrameBudgetMs = 6.0;

    // Bumped on each new deferred apply so an in-flight one from the previous
    // scene abandons itself instead of writing into the new scene half-way
    // through the new pass.
    private static int _applyGeneration;

    /// <summary>
    /// Runs the apply pass across frames, then invokes <paramref name="onComplete"/>.
    /// Falls back to a synchronous <see cref="SetAll"/> if there's no coroutine host
    /// yet (i.e. before TickDriver.Bootstrap).
    /// </summary>
    public static void SetAllDeferred(Action onComplete = null)
    {
        int generation = ++_applyGeneration;

        var runner = core.TickDriver.Runner;
        if (runner == null)
        {
            SetAll();
            try { onComplete?.Invoke(); }
            catch (Exception e) { Plugin.LogError($"[Apply] onComplete failed: {e}"); }
            return;
        }

        runner.StartCoroutine(SetAllRoutine(generation, onComplete));
    }

    private static System.Collections.IEnumerator SetAllRoutine(int generation, Action onComplete)
    {
        // Always give up the rest of the scene-load frame before doing anything.
        // That frame is the worst possible one to add work to, and it also lets the
        // scene finish initializing (LockerRoomPlayer / PlayerMesh) before the pass
        // scans it — a guarantee the old one-frame-delayed appearance re-apply
        // relied on.
        yield return null;

        var sw = System.Diagnostics.Stopwatch.StartNew();

        foreach (var step in ApplySteps)
        {
            // A newer apply superseded us — stop before touching anything else.
            if (generation != _applyGeneration) yield break;

            RunStep(step);

            if (sw.Elapsed.TotalMilliseconds >= ApplyFrameBudgetMs)
            {
                yield return null;
                sw.Restart();
            }
        }

        if (generation != _applyGeneration) yield break;

        try { onComplete?.Invoke(); }
        catch (Exception e) { Plugin.LogError($"[Apply] onComplete failed: {e}"); }
    }

    // ── Matchmaking queue info overlay ──────────────────────────────

    public static void SetupMatchmakingListeners()
    {
        // No setup needed — the postfix reads stats directly from BackendManager
    }

    [HarmonyPatch(typeof(UIMatchmaking), nameof(UIMatchmaking.SetMatchingPhaseText))]
    public static class UIMatchmakingPhasePatch
    {
        private static readonly System.Reflection.FieldInfo MatchingPhaseLabelField =
            typeof(UIMatchmaking).GetField("matchingPhaseLabel",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        // B1231 renamed PoolStatistics.groupPlayers to groupPlayerCount. Read it
        // by reflection rather than binding the property directly, because a
        // direct reference to a member the running build no longer has makes the
        // runtime fail to JIT this whole postfix: the MissingMethodException is
        // raised on first call, before any statement runs, so the try/catch below
        // cannot contain it. Harmony's wrapper then propagates it out into
        // UIMatchmakingController.UpdateMatching — which calls
        // SetMatchingPhaseText *ahead of* the calls that show and hide the
        // panel's buttons in every one of its branches. A stale name here
        // therefore aborts vanilla's repaint half-way and strands whatever the
        // previous paint left on screen, most visibly B1231's START MATCHMAKING
        // button, which then sits over the game for the rest of the session.
        private static readonly System.Reflection.PropertyInfo GroupPlayerCountProp =
            ResolveGroupPlayerCount();

        private static System.Reflection.PropertyInfo ResolveGroupPlayerCount()
        {
            var t = AccessTools.TypeByName("PoolStatistics");
            // B1231+ name first, then the pre-B1231 one.
            var p = t?.GetProperty("groupPlayerCount") ?? t?.GetProperty("groupPlayers");
            if (p == null)
                Plugin.LogWarning("[QoL] matchmaking queue count: no player-count property on PoolStatistics; queue size will be omitted");
            return p;
        }

        [HarmonyPostfix]
        public static void Postfix(UIMatchmaking __instance, string text)
        {
            if (text != "LOOKING FOR A MATCH...") return;
            if (GroupPlayerCountProp == null) return;

            try
            {
                var stats = BackendManager.PlayerState.PlayerStatistics;
                if (stats?.matchmakingManager?.pools == null) return;

                int total = 0;
                foreach (var pool in stats.matchmakingManager.pools)
                {
                    if (pool == null) continue;
                    total += Convert.ToInt32(GroupPlayerCountProp.GetValue(pool));
                }

                if (total <= 0) return;

                var label = MatchingPhaseLabelField?.GetValue(__instance) as Label;
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