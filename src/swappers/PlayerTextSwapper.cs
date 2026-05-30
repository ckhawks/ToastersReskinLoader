using System;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace ToasterReskinLoader.swappers;

public static class PlayerTextSwapper
{
    static readonly FieldInfo _usernameTextField = typeof(PlayerTorso)
        .GetField("usernameText",
            BindingFlags.Instance | BindingFlags.NonPublic);

    static readonly FieldInfo _numberTextField = typeof(PlayerTorso)
        .GetField("numberText",
            BindingFlags.Instance | BindingFlags.NonPublic);

    public static void SetPlayerTextColors(Player player)
    {
        try
        {
            if (player?.PlayerBody?.PlayerMesh?.PlayerTorso == null)
            {
                Plugin.LogError("Player or PlayerTorso is null");
                return;
            }

            PlayerTorso playerTorso = player.PlayerBody.PlayerMesh.PlayerTorso;

            Color textColor;
            Color outlineColor;
            float outlineWidth;
            if (player.Team == PlayerTeam.Blue)
            {
                if (player.Role == PlayerRole.Goalie)
                {
                    textColor = ReskinProfileManager.currentProfile.blueGoalieLetteringColor;
                    outlineColor = ReskinProfileManager.currentProfile.blueGoalieNumberOutlineColor;
                    outlineWidth = ReskinProfileManager.currentProfile.blueGoalieNumberOutlineWidth;
                }
                else
                {
                    textColor = ReskinProfileManager.currentProfile.blueSkaterLetteringColor;
                    outlineColor = ReskinProfileManager.currentProfile.blueSkaterNumberOutlineColor;
                    outlineWidth = ReskinProfileManager.currentProfile.blueSkaterNumberOutlineWidth;
                }
            }
            else if (player.Team == PlayerTeam.Red)
            {
                if (player.Role == PlayerRole.Goalie)
                {
                    textColor = ReskinProfileManager.currentProfile.redGoalieLetteringColor;
                    outlineColor = ReskinProfileManager.currentProfile.redGoalieNumberOutlineColor;
                    outlineWidth = ReskinProfileManager.currentProfile.redGoalieNumberOutlineWidth;
                }
                else
                {
                    textColor = ReskinProfileManager.currentProfile.redSkaterLetteringColor;
                    outlineColor = ReskinProfileManager.currentProfile.redSkaterNumberOutlineColor;
                    outlineWidth = ReskinProfileManager.currentProfile.redSkaterNumberOutlineWidth;
                }
            }
            else
            {
                textColor = Color.white;
                outlineColor = Color.black;
                outlineWidth = 0f;
            }

            var usernameText = (TMP_Text)_usernameTextField.GetValue(playerTorso);
            var numberText = (TMP_Text)_numberTextField.GetValue(playerTorso);

            if (usernameText != null)
            {
                usernameText.color = textColor;
            }

            if (numberText != null)
            {
                numberText.color = textColor;
                ApplyNumberOutline(numberText, outlineColor, outlineWidth);
            }

            Plugin.LogDebug($"Set {player.Username.Value} lettering color to {textColor}, outline {outlineColor} width {outlineWidth}");
        }
        catch (Exception ex)
        {
            Plugin.LogError($"Error setting player text colors: {ex.Message}");
        }
    }

    // Applies an outline to a TMP_Text by getting a unique material instance via
    // fontMaterial (TMP creates a per-component instance on access) so the change
    // doesn't bleed onto other text using the same shared material.
    public static void ApplyNumberOutline(TMP_Text text, Color color, float width)
    {
        try
        {
            var mat = text.fontMaterial;
            if (mat == null) return;
            if (mat.HasProperty(ShaderUtilities.ID_OutlineColor))
                mat.SetColor(ShaderUtilities.ID_OutlineColor, color);
            if (mat.HasProperty(ShaderUtilities.ID_OutlineWidth))
                mat.SetFloat(ShaderUtilities.ID_OutlineWidth, Mathf.Clamp01(width));
        }
        catch (Exception ex)
        {
            Plugin.LogError($"Error applying number outline: {ex.Message}");
        }
    }

    public static void UpdateTeamLettering(PlayerTeam team, PlayerRole? role = null)
    {
        var players = UnityEngine.Object.FindObjectsByType<Player>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            if (player.Team == team)
            {
                if (role == null || player.Role == role)
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

    public static void OnBlueSkaterNumberOutlineChanged() => UpdateTeamLettering(PlayerTeam.Blue, PlayerRole.Attacker);
    public static void OnRedSkaterNumberOutlineChanged() => UpdateTeamLettering(PlayerTeam.Red, PlayerRole.Attacker);
    public static void OnBlueGoalieNumberOutlineChanged() => UpdateTeamLettering(PlayerTeam.Blue, PlayerRole.Goalie);
    public static void OnRedGoalieNumberOutlineChanged() => UpdateTeamLettering(PlayerTeam.Red, PlayerRole.Goalie);
}
