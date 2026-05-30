using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using ToasterReskinLoader.swappers;
using ToasterReskinLoader.ui.sections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;
using Object = UnityEngine.Object;

namespace ToasterReskinLoader.ui;

public static class ReskinMenu
{
    public static VisualElement rootContainer;
    public static VisualElement mainContainer;
    public static UIMainMenu uiMainMenu;

    public static ScrollView sidebarScrollView;
    public static VisualElement contentScrollViewContent;
    
    // menu state
    public static string[] sections = new []{"Packs", "Presets", "Appearance", "Players", "Sticks", "Tapes", "Skaters", "Goalies", "Pucks", "Puck FX", "Arena",
        "Skybox", "Shadows", "User Interface", "Quality of Life", "Glossiness", "Extras", "About" };
    public static int selectedSectionIndex = 0;

    // Visual sidebar layout. A null group renders top-level section button(s); a named group
    // renders a collapsible header with indented child section buttons. Every name here must
    // exist in `sections` above.
    private static readonly (string Group, string[] Items)[] sidebarLayout =
    {
        (null, new[] { "Packs" }),
        (null, new[] { "Presets" }),
        ("Reskins", new[] { "Players", "Sticks", "Tapes", "Pucks" }),
        ("Effects", new[] { "Puck FX", "Skybox", "Shadows", "Glossiness" }),
        (null, new[] { "Arena" }),
        (null, new[] { "Appearance" }),
        (null, new[] { "User Interface" }),
        (null, new[] { "Quality of Life" }),
        (null, new[] { "Extras" }),
        (null, new[] { "About" }),
    };

    // Section name -> its sidebar button, so selection restyling works regardless of grouping.
    private static readonly Dictionary<string, Button> sectionButtons = new();

    public static void Show()
    {
        // If we're in main menu, hide main menu
        // If we're in game, hide pause menu
        
        if (rootContainer == null)
        {
            Create();
        }
        else
        {
            CreateContentForSection(selectedSectionIndex);
        }
        rootContainer.visible = true;
        rootContainer.enabledSelf = true;
        rootContainer.style.display = DisplayStyle.Flex;
        mainContainer.visible = true;
        mainContainer.enabledSelf = true;
        mainContainer.style.display = DisplayStyle.Flex;


        if (ChangingRoomHelper.IsInMainMenu())
        {
            if (MonoBehaviourSingleton<UIManager>.Instance != null)
            {
                MonoBehaviourSingleton<UIManager>.Instance.MainMenu.Hide();
                MonoBehaviourSingleton<UIManager>.Instance.Footer.Hide();
            }

            ChangingRoomHelper.ShowBaseFocus();
        }
        else
        {
            MonoBehaviourSingleton<UIManager>.Instance?.PauseMenu.Hide();
            MonoBehaviourSingleton<UIManager>.Instance?.GameState.Hide();
            // Keep minimap visible if on the User Interface tab (for previewing minimap settings)
            // but not in the main menu where the minimap doesn't exist
            if (sections[selectedSectionIndex] != "User Interface" || ChangingRoomHelper.IsInMainMenu())
                MonoBehaviourSingleton<UIManager>.Instance?.Minimap.Hide();
        }

        // Tell the game's state system that the mouse should be visible
        GlobalStateManager.SetUIState(new Dictionary<string, object> { { "isMouseRequired", true } });
    }

    public static void Hide()
    {
        // If we're in game, show pause menu
        // If we're in main menu, show main menu
        if (rootContainer == null) Create();
        mainContainer.visible = false;
        mainContainer.enabledSelf = false;
        mainContainer.style.display = DisplayStyle.None;
        rootContainer.visible = false;
        rootContainer.enabledSelf = false;
        rootContainer.style.display = DisplayStyle.None;


        if (ChangingRoomHelper.IsInMainMenu())
        {
            if (MonoBehaviourSingleton<UIManager>.Instance != null)
            {
                MonoBehaviourSingleton<UIManager>.Instance.MainMenu.Show();
                MonoBehaviourSingleton<UIManager>.Instance.Footer.Show();
            }

            ChangingRoomHelper.Unfocus();
            ChangingRoomHelper.ResetPreviewContext();
        }
        else
        {
            MonoBehaviourSingleton<UIManager>.Instance?.PauseMenu.Show();
            MonoBehaviourSingleton<UIManager>.Instance?.GameState.Show();
            MonoBehaviourSingleton<UIManager>.Instance?.Minimap.Show();
        }
    }

    public static void Create()
    {
        rootContainer = new VisualElement();
        rootContainer.style.position = Position.Absolute;
        rootContainer.style.left = 0;
        rootContainer.style.top = 0;
        rootContainer.style.right = 0;
        rootContainer.style.bottom = 0;
        rootContainer.style.flexDirection = FlexDirection.Row;
        rootContainer.style.alignItems = Align.Center;
        rootContainer.style.justifyContent = Justify.FlexStart;
        rootContainer.pickingMode = PickingMode.Ignore;
        
        mainContainer = new VisualElement();
        mainContainer.style.backgroundColor = new StyleColor(new Color(0.196f, 0.196f,0.196f, 1));
        mainContainer.style.marginLeft = new StyleLength(new Length(40));
        mainContainer.style.maxWidth = new StyleLength(new Length(45, LengthUnit.Percent));
        mainContainer.style.minWidth = new StyleLength(new Length(45, LengthUnit.Percent));
        mainContainer.style.maxHeight = new StyleLength(new Length(75, LengthUnit.Percent));
        mainContainer.style.minHeight = new StyleLength(new Length(75, LengthUnit.Percent));
        mainContainer.pickingMode = PickingMode.Position;
        
        VisualElement titleContainer = new VisualElement();
        titleContainer.style.flexDirection = FlexDirection.Row;
        titleContainer.style.justifyContent = Justify.SpaceBetween;
        titleContainer.style.alignItems = Align.Center;
        titleContainer.style.minHeight = 74;
        titleContainer.style.maxHeight = 74;
        titleContainer.style.height = 74;
        titleContainer.style.paddingLeft = 10;
        titleContainer.style.paddingTop = 10;
        titleContainer.style.paddingRight = 10;
        titleContainer.style.paddingBottom = 10;
        titleContainer.style.backgroundColor = new StyleColor(new Color(0.14f, 0.14f, 0.14f));
        
        Label title = new Label("Reskin Manager");
        title.style.fontSize = 30;
        title.style.color = Color.white;
        titleContainer.Add(title);
        Button reloadButton = new Button();
        reloadButton.text = "Reload";
        reloadButton.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
        reloadButton.style.paddingLeft = 8;
        reloadButton.style.paddingTop = 8;
        reloadButton.style.paddingRight = 8;
        reloadButton.style.paddingBottom = 8;
        UITools.AddHoverEffectsForButton(reloadButton);
        reloadButton.RegisterCallback<ClickEvent>(new EventCallback<ClickEvent>(ReloadButtonClickHandler));
        void ReloadButtonClickHandler(ClickEvent evt)
        {
            try
            {
                void reload()
                {
                    Plugin.Log($"Reloading packs, profile, and textures...");
                    ReskinRegistry.ReloadPacks();
                    Plugin.LogDebug($"Reloading profile...");
                    ReskinProfileManager.LoadProfile();
                    Plugin.LogDebug($"Clearing texture cache...");
                    TextureManager.ClearTextureCache();
                    Plugin.LogDebug($"Loading textures for active reskins...");
                    ReskinProfileManager.LoadTexturesForActiveReskins();
                    Plugin.LogDebug($"Setting all swappers...");
                    SwapperManager.SetAll();
                    // In the locker room SetAll doesn't touch the preview mannequin, whose
                    // materials still reference the textures ClearTextureCache just destroyed
                    // (black player / invisible stick). Re-drive the preview so it re-binds the
                    // freshly reloaded textures. No-op outside the main menu.
                    ChangingRoomHelper.RefreshPreview();
                    Plugin.LogDebug($"Recreating content for selecting section...");
                    CreateContentForSection(selectedSectionIndex);
                    Plugin.Log($"Reload complete!");
                    reloadButton.text = "Reloaded!";
                    reloadButton.SetEnabled(false);
                    reloadButton.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f, 0.5f));

                    void postreload()
                    {
                        reloadButton.text = "Reload";
                        reloadButton.SetEnabled(true);
                        reloadButton.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
                    }
                    
                    contentScrollViewContent.schedule.Execute(postreload).ExecuteLater(2000);
                }
                reloadButton.text = "Reloading...";
                contentScrollViewContent.schedule.Execute(reload).ExecuteLater(10);
            }
            catch (Exception e)
            {
                Plugin.LogError($"Failed to reload packs: {e.Message}");
                reloadButton.text = "Reload Error";
            }
        }
        titleContainer.Add(reloadButton);
        mainContainer.Add(titleContainer);
        
        VisualElement pageContainer = new VisualElement();
        pageContainer.style.flexDirection = FlexDirection.Row;
        pageContainer.style.maxWidth = new StyleLength(new Length(100, LengthUnit.Percent));
        pageContainer.style.minWidth = new StyleLength(new Length(100, LengthUnit.Percent));
        pageContainer.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
        pageContainer.style.flexGrow = 1; // Take all available space, pushing bottom row to the bottom
        mainContainer.Add(pageContainer);


        VisualElement sidebarContainer = new VisualElement();
        sidebarContainer.style.flexDirection = FlexDirection.Column;
        sidebarContainer.style.minWidth = new StyleLength(new Length(30, LengthUnit.Percent));
        sidebarContainer.style.maxWidth = new StyleLength(new Length(30, LengthUnit.Percent));
        sidebarContainer.style.width = new StyleLength(new Length(30, LengthUnit.Percent));
        sidebarContainer.style.maxHeight = new StyleLength(new Length(100, LengthUnit.Percent));
        sidebarContainer.style.minHeight = new StyleLength(new Length(100, LengthUnit.Percent));
        sidebarContainer.style.height = new StyleLength(new Length(100, LengthUnit.Percent));
        pageContainer.Add(sidebarContainer);
        
        sidebarScrollView = new ScrollView();
        sidebarScrollView.style.backgroundColor = new StyleColor(new Color(64f / 255f, 64f / 255f, 64f / 255f, 1));
        sidebarContainer.Add(sidebarScrollView);
        sectionButtons.Clear();
        foreach (var (group, items) in sidebarLayout)
        {
            if (group == null)
            {
                foreach (var name in items)
                    sidebarScrollView.Add(MakeSectionButton(name, 15));
            }
            else
            {
                var body = new VisualElement();
                body.style.flexDirection = FlexDirection.Column;

                sidebarScrollView.Add(MakeGroupHeader(group, body));
                foreach (var name in items)
                    body.Add(MakeSectionButton(name, 30));
                sidebarScrollView.Add(body);
            }
        }
        
        VisualElement contentContainer = new VisualElement();
        contentContainer.style.flexDirection = FlexDirection.Column;
        contentContainer.style.minHeight = new StyleLength(new Length(100, LengthUnit.Percent));
        contentContainer.style.maxHeight = new StyleLength(new Length(100, LengthUnit.Percent));
        contentContainer.style.height = new StyleLength(new Length(100, LengthUnit.Percent));
        contentContainer.style.maxWidth = new StyleLength(new Length(70, LengthUnit.Percent));
        contentContainer.style.minWidth = new StyleLength(new Length(70, LengthUnit.Percent));
        contentContainer.style.width = new StyleLength(new Length(70, LengthUnit.Percent));
        pageContainer.Add(contentContainer);
        ScrollView contentScrollView = new ScrollView(ScrollViewMode.Vertical);
        contentScrollView.style.flexDirection = FlexDirection.Column;
        contentScrollView.style.maxWidth = new StyleLength(new Length(100, LengthUnit.Percent));
        contentScrollView.style.overflow = Overflow.Hidden;
        contentScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        contentScrollViewContent = new VisualElement();
        contentScrollViewContent.style.flexDirection = FlexDirection.Column;
        contentScrollViewContent.style.paddingLeft = 16;
        contentScrollViewContent.style.paddingTop = 16;
        contentScrollViewContent.style.paddingRight = 16;
        contentScrollViewContent.style.paddingBottom = 16;
        contentScrollView.Add(contentScrollViewContent);
        contentContainer.Add(contentScrollView);
        CreateContentForSection(selectedSectionIndex);
        
        VisualElement bottomRow = new VisualElement();
        bottomRow.style.flexDirection = FlexDirection.Row;
        bottomRow.style.justifyContent = Justify.FlexEnd;
        bottomRow.style.minHeight = 74;
        bottomRow.style.maxHeight = 74;
        bottomRow.style.paddingLeft = 10;
        bottomRow.style.paddingRight = 10;
        bottomRow.style.paddingTop = 10;
        bottomRow.style.paddingBottom = 10;
        bottomRow.style.backgroundColor = new StyleColor(new Color(0.14f, 0.14f, 0.14f));
        
        Button closeButton = new Button();
        closeButton.text = "CLOSE";
        closeButton.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
        closeButton.style.unityTextAlign = TextAnchor.MiddleLeft;
        closeButton.style.width = 250;
        closeButton.style.minWidth = 250;
        closeButton.style.maxWidth = 250;
        closeButton.style.height = 50;
        closeButton.style.minHeight = 50;
        closeButton.style.maxHeight = 50;
        closeButton.style.paddingTop = 12;
        closeButton.style.paddingBottom = 12;
        closeButton.style.paddingLeft = 16;
        closeButton.style.paddingRight = 16;
        UITools.AddHoverEffectsForButton(closeButton);
        closeButton.RegisterCallback<ClickEvent>(QuickChatPlusSettingsCloseButtonClickHandler);
        
        bottomRow.Add(closeButton);
        mainContainer.Add(bottomRow);
        rootContainer.Add(mainContainer);
        rootContainer.visible = false;
        rootContainer.enabledSelf = false;
        MonoBehaviourSingleton<UIManager>.Instance.RootVisualElement.Add(rootContainer);
        return;

        static void QuickChatPlusSettingsCloseButtonClickHandler(ClickEvent evt)
        {
            Hide();
        }
    }

    public static void CreateContentForSection(int sectionIndex)
    {
        contentScrollViewContent.Clear(); // discard existing content
        Label contentSectionTitle = new Label(sections[sectionIndex]);
        contentSectionTitle.style.fontSize = 30;
        contentSectionTitle.style.color = Color.white;
        contentScrollViewContent.Add(contentSectionTitle);

        ChangingRoomHelper.ShowBaseFocus();

        switch (sections[sectionIndex])
        {
            case "Packs":
                PacksSection.CreateSection(contentScrollViewContent);
                break;
            case "Presets":
                PresetsSection.CreateSection(contentScrollViewContent);
                break;
            case "Players":
                PlayersSection.CreateSection(contentScrollViewContent);
                break;
            case "Skaters":
                SkaterSection.CreateSection(contentScrollViewContent);
                break;
            case "Sticks":
                SticksSection.CreateSection(contentScrollViewContent);
                break;
            case "Pucks":
                PucksSection.CreateSection(contentScrollViewContent);
                break;
            case "Arena":
                ArenaSection.CreateSection(contentScrollViewContent);
                break;
            case "About":
                AboutSection.CreateSection(contentScrollViewContent);
                break;
            case "Skybox":
                SkyboxSection.CreateSection(contentScrollViewContent);
                break;
            case "Goalies":
                GoaliesSection.CreateSection(contentScrollViewContent);
                break;
            case "Tapes":
                TapesSection.CreateSection(contentScrollViewContent);
                break;
            case "Puck FX":
                PuckFXSection.CreateSection(contentScrollViewContent);
                break;
            case "Shadows":
                ShadowsSection.CreateSection(contentScrollViewContent);
                break;
            case "Glossiness":
                GlossSection.CreateSection(contentScrollViewContent);
                break;
            case "Appearance":
                PlayerCustomizationSection.CreateSection(contentScrollViewContent);
                break;
            case "User Interface":
                UISection.CreateSection(contentScrollViewContent);
                break;
            case "Quality of Life":
                PlayerQoLSection.CreateSection(contentScrollViewContent);
                break;
            case "Extras":
                ExtrasSection.CreateSection(contentScrollViewContent);
                break;
            default:
                Label contentSectionDummyText = new Label("This section does not yet exist.");
                contentSectionDummyText.style.fontSize = 14;
                contentSectionDummyText.style.color = Color.white;
                contentSectionDummyText.style.marginTop = 20;
                contentScrollViewContent.Add(contentSectionDummyText);
                break;
        }
    }
    public static void UpdateToSection(int sectionIndex)
    {
        ChangingRoomHelper.Scan();
        int oldSectionIndex = selectedSectionIndex;
        selectedSectionIndex = sectionIndex;

        // Show minimap when on the User Interface tab so the user can preview changes
        // (only in-game — minimap doesn't exist in the main menu)
        if (!ChangingRoomHelper.IsInMainMenu())
        {
            var minimap = MonoBehaviourSingleton<UIManager>.Instance?.Minimap;
            if (minimap != null)
            {
                if (sections[sectionIndex] == "User Interface")
                    minimap.Show();
                else if (sections[oldSectionIndex] == "User Interface")
                    minimap.Hide();
            }
        }

        CreateContentForSection(sectionIndex);

        if (sectionButtons.TryGetValue(sections[oldSectionIndex], out var oldSectionButton))
            ApplySidebarButtonStyle(oldSectionButton, false);
        if (sectionButtons.TryGetValue(sections[sectionIndex], out var newSectionButton))
            ApplySidebarButtonStyle(newSectionButton, true);
    }

    public static void UpdateToSectionByName(string name)
    {
        int idx = Array.IndexOf(sections, name);
        if (idx >= 0) UpdateToSection(idx);
    }

    private static void ApplySidebarButtonStyle(Button button, bool selected)
    {
        button.style.backgroundColor = new StyleColor(selected
            ? new Color(0.7f, 0.7f, 0.7f)
            : new Color(0.25f, 0.25f, 0.25f));
        button.style.color = selected ? Color.black : Color.white;
    }

    private static Button MakeSectionButton(string name, int paddingLeft)
    {
        var button = new Button { name = $"{name}SidebarButton", text = name };
        button.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
        button.style.minWidth = new StyleLength(new Length(100, LengthUnit.Percent));
        button.style.maxWidth = new StyleLength(new Length(100, LengthUnit.Percent));
        button.style.minHeight = 50;
        button.style.maxHeight = 50;
        button.style.height = 50;
        button.style.unityTextAlign = TextAnchor.MiddleLeft;
        button.style.paddingLeft = paddingLeft;

        ApplySidebarButtonStyle(button, name == sections[selectedSectionIndex]);

        button.RegisterCallback<MouseEnterEvent>(_ =>
        {
            button.style.backgroundColor = Color.white;
            button.style.color = Color.black;
        });
        button.RegisterCallback<MouseLeaveEvent>(_ =>
            ApplySidebarButtonStyle(button, name == sections[selectedSectionIndex]));
        button.RegisterCallback<ClickEvent>(_ => UpdateToSectionByName(name));

        sectionButtons[name] = button;
        return button;
    }

    // Collapsible group header. Clicking toggles the body container's visibility.
    private static Button MakeGroupHeader(string group, VisualElement body)
    {
        var header = new Button { text = $"▾  {group}" };
        header.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
        header.style.minWidth = new StyleLength(new Length(100, LengthUnit.Percent));
        header.style.maxWidth = new StyleLength(new Length(100, LengthUnit.Percent));
        header.style.minHeight = 40;
        header.style.maxHeight = 40;
        header.style.height = 40;
        header.style.unityTextAlign = TextAnchor.MiddleLeft;
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.paddingLeft = 12;
        header.style.backgroundColor = new StyleColor(new Color(0.16f, 0.16f, 0.16f));
        header.style.color = new Color(0.8f, 0.8f, 0.8f);

        bool expanded = true;
        header.RegisterCallback<ClickEvent>(_ =>
        {
            expanded = !expanded;
            body.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            header.text = (expanded ? "▾  " : "▸  ") + group;
        });

        return header;
    }
    
    // Make it so that if Reskin menu is open, pressing Escape closes it
    [HarmonyPatch(typeof(UIManager), "OnPauseActionPerformed")]
    private static class UIManagerOnPauseActionPerformedPatch
    {
        [HarmonyPrefix]
        static bool Prefix(UIManager __instance, InputAction.CallbackContext context)
        {
            if (rootContainer == null) return true;

            if (rootContainer.visible || rootContainer.enabledSelf || rootContainer.style.display == DisplayStyle.Flex)
            {
                Hide();

                if (GlobalStateManager.UIState.Phase == UIPhase.Playing)
                {
                    MonoBehaviourSingleton<UIManager>.Instance.PauseMenu.Toggle();
                }

                return false;
            }

            return true;
        }
    }

    // NOTE: ReplayManagerController no longer has Event_OnGamePhaseChanged in b310.
    // The UIAnnouncements Show/Hide patches below handle keeping the cursor visible during game phase changes.

    // NOTE: UIAnnouncements.Hide is inherited from UIView and not overridden,
    // so Harmony can't patch it on UIAnnouncements directly. Only Show is overridden.

    [HarmonyPatch(typeof(UIAnnouncements), nameof(UIAnnouncements.Show))]
    private static class UIAnnouncementsShowPatch
    {
        [HarmonyPostfix]
        static void Postfix(UIAnnouncements __instance)
        {
            try
            {
                if (rootContainer == null) return;

                if (rootContainer.visible || rootContainer.enabledSelf || rootContainer.style.display == DisplayStyle.Flex)
                {
                    if (GlobalStateManager.UIState.Phase == UIPhase.Playing)
                    {
                        GlobalStateManager.SetUIState(new Dictionary<string, object> { { "isMouseRequired", true } });
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.LogError($"Error while handling UIAnnouncementsShowPatch postfix: {e}");
            }
        }
    }
}