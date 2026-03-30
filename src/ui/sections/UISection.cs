using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ToasterReskinLoader.swappers;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.ui.sections;

public static class UISection
{
    public static void CreateSection(VisualElement contentScrollViewContent)
    {
        Label description = UITools.CreateConfigurationLabel(
            "Customize team colors across all UI elements: scoreboard, goal announcements, minimap, chat, team/position select, goal frames, and more.");
        description.style.marginBottom = 12;
        description.style.whiteSpace = WhiteSpace.Normal;
        contentScrollViewContent.Add(description);

        var dependentControls = new List<VisualElement>();

        // Enable toggle
        VisualElement enableRow = UITools.CreateConfigurationRow();
        enableRow.Add(UITools.CreateConfigurationLabel("Enable Custom Team Colors"));

        Toggle enableToggle = UITools.CreateConfigurationCheckbox(ReskinProfileManager.currentProfile.teamColorsEnabled);
        enableToggle.value = ReskinProfileManager.currentProfile.teamColorsEnabled;
        enableToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
        {
            ReskinProfileManager.currentProfile.teamColorsEnabled = evt.newValue;
            ReskinProfileManager.SaveProfile();
            ArenaSwapper.UpdateGoalFrameColors();
            TeamIndicatorSwapper.UpdateVisibility();
            TeamColorSwapper.RefreshAll();
            ToasterReskinLoaderAPI.NotifyTeamColorsChanged();
            UITools.UpdateDependentControlsState(dependentControls, evt.newValue);
        });
        enableRow.Add(enableToggle);
        contentScrollViewContent.Add(enableRow);

        // Blue team color
        var blueColorSection = UITools.CreateColorConfigurationRow(
            "<b>Blue Team Color</b>",
            ReskinProfileManager.currentProfile.blueTeamColor,
            false,
            newColor =>
            {
                ReskinProfileManager.currentProfile.blueTeamColor = newColor;
                ArenaSwapper.UpdateGoalFrameColors();
                TeamIndicatorSwapper.UpdateVisibility();
                TeamColorSwapper.RefreshAll();
                ToasterReskinLoaderAPI.NotifyTeamColorsChanged();
            },
            () => { ReskinProfileManager.SaveProfile(); }
        );
        contentScrollViewContent.Add(blueColorSection);
        dependentControls.Add(blueColorSection);

        // Red team color
        var redColorSection = UITools.CreateColorConfigurationRow(
            "<b>Red Team Color</b>",
            ReskinProfileManager.currentProfile.redTeamColor,
            false,
            newColor =>
            {
                ReskinProfileManager.currentProfile.redTeamColor = newColor;
                ArenaSwapper.UpdateGoalFrameColors();
                TeamIndicatorSwapper.UpdateVisibility();
                TeamColorSwapper.RefreshAll();
                ToasterReskinLoaderAPI.NotifyTeamColorsChanged();
            },
            () => { ReskinProfileManager.SaveProfile(); }
        );
        contentScrollViewContent.Add(redColorSection);
        dependentControls.Add(redColorSection);

        // Team names
        VisualElement blueNameRow = UITools.CreateConfigurationRow();
        blueNameRow.Add(UITools.CreateConfigurationLabel("Blue Team Name"));
        var blueNameField = CreateTextInput(
            ReskinProfileManager.currentProfile.blueTeamName,
            "BLUE",
            val =>
            {
                ReskinProfileManager.currentProfile.blueTeamName = val;
                ReskinProfileManager.SaveProfile();
            });
        blueNameRow.Add(blueNameField);
        contentScrollViewContent.Add(blueNameRow);
        dependentControls.Add(blueNameRow);

        VisualElement redNameRow = UITools.CreateConfigurationRow();
        redNameRow.Add(UITools.CreateConfigurationLabel("Red Team Name"));
        var redNameField = CreateTextInput(
            ReskinProfileManager.currentProfile.redTeamName,
            "RED",
            val =>
            {
                ReskinProfileManager.currentProfile.redTeamName = val;
                ReskinProfileManager.SaveProfile();
            });
        redNameRow.Add(redNameField);
        contentScrollViewContent.Add(redNameRow);
        dependentControls.Add(redNameRow);

        Label nameNote = UITools.CreateConfigurationLabel(
            "Leave blank to use default names. Used in goal announcements (e.g. \"BLUE SCORES!\").");
        nameNote.style.marginTop = 4;
        nameNote.style.fontSize = 13;
        nameNote.style.color = new Color(0.7f, 0.7f, 0.7f);
        nameNote.style.whiteSpace = WhiteSpace.Normal;
        contentScrollViewContent.Add(nameNote);
        dependentControls.Add(nameNote);

        Label note = UITools.CreateConfigurationLabel(
            "UI color changes apply on the next UI update (next goal, chat message, scoreboard refresh, etc.). Goal frames and team indicator update immediately.");
        note.style.marginTop = 8;
        note.style.fontSize = 13;
        note.style.color = new Color(0.7f, 0.7f, 0.7f);
        note.style.whiteSpace = WhiteSpace.Normal;
        contentScrollViewContent.Add(note);
        dependentControls.Add(note);

        // Separator
        VisualElement separator = new VisualElement();
        separator.style.height = 1;
        separator.style.backgroundColor = new Color(0.4f, 0.4f, 0.4f);
        separator.style.marginTop = 16;
        separator.style.marginBottom = 16;
        contentScrollViewContent.Add(separator);

        // Team Indicator toggle
        VisualElement indicatorRow = UITools.CreateConfigurationRow();
        indicatorRow.Add(UITools.CreateConfigurationLabel("Enable Team Indicator"));

        Toggle indicatorToggle = UITools.CreateConfigurationCheckbox(ReskinProfileManager.currentProfile.teamIndicatorEnabled);
        indicatorToggle.value = ReskinProfileManager.currentProfile.teamIndicatorEnabled;
        indicatorToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
        {
            ReskinProfileManager.currentProfile.teamIndicatorEnabled = evt.newValue;
            ReskinProfileManager.SaveProfile();
            TeamIndicatorSwapper.UpdateVisibility();
        });
        indicatorRow.Add(indicatorToggle);
        contentScrollViewContent.Add(indicatorRow);

        Label indicatorNote = UITools.CreateConfigurationLabel(
            "Shows a colored bar at the bottom of the screen indicating your current team. Uses custom team colors if enabled above.");
        indicatorNote.style.marginTop = 4;
        indicatorNote.style.fontSize = 13;
        indicatorNote.style.color = new Color(0.7f, 0.7f, 0.7f);
        indicatorNote.style.whiteSpace = WhiteSpace.Normal;
        contentScrollViewContent.Add(indicatorNote);

        // Reset button
        Button resetButton = new Button
        {
            text = "Reset to default",
            style =
            {
                backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f)),
                unityTextAlign = TextAnchor.MiddleLeft,
                fontSize = 18,
                marginTop = 8,
                paddingTop = 8,
                paddingBottom = 8,
                paddingLeft = 15
            }
        };
        UITools.AddHoverEffectsForButton(resetButton);
        resetButton.RegisterCallback<ClickEvent>(evt =>
        {
            ReskinProfileManager.ResetTeamColorsToDefault();
            ArenaSwapper.UpdateGoalFrameColors();
            TeamIndicatorSwapper.UpdateVisibility();
            TeamColorSwapper.RefreshAll();
            ToasterReskinLoaderAPI.NotifyTeamColorsChanged();

            Label title = (Label)contentScrollViewContent.Children().ToArray()[0];
            contentScrollViewContent.Clear();
            contentScrollViewContent.Add(title);
            CreateSection(contentScrollViewContent);
        });
        contentScrollViewContent.Add(resetButton);

        // ── Minimap ─────────────────────────────────────────────────────
        VisualElement minimapSeparator = new VisualElement();
        minimapSeparator.style.height = 1;
        minimapSeparator.style.backgroundColor = new Color(0.4f, 0.4f, 0.4f);
        minimapSeparator.style.marginTop = 16;
        minimapSeparator.style.marginBottom = 16;
        contentScrollViewContent.Add(minimapSeparator);

        Label minimapHeader = new Label("<b>Minimap</b>");
        minimapHeader.style.fontSize = 20;
        minimapHeader.style.color = Color.white;
        minimapHeader.style.marginBottom = 8;
        contentScrollViewContent.Add(minimapHeader);

        // Blue number text color
        var blueNumberColor = UITools.CreateColorConfigurationRow(
            "Blue Number Text Color",
            ReskinProfileManager.currentProfile.blueMinimapNumberColor,
            false,
            newColor =>
            {
                ReskinProfileManager.currentProfile.blueMinimapNumberColor = newColor;
                ToasterReskinLoaderAPI.NotifyMinimapSettingsChanged();
            },
            () => { ReskinProfileManager.SaveProfile(); }
        );
        contentScrollViewContent.Add(blueNumberColor);

        // Red number text color
        var redNumberColor = UITools.CreateColorConfigurationRow(
            "Red Number Text Color",
            ReskinProfileManager.currentProfile.redMinimapNumberColor,
            false,
            newColor =>
            {
                ReskinProfileManager.currentProfile.redMinimapNumberColor = newColor;
                ToasterReskinLoaderAPI.NotifyMinimapSettingsChanged();
            },
            () => { ReskinProfileManager.SaveProfile(); }
        );
        contentScrollViewContent.Add(redNumberColor);

        // Puck color
        var puckColor = UITools.CreateColorConfigurationRow(
            "Puck Color",
            ReskinProfileManager.currentProfile.minimapPuckColor,
            false,
            newColor =>
            {
                ReskinProfileManager.currentProfile.minimapPuckColor = newColor;
                ToasterReskinLoaderAPI.NotifyMinimapSettingsChanged();
            },
            () => { ReskinProfileManager.SaveProfile(); }
        );
        contentScrollViewContent.Add(puckColor);

        // Player icon scale
        CreateSliderRow(contentScrollViewContent, "Player Icon Scale", 0.5f, 3f,
            () => ReskinProfileManager.currentProfile.minimapPlayerScale,
            val =>
            {
                ReskinProfileManager.currentProfile.minimapPlayerScale = val;
                ReskinProfileManager.SaveProfile();
                ToasterReskinLoaderAPI.NotifyMinimapSettingsChanged();
            });

        // Puck icon scale
        CreateSliderRow(contentScrollViewContent, "Puck Icon Scale", 0.5f, 3f,
            () => ReskinProfileManager.currentProfile.minimapPuckScale,
            val =>
            {
                ReskinProfileManager.currentProfile.minimapPuckScale = val;
                ReskinProfileManager.SaveProfile();
                ToasterReskinLoaderAPI.NotifyMinimapSettingsChanged();
            });

        // Minimap refresh rate (game default 30, range 1-120)
        CreateSliderRow(contentScrollViewContent, "Minimap Refresh Rate", 1f, 120f,
            () => ReskinProfileManager.currentProfile.minimapRefreshRate,
            val =>
            {
                ReskinProfileManager.currentProfile.minimapRefreshRate = Mathf.RoundToInt(val);
                ReskinProfileManager.SaveProfile();
                MinimapSwapper.ApplyRefreshRate();
            });

        // Minimap reset button
        Button minimapResetButton = new Button
        {
            text = "Reset minimap to default",
            style =
            {
                backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f)),
                unityTextAlign = TextAnchor.MiddleLeft,
                fontSize = 18,
                marginTop = 8,
                paddingTop = 8,
                paddingBottom = 8,
                paddingLeft = 15
            }
        };
        UITools.AddHoverEffectsForButton(minimapResetButton);
        minimapResetButton.RegisterCallback<ClickEvent>(evt =>
        {
            ReskinProfileManager.ResetMinimapToDefault();
            ToasterReskinLoaderAPI.NotifyMinimapSettingsChanged();

            Label title = (Label)contentScrollViewContent.Children().First();
            contentScrollViewContent.Clear();
            contentScrollViewContent.Add(title);
            CreateSection(contentScrollViewContent);
        });
        contentScrollViewContent.Add(minimapResetButton);

        // ── Chat ────────────────────────────────────────────────────────
        VisualElement chatSeparator = new VisualElement();
        chatSeparator.style.height = 1;
        chatSeparator.style.backgroundColor = new Color(0.4f, 0.4f, 0.4f);
        chatSeparator.style.marginTop = 16;
        chatSeparator.style.marginBottom = 16;
        contentScrollViewContent.Add(chatSeparator);

        Label chatHeader = new Label("<b>Chat</b>");
        chatHeader.style.fontSize = 20;
        chatHeader.style.color = Color.white;
        chatHeader.style.marginBottom = 8;
        contentScrollViewContent.Add(chatHeader);

        CreateSliderRow(contentScrollViewContent, "Chat Height", 200f, 1300f,
            () => ReskinProfileManager.currentProfile.chatHeight,
            val =>
            {
                ReskinProfileManager.currentProfile.chatHeight = val;
                ReskinProfileManager.SaveProfile();
                ApplyChatHeight(val);
            });

        VisualElement chatBgRow = UITools.CreateConfigurationRow();
        chatBgRow.Add(UITools.CreateConfigurationLabel("Chat Background"));
        Toggle chatBgToggle = UITools.CreateConfigurationCheckbox(ReskinProfileManager.currentProfile.chatBackground);
        chatBgToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
        {
            ReskinProfileManager.currentProfile.chatBackground = evt.newValue;
            ReskinProfileManager.SaveProfile();
            ApplyChatBackground(evt.newValue);
        });
        chatBgRow.Add(chatBgToggle);
        contentScrollViewContent.Add(chatBgRow);

        VisualElement emojiRow = UITools.CreateConfigurationRow();
        emojiRow.Add(UITools.CreateConfigurationLabel("Render All Emojis in Chat"));
        Toggle emojiToggle = UITools.CreateConfigurationCheckbox(ReskinProfileManager.currentProfile.chatRenderAllEmojis);
        emojiToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
        {
            ReskinProfileManager.currentProfile.chatRenderAllEmojis = evt.newValue;
            ReskinProfileManager.SaveProfile();
        });
        emojiRow.Add(emojiToggle);
        contentScrollViewContent.Add(emojiRow);

        CreateSliderRow(contentScrollViewContent, "Quick Chat Menu X Position", 0f, 100f,
            () => ReskinProfileManager.currentProfile.quickChatX,
            val =>
            {
                ReskinProfileManager.currentProfile.quickChatX = val;
                ReskinProfileManager.SaveProfile();
                ApplyQuickChatPosition();
            });

        CreateSliderRow(contentScrollViewContent, "Quick Chat Menu Y Position", 0f, 100f,
            () => ReskinProfileManager.currentProfile.quickChatY,
            val =>
            {
                ReskinProfileManager.currentProfile.quickChatY = val;
                ReskinProfileManager.SaveProfile();
                ApplyQuickChatPosition();
            });

        // Set initial state
        UITools.UpdateDependentControlsState(dependentControls, ReskinProfileManager.currentProfile.teamColorsEnabled);
    }

    private static void CreateSliderRow(
        VisualElement container,
        string label,
        float min,
        float max,
        System.Func<float> getter,
        System.Action<float> setter)
    {
        var row = UITools.CreateConfigurationRow();
        row.Add(UITools.CreateConfigurationLabel(label));
        var slider = UITools.CreateConfigurationSlider(min, max, getter(), 300);

        slider.RegisterCallback<ChangeEvent<float>>(evt =>
        {
            setter(evt.newValue);
            ReskinProfileManager.SaveProfile();
        });
        slider.RegisterCallback<PointerUpEvent>(evt => ReskinProfileManager.SaveProfile());

        row.Add(slider);
        container.Add(row);
    }

    // ── Chat height ────────────────────────────────────────────────

    private static readonly FieldInfo _chatField = typeof(UIChat)
        .GetField("chat", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo _scrollViewField = typeof(UIChat)
        .GetField("scrollView", BindingFlags.Instance | BindingFlags.NonPublic);

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

    private static readonly FieldInfo _quickChatField = typeof(UIChat)
        .GetField("quickChat", BindingFlags.Instance | BindingFlags.NonPublic);

    public static void ApplyQuickChatPosition()
    {
        var uiChat = MonoBehaviourSingleton<UIManager>.Instance?.Chat;
        if (uiChat == null) return;

        var quickChat = _quickChatField?.GetValue(uiChat) as VisualElement;
        if (quickChat == null) return;

        quickChat.style.left = new StyleLength(new Length(ReskinProfileManager.currentProfile.quickChatX, LengthUnit.Percent));
        quickChat.style.top = new StyleLength(new Length(ReskinProfileManager.currentProfile.quickChatY, LengthUnit.Percent));
    }

    private static TextField CreateTextInput(string value, string placeholder, System.Action<string> onChanged)
    {
        var field = new TextField();
        field.value = value ?? "";
        field.style.width = 300;
        field.style.minWidth = 300;
        field.style.maxWidth = 300;
        field.style.fontSize = 14;

        // Style the inner input
        field.RegisterCallback<AttachToPanelEvent>(evt =>
        {
            var input = field.Q(className: "unity-base-text-field__input");
            if (input != null)
            {
                input.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
                input.style.color = Color.white;
                input.style.paddingLeft = 8;
                input.style.paddingRight = 8;
                input.style.paddingTop = 4;
                input.style.paddingBottom = 4;
            }
        });

        // Show placeholder when empty
        if (string.IsNullOrEmpty(value))
            field.value = "";

        field.RegisterCallback<ChangeEvent<string>>(evt =>
        {
            onChanged?.Invoke(evt.newValue);
        });

        return field;
    }
}
