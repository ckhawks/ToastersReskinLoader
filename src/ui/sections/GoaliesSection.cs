using System;
using System.Collections.Generic;
using System.Linq;
using ToasterReskinLoader.swappers;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.ui.sections;

public static class GoaliesSection
{
    // Helper to set enabled state of all sliders in a color configuration row
    private static void SetColorSliderEnabled(VisualElement colorConfigSection, bool enabled)
    {
        if (colorConfigSection?.childCount > 1)
        {
            var slidersContainer = colorConfigSection[1]; // slidersContainer is at index 1
            if (slidersContainer != null)
            {
                foreach (var row in slidersContainer.Children())
                {
                    foreach (var child in row.Children())
                    {
                        if (child is Slider slider)
                        {
                            slider.SetEnabled(enabled);
                        }
                    }
                }
            }
        }
    }

    public static void CreateSection(VisualElement contentScrollViewContent)
    {
        Label description = new Label();
        description.text = $"Note: Customization will not show in changing room currently.";
        description.style.color = new Color(0.7f, 0.7f, 0.7f);
        description.style.fontSize = 14;
        description.style.marginBottom = 8;
        contentScrollViewContent.Add(description);
        
        // Get all reskin entries upfront
        List<ReskinRegistry.ReskinEntry> jerseyTorsos = ReskinRegistry.GetReskinEntriesByType("jersey_torso");
        ReskinRegistry.ReskinEntry unchangedJerseyTorsoEntry = new ReskinRegistry.ReskinEntry
        {
            Name = "Unchanged",
            Path = null,
            Type = "jersey_torso"
        };
        jerseyTorsos.Insert(0, unchangedJerseyTorsoEntry);

        List<ReskinRegistry.ReskinEntry> jerseyGroins = ReskinRegistry.GetReskinEntriesByType("jersey_groin");
        ReskinRegistry.ReskinEntry unchangedJerseyGroinEntry = new ReskinRegistry.ReskinEntry
        {
            Name = "Unchanged",
            Path = null,
            Type = "jersey_groin"
        };
        jerseyGroins.Insert(0, unchangedJerseyGroinEntry);

        var legPadEntries = ReskinRegistry.GetReskinEntriesByType("legpad");
        ReskinRegistry.ReskinEntry unchangedLegPadEntry = new ReskinRegistry.ReskinEntry
        {
            Name = "Unchanged",
            Path = null,
            Type = "legpad"
        };
        List<ReskinRegistry.ReskinEntry> legPadOptions = new List<ReskinRegistry.ReskinEntry> { unchangedLegPadEntry };
        legPadOptions.AddRange(legPadEntries);

        // BLUE TEAM
        Label blueTeamTitle = new Label("Blue");
        blueTeamTitle.style.fontSize = 24;
        blueTeamTitle.style.color = Color.white;
        contentScrollViewContent.Add(blueTeamTitle);

        CreateGoalieJerseyUI(contentScrollViewContent, "Blue", "blue", jerseyTorsos, jerseyGroins, unchangedJerseyTorsoEntry, unchangedJerseyGroinEntry);
        CreateTeamLegPadsUI(contentScrollViewContent, "blue", legPadOptions);
        CreateTeamHeadgearUI(contentScrollViewContent, "blue");

        // Add spacing between teams
        VisualElement spacer = new VisualElement();
        spacer.style.height = 30;
        contentScrollViewContent.Add(spacer);

        // RED TEAM
        Label redTeamTitle = new Label("Red");
        redTeamTitle.style.fontSize = 24;
        redTeamTitle.style.color = Color.white;
        contentScrollViewContent.Add(redTeamTitle);

        CreateGoalieJerseyUI(contentScrollViewContent, "Red", "red", jerseyTorsos, jerseyGroins, unchangedJerseyTorsoEntry, unchangedJerseyGroinEntry);
        CreateTeamLegPadsUI(contentScrollViewContent, "red", legPadOptions);
        CreateTeamHeadgearUI(contentScrollViewContent, "red");
    }

    // Creates goalie jersey UI (torso and groin)
    private static void CreateGoalieJerseyUI(VisualElement contentScrollViewContent, string teamLabel, string teamSlot,
        List<ReskinRegistry.ReskinEntry> jerseyTorsos, List<ReskinRegistry.ReskinEntry> jerseyGroins,
        ReskinRegistry.ReskinEntry unchangedTorsoEntry, ReskinRegistry.ReskinEntry unchangedGroinEntry)
    {
        // Torso
        VisualElement torsoRow = UITools.CreateConfigurationRow();
        torsoRow.Add(UITools.CreateConfigurationLabel("Goalie Torso"));

        PopupField<ReskinRegistry.ReskinEntry> torsoDropdown = UITools.CreateConfigurationDropdownField();
        torsoDropdown.RegisterCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(
            new EventCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(evt =>
            {
                ReskinRegistry.ReskinEntry chosen = evt.newValue;
                Plugin.Log($"User picked ID={chosen.Path}, Name={chosen.Name}");
                ReskinProfileManager.SetSelectedReskinInCurrentProfile(chosen, "jersey_torso", $"{teamSlot}_goalie");
            })
        );
        torsoDropdown.choices = jerseyTorsos;
        torsoDropdown.value = (teamSlot == "blue" ? ReskinProfileManager.currentProfile.blueGoalieTorso : ReskinProfileManager.currentProfile.redGoalieTorso) ?? unchangedTorsoEntry;
        torsoRow.Add(torsoDropdown);
        contentScrollViewContent.Add(torsoRow);

        // Groin
        VisualElement groinRow = UITools.CreateConfigurationRow();
        groinRow.Add(UITools.CreateConfigurationLabel("Goalie Groin"));

        PopupField<ReskinRegistry.ReskinEntry> groinDropdown = UITools.CreateConfigurationDropdownField();
        groinDropdown.RegisterCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(
            new EventCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(evt =>
            {
                ReskinRegistry.ReskinEntry chosen = evt.newValue;
                Plugin.Log($"User picked ID={chosen.Path}, Name={chosen.Name}");
                ReskinProfileManager.SetSelectedReskinInCurrentProfile(chosen, "jersey_groin", $"{teamSlot}_goalie");
            })
        );
        groinDropdown.choices = jerseyGroins;
        groinDropdown.value = (teamSlot == "blue" ? ReskinProfileManager.currentProfile.blueGoalieGroin : ReskinProfileManager.currentProfile.redGoalieGroin) ?? unchangedGroinEntry;
        groinRow.Add(groinDropdown);
        contentScrollViewContent.Add(groinRow);
    }

    // Creates leg pads UI for a team
    private static void CreateTeamLegPadsUI(VisualElement contentScrollViewContent, string team, List<ReskinRegistry.ReskinEntry> legPadOptions)
    {
        // Store references to leg pad dropdowns to track "Unchanged" state
        List<PopupField<ReskinRegistry.ReskinEntry>> legPadDropdowns = new List<PopupField<ReskinRegistry.ReskinEntry>>();

        // Leg Pad Dropdowns - with tracking
        legPadDropdowns.Add(CreateLegPadDropdownRowWithReference(contentScrollViewContent, $"{(team == "blue" ? "Blue" : "Red")} Left Pad", $"{team}_left", legPadOptions));
        legPadDropdowns.Add(CreateLegPadDropdownRowWithReference(contentScrollViewContent, $"{(team == "blue" ? "Blue" : "Red")} Right Pad", $"{team}_right", legPadOptions));

        // Container for color sections so we can recreate them on reset
        VisualElement legPadColorsContainer = new VisualElement();
        contentScrollViewContent.Add(legPadColorsContainer);

        // Helper to check if any leg pad is set to "Unchanged"
        bool AnyPadUnchanged() => legPadDropdowns.Any(d => d.value.Path == null);

        // Helper function to recreate color sections
        void RecreateColorSections()
        {
            legPadColorsContainer.Clear();

            Color blueColor = ReskinProfileManager.currentProfile.blueLegPadDefaultColor;
            Color redColor = ReskinProfileManager.currentProfile.redLegPadDefaultColor;

            if (team == "blue")
            {
                var colorSection = UITools.CreateColorConfigurationRow(
                    "Pad Color (Unchanged)",
                    blueColor,
                    false,
                    newColor =>
                    {
                        ReskinProfileManager.currentProfile.blueLegPadDefaultColor = newColor;
                        ReskinProfileManager.SaveProfile();
                        GoalieEquipmentSwapper.OnBlueLegPadColorChanged();
                    },
                    () => { ReskinProfileManager.SaveProfile(); }
                );
                SetColorSliderEnabled(colorSection, AnyPadUnchanged());
                legPadColorsContainer.Add(colorSection);
            }
            else
            {
                var colorSection = UITools.CreateColorConfigurationRow(
                    "Pad Color (Unchanged)",
                    redColor,
                    false,
                    newColor =>
                    {
                        ReskinProfileManager.currentProfile.redLegPadDefaultColor = newColor;
                        ReskinProfileManager.SaveProfile();
                        GoalieEquipmentSwapper.OnRedLegPadColorChanged();
                    },
                    () => { ReskinProfileManager.SaveProfile(); }
                );
                SetColorSliderEnabled(colorSection, AnyPadUnchanged());
                legPadColorsContainer.Add(colorSection);
            }
        }

        // Create color sections for the first time
        RecreateColorSections();

        // Reset Button
        Button resetButton = new Button
        {
            text = "Reset leg pad colors",
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
            Plugin.Log("Resetting leg pad colors to default");
            GoalieEquipmentSwapper.ResetLegPadColorsToDefault();
            RecreateColorSections();
        });
        contentScrollViewContent.Add(resetButton);
    }

    // Helper to create leg pad dropdown row and return the dropdown reference
    private static PopupField<ReskinRegistry.ReskinEntry> CreateLegPadDropdownRowWithReference(VisualElement contentScrollViewContent, string labelText, string slot, List<ReskinRegistry.ReskinEntry> options)
    {
        VisualElement row = UITools.CreateConfigurationRow();
        row.Add(UITools.CreateConfigurationLabel(labelText));

        PopupField<ReskinRegistry.ReskinEntry> dropdown = UITools.CreateConfigurationDropdownField();
        dropdown.RegisterCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(
            new EventCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(evt =>
            {
                ReskinRegistry.ReskinEntry chosen = evt.newValue;
                Plugin.Log($"User picked leg pad: {chosen.Name}");
                ReskinProfileManager.SetSelectedReskinInCurrentProfile(chosen, "legpad", slot);
            })
        );

        dropdown.choices = options;
        dropdown.value = GetCurrentLegPad(slot) ?? options[0];
        row.Add(dropdown);
        contentScrollViewContent.Add(row);

        return dropdown;
    }

    // Creates headgear UI for a team
    private static void CreateTeamHeadgearUI(VisualElement contentScrollViewContent, string team)
    {
        // References to track dropdown states
        PopupField<ReskinRegistry.ReskinEntry> helmetDropdown = null;
        PopupField<ReskinRegistry.ReskinEntry> maskDropdown = null;

        // Container for all headgear UI so we can recreate color sections on reset
        VisualElement headgearColorsContainer = new VisualElement();
        contentScrollViewContent.Add(headgearColorsContainer);

        // Helper function to recreate color sections
        void RecreateHeadgearColorSections()
        {
            headgearColorsContainer.Clear();

            // HELMET SECTION
            var helmetEntries = ReskinRegistry.GetReskinEntriesByType("helmet");
            if (helmetEntries.Count > 0)
            {
                ReskinRegistry.ReskinEntry unchangedEntry = new ReskinRegistry.ReskinEntry
                {
                    Name = "Unchanged",
                    Path = null,
                    Type = "helmet"
                };

                List<ReskinRegistry.ReskinEntry> helmetOptions = new List<ReskinRegistry.ReskinEntry> { unchangedEntry };
                helmetOptions.AddRange(helmetEntries);

                // Helmet dropdown - store reference
                helmetDropdown = CreateHeadgearDropdownRow(headgearColorsContainer, "Helmet", "helmet", team, helmetOptions);

                // Helmet Color
                var helmetColorSection = UITools.CreateColorConfigurationRow(
                    "Helmet Color (Unchanged)",
                    team == "blue" ? ReskinProfileManager.currentProfile.blueGoalieHelmetColor : ReskinProfileManager.currentProfile.redGoalieHelmetColor,
                    false,
                    newColor =>
                    {
                        if (team == "blue")
                        {
                            ReskinProfileManager.currentProfile.blueGoalieHelmetColor = newColor;
                            GoalieHelmetSwapper.OnBlueHelmetColorChanged();
                        }
                        else
                        {
                            ReskinProfileManager.currentProfile.redGoalieHelmetColor = newColor;
                            GoalieHelmetSwapper.OnRedHelmetColorChanged();
                        }
                        ReskinProfileManager.SaveProfile();
                    },
                    () => { ReskinProfileManager.SaveProfile(); }
                );
                // Set initial enabled state and register callback
                SetColorSliderEnabled(helmetColorSection, helmetDropdown.value.Path == null);
                helmetDropdown.RegisterCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(
                    new EventCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(evt =>
                    {
                        SetColorSliderEnabled(helmetColorSection, evt.newValue.Path == null);
                    })
                );
                headgearColorsContainer.Add(helmetColorSection);
            }

            // MASK SECTION
            var maskEntries = ReskinRegistry.GetReskinEntriesByType("goalie_mask");
            if (maskEntries.Count > 0)
            {
                ReskinRegistry.ReskinEntry unchangedEntry = new ReskinRegistry.ReskinEntry
                {
                    Name = "Unchanged",
                    Path = null,
                    Type = "goalie_mask"
                };

                List<ReskinRegistry.ReskinEntry> maskOptions = new List<ReskinRegistry.ReskinEntry> { unchangedEntry };
                maskOptions.AddRange(maskEntries);

                // Mask dropdown - store reference
                maskDropdown = CreateHeadgearDropdownRow(headgearColorsContainer, "Mask (Neck Shield)", "goalie_mask", team, maskOptions);

                // Mask Color
                var maskColorSection = UITools.CreateColorConfigurationRow(
                    "Mask Color (Unchanged)",
                    team == "blue" ? ReskinProfileManager.currentProfile.blueGoalieMaskColor : ReskinProfileManager.currentProfile.redGoalieMaskColor,
                    false,
                    newColor =>
                    {
                        if (team == "blue")
                        {
                            ReskinProfileManager.currentProfile.blueGoalieMaskColor = newColor;
                            GoalieHelmetSwapper.OnBlueMaskColorChanged();
                        }
                        else
                        {
                            ReskinProfileManager.currentProfile.redGoalieMaskColor = newColor;
                            GoalieHelmetSwapper.OnRedMaskColorChanged();
                        }
                        ReskinProfileManager.SaveProfile();
                    },
                    () => { ReskinProfileManager.SaveProfile(); }
                );
                // Set initial enabled state and register callback
                SetColorSliderEnabled(maskColorSection, maskDropdown.value.Path == null);
                maskDropdown.RegisterCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(
                    new EventCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(evt =>
                    {
                        SetColorSliderEnabled(maskColorSection, evt.newValue.Path == null);
                    })
                );
                headgearColorsContainer.Add(maskColorSection);
            }

            // CAGE SECTION (color only - always enabled)
            var cageColorSection = UITools.CreateColorConfigurationRow(
                "Cage Color",
                team == "blue" ? ReskinProfileManager.currentProfile.blueGoalieCageColor : ReskinProfileManager.currentProfile.redGoalieCageColor,
                false,
                newColor =>
                {
                    if (team == "blue")
                    {
                        ReskinProfileManager.currentProfile.blueGoalieCageColor = newColor;
                        GoalieHelmetSwapper.OnBlueCageColorChanged();
                    }
                    else
                    {
                        ReskinProfileManager.currentProfile.redGoalieCageColor = newColor;
                        GoalieHelmetSwapper.OnRedCageColorChanged();
                    }
                    ReskinProfileManager.SaveProfile();
                },
                () => { ReskinProfileManager.SaveProfile(); }
            );
            headgearColorsContainer.Add(cageColorSection);
        }

        // Create color sections for the first time
        RecreateHeadgearColorSections();

        // Reset Button for Headgear Colors
        Button resetButton = new Button
        {
            text = "Reset headgear colors",
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
            Plugin.Log("Resetting headgear colors to default");
            GoalieHelmetSwapper.ResetHeadgearColorsToDefault();
            RecreateHeadgearColorSections();
        });
        contentScrollViewContent.Add(resetButton);
    }

    // Helper to create a headgear dropdown row and return the dropdown reference
    private static PopupField<ReskinRegistry.ReskinEntry> CreateHeadgearDropdownRow(VisualElement contentScrollViewContent, string labelText, string type, string team, List<ReskinRegistry.ReskinEntry> options)
    {
        VisualElement row = UITools.CreateConfigurationRow();
        row.Add(UITools.CreateConfigurationLabel(labelText));

        PopupField<ReskinRegistry.ReskinEntry> dropdown = UITools.CreateConfigurationDropdownField();
        dropdown.RegisterCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(
            new EventCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(evt =>
            {
                ReskinRegistry.ReskinEntry chosen = evt.newValue;
                Plugin.Log($"User picked {type} for {team}: {chosen.Name}");
                // For helmet type, encode goalie context in slot
                string slot = (type == "helmet") ? $"goalie_{team}" : team;
                ReskinProfileManager.SetSelectedReskinInCurrentProfile(chosen, type, slot);
            })
        );

        dropdown.choices = options;

        // Get current selection based on type and team
        ReskinRegistry.ReskinEntry currentEntry = null;
        if (type == "helmet")
            currentEntry = team == "blue" ? ReskinProfileManager.currentProfile.blueGoalieHelmet : ReskinProfileManager.currentProfile.redGoalieHelmet;
        else if (type == "goalie_mask")
            currentEntry = team == "blue" ? ReskinProfileManager.currentProfile.blueGoalieMask : ReskinProfileManager.currentProfile.redGoalieMask;

        dropdown.value = currentEntry ?? options[0];
        row.Add(dropdown);
        contentScrollViewContent.Add(row);

        return dropdown;
    }

    // Helper to create a single leg pad dropdown row
    private static void CreateLegPadDropdownRow(VisualElement contentScrollViewContent, string labelText, string slot, List<ReskinRegistry.ReskinEntry> options)
    {
        VisualElement row = UITools.CreateConfigurationRow();
        row.Add(UITools.CreateConfigurationLabel(labelText));

        PopupField<ReskinRegistry.ReskinEntry> dropdown = UITools.CreateConfigurationDropdownField();
        dropdown.RegisterCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(
            new EventCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(evt =>
            {
                ReskinRegistry.ReskinEntry chosen = evt.newValue;
                Plugin.Log($"User picked leg pad: {chosen.Name}");
                ReskinProfileManager.SetSelectedReskinInCurrentProfile(chosen, "legpad", slot);
            })
        );

        dropdown.choices = options;
        dropdown.value = GetCurrentLegPad(slot) ?? options[0];
        row.Add(dropdown);
        contentScrollViewContent.Add(row);
    }

    // Gets the current leg pad entry for a slot
    private static ReskinRegistry.ReskinEntry GetCurrentLegPad(string slot)
    {
        return slot switch
        {
            "blue_left" => ReskinProfileManager.currentProfile.blueLegPadLeft,
            "blue_right" => ReskinProfileManager.currentProfile.blueLegPadRight,
            "red_left" => ReskinProfileManager.currentProfile.redLegPadLeft,
            "red_right" => ReskinProfileManager.currentProfile.redLegPadRight,
            _ => null
        };
    }
}
