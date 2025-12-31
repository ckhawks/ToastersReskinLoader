using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.ui.sections;

public static class GoalieSection
{
    public static void CreateSection(VisualElement contentScrollViewContent)
    {
        // LEG PADS SECTION
        CreateLegPadsSection(contentScrollViewContent);

        // Add spacing between sections
        VisualElement spacer = new VisualElement();
        spacer.style.height = 30;
        contentScrollViewContent.Add(spacer);

        // HELMETS SECTION
        // CreateHelmetsSection(contentScrollViewContent);
    }

    // Creates the leg pads section (blue/red, left/right)
    private static void CreateLegPadsSection(VisualElement contentScrollViewContent)
    {
        var legPadEntries = ReskinRegistry.GetReskinEntriesByType("legpad");

        if (legPadEntries.Count == 0)
        {
            Label noPacksLabel = new Label("No leg pad reskins found. Add some to your reskinpacks folder!");
            noPacksLabel.style.fontSize = 14;
            noPacksLabel.style.color = Color.yellow;
            noPacksLabel.style.marginTop = 20;
            contentScrollViewContent.Add(noPacksLabel);
            return;
        }

        // Section title
        Label legPadsTitle = new Label("Leg Pads");
        legPadsTitle.style.fontSize = 24;
        legPadsTitle.style.color = Color.white;
        contentScrollViewContent.Add(legPadsTitle);

        // Create the "Unchanged" entry
        ReskinRegistry.ReskinEntry unchangedEntry = new ReskinRegistry.ReskinEntry
        {
            Name = "Unchanged",
            Path = null,
            Type = "legpad"
        };

        List<ReskinRegistry.ReskinEntry> legPadOptions = new List<ReskinRegistry.ReskinEntry> { unchangedEntry };
        legPadOptions.AddRange(legPadEntries);

        // BLUE TEAM
        CreateLegPadDropdownRow(contentScrollViewContent, "Blue Left", "blue_left", legPadOptions);
        CreateLegPadDropdownRow(contentScrollViewContent, "Blue Right", "blue_right", legPadOptions);

        // RED TEAM
        CreateLegPadDropdownRow(contentScrollViewContent, "Red Left", "red_left", legPadOptions);
        CreateLegPadDropdownRow(contentScrollViewContent, "Red Right", "red_right", legPadOptions);
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

    // Creates the goalie helmets section (blue/red)
    private static void CreateHelmetsSection(VisualElement contentScrollViewContent)
    {
        var helmetEntries = ReskinRegistry.GetReskinEntriesByType("goalie_helmet");

        if (helmetEntries.Count == 0)
        {
            Label noPacksLabel = new Label("No goalie helmet textures found. Add some to your reskinpacks folder!");
            noPacksLabel.style.fontSize = 14;
            noPacksLabel.style.color = Color.yellow;
            noPacksLabel.style.marginTop = 20;
            contentScrollViewContent.Add(noPacksLabel);
            return;
        }

        // Section title
        Label title = new Label("Goalie Helmets");
        title.style.fontSize = 24;
        title.style.color = Color.white;
        contentScrollViewContent.Add(title);

        // Create the "Unchanged" entry
        ReskinRegistry.ReskinEntry unchangedEntry = new ReskinRegistry.ReskinEntry
        {
            Name = "Unchanged",
            Path = null,
            Type = "goalie_helmet"
        };

        List<ReskinRegistry.ReskinEntry> helmetOptions = new List<ReskinRegistry.ReskinEntry> { unchangedEntry };
        helmetOptions.AddRange(helmetEntries);

        // BLUE TEAM
        CreateHelmetDropdownRow(contentScrollViewContent, "Blue Team", "blue", helmetOptions);

        // RED TEAM
        CreateHelmetDropdownRow(contentScrollViewContent, "Red Team", "red", helmetOptions);
    }

    // Helper to create a helmet dropdown row
    private static void CreateHelmetDropdownRow(VisualElement contentScrollViewContent, string labelText, string team, List<ReskinRegistry.ReskinEntry> options)
    {
        VisualElement row = UITools.CreateConfigurationRow();
        row.Add(UITools.CreateConfigurationLabel(labelText));

        PopupField<ReskinRegistry.ReskinEntry> dropdown = UITools.CreateConfigurationDropdownField();
        dropdown.RegisterCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(
            new EventCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(evt =>
            {
                ReskinRegistry.ReskinEntry chosen = evt.newValue;
                Plugin.Log($"User picked helmet for {team}: {chosen.Name}");
                ReskinProfileManager.SetSelectedReskinInCurrentProfile(chosen, "goalie_helmet", team);
            })
        );

        dropdown.choices = options;
        dropdown.value = GetCurrentHelmet(team) ?? options[0];
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

    // Gets the current helmet entry for a team
    private static ReskinRegistry.ReskinEntry GetCurrentHelmet(string team)
    {
        return team switch
        {
            "blue" => ReskinProfileManager.currentProfile.blueGoalieHelmet,
            "red" => ReskinProfileManager.currentProfile.redGoalieHelmet,
            _ => null
        };
    }
}

