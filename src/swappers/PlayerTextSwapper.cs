using System;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace ToasterReskinLoader.swappers;

public static class PlayerTextSwapper
{
    static readonly FieldInfo _usernameTextField = typeof(PlayerMesh)
        .GetField("usernameText",
            BindingFlags.Instance | BindingFlags.NonPublic);

    static readonly FieldInfo _numberTextField = typeof(PlayerMesh)
        .GetField("numberText",
            BindingFlags.Instance | BindingFlags.NonPublic);

    public static void SetPlayerTextColors(Player player)
    {
        try
        {
            if (player?.PlayerBody?.PlayerMesh == null)
            {
                Plugin.LogError("Player or PlayerMesh is null");
                return;
            }

            PlayerMesh playerMesh = player.PlayerBody.PlayerMesh;

            // Determine which color to use based on team and role
            Color textColor;
            if (player.Team.Value == PlayerTeam.Blue)
            {
                textColor = player.Role.Value == PlayerRole.Goalie
                    ? ReskinProfileManager.currentProfile.blueGoalieLetteringColor
                    : ReskinProfileManager.currentProfile.blueSkaterLetteringColor;
            }
            else if (player.Team.Value == PlayerTeam.Red)
            {
                textColor = player.Role.Value == PlayerRole.Goalie
                    ? ReskinProfileManager.currentProfile.redGoalieLetteringColor
                    : ReskinProfileManager.currentProfile.redSkaterLetteringColor;
            }
            else
            {
                // Default to white for spectators or other teams
                textColor = Color.white;
            }

            // Get the TMP_Text components via reflection
            var usernameText = (TMP_Text)_usernameTextField.GetValue(playerMesh);
            var numberText = (TMP_Text)_numberTextField.GetValue(playerMesh);

            if (usernameText != null)
            {
                usernameText.color = textColor;
            }

            if (numberText != null)
            {
                numberText.color = textColor;
            }

            Plugin.LogDebug($"Set {player.Username.Value} lettering color to {textColor}");
        }
        catch (Exception ex)
        {
            Plugin.LogError($"Error setting player text colors: {ex.Message}");
        }
    }

    public static void UpdateTeamLettering(PlayerTeam team, PlayerRole? role = null)
    {
        var players = UnityEngine.Object.FindObjectsByType<Player>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            if (player.Team.Value == team)
            {
                if (role == null || player.Role.Value == role)
                {
                    SetPlayerTextColors(player);
                }
            }
        }
    }

    public static void OnBlueSkaterLetteringColorChanged() => UpdateTeamLettering(PlayerTeam.Blue, PlayerRole.Attacker);
    public static void OnRedSkaterLetteringColorChanged() => UpdateTeamLettering(PlayerTeam.Red, PlayerRole.Attacker);
    public static void OnBlueGoalieLetteringColorChanged() => UpdateTeamLettering(PlayerTeam.Blue, PlayerRole.Goalie);
    public static void OnRedGoalieLetteringColorChanged() => UpdateTeamLettering(PlayerTeam.Red, PlayerRole.Goalie);
}
