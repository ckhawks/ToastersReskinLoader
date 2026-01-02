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
    public static string[] sections = new []{"Packs", "Sticks", "Tapes", "Skaters", "Goalies", "Pucks", "Arena",
        // "Full Arena",
        "Skybox", "About" };
    // "Sounds", "Other"
    public static int selectedSectionIndex = 0;

    public static void Show()
    {
        // If we're in main menu, hide main menu
        // If we're in game, hide pause menu
        
        // Plugin.Log($"Show reskin manager menu");
        if (rootContainer == null)
        {
            Create();
        }
        else
        {
            CreateContentForSection(selectedSectionIndex); // TODO this isn't the most optimized but it will retrigger things that should happen when open section
        }
        rootContainer.visible = true;
        rootContainer.enabledSelf = true;
        rootContainer.style.display = DisplayStyle.Flex;
        mainContainer.visible = true;
        mainContainer.enabledSelf = true;
        mainContainer.style.display = DisplayStyle.Flex;


        // Plugin.Log($"Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
        if (ChangingRoomHelper.IsInMainMenu())
        {
            // Plugin.Log($"here1");
            if (UIManager.Instance != null)
            {
                // Plugin.Log($"here2");
                UIManager.Instance.HideMainMenuComponents();
                // Plugin.Log($"here3");
                UIManager.Instance.isMouseActive = true;
                // Plugin.Log($"here4");
                Cursor.lockState = CursorLockMode.None;
                // Plugin.Log($"here5");
                Cursor.visible = true;
                // Plugin.Log($"here6");
            }

            ChangingRoomHelper.ShowBaseFocus();
        }
        else
        {
            if (UIPauseMenu.Instance != null)
            {
                UIPauseMenu.Instance.Hide();
            }

            if (UIGameState.Instance != null)
            {
                UIGameState.Instance.Hide();
            }

            if (UIMinimap.Instance != null)
            {
                UIMinimap.Instance.Hide();
            }
        }
        
        // Plugin.Log($"here8");
        if (UIManager.Instance != null)
        {
            UIManager.Instance.isMouseActive = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        // Plugin.Log($"Finish reskin manager menu");
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
        
        // Plugin.Log($"Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
        if (ChangingRoomHelper.IsInMainMenu())
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowMainMenuComponents();
            }

            ChangingRoomHelper.Unfocus();
            ChangingRoomHelper.UpdateStickToPersonalSelected();
        }
        else
        {
            if (UIPauseMenu.Instance != null)
            {
                UIPauseMenu.Instance.Show();
            }
            
            if (UIGameState.Instance != null)
            {
                UIGameState.Instance.Show();
            }

            if (UIMinimap.Instance != null)
            {
                UIMinimap.Instance.Show();
            }
        }
    }

    public static void Create()
    {
        // Plugin.Log($"Create reskin manager menu 1");
        rootContainer = new VisualElement();
        rootContainer.style.flexDirection = FlexDirection.Row;
        rootContainer.style.height = new StyleLength(new Length(100, LengthUnit.Percent));
        rootContainer.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
        rootContainer.style.alignItems = Align.Center;
        rootContainer.style.justifyContent = Justify.FlexStart;
        rootContainer.pickingMode = PickingMode.Ignore; // TODO maybe remove this
        
        // Plugin.Log($"Create reskin manager menu 2");
        mainContainer = new VisualElement();
        mainContainer.style.backgroundColor = new StyleColor(new Color(0.196f, 0.196f,0.196f, 1)); // TODO took this off
        mainContainer.style.marginLeft = new StyleLength(new Length(40));
        mainContainer.style.maxWidth = new StyleLength(new Length(45, LengthUnit.Percent));
        mainContainer.style.minWidth = new StyleLength(new Length(45, LengthUnit.Percent));
        mainContainer.style.maxHeight = new StyleLength(new Length(75, LengthUnit.Percent));
        mainContainer.style.minHeight = new StyleLength(new Length(75, LengthUnit.Percent));
        mainContainer.pickingMode = PickingMode.Position; // TODO maybe remove this
        
        // Plugin.Log($"Create reskin manager menu 3");
        VisualElement titleContainer = new VisualElement();
        titleContainer.style.flexDirection = FlexDirection.Row;
        // titleContainer.style.flexGrow = 10f;
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
        
        // Plugin.Log($"Create reskin manager menu 4");
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
        reloadButton.RegisterCallback<MouseEnterEvent>(new EventCallback<MouseEnterEvent>((evt) =>
        {
            reloadButton.style.backgroundColor = Color.white;
            reloadButton.style.color = Color.black;
        }));
        reloadButton.RegisterCallback<MouseLeaveEvent>(new EventCallback<MouseLeaveEvent>((evt) =>
        {
            reloadButton.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
            reloadButton.style.color = Color.white;
        }));
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
        
        // Plugin.Log($"Create reskin manager menu 5");
        VisualElement pageContainer = new VisualElement();
        pageContainer.style.flexDirection = FlexDirection.Row;
        pageContainer.style.maxWidth = new StyleLength(new Length(100, LengthUnit.Percent));
        pageContainer.style.minWidth = new StyleLength(new Length(100, LengthUnit.Percent));
        pageContainer.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
        pageContainer.style.maxHeight = new StyleLength(new Length(76, LengthUnit.Percent)); // TODO i made this 75->100
        pageContainer.style.minHeight = new StyleLength(new Length(76, LengthUnit.Percent));
        pageContainer.style.height = new StyleLength(new Length(76, LengthUnit.Percent));
        mainContainer.Add(pageContainer);
        
        
        // Plugin.Log($"Create reskin manager menu 6");
        VisualElement sidebarContainer = new VisualElement();
        sidebarContainer.style.flexDirection = FlexDirection.Column;
        sidebarContainer.style.minWidth = new StyleLength(new Length(30, LengthUnit.Percent));
        sidebarContainer.style.maxWidth = new StyleLength(new Length(30, LengthUnit.Percent));
        sidebarContainer.style.width = new StyleLength(new Length(30, LengthUnit.Percent));
        sidebarContainer.style.maxHeight = new StyleLength(new Length(100, LengthUnit.Percent));
        sidebarContainer.style.minHeight = new StyleLength(new Length(100, LengthUnit.Percent));
        sidebarContainer.style.height = new StyleLength(new Length(100, LengthUnit.Percent));
        pageContainer.Add(sidebarContainer);
        
        // Plugin.Log($"Create reskin manager menu 7");
        sidebarScrollView = new ScrollView();
        sidebarScrollView.style.backgroundColor = new StyleColor(new Color(64f / 255f, 64f / 255f, 64f / 255f, 1));
        sidebarContainer.Add(sidebarScrollView);
        for (int i = 0; i < sections.Length; i++)
        {
            Button button = new Button();
            button.name = $"{sections[i]}SidebarButton";
            button.text = sections[i];
            button.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            button.style.minWidth = new StyleLength(new Length(100, LengthUnit.Percent));
            button.style.maxWidth = new StyleLength(new Length(100, LengthUnit.Percent));
            button.style.minHeight = 50;
            button.style.maxHeight = 50;
            button.style.height = 50;
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            button.style.paddingLeft = 15;
            button.RegisterCallback<MouseEnterEvent>(new EventCallback<MouseEnterEvent>((evt) =>
            {
                button.style.backgroundColor = Color.white;
                button.style.color = Color.black;
            }));
            var i1 = i;
            button.RegisterCallback<MouseLeaveEvent>(new EventCallback<MouseLeaveEvent>((evt) =>
            {
                if (i1 == selectedSectionIndex)
                {
                    button.style.backgroundColor = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
                    button.style.color = Color.black;
                }
                else
                {
                    button.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
                    button.style.color = Color.white;
                }
            }));
            
            button.RegisterCallback<ClickEvent>(new EventCallback<ClickEvent>(SidebarSectionClickHandler));
            void SidebarSectionClickHandler(ClickEvent evt)
            {
                // Plugin.Log($"Sidebar section #{i1} {sections[i1]} clicked!");
                // TODO make this change sections
                UpdateToSection(i1);
            }

            // Start with the first section selected
            if (i == selectedSectionIndex)
            {
                button.style.backgroundColor = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
                button.style.color = Color.black;
            }
            sidebarScrollView.Add(button);
        }
        
        // Plugin.Log($"Create reskin manager menu 8");
        VisualElement contentContainer = new VisualElement();
        contentContainer.style.flexDirection = FlexDirection.Column;
        // contentContainer.style.paddingLeft = 16;
        // contentContainer.style.paddingTop = 16;
        // contentContainer.style.paddingRight = 16;
        // contentContainer.style.paddingBottom = 16;
        contentContainer.style.minHeight = new StyleLength(new Length(100, LengthUnit.Percent));
        contentContainer.style.maxHeight = new StyleLength(new Length(100, LengthUnit.Percent));
        contentContainer.style.height = new StyleLength(new Length(100, LengthUnit.Percent));
        contentContainer.style.maxWidth = new StyleLength(new Length(70, LengthUnit.Percent));
        contentContainer.style.minWidth = new StyleLength(new Length(70, LengthUnit.Percent));
        contentContainer.style.width = new StyleLength(new Length(70, LengthUnit.Percent));
        pageContainer.Add(contentContainer);
        ScrollView contentScrollView = new ScrollView();
        // contentScrollView.style.paddingLeft = 16;
        // contentScrollView.style.paddingTop = 16;
        // contentScrollView.style.paddingRight = 16;
        // contentScrollView.style.paddingBottom = 16;
        contentScrollView.style.flexDirection = FlexDirection.Column;
        contentScrollView.style.maxWidth = new StyleLength(new Length(100, LengthUnit.Percent));
        contentScrollView.style.overflow = Overflow.Hidden;
        contentScrollViewContent = new VisualElement();
        contentScrollViewContent.style.flexDirection = FlexDirection.Column;
        contentScrollViewContent.style.paddingLeft = 16;
        contentScrollViewContent.style.paddingTop = 16;
        contentScrollViewContent.style.paddingRight = 16;
        contentScrollViewContent.style.paddingBottom = 16;
        contentScrollView.Add(contentScrollViewContent);
        // contentScrollView.style.minHeight = new StyleLength(new Length(100, LengthUnit.Percent));
        // contentScrollView.style.maxHeight = new StyleLength(new Length(100, LengthUnit.Percent));
        // contentScrollView.style.height = new StyleLength(new Length(100, LengthUnit.Percent));
        // contentScrollView.style.maxWidth = new StyleLength(new Length(100, LengthUnit.Percent));
        // contentScrollView.style.minWidth = new StyleLength(new Length(100, LengthUnit.Percent));
        // contentScrollView.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
        contentContainer.Add(contentScrollView);
        CreateContentForSection(selectedSectionIndex);
        
        // Plugin.Log($"Create reskin manager menu 9");
        VisualElement bottomRow = new VisualElement();
        bottomRow.style.flexDirection = FlexDirection.Row;
        bottomRow.style.justifyContent = Justify.FlexEnd;
        // bottomRow.style.marginTop = 8;
        bottomRow.style.minHeight = 74;
        bottomRow.style.maxHeight = 74;
        bottomRow.style.paddingLeft = 10;
        bottomRow.style.paddingRight = 10;
        bottomRow.style.paddingTop = 10;
        bottomRow.style.paddingBottom = 10;
        bottomRow.style.backgroundColor = new StyleColor(new Color(0.14f, 0.14f, 0.14f));
        
        // Plugin.Log($"Create reskin manager menu 10");
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
        closeButton.RegisterCallback<MouseEnterEvent>(new EventCallback<MouseEnterEvent>((evt) =>
        {
            closeButton.style.backgroundColor = Color.white;
            closeButton.style.color = Color.black;
        }));
        closeButton.RegisterCallback<MouseLeaveEvent>(new EventCallback<MouseLeaveEvent>((evt) =>
        {
            closeButton.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
            closeButton.style.color = Color.white;
        }));
        closeButton.RegisterCallback<ClickEvent>(QuickChatPlusSettingsCloseButtonClickHandler);
        // Plugin.Log($"Create reskin manager menu 11");
        
        bottomRow.Add(closeButton);
        mainContainer.Add(bottomRow);
        rootContainer.Add(mainContainer);
        rootContainer.visible = false;
        rootContainer.enabledSelf = false;
        ReskinMenuAccessButtons.mainMenuSettingsButton.parent.parent.Add(rootContainer);
        // Plugin.Log($"Create reskin manager menu 12");
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
        ChangingRoomHelper.UpdateStickToPersonalSelected();

        switch (sections[sectionIndex])
        {
            case "Packs":
                PacksSection.CreateSection(contentScrollViewContent);
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
            // case "Full Arena":
            //     FullArenaSection.CreateSection(contentScrollViewContent);
            //     break;
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
        CreateContentForSection(sectionIndex);

        List<VisualElement> sidebarSectionButtons =
            sidebarScrollView.Children().ToList();

        // Plugin.Log($"New section: {selectedSectionIndex}, old section: {oldSectionIndex}");

        // for (int i = 0; i < sidebarSectionButtons.Count; i++)
        // {
        //     Plugin.Log($"{i}: {sidebarSectionButtons[i].name}");
        // }
        // for some reason, this is not casting properly and thus nothing below this is running
        Button oldSectionButton = sidebarSectionButtons[oldSectionIndex] as Button;
        oldSectionButton.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
        oldSectionButton.style.color = Color.white;
        Button newSectionButton = sidebarSectionButtons[sectionIndex] as Button;
        newSectionButton.style.backgroundColor = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
        newSectionButton.style.color = Color.black;
        // Plugin.Log($"");
    }
    
    // Make it so that if Reskin menu is open, pressing Escape closes it
    [HarmonyPatch(typeof(UIManagerInputs), "OnPauseActionPerformed")]
    private static class UIManagerInputsOnPauseActionPerformedPatch
    {
        [HarmonyPrefix]
        static bool Prefix(UIManagerInputs __instance, InputAction.CallbackContext context)
        {
            if (rootContainer == null) return true;

            if (rootContainer.visible || rootContainer.enabledSelf || rootContainer.style.display == DisplayStyle.Flex)
            {
                Hide();

                if (UIManager.Instance.UIState == UIState.Play)
                {
                    UIManager.Instance.PauseMenu.Toggle();
                }
                
                return false;
            }

            return true;
        }
    }

    // GET THAT FUCKING SCOREBOARD OFF
    // [HarmonyPatch(typeof(ScoreboardController), "Event_OnGamePhaseChanged")]
    // private static class ScoreboardControllerOnGamePhaseChangedPatch
    // {
    //     [HarmonyPrefix]
    //     static bool Prefix(ScoreboardController __instance, Dictionary<string, object> message)
    //     {
    //         try
    //         {
    //             if (ReskinProfileManager.currentProfile.scoreboardEnabled)
    //             {
    //                 // ArenaSwapper.UpdateScoreboardState();
    //             }
    //             else
    //             {
    //                 ArenaSwapper.UpdateScoreboardState();
    //                 return false;
    //             }
    //             
    //         }
    //         catch (Exception e)
    //         {
    //             Plugin.LogError($"Error while handling ScoreboardController OnGamePhaseChanged postfix: {e}");
    //         }
    //
    //         return true;
    //     }
    // }
    
    // If the game phase changes, keep the mouse visible (make it visible again to counteract other things)
    [HarmonyPatch(typeof(ReplayManagerController), "Event_OnGamePhaseChanged" )]
    private static class ReplayManagerControllerOnGamePhaseChangedPatch
    {
        [HarmonyPostfix]
        static void Postfix(ReplayManagerController __instance, Dictionary<string, object> message)
        {
            try
            {
                if (rootContainer == null) return;
            
                if (rootContainer.visible || rootContainer.enabledSelf || rootContainer.style.display == DisplayStyle.Flex)
                {
                    if (UIManager.Instance.UIState == UIState.Play)
                    {
                        UIManager.Instance.isMouseActive = true;
                        Cursor.lockState = CursorLockMode.None;
                        Cursor.visible = true;
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.LogError($"Error while handling ReplayManagerController OnGamePhaseChanged postfix: {e}");
            }
        }
    }

    [HarmonyPatch(typeof(UIAnnouncement), nameof(UIAnnouncement.Hide))]
    private static class UIAnnouncementHidePatch
    {
        [HarmonyPostfix]
        static void Postfix(UIAnnouncement __instance)
        {
            try
            {
                if (rootContainer == null) return;
            
                if (rootContainer.visible || rootContainer.enabledSelf || rootContainer.style.display == DisplayStyle.Flex)
                {
                    if (UIManager.Instance.UIState == UIState.Play)
                    {
                        UIManager.Instance.isMouseActive = true;
                        Cursor.lockState = CursorLockMode.None;
                        Cursor.visible = true;
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.LogError($"Error while handling UIAnnouncementHidePatch postfix: {e}");
            }
        }
    }
    
    [HarmonyPatch(typeof(UIAnnouncement), nameof(UIAnnouncement.Show))]
    private static class UIAnnouncementShowPatch
    {
        [HarmonyPostfix]
        static void Postfix(UIAnnouncement __instance)
        {
            try
            {
                if (rootContainer == null) return;
            
                if (rootContainer.visible || rootContainer.enabledSelf || rootContainer.style.display == DisplayStyle.Flex)
                {
                    if (UIManager.Instance.UIState == UIState.Play)
                    {
                        UIManager.Instance.isMouseActive = true;
                        Cursor.lockState = CursorLockMode.None;
                        Cursor.visible = true;
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.LogError($"Error while handling UIAnnouncementShowPatch postfix: {e}");
            }
        }
    }
}