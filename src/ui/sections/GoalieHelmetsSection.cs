using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.ui.sections
{
    public class GoalieHelmetsSection
    {
        private VisualElement container;
        
        private DropdownField blueHelmetDropdown;
        private DropdownField redHelmetDropdown;
        
        private List<ReskinRegistry.ReskinEntry> helmetEntries;
        
        public void Initialize(VisualElement root)
        {
            container = new VisualElement();
            container.style.flexDirection = FlexDirection.Column;
            

            Label header = new Label("Goalie Helmets");
            header.style.fontSize = 24;
            header.style.color = Color.white;
            header.style.marginBottom = 20;
            container.Add(header);
            

            Label description = new Label("Custom textures for goalie helmets (mask).");
            description.style.fontSize = 14;
            description.style.color = new Color(0.8f, 0.8f, 0.8f);
            description.style.marginBottom = 30;
            container.Add(description);
            

            LoadHelmetEntries();
            

            Label blueTeamLabel = new Label("BLUE TEAM");
            blueTeamLabel.style.fontSize = 18;
            blueTeamLabel.style.color = new Color(0.3f, 0.5f, 1f);
            blueTeamLabel.style.marginBottom = 10;
            container.Add(blueTeamLabel);
            
            blueHelmetDropdown = CreateHelmetDropdown("blue");
            container.Add(blueHelmetDropdown);
            

            VisualElement spacer = new VisualElement();
            spacer.style.height = 20;
            container.Add(spacer);
            

            Label redTeamLabel = new Label("RED TEAM");
            redTeamLabel.style.fontSize = 18;
            redTeamLabel.style.color = new Color(1f, 0.3f, 0.3f);
            redTeamLabel.style.marginBottom = 10;
            container.Add(redTeamLabel);
            
            redHelmetDropdown = CreateHelmetDropdown("red");
            container.Add(redHelmetDropdown);
            
            root.Add(container);
        }
        
        private void LoadHelmetEntries()
        {
            helmetEntries = ReskinRegistry.GetReskinEntriesByType("goalie_helmet");
        }
        
        private DropdownField CreateHelmetDropdown(string team)
        {
            DropdownField dropdown = new DropdownField();
            dropdown.style.width = new StyleLength(new Length(300, LengthUnit.Pixel));
            dropdown.style.marginBottom = 20;
            

            List<string> options = new List<string> { "None (Default)" };
            options.AddRange(helmetEntries.Select(e => e.Name));
            
            dropdown.choices = options;
            

            ReskinRegistry.ReskinEntry currentEntry = team == "blue" 
                ? ReskinProfileManager.currentProfile.blueGoalieHelmet
                : ReskinProfileManager.currentProfile.redGoalieHelmet;
            
            dropdown.value = currentEntry?.Name ?? "None (Default)";

            dropdown.RegisterValueChangedCallback(evt => OnHelmetSelected(team, evt.newValue));
            
            return dropdown;
        }
        
        private void OnHelmetSelected(string team, string selectedName)
        {
            Plugin.LogDebug($"Helmet selected for {team} team: {selectedName}");
            
            ReskinRegistry.ReskinEntry selectedEntry = null;
            
            if (selectedName != "None (Default)")
            {
                selectedEntry = helmetEntries.FirstOrDefault(e => e.Name == selectedName);
                
                if (selectedEntry == null)
                {
                    Plugin.LogError($"Could not find helmet entry with name: {selectedName}");
                    return;
                }
            }
            
            ReskinProfileManager.SetSelectedReskinInCurrentProfile(selectedEntry, "goalie_helmet", team);
        }
        
        public void Reload()
        {
            LoadHelmetEntries();
            

            List<string> options = new List<string> { "None (Default)" };
            options.AddRange(helmetEntries.Select(e => e.Name));
            
            if (blueHelmetDropdown != null)
            {
                blueHelmetDropdown.choices = options;
                blueHelmetDropdown.value = ReskinProfileManager.currentProfile.blueGoalieHelmet?.Name ?? "None (Default)";
            }
            
            if (redHelmetDropdown != null)
            {
                redHelmetDropdown.choices = options;
                redHelmetDropdown.value = ReskinProfileManager.currentProfile.redGoalieHelmet?.Name ?? "None (Default)";
            }
        }
        
        public void Show()
        {
            if (container != null)
                container.style.display = DisplayStyle.Flex;
        }
        
        public void Hide()
        {
            if (container != null)
                container.style.display = DisplayStyle.None;
        }
    }
}