using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.ui;

public static class ReskinMenuAccessButtons
{
    public static UIMainMenu mainMenu;
    public static Button mainMenuSettingsButton;
    public static Button pauseMenuSettingsButton;

    static readonly FieldInfo _mainMenuSettingsButtonField = typeof(UIMainMenu)
        .GetField("settingsButton",
            BindingFlags.Instance | BindingFlags.NonPublic);

    static readonly FieldInfo _mainMenuExitGameButtonField = typeof(UIMainMenu)
        .GetField("exitGameButton",
            BindingFlags.Instance | BindingFlags.NonPublic);

    static readonly FieldInfo _pauseMenuSettingsButtonField = typeof(UIPauseMenu)
        .GetField("settingsButton",
            BindingFlags.Instance | BindingFlags.NonPublic);

    private static void AddReskinMenuButtonToPauseMenu(UIPauseMenu pauseMenu)
    {
        VisualElement containerVisualElement = pauseMenuSettingsButton.parent;

        if (containerVisualElement == null)
        {
            Plugin.LogError("Container VisualElement not found (parent of settingsButton missing)!");
            return;
        }

        Button reskinMenuButton = CreateReskinMenuButton(pauseMenuSettingsButton);
        containerVisualElement.Insert(1, reskinMenuButton);
    }

    private static void AddReskinMenuButtonToMainMenu(UIMainMenu mainMenu)
    {
        // Follow QuickChatPlus pattern: find button container from a known button
        Button exitGameButton = (Button)_mainMenuExitGameButtonField?.GetValue(mainMenu);
        VisualElement containerVisualElement = mainMenuSettingsButton.parent;

        if (containerVisualElement == null)
        {
            Plugin.LogError("Container VisualElement not found (parent of settingsButton missing)!");
            return;
        }

        Button reskinMenuButton = CreateReskinMenuButton(mainMenuSettingsButton);

        // Insert before exit game button if possible
        if (exitGameButton != null)
        {
            int exitIndex = containerVisualElement.IndexOf(exitGameButton);
            if (exitIndex >= 0)
            {
                containerVisualElement.Insert(exitIndex, reskinMenuButton);
                return;
            }
        }

        // Fallback: insert after settings
        int settingsIndex = containerVisualElement.IndexOf(mainMenuSettingsButton);
        if (settingsIndex >= 0)
        {
            containerVisualElement.Insert(settingsIndex + 1, reskinMenuButton);
        }
        else
        {
            containerVisualElement.Add(reskinMenuButton);
        }
    }

    private static Button CreateReskinMenuButton(Button referenceButton)
    {
        Button button = new Button();
        button.text = "RESKIN MANAGER";

        // Copy USS classes from existing button to match the stylesheet
        foreach (string cls in referenceButton.GetClasses())
        {
            button.AddToClassList(cls);
        }

        button.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));

        UITools.AddHoverEffectsForButton(button);
        button.RegisterCallback<ClickEvent>(MainMenuOpenReskinManagerClickHandler);

        return button;

        void MainMenuOpenReskinManagerClickHandler(ClickEvent evt)
        {
            ReskinMenu.Show();
        }
    }

    public static void Setup()
    {
        Plugin.Log($"ReskinMenuAccessButtons::Setup()");
        var uiManager = MonoBehaviourSingleton<UIManager>.Instance;
        mainMenu = uiManager.MainMenu;
        ReskinMenu.uiMainMenu = mainMenu;

        LocateReferenceButtons();
        AddReskinMenuButtonToPauseMenu(uiManager.PauseMenu);
        AddReskinMenuButtonToMainMenu(mainMenu);
    }

    private static void LocateReferenceButtons()
    {
        var uiManager = MonoBehaviourSingleton<UIManager>.Instance;
        mainMenuSettingsButton = (Button)_mainMenuSettingsButtonField.GetValue(uiManager.MainMenu);
        Plugin.Log($"Located main menu settings button: {mainMenuSettingsButton}");
        pauseMenuSettingsButton = (Button)_pauseMenuSettingsButtonField.GetValue(uiManager.PauseMenu);
        Plugin.Log($"Located pause menu settings button: {pauseMenuSettingsButton}");
    }
}
