using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.swappers;

public static class TeamIndicatorSwapper
{
    private static VisualElement _teamColorBar;

    private static readonly Color DefaultBlue = new Color(23f / 255, 92f / 255, 230f / 255);
    private static readonly Color DefaultRed = new Color(229f / 255, 23f / 255, 23f / 255);
    private static readonly Color DefaultSpectator = new Color(128f / 255, 128f / 255, 128f / 255);

    public static void Setup()
    {
        try
        {
            var uiManager = MonoBehaviourSingleton<UIManager>.Instance;
            if (uiManager == null)
            {
                Plugin.LogWarning("TeamIndicator: UIManager not available yet.");
                return;
            }

            // Parent to the top-level RootVisualElement to avoid any HUD padding
            VisualElement root = uiManager.RootVisualElement;
            if (root == null)
            {
                Plugin.LogWarning("TeamIndicator: RootVisualElement not found.");
                return;
            }

            CreateBar(root);
            Plugin.LogDebug("TeamIndicator: Bar created.");
        }
        catch (Exception e)
        {
            Plugin.LogError($"TeamIndicator: Setup failed: {e.Message}");
        }
    }

    private static void CreateBar(VisualElement root)
    {
        // Clean up if already exists
        _teamColorBar?.RemoveFromHierarchy();

        _teamColorBar = new VisualElement();
        _teamColorBar.name = "TeamColorBar";
        _teamColorBar.style.position = Position.Absolute;
        _teamColorBar.style.bottom = 0;
        _teamColorBar.style.left = 0;
        _teamColorBar.style.right = 0;
        _teamColorBar.style.height = 5;
        _teamColorBar.style.minWidth = Length.Percent(100f);
        _teamColorBar.style.maxWidth = Length.Percent(100f);
        _teamColorBar.style.backgroundColor = DefaultSpectator;
        _teamColorBar.style.display = DisplayStyle.None;

        root.Add(_teamColorBar);
    }

    public static void UpdateVisibility()
    {
        if (_teamColorBar == null) return;

        var profile = ReskinProfileManager.currentProfile;
        if (profile == null || !profile.teamIndicatorEnabled)
        {
            _teamColorBar.style.display = DisplayStyle.None;
            return;
        }

        // Re-apply current team color
        var localPlayer = PlayerManager.Instance?.GetLocalPlayer();
        if (localPlayer != null)
        {
            SetTeamColor(localPlayer.GameState.Value.Team);
        }
    }

    public static void SetTeamColor(PlayerTeam team)
    {
        if (_teamColorBar == null) return;

        var profile = ReskinProfileManager.currentProfile;
        if (profile == null || !profile.teamIndicatorEnabled)
        {
            _teamColorBar.style.display = DisplayStyle.None;
            return;
        }

        bool useCustomColors = profile.teamColorsEnabled;

        switch (team)
        {
            case PlayerTeam.Blue:
                _teamColorBar.style.backgroundColor = new StyleColor(
                    useCustomColors ? profile.blueTeamColor : DefaultBlue);
                _teamColorBar.style.display = DisplayStyle.Flex;
                break;
            case PlayerTeam.Red:
                _teamColorBar.style.backgroundColor = new StyleColor(
                    useCustomColors ? profile.redTeamColor : DefaultRed);
                _teamColorBar.style.display = DisplayStyle.Flex;
                break;
            default:
                _teamColorBar.style.backgroundColor = new StyleColor(DefaultSpectator);
                _teamColorBar.style.display = DisplayStyle.None;
                break;
        }
    }

    public static void Cleanup()
    {
        _teamColorBar?.RemoveFromHierarchy();
        _teamColorBar = null;
    }

    /// <summary>
    /// Patch Player.OnPlayerGameStateChanged to update the bar when the local player changes team.
    /// </summary>
    [HarmonyPatch(typeof(Player), "OnPlayerGameStateChanged")]
    public static class OnPlayerGameStateChangedPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Player __instance, PlayerGameState oldGameState, PlayerGameState newGameState)
        {
            try
            {
                if (oldGameState.Team == newGameState.Team) return;

                var localPlayer = PlayerManager.Instance?.GetLocalPlayer();
                if (localPlayer == null || __instance.OwnerClientId != localPlayer.OwnerClientId) return;

                Plugin.LogDebug($"TeamIndicator: Local player team changed to {newGameState.Team}");
                SetTeamColor(newGameState.Team);
            }
            catch (Exception e)
            {
                Plugin.LogDebug($"TeamIndicator: OnPlayerGameStateChanged error: {e.Message}");
            }
        }
    }
}
