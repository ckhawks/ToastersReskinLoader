using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.ui.sections
{
    public class GoalieEquipmentSection
    {
        private VisualElement container;
        private ScrollView scrollView;

        private RadioButtonGroup blueLeftLegPadGroup;
        private RadioButtonGroup blueRightLegPadGroup;
        private RadioButtonGroup redLeftLegPadGroup;
        private RadioButtonGroup redRightLegPadGroup;
        

        private List<ReskinRegistry.ReskinEntry> legPadEntries;
        
        public void Initialize(VisualElement root)
        {
            container = root.Query("GoalieEquipmentContainer");

            scrollView = container.Query<ScrollView>("GoalieEquipmentScrollView");
            

            blueLeftLegPadGroup = container.Query<RadioButtonGroup>("BlueLeftLegPadGroup");
            blueRightLegPadGroup = container.Query<RadioButtonGroup>("BlueRightLegPadGroup");
            redLeftLegPadGroup = container.Query<RadioButtonGroup>("RedLeftLegPadGroup");
            redRightLegPadGroup = container.Query<RadioButtonGroup>("RedRightLegPadGroup");
            

            if (blueLeftLegPadGroup != null)
                blueLeftLegPadGroup.RegisterValueChangedCallback(evt => OnBlueLeftLegPadChanged(evt.newValue));
            
            if (blueRightLegPadGroup != null)
                blueRightLegPadGroup.RegisterValueChangedCallback(evt => OnBlueRightLegPadChanged(evt.newValue));
            
            if (redLeftLegPadGroup != null)
                redLeftLegPadGroup.RegisterValueChangedCallback(evt => OnRedLeftLegPadChanged(evt.newValue));
            
            if (redRightLegPadGroup != null)
                redRightLegPadGroup.RegisterValueChangedCallback(evt => OnRedRightLegPadChanged(evt.newValue));
            

            LoadLegPadEntries();
            

            UpdateUI();
        }
        
        private void LoadLegPadEntries()
        {
            // Download all "legpad" type skins
            legPadEntries = ReskinRegistry.GetReskinEntriesByType("legpad");
            
            // Add the "None" option (reset to standard texture)
            var noneEntry = new ReskinRegistry.ReskinEntry
            {
                Name = "None (Default)",
                Type = "legpad",
                Path = null
            };
            
            legPadEntries.Insert(0, noneEntry);
        }
        
        private void UpdateUI()
        {
            // Clearing the current options
            ClearRadioButtonGroups();
            
            // Filling groups with options
            PopulateRadioButtonGroup(blueLeftLegPadGroup, legPadEntries, "blue_left");
            PopulateRadioButtonGroup(blueRightLegPadGroup, legPadEntries, "blue_right");
            PopulateRadioButtonGroup(redLeftLegPadGroup, legPadEntries, "red_left");
            PopulateRadioButtonGroup(redRightLegPadGroup, legPadEntries, "red_right");
            
            // Setting the currently selected values from the profile
            SetSelectedValues();
        }
        
        private void ClearRadioButtonGroups()
        {
            if (blueLeftLegPadGroup != null) blueLeftLegPadGroup.Clear();
            if (blueRightLegPadGroup != null) blueRightLegPadGroup.Clear();
            if (redLeftLegPadGroup != null) redLeftLegPadGroup.Clear();
            if (redRightLegPadGroup != null) redRightLegPadGroup.Clear();
        }
        
        private void PopulateRadioButtonGroup(RadioButtonGroup group, List<ReskinRegistry.ReskinEntry> entries, string slot)
        {
            if (group == null) return;
            
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                

                var radioButton = new RadioButton();
                radioButton.text = entry.Name;
                radioButton.userData = new KeyValuePair<ReskinRegistry.ReskinEntry, string>(entry, slot);
                
                // If it's a "None" option, make it special
                if (entry.Name == "None (Default)")
                {
                    radioButton.AddToClassList("default-option");
                }
                
                group.Add(radioButton);
            }
        }
        
        private void SetSelectedValues()
        {
            // Setting the selected values based on the current profile
            SetSelectedInGroup(blueLeftLegPadGroup, ReskinProfileManager.currentProfile.blueLegPadLeft);
            SetSelectedInGroup(blueRightLegPadGroup, ReskinProfileManager.currentProfile.blueLegPadRight);
            SetSelectedInGroup(redLeftLegPadGroup, ReskinProfileManager.currentProfile.redLegPadLeft);
            SetSelectedInGroup(redRightLegPadGroup, ReskinProfileManager.currentProfile.redLegPadRight);
        }
        
        private void SetSelectedInGroup(RadioButtonGroup group, ReskinRegistry.ReskinEntry selectedEntry)
        {
            if (group == null) return;
            

            int selectedIndex = 0; // Default is "None"
            
            if (selectedEntry != null && selectedEntry.Name != null)
            {
                for (int i = 0; i < legPadEntries.Count; i++)
                {
                    if (legPadEntries[i].Name == selectedEntry.Name)
                    {
                        selectedIndex = i;
                        break;
                    }
                }
            }
            
            group.value = selectedIndex;
        }
        
        private void OnBlueLeftLegPadChanged(int index)
        {
            if (index < 0 || index >= legPadEntries.Count) return;
            
            var selectedEntry = legPadEntries[index];
            string slot = "blue_left";
            

            ReskinRegistry.ReskinEntry entryToSet = (selectedEntry.Name == "None (Default)") ? null : selectedEntry;
            
            ReskinProfileManager.SetSelectedReskinInCurrentProfile(entryToSet, "legpad", slot);
        }
        
        private void OnBlueRightLegPadChanged(int index)
        {
            if (index < 0 || index >= legPadEntries.Count) return;
            
            var selectedEntry = legPadEntries[index];
            string slot = "blue_right";
            
            ReskinRegistry.ReskinEntry entryToSet = (selectedEntry.Name == "None (Default)") ? null : selectedEntry;
            
            ReskinProfileManager.SetSelectedReskinInCurrentProfile(entryToSet, "legpad", slot);
        }
        
        private void OnRedLeftLegPadChanged(int index)
        {
            if (index < 0 || index >= legPadEntries.Count) return;
            
            var selectedEntry = legPadEntries[index];
            string slot = "red_left";
            
            ReskinRegistry.ReskinEntry entryToSet = (selectedEntry.Name == "None (Default)") ? null : selectedEntry;
            
            ReskinProfileManager.SetSelectedReskinInCurrentProfile(entryToSet, "legpad", slot);
        }
        
        private void OnRedRightLegPadChanged(int index)
        {
            if (index < 0 || index >= legPadEntries.Count) return;
            
            var selectedEntry = legPadEntries[index];
            string slot = "red_right";
            
            ReskinRegistry.ReskinEntry entryToSet = (selectedEntry.Name == "None (Default)") ? null : selectedEntry;
            
            ReskinProfileManager.SetSelectedReskinInCurrentProfile(entryToSet, "legpad", slot);
        }
        
        public void Reload()
        {
            LoadLegPadEntries();
            UpdateUI();
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