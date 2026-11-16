using System;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace ToasterReskinLoader.emotes;

// Drag-selecting chat text and pressing Ctrl+C copies the laid-out characters, where each
// emote is a private-use character (U+E000 + its sprite index) that other apps draw as a box.
// After Unity's selection copy writes the clipboard, swap those back to the :name: codes, so
// the copied text can be pasted into chat and sent as the same emotes.
[HarmonyPatch]
public static class SelectionCopyPatch
{
    static MethodBase TargetMethod()
    {
        return AccessTools.Method(AccessTools.TypeByName("UnityEngine.TextSelectingUtilities"), "Copy");
    }

    [HarmonyPostfix]
    public static void Postfix()
    {
        try
        {
            EmoteAtlas atlas = EmoteRuntime.Atlas;
            if (atlas == null || atlas.Count == 0)
            {
                return;
            }
            string copied = GUIUtility.systemCopyBuffer;
            string fixedText = ReplaceSpriteChars(copied, atlas);
            if (!ReferenceEquals(fixedText, copied))
            {
                GUIUtility.systemCopyBuffer = fixedText;
            }
        }
        catch (Exception e)
        {
            Plugin.LogError($"[Emotes] Fixing copied emotes failed: {e.Message}");
        }
    }

    // Returns the same instance when nothing needed replacing.
    static string ReplaceSpriteChars(string text, EmoteAtlas atlas)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }
        StringBuilder sb = null;
        for (int i = 0; i < text.Length; i++)
        {
            EmoteSlot slot = atlas.GetBySpriteChar(text[i]);
            if (slot == null)
            {
                sb?.Append(text[i]);
                continue;
            }
            if (sb == null)
            {
                sb = new StringBuilder(text.Length + 16);
                sb.Append(text, 0, i);
            }
            sb.Append(':').Append(slot.Name).Append(':');
        }
        return sb == null ? text : sb.ToString();
    }
}
