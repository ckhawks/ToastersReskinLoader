using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ToasterReskinLoader.qol;
using ToasterReskinLoader.swappers;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.ui.sections;

public static class UISection
{
    // Minimap settings live in the QoL profile now (HUD). Team colors stay in the reskin profile.
    private static QoLConfig Cfg => QoLRunner.Instance?.Config;
    private static void SaveQoL() => QoLRunner.Instance?.SaveAndRefresh();

    private static void ResetMinimapToDefault()
    {
        var d = new QoLConfig();
        var c = Cfg;
        if (c == null) return;
        c.blueMinimapNumberColor = d.blueMinimapNumberColor;
        c.redMinimapNumberColor = d.redMinimapNumberColor;
        c.minimapPuckColor = d.minimapPuckColor;
        c.minimapPlayerScale = d.minimapPlayerScale;
        c.minimapPuckScale = d.minimapPuckScale;
        c.minimapRefreshRate = d.minimapRefreshRate;
        c.localPlayerMinimapIconEnabled = d.localPlayerMinimapIconEnabled;
        c.blueLocalPlayerMinimapIconColor = d.blueLocalPlayerMinimapIconColor;
        c.redLocalPlayerMinimapIconColor = d.redLocalPlayerMinimapIconColor;
        SaveQoL();
    }

    public static void CreateSection(VisualElement contentScrollViewContent)
    {
        Label description = UITools.CreateConfigurationLabel(
            "Personal display settings: team indicator, minimap, and chat. " +
            "Team colors and names are now set per-team in the Players tab.");
        description.style.marginBottom = 12;
        description.style.whiteSpace = WhiteSpace.Normal;
        contentScrollViewContent.Add(description);

        // Team Indicator toggle
        VisualElement indicatorRow = UITools.CreateConfigurationRow();
        indicatorRow.Add(UITools.CreateConfigurationLabel("Enable Team Indicator"));

        Toggle indicatorToggle = UITools.CreateConfigurationCheckbox(Cfg.teamIndicatorEnabled);
        indicatorToggle.value = Cfg.teamIndicatorEnabled;
        indicatorToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
        {
            Cfg.teamIndicatorEnabled = evt.newValue;
            SaveQoL();
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
            Cfg.blueMinimapNumberColor,
            false,
            newColor =>
            {
                Cfg.blueMinimapNumberColor = newColor;
                ToasterReskinLoaderAPI.NotifyMinimapSettingsChanged();
            },
            () => { SaveQoL(); }
        );
        contentScrollViewContent.Add(blueNumberColor);

        // Red number text color
        var redNumberColor = UITools.CreateColorConfigurationRow(
            "Red Number Text Color",
            Cfg.redMinimapNumberColor,
            false,
            newColor =>
            {
                Cfg.redMinimapNumberColor = newColor;
                ToasterReskinLoaderAPI.NotifyMinimapSettingsChanged();
            },
            () => { SaveQoL(); }
        );
        contentScrollViewContent.Add(redNumberColor);

        // Puck color
        var puckColor = UITools.CreateColorConfigurationRow(
            "Puck Color",
            Cfg.minimapPuckColor,
            false,
            newColor =>
            {
                Cfg.minimapPuckColor = newColor;
                ToasterReskinLoaderAPI.NotifyMinimapSettingsChanged();
            },
            () => { SaveQoL(); }
        );
        contentScrollViewContent.Add(puckColor);

        // ── Local Player Icon Color ──
        var localIconDependentControls = new List<VisualElement>();

        VisualElement localIconRow = UITools.CreateConfigurationRow();
        localIconRow.Add(UITools.CreateConfigurationLabel("Custom Local Player Icon Color"));

        Toggle localIconToggle = UITools.CreateConfigurationCheckbox(Cfg.localPlayerMinimapIconEnabled);
        localIconToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
        {
            Cfg.localPlayerMinimapIconEnabled = evt.newValue;
            SaveQoL();
            ToasterReskinLoaderAPI.NotifyMinimapSettingsChanged();
            TeamColorSwapper.RefreshAll();
            UITools.UpdateDependentControlsState(localIconDependentControls, evt.newValue);
        });
        localIconRow.Add(localIconToggle);
        contentScrollViewContent.Add(localIconRow);

        var blueLocalIconColor = UITools.CreateColorConfigurationRow(
            "Blue Local Player Icon Color",
            Cfg.blueLocalPlayerMinimapIconColor,
            false,
            newColor =>
            {
                Cfg.blueLocalPlayerMinimapIconColor = newColor;
                ToasterReskinLoaderAPI.NotifyMinimapSettingsChanged();
            },
            () => { SaveQoL(); }
        );
        contentScrollViewContent.Add(blueLocalIconColor);
        localIconDependentControls.Add(blueLocalIconColor);

        var redLocalIconColor = UITools.CreateColorConfigurationRow(
            "Red Local Player Icon Color",
            Cfg.redLocalPlayerMinimapIconColor,
            false,
            newColor =>
            {
                Cfg.redLocalPlayerMinimapIconColor = newColor;
                ToasterReskinLoaderAPI.NotifyMinimapSettingsChanged();
            },
            () => { SaveQoL(); }
        );
        contentScrollViewContent.Add(redLocalIconColor);
        localIconDependentControls.Add(redLocalIconColor);

        UITools.UpdateDependentControlsState(localIconDependentControls, Cfg.localPlayerMinimapIconEnabled);

        // Player icon scale
        CreateSliderRow(contentScrollViewContent, "Player Icon Scale", 0.5f, 3f,
            () => Cfg.minimapPlayerScale,
            val =>
            {
                Cfg.minimapPlayerScale = val;
                SaveQoL();
                ToasterReskinLoaderAPI.NotifyMinimapSettingsChanged();
            });

        // Puck icon scale
        CreateSliderRow(contentScrollViewContent, "Puck Icon Scale", 0.5f, 3f,
            () => Cfg.minimapPuckScale,
            val =>
            {
                Cfg.minimapPuckScale = val;
                SaveQoL();
                ToasterReskinLoaderAPI.NotifyMinimapSettingsChanged();
            });

        // Minimap refresh rate (game default 30, range 1-120)
        CreateSliderRow(contentScrollViewContent, "Minimap Refresh Rate", 1f, 120f,
            () => Cfg.minimapRefreshRate,
            val =>
            {
                Cfg.minimapRefreshRate = Mathf.RoundToInt(val);
                SaveQoL();
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
            ResetMinimapToDefault();
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
            () => Cfg.chatHeight,
            val =>
            {
                Cfg.chatHeight = val;
                SaveQoL();
                ApplyChatHeight(val);
            });

        VisualElement chatBgRow = UITools.CreateConfigurationRow();
        chatBgRow.Add(UITools.CreateConfigurationLabel("Chat Background"));
        Toggle chatBgToggle = UITools.CreateConfigurationCheckbox(Cfg.chatBackground);
        chatBgToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
        {
            Cfg.chatBackground = evt.newValue;
            SaveQoL();
            ApplyChatBackground(evt.newValue);
        });
        chatBgRow.Add(chatBgToggle);
        contentScrollViewContent.Add(chatBgRow);

        VisualElement emojiRow = UITools.CreateConfigurationRow();
        emojiRow.Add(UITools.CreateConfigurationLabel("Render All Emojis in Chat"));
        Toggle emojiToggle = UITools.CreateConfigurationCheckbox(Cfg.chatRenderAllEmojis);
        emojiToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
        {
            Cfg.chatRenderAllEmojis = evt.newValue;
            SaveQoL();
        });
        emojiRow.Add(emojiToggle);
        contentScrollViewContent.Add(emojiRow);

        CreateSliderRow(contentScrollViewContent, "Quick Chat Menu X Position", 0f, 100f,
            () => Cfg.quickChatX,
            val =>
            {
                Cfg.quickChatX = val;
                SaveQoL();
                ApplyQuickChatPosition();
            });

        CreateSliderRow(contentScrollViewContent, "Quick Chat Menu Y Position", 0f, 100f,
            () => Cfg.quickChatY,
            val =>
            {
                Cfg.quickChatY = val;
                SaveQoL();
                ApplyQuickChatPosition();
            });
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

        quickChat.style.left = new StyleLength(new Length(Cfg?.quickChatX ?? 0f, LengthUnit.Percent));
        quickChat.style.top = new StyleLength(new Length(Cfg?.quickChatY ?? 50f, LengthUnit.Percent));
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
