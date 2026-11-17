// Chat — behavior and appearance. Consolidates chat settings that used to be
// split across the HUD, General, and Chat & Scoreboard pages. Part of the
// "Tweaks" sidebar group (HUD).
//
// Also owns the ApplyChatHeight / ApplyChatBackground / ApplyQuickChatPosition
// helpers (called from Plugin and SwapperManager on load / scene change).

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using ToasterReskinLoader.core;
using ToasterReskinLoader.hud; // HideInactiveChat

namespace ToasterReskinLoader.ui.sections;

public static class ChatSection
{
    private static SettingsConfig Cfg => Settings.Current;

    public static void CreateSection(VisualElement root)
    {
        var cfg = SettingsUI.RequireConfig(root, "Chat behavior and appearance.");
        if (cfg == null) return;

        // ── Behavior ──
        SettingsUI.ToggleRow(root, "Drag-highlight and copy lines", cfg.enableChatDragSelect,
            v => { cfg.enableChatDragSelect = v; Settings.Save(); });
        SettingsUI.ToggleRow(root, "Hide when inactive", cfg.enableHideInactiveChat,
            v => { cfg.enableHideInactiveChat = v; Settings.Save(); HideInactiveChat.RefreshVisualState(); });
        SettingsUI.ToggleRow(root, "Don't fade out old messages", cfg.enableChatNoFade,
            v => { cfg.enableChatNoFade = v; Settings.Save(); HideInactiveChat.RefreshVisualState(); });
        SettingsUI.ToggleRow(root, "Prefix player names with jersey number", cfg.enableNumberedNames,
            v => { cfg.enableNumberedNames = v; Settings.Save(); });

        SettingsUI.Separator(root);
        SettingsUI.Header(root, "Appearance");

        SettingsUI.SliderRow(root, "Chat Height", 200f, 1300f, cfg.chatHeight,
            val => { cfg.chatHeight = val; Settings.Save(); ApplyChatHeight(val); });

        SettingsUI.ToggleRow(root, "Chat Background", cfg.chatBackground,
            v => { cfg.chatBackground = v; Settings.Save(); ApplyChatBackground(v); });
        SettingsUI.ToggleRow(root, "Render All Emojis in Chat", cfg.chatRenderAllEmojis,
            v => { cfg.chatRenderAllEmojis = v; Settings.Save(); });
        SettingsUI.Note(root,
            "Lets emoji and other special Unicode characters appear in chat instead of being stripped by the game's text filter.");

        SettingsUI.SliderRow(root, "Quick Chat Menu X Position", 0f, 100f, cfg.quickChatX,
            val => { cfg.quickChatX = val; Settings.Save(); ApplyQuickChatPosition(); });
        SettingsUI.SliderRow(root, "Quick Chat Menu Y Position", 0f, 100f, cfg.quickChatY,
            val => { cfg.quickChatY = val; Settings.Save(); ApplyQuickChatPosition(); });

        SettingsUI.Separator(root);
        SettingsUI.Header(root, "Emotes");

        // Only matters while emotes are off, so it's greyed out otherwise.
        VisualElement whenOffRow = null;
        SettingsUI.ToggleRow(root, "Show emotes", cfg.enableEmotes,
            v => { cfg.enableEmotes = v; Settings.Save(); whenOffRow?.SetEnabled(!v); });
        SettingsUI.Note(root,
            "Turns :name: codes in chat into images, with autocomplete and the emote picker. Applies to new messages.");

        SettingsUI.Note(root,
            "Your own emotes are set up on puckstats.io: sign in with Steam, then upload emotes or add them from Browse. Changes show up the next time you join a server.");
        {
            var row = UITools.CreateConfigurationRow();
            var manage = new Button { text = "Manage my emotes on puckstats.io" };
            UITools.StyleConfigButton(manage);
            manage.clicked += () => Application.OpenURL("https://puckstats.io/emotes?tab=mine");
            row.Add(manage);
            root.Add(row);
        }

        SettingsUI.ToggleRow(root, "Pause animated emotes when chat fades", cfg.pauseEmotesWhenChatFaded,
            v => { cfg.pauseEmotesWhenChatFaded = v; Settings.Save(); });
        SettingsUI.Note(root,
            "Faded messages that are still visible stop animating. Messages faded out completely or hidden always pause.");

        {
            var row = UITools.CreateConfigurationRow();
            whenOffRow = row;
            row.SetEnabled(!cfg.enableEmotes);
            row.Add(UITools.CreateConfigurationLabel("When emotes are off"));
            var labels = new List<string> { "Show the :name: text", "Remove them from messages", "Hide emote-only messages" };
            var values = new List<string> { "showName", "remove", "hideEmoteOnly" };
            var currentIdx = Math.Max(0, values.IndexOf(cfg.emotesWhenHidden ?? "showName"));
            var dd = UITools.CreateStringDropdownField(labels, labels[currentIdx]);
            dd.RegisterCallback<ChangeEvent<string>>(evt =>
            {
                var idx = labels.IndexOf(evt.newValue);
                if (idx < 0) idx = 0;
                cfg.emotesWhenHidden = values[idx];
                Settings.Save();
            });
            row.Add(dd);
            root.Add(row);
        }

    }

    // ── Apply helpers (called from Plugin / SwapperManager) ──────────────

    private static readonly FieldInfo _chatField = typeof(UIChat)
        .GetField("chat", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo _scrollViewField = typeof(UIChat)
        .GetField("scrollView", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo _quickChatField = typeof(UIChat)
        .GetField("quickChat", BindingFlags.Instance | BindingFlags.NonPublic);

    public static void ApplyChatHeight(float height)
    {
        var uiChat = MonoBehaviourSingleton<UIManager>.Instance?.Chat;
        if (uiChat == null) return;

        var chat = _chatField?.GetValue(uiChat) as VisualElement;
        var scrollView = _scrollViewField?.GetValue(uiChat) as ScrollView;

        if (chat != null) chat.style.minHeight = new StyleLength(height);
        if (scrollView != null) scrollView.style.minHeight = new StyleLength(height);
    }

    public static void ApplyChatBackground(bool enabled)
    {
        var uiChat = MonoBehaviourSingleton<UIManager>.Instance?.Chat;
        if (uiChat == null) return;

        var chat = _chatField?.GetValue(uiChat) as VisualElement;
        if (chat == null) return;

        chat.style.backgroundColor = enabled
            ? new StyleColor(new Color(0f, 0f, 0f, 0.1f))
            : new StyleColor(StyleKeyword.None);

        var scrollView = _scrollViewField?.GetValue(uiChat) as ScrollView;
        if (scrollView != null)
            scrollView.style.paddingTop = enabled ? 10 : 0;
    }

    public static void ApplyQuickChatPosition()
    {
        var uiChat = MonoBehaviourSingleton<UIManager>.Instance?.Chat;
        if (uiChat == null) return;

        var quickChat = _quickChatField?.GetValue(uiChat) as VisualElement;
        if (quickChat == null) return;

        quickChat.style.left = new StyleLength(new Length(Cfg?.quickChatX ?? 0f, LengthUnit.Percent));
        quickChat.style.top = new StyleLength(new Length(Cfg?.quickChatY ?? 50f, LengthUnit.Percent));
    }
}
