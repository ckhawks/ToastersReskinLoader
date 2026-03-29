using System.Collections.Generic;
using ToasterReskinLoader.api;
using ToasterReskinLoader.swappers;
using ToasterReskinLoader.ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.ui.sections;

public static class PlayerCustomizationSection
{
    private static int selectedBodyTypeIndex = 0;
    private static Color selectedSkinTone = GenderSwapper.SKIN_TONES[0];
    private static Color selectedHairColor = GenderSwapper.HAIR_COLORS[0];
    private static int selectedHatId = 0;
    private static bool subscribedToLoad;

    /// <summary>Whether the local player has selected body type 2 (female model).</summary>
    public static bool IsFemaleBodyType => selectedBodyTypeIndex == 1;

    /// <summary>The local player's selected hat ID (-1 = none).</summary>
    public static int SelectedHatId => selectedHatId;

    private static readonly List<string> BODY_TYPE_CHOICES = new List<string> { "Body Type 1", "Body Type 2" };

    // Hat choices are driven by HatSwapper.AllHats

    /// <summary>
    /// Subscribe to the server appearance load event (called once).
    /// Updates static state and applies to locker room when data arrives.
    /// </summary>
    public static void SubscribeToServerLoad()
    {
        if (subscribedToLoad) return;
        subscribedToLoad = true;

        AppearanceAPI.OnLocalAppearanceLoaded += data =>
        {
            selectedBodyTypeIndex = data.bodyType;
            selectedSkinTone = data.skinTone;
            selectedHairColor = data.hairColor;
            selectedHatId = data.hatId;
            Plugin.Log($"[Appearance] Loaded from server: bodyType={data.bodyType}, skin=({data.skinTone.r:F2},{data.skinTone.g:F2},{data.skinTone.b:F2}), hair=({data.hairColor.r:F2},{data.hairColor.g:F2},{data.hairColor.b:F2})");

            // Apply to locker room visuals only — don't POST back what we just loaded
            ApplyToLockerRoom(syncToServer: false);
        };

        // Rebuild the Appearance panel when unlocks change (XP bar + hat dropdown)
        int lastKnownLevel = AppearanceAPI.PlayerLevel;
        AppearanceAPI.OnUnlocksChanged += () =>
        {
            int currentLevel = AppearanceAPI.PlayerLevel;
            if (currentLevel != lastKnownLevel && lastKnownLevel > 0)
            {
                lastKnownLevel = currentLevel;
                if (ReskinMenu.rootContainer != null && ReskinMenu.rootContainer.visible)
                    ReskinMenu.Hide();
                return;
            }
            lastKnownLevel = currentLevel;

            if (ReskinMenu.sections[ReskinMenu.selectedSectionIndex] == "Appearance")
                ReskinMenu.CreateContentForSection(ReskinMenu.selectedSectionIndex);
        };
    }

    // Beard: -1 = none, 1536-1540
    private static readonly List<string> BEARD_CHOICES = new List<string>
    {
        "None", "Full", "Chin Curtain", "Goatee", "Mutton Chops", "Spade"
    };
    private static readonly int[] BEARD_IDS = { -1, 1536, 1537, 1538, 1539, 1540 };

    // Mustache: -1 = none, 1024-1030 (1028 "Toothbrush" intentionally excluded)
    private static readonly List<string> MUSTACHE_CHOICES = new List<string>
    {
        "None", "Chevron", "Lampshade", "Pencil", "Sheriff", "Walrus", "HQM"
    };
    private static readonly int[] MUSTACHE_IDS = { -1, 1024, 1025, 1026, 1027, 1029, 1030 };

    public static void CreateSection(VisualElement contentScrollViewContent)
    {
        bool inMenu = ChangingRoomHelper.IsInMainMenu();
        if (inMenu)
            ChangingRoomHelper.ShowBody();

        Label description = new Label();
        description.text = inMenu
            ? "Customize your player's appearance. These settings are synced to other players."
            : "Appearance customization can only be changed from the main menu.";
        description.style.fontSize = 14;
        description.style.color = new Color(0.7f, 0.7f, 0.7f);
        description.style.whiteSpace = WhiteSpace.Normal;
        description.style.marginTop = 8;
        description.style.marginBottom = 12;
        contentScrollViewContent.Add(description);

        // -- Backend warning --
        if (AppearanceAPI.BackendReachable == false)
        {
            Label backendWarning = new Label("Unable to reach appearance server — customizations may not save or load.");
            backendWarning.style.fontSize = 13;
            backendWarning.style.color = new Color(1f, 0.35f, 0.35f);
            backendWarning.style.whiteSpace = WhiteSpace.Normal;
            backendWarning.style.marginBottom = 8;
            contentScrollViewContent.Add(backendWarning);
        }

        // -- XP / Level Bar --
        AddXpBar(contentScrollViewContent);

        // Controls container — disabled when in-game
        VisualElement controlsContainer = new VisualElement();
        if (!inMenu)
        {
            controlsContainer.SetEnabled(false);
            controlsContainer.style.opacity = 0.5f;
        }
        contentScrollViewContent.Add(controlsContainer);

        // -- Hat --
        AddSectionLabel(controlsContainer, "Hat");

        var hatNames = new List<string>();
        foreach (var h in HatSwapper.AllHats)
        {
            if (AppearanceAPI.IsHatUnlocked(h.Id))
                hatNames.Add(h.Name);
        }

        // If current hat isn't unlocked, fall back to "None"
        string currentHat = AppearanceAPI.IsHatUnlocked(selectedHatId)
            ? HatSwapper.GetHatName(selectedHatId)
            : "None";

        VisualElement hatRow = UITools.CreateConfigurationRow();
        hatRow.Add(UITools.CreateConfigurationLabel("Hat"));
        var hatDropdown = UITools.CreateStringDropdownField(hatNames, currentHat);
        hatDropdown.RegisterCallback<ChangeEvent<string>>(evt =>
        {
            foreach (var h in HatSwapper.AllHats)
            {
                if (h.Name == evt.newValue)
                {
                    selectedHatId = h.Id;
                    ApplyToLockerRoom();
                    break;
                }
            }
        });
        hatRow.Add(hatDropdown);
        controlsContainer.Add(hatRow);

        // -- Body Type --
        AddSectionLabel(controlsContainer, "Body Type");

        VisualElement bodyRow = UITools.CreateConfigurationRow();
        bodyRow.Add(UITools.CreateConfigurationLabel("Body Model"));
        var bodyDropdown = UITools.CreateStringDropdownField(BODY_TYPE_CHOICES, BODY_TYPE_CHOICES[selectedBodyTypeIndex]);
        bodyDropdown.RegisterCallback<ChangeEvent<string>>(evt =>
        {
            selectedBodyTypeIndex = evt.newValue == "Body Type 2" ? 1 : 0;
            ApplyToLockerRoom();
        });
        bodyRow.Add(bodyDropdown);
        controlsContainer.Add(bodyRow);

        // -- Skin Tone --
        AddSectionLabel(controlsContainer, "Skin Tone");
        AddColorPicker(controlsContainer, "Custom Skin Tone", GenderSwapper.SKIN_TONES, selectedSkinTone,
            color => { selectedSkinTone = color; ApplyToLockerRoom(); });

        // -- Facial Hair Style --
        AddSectionLabel(controlsContainer, "Facial Hair Style");

        // Get current game settings for defaults
        int currentBeardId = SettingsManager.BeardID;
        int currentMustacheId = SettingsManager.MustacheID;
        string currentBeard = GetNameFromId(currentBeardId, BEARD_IDS, BEARD_CHOICES);
        string currentMustache = GetNameFromId(currentMustacheId, MUSTACHE_IDS, MUSTACHE_CHOICES);

        VisualElement beardRow = UITools.CreateConfigurationRow();
        beardRow.Add(UITools.CreateConfigurationLabel("Beard"));
        var beardDropdown = UITools.CreateStringDropdownField(BEARD_CHOICES, currentBeard);
        beardDropdown.RegisterCallback<ChangeEvent<string>>(evt =>
        {
            int idx = BEARD_CHOICES.IndexOf(evt.newValue);
            if (idx >= 0)
            {
                SettingsManager.UpdateBeardID(BEARD_IDS[idx]);
                ChangingRoomHelper.SetBeardID(BEARD_IDS[idx]);
                ApplyToLockerRoom();
            }
        });
        beardRow.Add(beardDropdown);
        controlsContainer.Add(beardRow);

        VisualElement mustacheRow = UITools.CreateConfigurationRow();
        mustacheRow.Add(UITools.CreateConfigurationLabel("Mustache"));
        var mustacheDropdown = UITools.CreateStringDropdownField(MUSTACHE_CHOICES, currentMustache);
        mustacheDropdown.RegisterCallback<ChangeEvent<string>>(evt =>
        {
            int idx = MUSTACHE_CHOICES.IndexOf(evt.newValue);
            if (idx >= 0)
            {
                SettingsManager.UpdateMustacheID(MUSTACHE_IDS[idx]);
                ChangingRoomHelper.SetMustacheID(MUSTACHE_IDS[idx]);
                ApplyToLockerRoom();
            }
        });
        mustacheRow.Add(mustacheDropdown);
        controlsContainer.Add(mustacheRow);

        // -- Facial Hair Color --
        AddSectionLabel(controlsContainer, "Facial Hair Color");
        AddColorPicker(controlsContainer, "Custom Hair Color", GenderSwapper.HAIR_COLORS, selectedHairColor,
            color => { selectedHairColor = color; ApplyToLockerRoom(); });

        // -- Hair Style (TODO) --
        AddSectionLabel(controlsContainer, "Hair Style");
        Label hairTodo = new Label("Coming soon - hair style customization is not yet available.");
        hairTodo.style.fontSize = 14;
        hairTodo.style.color = new Color(0.5f, 0.5f, 0.5f);
        hairTodo.style.whiteSpace = WhiteSpace.Normal;
        hairTodo.style.marginTop = 4;
        controlsContainer.Add(hairTodo);

        // Initial apply to locker room preview
        if (inMenu)
            ApplyToLockerRoom();

        // -- Display Settings (always enabled, even in-game) --
        AddSectionLabel(contentScrollViewContent, "Display Settings");

        var dependentControls = new System.Collections.Generic.List<VisualElement>();

        VisualElement showPersonalizationRow = UITools.CreateConfigurationRow();
        showPersonalizationRow.Add(UITools.CreateConfigurationLabel("Show Personalization"));
        Toggle showPersonalizationToggle = UITools.CreateConfigurationCheckbox(Plugin.modSettings.ShowPersonalization);
        showPersonalizationToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
        {
            Plugin.modSettings.ShowPersonalization = evt.newValue;
            Plugin.modSettings.Save();
            UITools.UpdateDependentControlsState(dependentControls, evt.newValue);
            if (!evt.newValue)
                HatSwapper.ClearHats();
            AppearanceAPI.ReapplyAllAppearances();
            // Re-apply hat to locker room preview when toggling back on
            if (evt.newValue && inMenu)
                ApplyToLockerRoom(syncToServer: false);
        });
        showPersonalizationRow.Add(showPersonalizationToggle);
        contentScrollViewContent.Add(showPersonalizationRow);

        VisualElement showHatsRow = UITools.CreateConfigurationRow();
        showHatsRow.Add(UITools.CreateConfigurationLabel("Show Other Players' Hats"));
        Toggle showHatsToggle = UITools.CreateConfigurationCheckbox(Plugin.modSettings.ShowOtherPlayersHats);
        showHatsToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
        {
            Plugin.modSettings.ShowOtherPlayersHats = evt.newValue;
            Plugin.modSettings.Save();
            if (!evt.newValue)
                HatSwapper.ClearHats();
            AppearanceAPI.ReapplyAllAppearances();
        });
        showHatsRow.Add(showHatsToggle);
        contentScrollViewContent.Add(showHatsRow);
        dependentControls.Add(showHatsRow);

        VisualElement showSkinRow = UITools.CreateConfigurationRow();
        showSkinRow.Add(UITools.CreateConfigurationLabel("Show Non-Natural Skin Tones"));
        Toggle showSkinToggle = UITools.CreateConfigurationCheckbox(Plugin.modSettings.ShowNonNaturalSkinTones);
        showSkinToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
        {
            Plugin.modSettings.ShowNonNaturalSkinTones = evt.newValue;
            Plugin.modSettings.Save();
            AppearanceAPI.ReapplyAllAppearances();
        });
        showSkinRow.Add(showSkinToggle);
        contentScrollViewContent.Add(showSkinRow);
        dependentControls.Add(showSkinRow);

        UITools.UpdateDependentControlsState(dependentControls, Plugin.modSettings.ShowPersonalization);
    }

    private static string GetNameFromId(int id, int[] ids, List<string> names)
    {
        for (int i = 0; i < ids.Length; i++)
            if (ids[i] == id) return names[i];
        return "None";
    }

    private static void AddSectionLabel(VisualElement parent, string text)
    {
        Label label = new Label($"<b>{text}</b>");
        label.style.fontSize = 16;
        label.style.color = Color.white;
        label.style.marginTop = 16;
        label.style.marginBottom = 4;
        parent.Add(label);
    }

    private static void AddXpBar(VisualElement parent)
    {
        int level = AppearanceAPI.PlayerLevel;
        int xpToNext = AppearanceAPI.XpToNextLevel;
        int levelTotal = AppearanceAPI.LevelXpTotal;

        // Progress within the current level:
        // levelTotal is the full XP span for this level (snapshotted when the level changed).
        // xpToNext is how much XP remains. So xpEarned = levelTotal - xpToNext.
        int xpEarned = levelTotal > 0 ? levelTotal - xpToNext : 0;
        float progress = levelTotal > 0 ? Mathf.Clamp01((float)xpEarned / levelTotal) : 0f;

        // Level label
        Label levelLabel = new Label($"<b>Level {level}</b>");
        levelLabel.style.fontSize = 18;
        levelLabel.style.color = Color.white;
        levelLabel.style.marginBottom = 4;
        parent.Add(levelLabel);

        // XP bar container (background)
        VisualElement barBg = new VisualElement();
        barBg.style.height = 20;
        barBg.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
        barBg.style.borderTopLeftRadius = 4;
        barBg.style.borderTopRightRadius = 4;
        barBg.style.borderBottomLeftRadius = 4;
        barBg.style.borderBottomRightRadius = 4;
        barBg.style.marginBottom = 2;

        // XP bar fill
        VisualElement barFill = new VisualElement();
        barFill.style.height = new StyleLength(new Length(100, LengthUnit.Percent));
        barFill.style.width = new StyleLength(new Length(Mathf.Clamp01(progress) * 100f, LengthUnit.Percent));
        barFill.style.backgroundColor = new StyleColor(new Color(0.3f, 0.6f, 1f));
        barFill.style.borderTopLeftRadius = 4;
        barFill.style.borderTopRightRadius = 4;
        barFill.style.borderBottomLeftRadius = 4;
        barFill.style.borderBottomRightRadius = 4;
        barBg.Add(barFill);
        parent.Add(barBg);

        // XP text
        string xpText = levelTotal > 0
            ? $"{xpEarned} / {levelTotal} XP to level {level + 1}"
            : $"{AppearanceAPI.PlayerXP} XP";
        Label xpLabel = new Label(xpText);
        xpLabel.style.fontSize = 12;
        xpLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
        xpLabel.style.marginBottom = 8;
        parent.Add(xpLabel);
    }

    /// <summary>
    /// Creates a combined color picker: preset swatches with selection indicator + RGB sliders.
    /// Clicking a swatch updates the sliders; dragging sliders updates the preview.
    /// </summary>
    private static void AddColorPicker(VisualElement parent, string label, Color[] presets, Color initialColor, System.Action<Color> onColorChanged)
    {
        var swatches = new List<Button>();
        var selectedColor = initialColor;

        // Swatch row
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;
        row.style.marginBottom = 8;

        // We'll create the color row first (but add it after swatches) so swatches can reference the sliders
        VisualElement colorRowContainer = new VisualElement();

        void UpdateSwatchHighlights()
        {
            var activeBorder = Color.white;
            var defaultBorder = new Color(0.4f, 0.4f, 0.4f);
            for (int j = 0; j < swatches.Count; j++)
            {
                bool isSelected = ColorsApproxEqual(presets[j], selectedColor);
                var border = isSelected ? activeBorder : defaultBorder;
                swatches[j].style.borderTopColor = border;
                swatches[j].style.borderBottomColor = border;
                swatches[j].style.borderLeftColor = border;
                swatches[j].style.borderRightColor = border;
            }
        }

        void RebuildSliders()
        {
            colorRowContainer.Clear();
            colorRowContainer.Add(UITools.CreateColorConfigurationRow(
                label, selectedColor, false,
                color =>
                {
                    selectedColor = color;
                    onColorChanged(color);
                    UpdateSwatchHighlights();
                },
                null));
        }

        for (int i = 0; i < presets.Length; i++)
        {
            Color c = presets[i];
            Button swatch = new Button();
            swatch.style.width = 40;
            swatch.style.height = 40;
            swatch.style.marginRight = 4;
            swatch.style.marginBottom = 4;
            swatch.style.backgroundColor = c;
            swatch.style.borderTopWidth = 2;
            swatch.style.borderBottomWidth = 2;
            swatch.style.borderLeftWidth = 2;
            swatch.style.borderRightWidth = 2;

            swatch.RegisterCallback<ClickEvent>(evt =>
            {
                selectedColor = c;
                onColorChanged(c);
                UpdateSwatchHighlights();
                RebuildSliders();
            });
            swatch.RegisterCallback<MouseEnterEvent>(evt =>
            {
                swatch.style.borderTopColor = Color.white;
                swatch.style.borderBottomColor = Color.white;
                swatch.style.borderLeftColor = Color.white;
                swatch.style.borderRightColor = Color.white;
            });
            swatch.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                // Only revert if not the selected one
                if (!ColorsApproxEqual(c, selectedColor))
                {
                    var defaultBorder = new Color(0.4f, 0.4f, 0.4f);
                    swatch.style.borderTopColor = defaultBorder;
                    swatch.style.borderBottomColor = defaultBorder;
                    swatch.style.borderLeftColor = defaultBorder;
                    swatch.style.borderRightColor = defaultBorder;
                }
            });

            swatches.Add(swatch);
            row.Add(swatch);
        }

        parent.Add(row);

        // Build initial sliders
        RebuildSliders();
        parent.Add(colorRowContainer);

        // Set initial highlight
        UpdateSwatchHighlights();
    }

    private static bool ColorsApproxEqual(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.01f &&
               Mathf.Abs(a.g - b.g) < 0.01f &&
               Mathf.Abs(a.b - b.b) < 0.01f;
    }

    private static void ApplyToLockerRoom(bool syncToServer = true)
    {
        if (!ChangingRoomHelper.IsInMainMenu()) return;

        ChangingRoomHelper.Scan();
        var playerMesh = ChangingRoomHelper.GetPlayerMesh();
        if (playerMesh?.PlayerHead == null) return;

        GenderSwapper.ApplyHeadColors(playerMesh.PlayerHead, selectedSkinTone, selectedHairColor);
        GenderSwapper.ApplyToPlayerMesh(playerMesh, selectedBodyTypeIndex == 1);

        HatSwapper.AttachToPlayerMesh(playerMesh, selectedHatId);

        if (syncToServer)
        {
            AppearanceAPI.QueuePostAppearance(
                selectedBodyTypeIndex,
                selectedSkinTone,
                selectedHairColor,
                hatId: selectedHatId,
                hairId: -1
            );
        }
    }
}
