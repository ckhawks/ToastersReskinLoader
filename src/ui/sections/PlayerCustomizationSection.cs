using System.Collections.Generic;
using ToasterReskinLoader.api;
using ToasterReskinLoader.swappers;
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
        ChangingRoomHelper.ShowBody();

        // Appearance editing is only available from the main menu
        if (!ChangingRoomHelper.IsInMainMenu())
        {
            Label inGameNotice = new Label();
            inGameNotice.text = "Appearance customization is only available from the main menu.";
            inGameNotice.style.fontSize = 16;
            inGameNotice.style.color = new Color(0.7f, 0.7f, 0.7f);
            inGameNotice.style.whiteSpace = WhiteSpace.Normal;
            inGameNotice.style.marginTop = 20;
            contentScrollViewContent.Add(inGameNotice);
            return;
        }

        Label description = new Label();
        description.text = "Customize your player's appearance. These settings are synced to other players.";
        description.style.fontSize = 14;
        description.style.color = new Color(0.7f, 0.7f, 0.7f);
        description.style.whiteSpace = WhiteSpace.Normal;
        description.style.marginTop = 8;
        description.style.marginBottom = 12;
        contentScrollViewContent.Add(description);

        // -- Body Type --
        AddSectionLabel(contentScrollViewContent, "Body Type");

        VisualElement bodyRow = UITools.CreateConfigurationRow();
        bodyRow.Add(UITools.CreateConfigurationLabel("Body Model"));
        var bodyDropdown = UITools.CreateStringDropdownField(BODY_TYPE_CHOICES, BODY_TYPE_CHOICES[selectedBodyTypeIndex]);
        bodyDropdown.RegisterCallback<ChangeEvent<string>>(evt =>
        {
            selectedBodyTypeIndex = evt.newValue == "Body Type 2" ? 1 : 0;
            ApplyToLockerRoom();
        });
        bodyRow.Add(bodyDropdown);
        contentScrollViewContent.Add(bodyRow);

        // -- Skin Tone --
        AddSectionLabel(contentScrollViewContent, "Skin Tone");
        AddColorPicker(contentScrollViewContent, "Custom Skin Tone", GenderSwapper.SKIN_TONES, selectedSkinTone,
            color => { selectedSkinTone = color; ApplyToLockerRoom(); });

        // -- Facial Hair Style --
        AddSectionLabel(contentScrollViewContent, "Facial Hair Style");

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
        contentScrollViewContent.Add(beardRow);

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
        contentScrollViewContent.Add(mustacheRow);

        // -- Facial Hair Color --
        AddSectionLabel(contentScrollViewContent, "Facial Hair Color");
        AddColorPicker(contentScrollViewContent, "Custom Hair Color", GenderSwapper.HAIR_COLORS, selectedHairColor,
            color => { selectedHairColor = color; ApplyToLockerRoom(); });

        // -- Hat --
        AddSectionLabel(contentScrollViewContent, "Hat");

        var hatNames = new List<string>();
        foreach (var h in HatSwapper.AllHats)
            hatNames.Add(h.Name);

        string currentHat = HatSwapper.GetHatName(selectedHatId);
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
        contentScrollViewContent.Add(hatRow);

        // -- Hair Style (TODO) --
        AddSectionLabel(contentScrollViewContent, "Hair Style");
        Label hairTodo = new Label("Coming soon - hair style customization is not yet available.");
        hairTodo.style.fontSize = 14;
        hairTodo.style.color = new Color(0.5f, 0.5f, 0.5f);
        hairTodo.style.whiteSpace = WhiteSpace.Normal;
        hairTodo.style.marginTop = 4;
        contentScrollViewContent.Add(hairTodo);

        // Initial apply to locker room preview
        ApplyToLockerRoom();
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
