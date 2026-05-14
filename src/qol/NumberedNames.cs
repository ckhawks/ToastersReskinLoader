// Prefix the speaker's jersey number to their name in chat messages.
//
// Vanilla `UIChat.GetChatMessagePrefix` builds the chat line prefix as
//     "[TEAM] <color=#…>Username</color>: "
// — there's no number anywhere. We postfix it, look up the speaker's
// Player by SteamID via PlayerManager.GetPlayerBySteamId, and inject the
// number in front of the username so the line reads as
//     "[TEAM] <color=#…>#62 Amikiir</color>: "
// (using the same `Number.Value` source UIScoreboard uses).

using System;
using HarmonyLib;
using UnityEngine;

namespace ToasterReskinLoader.qol;

[HarmonyPatch(typeof(UIChat), "GetChatMessagePrefix")]
internal static class NumberedNames_Postfix
{
    private static void Postfix(ChatMessage chatMessage, ref string __result)
    {
        if (!(QoLRunner.Instance?.Config?.enableNumberedNames ?? false)) return;
        try
        {
            if (chatMessage.IsSystem) return;
            if (!chatMessage.SteamID.HasValue) return;
            string username = chatMessage.Username?.ToString();
            if (string.IsNullOrEmpty(username)) return;

            var pm = MonoBehaviourSingleton<PlayerManager>.Instance;
            if (pm == null) return;
            var player = pm.GetPlayerBySteamId(chatMessage.SteamID.Value);
            if (player == null) return;

            // Inject the number in front of the username text. The username
            // sits inside the team-color wrapper produced by
            // StringUtils.WrapInTeamColor, so prepending it inside the
            // existing prefix string keeps the team color applied to both
            // the number and the name. .Replace is safe here because the
            // username only appears once in the formed prefix.
            string numbered = $"#{player.Number.Value} {username}";
            __result = __result.Replace(username, numbered);
        }
        catch (Exception e) { Debug.LogWarning("[QoL] numbered chat names failed: " + e.Message); }
    }
}
