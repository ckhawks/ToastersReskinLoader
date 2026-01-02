using System.Collections.Generic;
using ToasterReskinLoader.swappers;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.ui.sections;

public static class TapesSection
{
    // Helper to show/hide controls based on mode
    private static void UpdateControlsVisibility(string mode, VisualElement colorSection, VisualElement textureRow)
    {
        if (mode == "RGB")
        {
            colorSection.style.display = DisplayStyle.Flex;
            textureRow.style.display = DisplayStyle.None;
        }
        else if (mode == "Textured")
        {
            colorSection.style.display = DisplayStyle.None;
            textureRow.style.display = DisplayStyle.Flex;
        }
        else // Unchanged
        {
            colorSection.style.display = DisplayStyle.None;
            textureRow.style.display = DisplayStyle.None;
        }
    }

    // Helper to create tape customization controls (mode + color + texture)
    private static void CreateTapeControls(
        VisualElement container,
        string label,
        string tapeType, // ReskinRegistry type
        string currentMode,
        ReskinRegistry.ReskinEntry currentTexture,
        Color currentColor,
        System.Action<string> onModeChanged,
        System.Action<Color> onColorChanged,
        System.Action<ReskinRegistry.ReskinEntry> onTextureChanged)
    {
        // Mode dropdown
        VisualElement modeRow = UITools.CreateConfigurationRow();
        modeRow.Add(UITools.CreateConfigurationLabel($"{label} Mode"));

        DropdownField modeDropdown = new DropdownField();
        modeDropdown.choices = new List<string> { "Unchanged", "RGB", "Textured" };
        modeDropdown.value = currentMode ?? "Unchanged";
        modeDropdown.style.minWidth = 400;
        modeDropdown.style.maxWidth = 400;
        modeDropdown.style.width = 400;
        modeDropdown.style.minHeight = 30;
        modeDropdown.style.maxHeight = 30;
        modeDropdown.style.fontSize = 16;
        modeRow.Add(modeDropdown);
        container.Add(modeRow);

        // RGB Color section
        var colorSection = UITools.CreateColorConfigurationRow(
            $"{label} Color (RGB Mode)",
            currentColor,
            false,
            newColor =>
            {
                onColorChanged(newColor);
                ReskinProfileManager.SaveProfile();
            },
            () => { ReskinProfileManager.SaveProfile(); }
        );

        // Texture dropdown
        List<ReskinRegistry.ReskinEntry> tapeTextures = ReskinRegistry.GetReskinEntriesByType(tapeType);
        ReskinRegistry.ReskinEntry unchangedEntry = new ReskinRegistry.ReskinEntry
        {
            Name = "Unchanged",
            Path = null,
            Type = tapeType
        };
        tapeTextures.Insert(0, unchangedEntry);

        VisualElement textureRow = UITools.CreateConfigurationRow();
        textureRow.Add(UITools.CreateConfigurationLabel($"{label} Texture"));

        PopupField<ReskinRegistry.ReskinEntry> textureDropdown = UITools.CreateConfigurationDropdownField();
        textureDropdown.RegisterCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(
            new EventCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(evt =>
            {
                onTextureChanged(evt.newValue);
                ReskinProfileManager.SaveProfile();
            })
        );
        textureDropdown.choices = tapeTextures;
        textureDropdown.value = currentTexture ?? unchangedEntry;
        textureRow.Add(textureDropdown);

        // Mode change callback
        modeDropdown.RegisterValueChangedCallback(evt =>
        {
            string mode = evt.newValue;
            onModeChanged(mode);
            UpdateControlsVisibility(mode, colorSection, textureRow);
            ReskinProfileManager.SaveProfile();
        });

        // Set initial visibility
        UpdateControlsVisibility(modeDropdown.value, colorSection, textureRow);

        // Add to container
        container.Add(colorSection);
        container.Add(textureRow);
    }

    public static void CreateSection(VisualElement contentScrollViewContent)
    {
        // Blue Team Skaters
        Label blueSkaterTitle = new Label("Blue Team Skaters");
        blueSkaterTitle.style.fontSize = 24;
        blueSkaterTitle.style.color = Color.white;
        blueSkaterTitle.style.marginTop = 16;
        contentScrollViewContent.Add(blueSkaterTitle);

        CreateTapeControls(
            contentScrollViewContent,
            "Blade Tape",
            "tape_attacker_blade",
            ReskinProfileManager.currentProfile.blueSkaterBladeTapeMode,
            ReskinProfileManager.currentProfile.blueSkaterBladeTape,
            ReskinProfileManager.currentProfile.blueSkaterBladeTapeColor,
            mode => {
                ReskinProfileManager.currentProfile.blueSkaterBladeTapeMode = mode;
                StickTapeSwapper.OnBlueSkaterTapeChanged();
            },
            color => {
                ReskinProfileManager.currentProfile.blueSkaterBladeTapeColor = color;
                StickTapeSwapper.OnBlueSkaterTapeChanged();
            },
            texture => {
                ReskinProfileManager.currentProfile.blueSkaterBladeTape = texture;
                StickTapeSwapper.OnBlueSkaterTapeChanged();
            }
        );

        CreateTapeControls(
            contentScrollViewContent,
            "Shaft Tape",
            "tape_attacker_shaft",
            ReskinProfileManager.currentProfile.blueSkaterShaftTapeMode,
            ReskinProfileManager.currentProfile.blueSkaterShaftTape,
            ReskinProfileManager.currentProfile.blueSkaterShaftTapeColor,
            mode => {
                ReskinProfileManager.currentProfile.blueSkaterShaftTapeMode = mode;
                StickTapeSwapper.OnBlueSkaterTapeChanged();
            },
            color => {
                ReskinProfileManager.currentProfile.blueSkaterShaftTapeColor = color;
                StickTapeSwapper.OnBlueSkaterTapeChanged();
            },
            texture => {
                ReskinProfileManager.currentProfile.blueSkaterShaftTape = texture;
                StickTapeSwapper.OnBlueSkaterTapeChanged();
            }
        );

        // Blue Team Goalies
        Label blueGoalieTitle = new Label("Blue Team Goalies");
        blueGoalieTitle.style.fontSize = 24;
        blueGoalieTitle.style.color = Color.white;
        blueGoalieTitle.style.marginTop = 16;
        contentScrollViewContent.Add(blueGoalieTitle);

        CreateTapeControls(
            contentScrollViewContent,
            "Blade Tape",
            "tape_goalie_blade",
            ReskinProfileManager.currentProfile.blueGoalieBladeTapeMode,
            ReskinProfileManager.currentProfile.blueGoalieBladeTape,
            ReskinProfileManager.currentProfile.blueGoalieBladeTapeColor,
            mode => {
                ReskinProfileManager.currentProfile.blueGoalieBladeTapeMode = mode;
                StickTapeSwapper.OnBlueGoalieTapeChanged();
            },
            color => {
                ReskinProfileManager.currentProfile.blueGoalieBladeTapeColor = color;
                StickTapeSwapper.OnBlueGoalieTapeChanged();
            },
            texture => {
                ReskinProfileManager.currentProfile.blueGoalieBladeTape = texture;
                StickTapeSwapper.OnBlueGoalieTapeChanged();
            }
        );

        CreateTapeControls(
            contentScrollViewContent,
            "Shaft Tape",
            "tape_goalie_shaft",
            ReskinProfileManager.currentProfile.blueGoalieShaftTapeMode,
            ReskinProfileManager.currentProfile.blueGoalieShaftTape,
            ReskinProfileManager.currentProfile.blueGoalieShaftTapeColor,
            mode => {
                ReskinProfileManager.currentProfile.blueGoalieShaftTapeMode = mode;
                StickTapeSwapper.OnBlueGoalieTapeChanged();
            },
            color => {
                ReskinProfileManager.currentProfile.blueGoalieShaftTapeColor = color;
                StickTapeSwapper.OnBlueGoalieTapeChanged();
            },
            texture => {
                ReskinProfileManager.currentProfile.blueGoalieShaftTape = texture;
                StickTapeSwapper.OnBlueGoalieTapeChanged();
            }
        );

        // Red Team Skaters
        Label redSkaterTitle = new Label("Red Team Skaters");
        redSkaterTitle.style.fontSize = 24;
        redSkaterTitle.style.color = Color.white;
        redSkaterTitle.style.marginTop = 16;
        contentScrollViewContent.Add(redSkaterTitle);

        CreateTapeControls(
            contentScrollViewContent,
            "Blade Tape",
            "tape_attacker_blade",
            ReskinProfileManager.currentProfile.redSkaterBladeTapeMode,
            ReskinProfileManager.currentProfile.redSkaterBladeTape,
            ReskinProfileManager.currentProfile.redSkaterBladeTapeColor,
            mode => {
                ReskinProfileManager.currentProfile.redSkaterBladeTapeMode = mode;
                StickTapeSwapper.OnRedSkaterTapeChanged();
            },
            color => {
                ReskinProfileManager.currentProfile.redSkaterBladeTapeColor = color;
                StickTapeSwapper.OnRedSkaterTapeChanged();
            },
            texture => {
                ReskinProfileManager.currentProfile.redSkaterBladeTape = texture;
                StickTapeSwapper.OnRedSkaterTapeChanged();
            }
        );

        CreateTapeControls(
            contentScrollViewContent,
            "Shaft Tape",
            "tape_attacker_shaft",
            ReskinProfileManager.currentProfile.redSkaterShaftTapeMode,
            ReskinProfileManager.currentProfile.redSkaterShaftTape,
            ReskinProfileManager.currentProfile.redSkaterShaftTapeColor,
            mode => {
                ReskinProfileManager.currentProfile.redSkaterShaftTapeMode = mode;
                StickTapeSwapper.OnRedSkaterTapeChanged();
            },
            color => {
                ReskinProfileManager.currentProfile.redSkaterShaftTapeColor = color;
                StickTapeSwapper.OnRedSkaterTapeChanged();
            },
            texture => {
                ReskinProfileManager.currentProfile.redSkaterShaftTape = texture;
                StickTapeSwapper.OnRedSkaterTapeChanged();
            }
        );

        // Red Team Goalies
        Label redGoalieTitle = new Label("Red Team Goalies");
        redGoalieTitle.style.fontSize = 24;
        redGoalieTitle.style.color = Color.white;
        redGoalieTitle.style.marginTop = 16;
        contentScrollViewContent.Add(redGoalieTitle);

        CreateTapeControls(
            contentScrollViewContent,
            "Blade Tape",
            "tape_goalie_blade",
            ReskinProfileManager.currentProfile.redGoalieBladeTapeMode,
            ReskinProfileManager.currentProfile.redGoalieBladeTape,
            ReskinProfileManager.currentProfile.redGoalieBladeTapeColor,
            mode => {
                ReskinProfileManager.currentProfile.redGoalieBladeTapeMode = mode;
                StickTapeSwapper.OnRedGoalieTapeChanged();
            },
            color => {
                ReskinProfileManager.currentProfile.redGoalieBladeTapeColor = color;
                StickTapeSwapper.OnRedGoalieTapeChanged();
            },
            texture => {
                ReskinProfileManager.currentProfile.redGoalieBladeTape = texture;
                StickTapeSwapper.OnRedGoalieTapeChanged();
            }
        );

        CreateTapeControls(
            contentScrollViewContent,
            "Shaft Tape",
            "tape_goalie_shaft",
            ReskinProfileManager.currentProfile.redGoalieShaftTapeMode,
            ReskinProfileManager.currentProfile.redGoalieShaftTape,
            ReskinProfileManager.currentProfile.redGoalieShaftTapeColor,
            mode => {
                ReskinProfileManager.currentProfile.redGoalieShaftTapeMode = mode;
                StickTapeSwapper.OnRedGoalieTapeChanged();
            },
            color => {
                ReskinProfileManager.currentProfile.redGoalieShaftTapeColor = color;
                StickTapeSwapper.OnRedGoalieTapeChanged();
            },
            texture => {
                ReskinProfileManager.currentProfile.redGoalieShaftTape = texture;
                StickTapeSwapper.OnRedGoalieTapeChanged();
            }
        );
    }
}
