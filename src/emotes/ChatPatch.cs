using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.emotes;

// ParseChatContent runs after the game strips player-typed rich text and filters characters,
// so the sprite tags we add here are the only tags in player messages.
[HarmonyPatch(typeof(UIChat), "ParseChatContent")]
public static class ChatParsePatch
{
    [HarmonyPostfix]
    public static void Postfix(ref string __result)
    {
        try
        {
            EmoteAtlas atlas = EmoteRuntime.Atlas;
            if (atlas == null)
            {
                return;
            }
            __result = ChatEmoteFormatter.Format(__result, atlas.Name, EmoteRuntime.IsUsable, EmoteRuntime.FormatOptions);
        }
        catch (Exception e)
        {
            Plugin.LogError($"[Emotes] Emote formatting failed: {e.Message}");
        }
    }
}

// Hooks the label AddChatMessage just created so hovering its emotes shows their names.
[HarmonyPatch(typeof(UIChat), nameof(UIChat.AddChatMessage))]
[HarmonyPriority(Priority.Last - 200)]
public static class ChatAddPatch
{
    static readonly AccessTools.FieldRef<UIChat, List<UIChatMessage>> uiChatMessages =
        AccessTools.FieldRefAccess<UIChat, List<UIChatMessage>>("uiChatMessages");

    [HarmonyPostfix]
    public static void Postfix(UIChat __instance)
    {
        try
        {
            EmoteAtlas atlas = EmoteRuntime.Atlas;
            List<UIChatMessage> messages = uiChatMessages(__instance);
            if (atlas == null || messages.Count == 0)
            {
                return;
            }
            string tag = "<sprite=\"" + atlas.Name + "\"";
            foreach (Label label in messages[messages.Count - 1].VisualElement.Query<Label>().ToList())
            {
                if (label.text != null && label.text.Contains(tag))
                {
                    EmoteHover.Track(label);
                }
            }
        }
        catch (Exception e)
        {
            Plugin.LogError($"[Emotes] Emote hover hook failed: {e.Message}");
        }
    }
}
