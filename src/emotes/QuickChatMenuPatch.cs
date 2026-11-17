using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.emotes;

// Quick chat entries can be ":name:" codes (e.g. Quick Chat Plus slots bound to emotes).
// They are sent as the local player, so the menu shows them the way they'll arrive.
public static class EmoteQuickChatMenu
{
    static readonly Regex spriteIdPattern = new Regex(@"name=""e(\d+)""", RegexOptions.Compiled);

    static readonly AccessTools.FieldRef<UIChat, VisualElement> quickChatMessages =
        AccessTools.FieldRefAccess<UIChat, VisualElement>("quickChatMessages");

    // Rows of the open menu: the label and its unformatted text.
    static readonly List<(Label label, string raw)> rows = new List<(Label, string)>();
    static UIChat openChat;

    static readonly EmoteFormatOptions menuOptions = new EmoteFormatOptions();

    public static void Setup()
    {
        EmoteFeed.SetsChanged += OnSetsChanged;
    }

    public static void Teardown()
    {
        EmoteFeed.SetsChanged -= OnSetsChanged;
        rows.Clear();
        openChat = null;
    }

    internal static void OnShown(UIChat chat)
    {
        rows.Clear();
        openChat = chat;
        VisualElement messages = quickChatMessages(chat);
        if (messages == null)
        {
            return;
        }
        foreach (Label label in messages.Query<Label>().ToList())
        {
            if (label.text != null && label.text.IndexOf(':') >= 0)
            {
                rows.Add((label, label.text));
            }
        }
        Render();
    }

    static void Render()
    {
        EmoteFeed feed = EmoteFeed.Instance;
        EmoteAtlas atlas = EmoteRuntime.Atlas;
        if (feed == null || atlas == null)
        {
            return;
        }
        // Hidden emotes stay as their names here whatever the chat setting, so a row never
        // turns blank.
        menuOptions.Render = EmoteRuntime.Enabled;
        menuOptions.WhenHidden = HiddenEmoteMode.ShowName;
        menuOptions.Jumbo = false;
        foreach (var (label, raw) in rows)
        {
            label.text = ChatEmoteFormatter.Format(raw, name =>
            {
                EmoteSlot slot = feed.Resolve(feed.LocalSteamId, name);
                return slot == null ? null : atlas.Tag(slot);
            }, menuOptions);
        }
    }

    static void OnSetsChanged(ICollection<string> steamIds)
    {
        if (rows.Count > 0 && openChat != null && openChat.IsQuickChatVisible)
        {
            Render();
        }
    }

    // Emotes in the open menu keep animating.
    public static void CollectVisible(HashSet<string> ids)
    {
        if (openChat == null || !openChat.IsQuickChatVisible)
        {
            return;
        }
        foreach (var (label, _) in rows)
        {
            if (label.panel == null || label.text == null)
            {
                continue;
            }
            foreach (Match m in spriteIdPattern.Matches(label.text))
            {
                ids.Add(m.Groups[1].Value);
            }
        }
    }
}

[HarmonyPatch(typeof(UIChat), nameof(UIChat.ShowQuickChat))]
[HarmonyPriority(Priority.Last)]
public static class QuickChatShowPatch
{
    [HarmonyPostfix]
    public static void Postfix(UIChat __instance)
    {
        try
        {
            EmoteQuickChatMenu.OnShown(__instance);
        }
        catch (Exception e)
        {
            Plugin.LogError($"[Emotes] Quick chat menu formatting failed: {e.Message}");
        }
    }
}
