using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.swappers;

/// <summary>
/// Overrides team colors across all game UI elements via Harmony patches:
/// minimap player dots, tab scoreboard rows, position select circles,
/// team select buttons, goal announcements, score HUD, chat username colors,
/// and goal frame meshes. Also handles custom team names in announcements
/// and team select buttons with player counts.
/// </summary>
public static class TeamColorSwapper
{
    public static string ColorToHex(Color color)
    {
        return $"#{ColorUtility.ToHtmlStringRGB(color)}";
    }

    private static Color? GetOverrideColor(PlayerTeam team)
    {
        var profile = ReskinProfileManager.currentProfile;
        if (profile == null || !profile.teamColorsEnabled) return null;

        return team switch
        {
            PlayerTeam.Blue => profile.blueTeamColor,
            PlayerTeam.Red => profile.redTeamColor,
            _ => null
        };
    }

    /// <summary>
    /// Re-applies all team color overrides to currently visible UI elements.
    /// Call this when the user changes their team color settings.
    /// </summary>
    public static void RefreshAll()
    {
        try
        {
            var uiManager = MonoBehaviourSingleton<UIManager>.Instance;
            if (uiManager == null) return;

            // Refresh minimap
            var minimap = uiManager.Minimap;
            if (minimap != null)
            {
                var mapField = typeof(UIMinimap).GetField("playerBodyVisualElementMap",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var map = (Dictionary<PlayerBody, VisualElement>)mapField?.GetValue(minimap);
                if (map != null)
                {
                    var profile = ReskinProfileManager.currentProfile;
                    foreach (var kvp in map)
                    {
                        if (!kvp.Key || !kvp.Key.Player) continue;

                        // Local player icon color is handled by MinimapSwapper
                        if (profile != null && profile.localPlayerMinimapIconEnabled && kvp.Key.Player.IsLocalPlayer)
                            continue;

                        Color? c = GetOverrideColor(kvp.Key.Player.Team);
                        VisualElement bodyEl = kvp.Value.Q("Player")?.Q("Body");
                        if (bodyEl != null)
                        {
                            if (c != null)
                                bodyEl.style.unityBackgroundImageTintColor = c.Value;
                            else
                                bodyEl.style.unityBackgroundImageTintColor = StyleKeyword.Null;
                        }
                    }
                }
            }

            // Refresh tab scoreboard
            var scoreboard = uiManager.GetComponent<UIScoreboard>();
            if (scoreboard != null)
            {
                var mapField = typeof(UIScoreboard).GetField("playerVisualElementMap",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var map = (Dictionary<Player, VisualElement>)mapField?.GetValue(scoreboard);
                if (map != null)
                {
                    foreach (var kvp in map)
                    {
                        if (!kvp.Key) continue;
                        Color? c = GetOverrideColor(kvp.Key.Team);
                        VisualElement playerEl = kvp.Value.Q("Player");
                        if (playerEl != null)
                        {
                            if (c != null)
                                playerEl.style.backgroundColor = c.Value;
                            else
                                playerEl.style.backgroundColor = StyleKeyword.Null;
                        }
                    }
                }
            }
            // Refresh score HUD text colors
            var gameState = uiManager.GameState;
            if (gameState != null)
            {
                var blueField = typeof(UIGameState).GetField("blueScoreLabel",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var redField = typeof(UIGameState).GetField("redScoreLabel",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Color? blueColor = GetOverrideColor(PlayerTeam.Blue);
                Color? redColor = GetOverrideColor(PlayerTeam.Red);

                var blueLabel = (Label)blueField?.GetValue(gameState);
                if (blueLabel != null)
                    blueLabel.style.color = blueColor.HasValue ? new StyleColor(blueColor.Value) : StyleKeyword.Null;

                var redLabel = (Label)redField?.GetValue(gameState);
                if (redLabel != null)
                    redLabel.style.color = redColor.HasValue ? new StyleColor(redColor.Value) : StyleKeyword.Null;
            }
        }
        catch (Exception e)
        {
            Plugin.LogDebug($"TeamColorSwapper.RefreshAll error: {e.Message}");
        }
    }

    // ── Minimap ──────────────────────────────────────────────────────────
    // Structure: Player > Body > Local, NumberLabel
    // The team color is on the Body child, not on Player itself.

    [HarmonyPatch(typeof(UIMinimap), nameof(UIMinimap.StylePlayer))]
    public static class MinimapStylePlayerPatch
    {
        static readonly FieldInfo _mapField = typeof(UIMinimap)
            .GetField("playerBodyVisualElementMap", BindingFlags.Instance | BindingFlags.NonPublic);

        [HarmonyPostfix]
        public static void Postfix(UIMinimap __instance, PlayerBody playerBody)
        {
            try
            {
                if (!playerBody || !playerBody.Player) return;

                var profile = ReskinProfileManager.currentProfile;

                // Local player icon color takes priority over team color
                if (profile != null && profile.localPlayerMinimapIconEnabled && playerBody.Player.IsLocalPlayer)
                    return;

                Color? c = GetOverrideColor(playerBody.Player.Team);
                if (c == null) return;

                var map = (Dictionary<PlayerBody, VisualElement>)_mapField?.GetValue(__instance);
                if (map == null || !map.ContainsKey(playerBody)) return;

                VisualElement playerEl = map[playerBody].Q("Player");
                if (playerEl == null) return;

                // Tint the Body child which is the actual arrow icon
                VisualElement bodyEl = playerEl.Q("Body");
                var color = c.Value;
                if (bodyEl != null)
                {
                    bodyEl.schedule.Execute(() =>
                    {
                        bodyEl.style.unityBackgroundImageTintColor = color;
                    });
                }
            }
            catch (Exception e)
            {
                Plugin.LogDebug($"TeamColorSwapper.Minimap error: {e.Message}");
            }
        }
    }

    // ── Tab Scoreboard ───────────────────────────────────────────────────
    // Structure: Player has bg=(0.25,0.25,0.25) - override with team color

    [HarmonyPatch(typeof(UIScoreboard), nameof(UIScoreboard.StylePlayer))]
    public static class ScoreboardStylePlayerPatch
    {
        static readonly FieldInfo _mapField = typeof(UIScoreboard)
            .GetField("playerVisualElementMap", BindingFlags.Instance | BindingFlags.NonPublic);

        [HarmonyPostfix]
        public static void Postfix(UIScoreboard __instance, Player player)
        {
            try
            {
                if (!player) return;
                Color? c = GetOverrideColor(player.Team);
                if (c == null) return;

                var map = (Dictionary<Player, VisualElement>)_mapField?.GetValue(__instance);
                if (map == null || !map.ContainsKey(player)) return;

                VisualElement playerEl = map[player].Q("Player");
                if (playerEl == null) return;

                var color = c.Value;
                playerEl.schedule.Execute(() =>
                {
                    playerEl.style.backgroundColor = color;
                });
            }
            catch (Exception e)
            {
                Plugin.LogDebug($"TeamColorSwapper.Scoreboard error: {e.Message}");
            }
        }
    }

    // ── Position Select ──────────────────────────────────────────────────
    // The Position element uses a tinted background image (circle sprite)

    [HarmonyPatch(typeof(UIPositionSelect), nameof(UIPositionSelect.StylePosition))]
    public static class PositionSelectStylePatch
    {
        static readonly FieldInfo _mapField = typeof(UIPositionSelect)
            .GetField("playerPositionVisualElementMap", BindingFlags.Instance | BindingFlags.NonPublic);

        [HarmonyPostfix]
        public static void Postfix(UIPositionSelect __instance, PlayerPosition playerPosition)
        {
            try
            {
                Color? c = GetOverrideColor(playerPosition.Team);
                if (c == null) return;

                var map = (Dictionary<PlayerPosition, VisualElement>)_mapField?.GetValue(__instance);
                if (map == null || !map.ContainsKey(playerPosition)) return;

                VisualElement posEl = map[playerPosition].Q("Position");
                if (posEl == null) return;

                var color = c.Value;
                bool isClaimed = playerPosition.IsClaimed;
                posEl.schedule.Execute(() =>
                {
                    if (isClaimed)
                        posEl.style.unityBackgroundImageTintColor = new Color(color.r, color.g, color.b, 0.4f);
                    else
                        posEl.style.unityBackgroundImageTintColor = color;
                });
            }
            catch (Exception e)
            {
                Plugin.LogDebug($"TeamColorSwapper.PositionSelect error: {e.Message}");
            }
        }
    }

    // ── Team Select ──────────────────────────────────────────────────────
    // UITeamSelect doesn't override Show(), so we patch UIView.Show and
    // check if the instance is a UITeamSelect.

    [HarmonyPatch(typeof(UIView), nameof(UIView.Show))]
    public static class UIViewShowPatch
    {
        static readonly FieldInfo _blueButtonField = typeof(UITeamSelect)
            .GetField("blueButton", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo _redButtonField = typeof(UITeamSelect)
            .GetField("redButton", BindingFlags.Instance | BindingFlags.NonPublic);

        [HarmonyPostfix]
        public static void Postfix(UIView __instance)
        {
            try
            {
                if (__instance is not UITeamSelect teamSelect) return;

                var profile = ReskinProfileManager.currentProfile;
                if (profile == null) return;

                var blueBtn = (Button)_blueButtonField?.GetValue(teamSelect);
                var redBtn = (Button)_redButtonField?.GetValue(teamSelect);

                int blueCount = PlayerManager.Instance != null
                    ? PlayerManager.Instance.GetPlayersByTeam(PlayerTeam.Blue).Count : 0;
                int redCount = PlayerManager.Instance != null
                    ? PlayerManager.Instance.GetPlayersByTeam(PlayerTeam.Red).Count : 0;

                string blueName = !string.IsNullOrEmpty(profile.blueTeamName)
                    ? profile.blueTeamName : "TEAM BLUE";
                string redName = !string.IsNullOrEmpty(profile.redTeamName)
                    ? profile.redTeamName : "TEAM RED";

                if (blueBtn != null)
                {
                    if (profile.teamColorsEnabled)
                    {
                        var bc = profile.blueTeamColor;
                        blueBtn.schedule.Execute(() => { blueBtn.style.backgroundColor = bc; });
                    }
                    blueBtn.text = $"{blueName} - {blueCount}";
                }
                if (redBtn != null)
                {
                    if (profile.teamColorsEnabled)
                    {
                        var rc = profile.redTeamColor;
                        redBtn.schedule.Execute(() => { redBtn.style.backgroundColor = rc; });
                    }
                    redBtn.text = $"{redName} - {redCount}";
                }
            }
            catch (Exception e)
            {
                Plugin.LogDebug($"TeamColorSwapper.TeamSelect error: {e.Message}");
            }
        }
    }

    // ── Goal Announcement ────────────────────────────────────────────────

    [HarmonyPatch(typeof(UIAnnouncements), nameof(UIAnnouncements.ShowScore))]
    public static class ShowScorePatch
    {
        static readonly FieldInfo _scoreField = typeof(UIAnnouncements)
            .GetField("score", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo _headerLabelField = typeof(UIAnnouncements)
            .GetField("headerLabel", BindingFlags.Instance | BindingFlags.NonPublic);

        [HarmonyPostfix]
        public static void Postfix(UIAnnouncements __instance, PlayerTeam team)
        {
            try
            {
                var profile = ReskinProfileManager.currentProfile;
                if (profile == null) return;

                // Custom team colors on the announcement header text
                if (profile.teamColorsEnabled)
                {
                    Color? c = GetOverrideColor(team);
                    if (c != null)
                    {
                        var headerLabel = (Label)_headerLabelField?.GetValue(__instance);
                        if (headerLabel != null)
                            headerLabel.style.color = c.Value;
                    }
                }

                // Custom team name in header
                string customName = null;
                if (team == PlayerTeam.Blue && !string.IsNullOrEmpty(profile.blueTeamName))
                    customName = profile.blueTeamName;
                else if (team == PlayerTeam.Red && !string.IsNullOrEmpty(profile.redTeamName))
                    customName = profile.redTeamName;

                if (customName != null)
                {
                    var headerLabel = (Label)_headerLabelField?.GetValue(__instance);
                    if (headerLabel != null)
                        headerLabel.text = $"{customName} SCORES!";
                }
            }
            catch (Exception e)
            {
                Plugin.LogDebug($"TeamColorSwapper.ShowScore error: {e.Message}");
            }
        }
    }

    // ── Score HUD (top-center score numbers) ─────────────────────────────

    [HarmonyPatch(typeof(UIGameState), nameof(UIGameState.SetScore))]
    public static class SetScorePatch
    {
        static readonly FieldInfo _blueScoreLabelField = typeof(UIGameState)
            .GetField("blueScoreLabel", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo _redScoreLabelField = typeof(UIGameState)
            .GetField("redScoreLabel", BindingFlags.Instance | BindingFlags.NonPublic);

        [HarmonyPostfix]
        public static void Postfix(UIGameState __instance, PlayerTeam team)
        {
            try
            {
                var profile = ReskinProfileManager.currentProfile;
                if (profile == null || !profile.teamColorsEnabled) return;

                if (team == PlayerTeam.Blue)
                {
                    var label = (Label)_blueScoreLabelField?.GetValue(__instance);
                    if (label != null) label.style.color = profile.blueTeamColor;
                }
                else if (team == PlayerTeam.Red)
                {
                    var label = (Label)_redScoreLabelField?.GetValue(__instance);
                    if (label != null) label.style.color = profile.redTeamColor;
                }
            }
            catch (Exception e)
            {
                Plugin.LogDebug($"TeamColorSwapper.SetScore error: {e.Message}");
            }
        }
    }

    // ── Chat ─────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(StringUtils), nameof(StringUtils.WrapInTeamColor))]
    public static class WrapInTeamColorPatch
    {
        [HarmonyPostfix]
        public static void Postfix(ref string __result, string username, PlayerTeam team)
        {
            try
            {
                Color? overrideColor = GetOverrideColor(team);
                if (overrideColor == null) return;

                __result = $"<color={ColorToHex(overrideColor.Value)}>{username}</color>";
            }
            catch (Exception e)
            {
                Plugin.LogDebug($"TeamColorSwapper.WrapInTeamColor error: {e.Message}");
            }
        }
    }

    // ── System chat messages (server-sent join/leave/etc) ────────────────
    // These arrive with color tags already baked in using the default hex
    // values. Replace them with the custom team colors.

    private const string DEFAULT_BLUE_HEX = "#3b82f6";
    private const string DEFAULT_RED_HEX = "#d13333";

    [HarmonyPatch(typeof(ChatManager), nameof(ChatManager.AddChatMessage))]
    public static class ChatManagerAddMessagePatch
    {
        [HarmonyPrefix]
        public static void Prefix(ChatMessage chatMessage)
        {
            try
            {
                if (!chatMessage.IsSystem) return;

                var profile = ReskinProfileManager.currentProfile;
                if (profile == null || !profile.teamColorsEnabled) return;

                string content = chatMessage.Content.ToString();
                bool changed = false;

                string blueHex = ColorToHex(profile.blueTeamColor);
                string redHex = ColorToHex(profile.redTeamColor);

                if (content.Contains(DEFAULT_BLUE_HEX, StringComparison.OrdinalIgnoreCase) && blueHex != DEFAULT_BLUE_HEX)
                {
                    content = content.Replace(DEFAULT_BLUE_HEX, blueHex, StringComparison.OrdinalIgnoreCase);
                    changed = true;
                }
                if (content.Contains(DEFAULT_RED_HEX, StringComparison.OrdinalIgnoreCase) && redHex != DEFAULT_RED_HEX)
                {
                    content = content.Replace(DEFAULT_RED_HEX, redHex, StringComparison.OrdinalIgnoreCase);
                    changed = true;
                }

                if (changed)
                {
                    chatMessage.Content = new Unity.Collections.FixedString512Bytes(content);
                }
            }
            catch (Exception e)
            {
                Plugin.LogDebug($"TeamColorSwapper.ChatMessage error: {e.Message}");
            }
        }
    }

    // ── Emoji filter bypass ──────────────────────────────────────────

    [HarmonyPatch(typeof(UIChat), "ParseChatContent")]
    public static class UIChatParseChatContentPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(ref string __result, string content, bool isSystem, Units units, bool filterProfanity)
        {
            if (!ReskinProfileManager.currentProfile.chatRenderAllEmojis) return true;
            if (isSystem) return true;

            // Non-system: filter rich text and profanity, but skip
            // FilterStringSpecialCharacters entirely so all Unicode (emojis) passes through.
            content = StringUtils.FilterStringRichText(content);
            if (filterProfanity)
            {
                content = StringUtils.FilterStringProfanity(content, true);
            }
            __result = content;
            return false;
        }
    }
}
