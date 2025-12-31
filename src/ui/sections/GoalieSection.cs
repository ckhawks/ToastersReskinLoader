using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.ui.sections
{
    /// <summary>
    /// Combined section for all goalie-related customization:
    /// - Leg pads (left/right, blue/red)
    /// - Helmets (blue/red)
    /// </summary>
    public static class GoalieSection
    {
        public static void CreateSection(VisualElement container)
        {
            Plugin.LogDebug("Creating combined Goalie Equipment and Helmets section");

            // LEG PADS SECTION
            CreateLegPadsSection(container);

            // Add spacing between sections
            VisualElement spacer = new VisualElement();
            spacer.style.height = 30;
            container.Add(spacer);

            // HELMETS SECTION
            CreateHelmetsSection(container);
        }

        /// <summary>Creates the leg pads section (blue/red, left/right)</summary>
        private static void CreateLegPadsSection(VisualElement container)
        {
            var legPadEntries = ReskinRegistry.GetReskinEntriesByType("legpad");

            if (legPadEntries.Count == 0)
            {
                Label noPacksLabel = new Label("No leg pad reskins found. Add some to your reskinpacks folder!");
                noPacksLabel.style.fontSize = 14;
                noPacksLabel.style.color = Color.yellow;
                noPacksLabel.style.marginTop = 20;
                container.Add(noPacksLabel);
                return;
            }

            // Section title
            Label legPadsTitle = new Label("LEG PADS");
            legPadsTitle.style.fontSize = 22;
            legPadsTitle.style.color = Color.white;
            legPadsTitle.style.marginBottom = 4;
            container.Add(legPadsTitle);

            List<string> legPadOptions = new List<string> { "None (Default)" };
            legPadOptions.AddRange(legPadEntries.Select(e => e.Name));

            // BLUE TEAM
            VisualElement blueSection = CreateTeamSection("BLUE TEAM", new Color(0.15f, 0.25f, 0.4f), Color.white);

            VisualElement blueTeamRow = new VisualElement();
            blueTeamRow.style.flexDirection = FlexDirection.Row;
            blueTeamRow.style.justifyContent = Justify.SpaceBetween;

            CreateLegPadDropdown(blueTeamRow, "Left Leg Pad", "blue_left", legPadOptions);
            CreateLegPadDropdown(blueTeamRow, "Right Leg Pad", "blue_right", legPadOptions);

            blueSection.Add(blueTeamRow);
            container.Add(blueSection);

            // RED TEAM
            VisualElement redSection = CreateTeamSection("RED TEAM", new Color(0.4f, 0.15f, 0.15f), Color.white);

            VisualElement redTeamRow = new VisualElement();
            redTeamRow.style.flexDirection = FlexDirection.Row;
            redTeamRow.style.justifyContent = Justify.SpaceBetween;

            CreateLegPadDropdown(redTeamRow, "Left Leg Pad", "red_left", legPadOptions);
            CreateLegPadDropdown(redTeamRow, "Right Leg Pad", "red_right", legPadOptions);

            redSection.Add(redTeamRow);
            container.Add(redSection);
        }

        // Creates a colored section container for a team with title
        private static VisualElement CreateTeamSection(string teamName, Color backgroundColor, Color textColor)
        {
            VisualElement section = new VisualElement();
            section.style.backgroundColor = new StyleColor(backgroundColor);
            section.style.paddingLeft = 12;
            section.style.paddingRight = 12;
            section.style.paddingTop = 10;
            section.style.paddingBottom = 10;
            section.style.marginTop = 10;
            section.style.marginBottom = 10;

            Label teamLabel = new Label(teamName);
            teamLabel.style.fontSize = 16;
            teamLabel.style.color = textColor;
            teamLabel.style.marginBottom = 10;

            section.Add(teamLabel);
            return section;
        }

        /// <summary>Helper to create a single leg pad dropdown with label</summary>
        private static void CreateLegPadDropdown(VisualElement parentRow, string labelText, string slot, List<string> options)
        {
            VisualElement column = new VisualElement();
            column.style.flexGrow = 1;

            Label label = new Label(labelText);
            label.style.fontSize = 12;
            label.style.color = Color.white;
            label.style.marginBottom = 5;
            column.Add(label);

            DropdownField dropdown = new DropdownField();
            dropdown.choices = options;
            dropdown.value = GetCurrentLegPadName(slot);
            dropdown.RegisterValueChangedCallback(evt => OnLegPadSelected(slot, evt.newValue));
            column.Add(dropdown);

            parentRow.Add(column);
        }

        /// <summary>Creates the goalie helmets section (blue/red)</summary>
        private static void CreateHelmetsSection(VisualElement container)
        {
            var helmetEntries = ReskinRegistry.GetReskinEntriesByType("goalie_helmet");

            if (helmetEntries.Count == 0)
            {
                Label noPacksLabel = new Label("No goalie helmet textures found. Add some to your reskinpacks folder!");
                noPacksLabel.style.fontSize = 14;
                noPacksLabel.style.color = Color.yellow;
                noPacksLabel.style.marginTop = 20;
                container.Add(noPacksLabel);
                return;
            }

            // Section title
            Label title = new Label("GOALIE HELMETS");
            title.style.fontSize = 22;
            title.style.color = Color.white;
            title.style.marginBottom = 4;
            container.Add(title);

            // Description
            Label description = new Label("Custom textures for goalie helmets (mask). Only visible when playing as a goalie.");
            description.style.fontSize = 12;
            description.style.color = new Color(0.8f, 0.8f, 0.8f);
            description.style.marginBottom = 20;
            description.style.whiteSpace = WhiteSpace.Normal;
            container.Add(description);

            // Two-column layout for blue/red
            VisualElement columnsContainer = new VisualElement();
            columnsContainer.style.flexDirection = FlexDirection.Row;
            columnsContainer.style.justifyContent = Justify.SpaceBetween;
            columnsContainer.style.marginBottom = 20;

            VisualElement blueColumn = CreateTeamHelmetColumn("blue", "BLUE TEAM", new Color(0.3f, 0.5f, 1f), helmetEntries);
            columnsContainer.Add(blueColumn);

            VisualElement redColumn = CreateTeamHelmetColumn("red", "RED TEAM", new Color(1f, 0.3f, 0.3f), helmetEntries);
            columnsContainer.Add(redColumn);

            container.Add(columnsContainer);
        }

        /// <summary>Helper to create a team's helmet column (blue/red)</summary>
        private static VisualElement CreateTeamHelmetColumn(string team, string teamName, Color teamColor, List<ReskinRegistry.ReskinEntry> helmetEntries)
        {
            // Background color based on team
            Color bgColor = team == "blue" ? new Color(0.15f, 0.25f, 0.4f) : new Color(0.4f, 0.15f, 0.15f);

            VisualElement column = new VisualElement();
            column.style.flexGrow = 1;
            column.style.backgroundColor = new StyleColor(bgColor);
            column.style.paddingLeft = 12;
            column.style.paddingRight = 12;
            column.style.paddingTop = 10;
            column.style.paddingBottom = 10;

            Label teamLabel = new Label(teamName);
            teamLabel.style.fontSize = 14;
            teamLabel.style.color = Color.white;
            teamLabel.style.marginBottom = 10;
            column.Add(teamLabel);

            DropdownField dropdown = new DropdownField();
            dropdown.style.width = new StyleLength(new Length(100, LengthUnit.Percent));

            List<string> options = new List<string> { "None (Default)" };
            options.AddRange(helmetEntries.Select(e => e.Name));

            dropdown.choices = options;

            ReskinRegistry.ReskinEntry currentEntry = team == "blue"
                ? ReskinProfileManager.currentProfile.blueGoalieHelmet
                : ReskinProfileManager.currentProfile.redGoalieHelmet;

            dropdown.value = currentEntry?.Name ?? "None (Default)";

            dropdown.RegisterValueChangedCallback(evt => OnGoalieHelmetSelected(team, evt.newValue));

            column.Add(dropdown);

            return column;
        }

        /// <summary>Gets the current leg pad name for a slot</summary>
        private static string GetCurrentLegPadName(string slot)
        {
            ReskinRegistry.ReskinEntry entry = slot switch
            {
                "blue_left" => ReskinProfileManager.currentProfile.blueLegPadLeft,
                "blue_right" => ReskinProfileManager.currentProfile.blueLegPadRight,
                "red_left" => ReskinProfileManager.currentProfile.redLegPadLeft,
                "red_right" => ReskinProfileManager.currentProfile.redLegPadRight,
                _ => null
            };

            return entry?.Name ?? "None (Default)";
        }

        /// <summary>Handles leg pad selection</summary>
        private static void OnLegPadSelected(string slot, string selectedName)
        {
            Plugin.LogDebug($"Leg pad selected: {slot} = {selectedName}");

            ReskinRegistry.ReskinEntry selectedEntry = null;

            if (selectedName != "None (Default)")
            {
                var legPadEntries = ReskinRegistry.GetReskinEntriesByType("legpad");
                selectedEntry = legPadEntries.FirstOrDefault(e => e.Name == selectedName);

                if (selectedEntry == null)
                {
                    Plugin.LogError($"Could not find leg pad entry with name: {selectedName}");
                    return;
                }
            }

            ReskinProfileManager.SetSelectedReskinInCurrentProfile(selectedEntry, "legpad", slot);
        }

        /// <summary>Handles goalie helmet selection</summary>
        private static void OnGoalieHelmetSelected(string team, string selectedName)
        {
            Plugin.LogDebug($"Goalie helmet selected for {team} team: {selectedName}");

            ReskinRegistry.ReskinEntry selectedEntry = null;

            if (selectedName != "None (Default)")
            {
                var helmetEntries = ReskinRegistry.GetReskinEntriesByType("goalie_helmet");
                selectedEntry = helmetEntries.FirstOrDefault(e => e.Name == selectedName);

                if (selectedEntry == null)
                {
                    Plugin.LogError($"Could not find goalie helmet entry with name: {selectedName}");
                    return;
                }
            }

            ReskinProfileManager.SetSelectedReskinInCurrentProfile(selectedEntry, "goalie_helmet", team);
        }
    }
}
