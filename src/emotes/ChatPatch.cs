using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.emotes;

// Chat side of emotes. A message's :name: codes resolve for its sender (server set first,
// then the sender's personal set), so AddChatMessage records who sent the message being
// formatted. Each message keeps its raw text so it can be re-rendered when a sender's set
// arrives after the message did, and so copying a line gives back the typed :name: codes.
//
// Records live as long as their label does, because they are also how EmoteFeed knows which
// emotes are still on screen: an emote in any live line must keep its atlas cell.
public static class EmoteChat
{
    static readonly Regex spriteIdPattern = new Regex(@"name=""e(\d+)""", RegexOptions.Compiled);

    class Record
    {
        public Label Label;
        public ScrollView ScrollView;
        public string Sender;
        public string Raw;
        public string Formatted;
        public List<string> Ids = new List<string>();
    }

    static List<string> IdsIn(string formatted)
    {
        var ids = new List<string>();
        foreach (Match m in spriteIdPattern.Matches(formatted))
        {
            ids.Add(m.Groups[1].Value);
        }
        return ids;
    }

    static void Prune()
    {
        for (LinkedListNode<Record> node = records.First; node != null;)
        {
            LinkedListNode<Record> next = node.Next;
            if (node.Value.Label.panel == null)
            {
                records.Remove(node);
            }
            node = next;
        }
    }

    // Emotes shown by any chat line that still exists.
    public static void CollectReferenced(HashSet<string> ids)
    {
        Prune();
        foreach (Record record in records)
        {
            ids.UnionWith(record.Ids);
        }
    }

    // Emotes in chat lines that can be seen right now: not faded out, not scrolled away.
    public static void CollectVisible(HashSet<string> ids)
    {
        foreach (Record record in records)
        {
            if (record.Ids.Count == 0 || record.Label.panel == null || record.Label.ClassListContains("blurred"))
            {
                continue;
            }
            if (record.Label.resolvedStyle.display == DisplayStyle.None || record.Label.resolvedStyle.opacity <= 0.01f)
            {
                continue;
            }
            VisualElement viewport = record.ScrollView?.contentViewport;
            if (viewport != null && !viewport.worldBound.Overlaps(record.Label.worldBound))
            {
                continue;
            }
            ids.UnionWith(record.Ids);
        }
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
        records.AddLast(new Record
        {
            Label = label,
            ScrollView = label.GetFirstAncestorOfType<ScrollView>(),
            Sender = sender,
            Raw = raw,
            Formatted = formatted,
            Ids = IdsIn(formatted),
        });
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
        record.Ids = IdsIn(formatted);
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
