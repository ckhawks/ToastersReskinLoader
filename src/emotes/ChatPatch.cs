using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.emotes;

// Chat side of emotes. A message's :name: codes resolve for its sender (server set first,
// then the sender's personal set), so AddChatMessage records who sent the message being
// formatted. Each message keeps its raw text so it can be re-rendered when a sender's set
// arrives after the message did, and so copying a line gives back the typed :name: codes.
public static class EmoteChat
{
    const int MAX_RECORDS = 200;

    class Record
    {
        public Label Label;
        public string Sender;
        public string Raw;
        public string Formatted;
    }

    // Set by the AddChatMessage prefix for the duration of that call.
    static string currentSender;
    static string currentRaw;
    static string currentFormatted;

    static readonly LinkedList<Record> records = new LinkedList<Record>();

    public static void Setup()
    {
        EmoteFeed.SetsChanged += OnSetsChanged;
    }

    public static void Teardown()
    {
        EmoteFeed.SetsChanged -= OnSetsChanged;
        records.Clear();
    }

    static string Format(string sender, string raw)
    {
        EmoteFeed feed = EmoteFeed.Instance;
        EmoteAtlas atlas = EmoteRuntime.Atlas;
        if (feed == null || atlas == null)
        {
            return raw;
        }
        return ChatEmoteFormatter.Format(raw, name =>
        {
            EmoteSlot slot = feed.Resolve(sender, name);
            return slot == null ? null : atlas.Tag(slot);
        }, EmoteRuntime.FormatOptions);
    }

    internal static void BeginMessage(ChatMessage chatMessage)
    {
        currentSender = chatMessage.IsSystem ? null : chatMessage.SteamID?.ToString();
        currentRaw = null;
        currentFormatted = null;
    }

    internal static string FormatContent(string raw)
    {
        currentRaw = raw;
        currentFormatted = Format(currentSender, raw);
        return currentFormatted;
    }

    internal static void EndMessage(UIChat chat, List<UIChatMessage> messages)
    {
        string raw = currentRaw;
        string formatted = currentFormatted;
        string sender = currentSender;
        currentSender = currentRaw = currentFormatted = null;
        if (raw == null || messages.Count == 0 || raw.IndexOf(':') < 0)
        {
            return;
        }

        // Another mod can split a line into several labels; the content is in the last one.
        Label label = messages[messages.Count - 1].VisualElement.Query<Label>().ToList()
            .LastOrDefault(l => l.text != null && l.text.EndsWith(formatted, StringComparison.Ordinal));
        if (label == null)
        {
            return;
        }
        records.AddLast(new Record { Label = label, Sender = sender, Raw = raw, Formatted = formatted });
        while (records.Count > MAX_RECORDS)
        {
            records.RemoveFirst();
        }
        if (formatted.Contains("<sprite"))
        {
            EmoteHover.Track(label);
        }
    }

    // Re-renders messages whose emotes may have changed: from these senders, or all of them
    // when the server set changed (steamIds == null).
    static void OnSetsChanged(ICollection<string> steamIds)
    {
        for (LinkedListNode<Record> node = records.First; node != null;)
        {
            LinkedListNode<Record> next = node.Next;
            Record record = node.Value;
            if (record.Label.panel == null)
            {
                records.Remove(node);
            }
            else if (steamIds == null || (record.Sender != null && steamIds.Contains(record.Sender)))
            {
                Rerender(record);
            }
            node = next;
        }
    }

    static void Rerender(Record record)
    {
        string formatted = Format(record.Sender, record.Raw);
        string text = record.Label.text ?? "";
        if (formatted == record.Formatted || !text.EndsWith(record.Formatted, StringComparison.Ordinal))
        {
            return;
        }
        record.Label.text = text.Substring(0, text.Length - record.Formatted.Length) + formatted;
        record.Formatted = formatted;
        if (formatted.Contains("<sprite"))
        {
            EmoteHover.Track(record.Label);
        }
    }

    // A chat line's text with emotes turned back into the :name: codes that were typed.
    public static string ForCopy(Label label, string text)
    {
        foreach (Record record in records)
        {
            if (record.Label == label && text.EndsWith(record.Formatted, StringComparison.Ordinal))
            {
                return text.Substring(0, text.Length - record.Formatted.Length) + record.Raw;
            }
        }
        return text;
    }
}

[HarmonyPatch(typeof(UIChat), nameof(UIChat.AddChatMessage))]
public static class ChatAddPrefixPatch
{
    [HarmonyPrefix]
    public static void Prefix(ChatMessage chatMessage)
    {
        try
        {
            EmoteChat.BeginMessage(chatMessage);
        }
        catch (Exception e)
        {
            Plugin.LogError($"[Emotes] Chat prefix failed: {e.Message}");
        }
    }
}

// ParseChatContent runs after the game strips player-typed rich text and filters characters,
// so the sprite tags added here are the only tags in player messages.
[HarmonyPatch(typeof(UIChat), "ParseChatContent")]
public static class ChatParsePatch
{
    [HarmonyPostfix]
    public static void Postfix(ref string __result)
    {
        try
        {
            __result = EmoteChat.FormatContent(__result);
        }
        catch (Exception e)
        {
            Plugin.LogError($"[Emotes] Emote formatting failed: {e.Message}");
        }
    }
}

// Runs after TRL's NumberedNames (Priority.Last - 100) so it sees the row's final labels.
[HarmonyPatch(typeof(UIChat), nameof(UIChat.AddChatMessage))]
[HarmonyPriority(Priority.Last - 200)]
public static class ChatAddPostfixPatch
{
    static readonly AccessTools.FieldRef<UIChat, List<UIChatMessage>> uiChatMessages =
        AccessTools.FieldRefAccess<UIChat, List<UIChatMessage>>("uiChatMessages");

    [HarmonyPostfix]
    public static void Postfix(UIChat __instance)
    {
        try
        {
            EmoteChat.EndMessage(__instance, uiChatMessages(__instance));
        }
        catch (Exception e)
        {
            Plugin.LogError($"[Emotes] Chat hook failed: {e.Message}");
        }
    }
}
