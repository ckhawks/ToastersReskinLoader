using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.ui.sections;

public static class SticksSection
{
    public static void CreateSection(VisualElement contentScrollViewContent)
    {
        void showStick()
        {
            ChangingRoomHelper.ShowStick();
        }
        
        contentScrollViewContent.schedule.Execute(showStick).ExecuteLater(2);
        

        Label monetizationDisclaimer = new Label(
            "<size=14>Please support the developer, GAFURIX, by subscribing to the Puck Patreon and purchasing the in-game cosmetics. Please do not use Toaster's Reskin Loader as a way to circumvent supporting the game's developer.</size><br>");
        monetizationDisclaimer.style.color = Color.white;
        monetizationDisclaimer.style.marginBottom = 16;
        contentScrollViewContent.Add(monetizationDisclaimer);
            
        // Attacker section
        List<ReskinRegistry.ReskinEntry> attackerStickReskins = ReskinRegistry.GetReskinEntriesByType("stick_attacker");
        ReskinRegistry.ReskinEntry unchangedEntry = new ReskinRegistry.ReskinEntry();
        unchangedEntry.Name = "Unchanged";
        unchangedEntry.Path = null;
        unchangedEntry.Type = "stick_attacker";
        attackerStickReskins.Insert(0, unchangedEntry);
        Label attackerSticksTitle = new Label("Skater");
        attackerSticksTitle.style.fontSize = 24;
        attackerSticksTitle.style.color = Color.white;
        contentScrollViewContent.Add(attackerSticksTitle);
            
        VisualElement attackerBluePersonalStickRow = UITools.CreateConfigurationRow();
        attackerBluePersonalStickRow.Add(UITools.CreateConfigurationLabel("Blue personal"));
            
        PopupField<ReskinRegistry.ReskinEntry> attackerBluePersonalStickDropdown = UITools.CreateConfigurationDropdownField();
        attackerBluePersonalStickDropdown.RegisterCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(
            new EventCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(evt =>
            {
                ReskinRegistry.ReskinEntry chosen = evt.newValue;
                Plugin.Log($"User picked ID={chosen.Path}, Name={chosen.Name}");
                ReskinProfileManager.SetSelectedReskinInCurrentProfile(chosen, "stick_attacker", "blue_personal");
                ChangingRoomHelper.UpdateStickDisplayToReskin(chosen, PlayerRole.Attacker);
            })
        );
        // attackerPersonalStickDropdown.index = 0;
        attackerBluePersonalStickDropdown.choices = attackerStickReskins;
        attackerBluePersonalStickDropdown.value = ReskinProfileManager.currentProfile.stickAttackerBluePersonal != null
            ? ReskinProfileManager.currentProfile.stickAttackerBluePersonal
            : unchangedEntry;
        attackerBluePersonalStickRow.Add(attackerBluePersonalStickDropdown);
        contentScrollViewContent.Add(attackerBluePersonalStickRow);
        
        VisualElement attackerRedPersonalStickRow = UITools.CreateConfigurationRow();
        attackerRedPersonalStickRow.Add(UITools.CreateConfigurationLabel("Red personal"));
            
        PopupField<ReskinRegistry.ReskinEntry> attackerRedPersonalStickDropdown = UITools.CreateConfigurationDropdownField();
        attackerRedPersonalStickDropdown.RegisterCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(
            new EventCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(evt =>
            {
                ReskinRegistry.ReskinEntry chosen = evt.newValue;
                Plugin.Log($"User picked ID={chosen.Path}, Name={chosen.Name}");
                ReskinProfileManager.SetSelectedReskinInCurrentProfile(chosen, "stick_attacker", "red_personal");
                ChangingRoomHelper.UpdateStickDisplayToReskin(chosen, PlayerRole.Attacker);
            })
        );
        // attackerPersonalStickDropdown.index = 0;
        attackerRedPersonalStickDropdown.choices = attackerStickReskins;
        attackerRedPersonalStickDropdown.value = ReskinProfileManager.currentProfile.stickAttackerRedPersonal != null
            ? ReskinProfileManager.currentProfile.stickAttackerRedPersonal
            : unchangedEntry;
        attackerRedPersonalStickRow.Add(attackerRedPersonalStickDropdown);
        contentScrollViewContent.Add(attackerRedPersonalStickRow);
        
            
        VisualElement attackerBlueStickRow = UITools.CreateConfigurationRow();
        attackerBlueStickRow.Add(UITools.CreateConfigurationLabel("Blue team"));
        PopupField<ReskinRegistry.ReskinEntry> attackerBlueStickDropdown = UITools.CreateConfigurationDropdownField();
        attackerBlueStickDropdown.RegisterCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(
            new EventCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(evt =>
            {
                ReskinRegistry.ReskinEntry chosen = evt.newValue;
                Plugin.Log($"User picked ID={chosen.Path}, Name={chosen.Name}");
                ReskinProfileManager.SetSelectedReskinInCurrentProfile(chosen, "stick_attacker", "blue_team");
                ChangingRoomHelper.UpdateStickDisplayToReskin(chosen, PlayerRole.Attacker);
            })
        );
        attackerBlueStickDropdown.choices = attackerStickReskins;
        // attackerBlueStickDropdown.index = 0;
        attackerBlueStickDropdown.value = ReskinProfileManager.currentProfile.stickAttackerBlue != null
            ? ReskinProfileManager.currentProfile.stickAttackerBlue
            : unchangedEntry;
        attackerBlueStickRow.Add(attackerBlueStickDropdown);
        contentScrollViewContent.Add(attackerBlueStickRow);
            
        VisualElement attackerRedStickRow = UITools.CreateConfigurationRow();
        attackerRedStickRow.Add(UITools.CreateConfigurationLabel("Red team"));
        PopupField<ReskinRegistry.ReskinEntry> attackerRedStickDropdown = UITools.CreateConfigurationDropdownField();
        attackerRedStickDropdown.RegisterCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(
            new EventCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(evt =>
            {
                ReskinRegistry.ReskinEntry chosen = evt.newValue;
                Plugin.Log($"User picked ID={chosen.Path}, Name={chosen.Name}");
                ReskinProfileManager.SetSelectedReskinInCurrentProfile(chosen, "stick_attacker", "red_team");
                ChangingRoomHelper.UpdateStickDisplayToReskin(chosen, PlayerRole.Attacker);
            })
        );
        attackerRedStickDropdown.choices = attackerStickReskins;
        attackerRedStickDropdown.value = ReskinProfileManager.currentProfile.stickAttackerRed != null
            ? ReskinProfileManager.currentProfile.stickAttackerRed
            : unchangedEntry;
        attackerRedStickRow.Add(attackerRedStickDropdown);
        contentScrollViewContent.Add(attackerRedStickRow);
            
        // Goalie section
        List<ReskinRegistry.ReskinEntry> goalieStickReskins = ReskinRegistry.GetReskinEntriesByType("stick_goalie");
        ReskinRegistry.ReskinEntry unchangedGoalieEntry = new ReskinRegistry.ReskinEntry();
        unchangedGoalieEntry.Name = "Unchanged";
        unchangedGoalieEntry.Path = null;
        unchangedGoalieEntry.Type = "stick_goalie";
        goalieStickReskins.Insert(0, unchangedGoalieEntry);
        Label goalieSticksTitle = new Label("Goalie");
        goalieSticksTitle.style.fontSize = 24;
        goalieSticksTitle.style.color = Color.white;
        contentScrollViewContent.Add(goalieSticksTitle);
        
        VisualElement goalieBluePersonalStickRow = UITools.CreateConfigurationRow();
        goalieBluePersonalStickRow.Add(UITools.CreateConfigurationLabel("Blue personal"));
            
        PopupField<ReskinRegistry.ReskinEntry> goalieBluePersonalStickDropdown = UITools.CreateConfigurationDropdownField();
        goalieBluePersonalStickDropdown.RegisterCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(
            new EventCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(evt =>
            {
                ReskinRegistry.ReskinEntry chosen = evt.newValue;
                Plugin.Log($"User picked ID={chosen.Path}, Name={chosen.Name}");
                ReskinProfileManager.SetSelectedReskinInCurrentProfile(chosen, "stick_goalie", "blue_personal");
                ChangingRoomHelper.UpdateStickDisplayToReskin(chosen, PlayerRole.Goalie);
            })
        );
        goalieBluePersonalStickDropdown.choices = goalieStickReskins;
        goalieBluePersonalStickDropdown.value = ReskinProfileManager.currentProfile.stickGoalieBluePersonal != null
            ? ReskinProfileManager.currentProfile.stickGoalieBluePersonal
            : unchangedEntry;
        goalieBluePersonalStickRow.Add(goalieBluePersonalStickDropdown);
        contentScrollViewContent.Add(goalieBluePersonalStickRow);
        
        VisualElement goalieRedPersonalStickRow = UITools.CreateConfigurationRow();
        goalieRedPersonalStickRow.Add(UITools.CreateConfigurationLabel("Red personal"));

        PopupField<ReskinRegistry.ReskinEntry> goalieRedPersonalStickDropdown = UITools.CreateConfigurationDropdownField();
        goalieRedPersonalStickDropdown.RegisterCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(
            new EventCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(evt =>
            {
                ReskinRegistry.ReskinEntry chosen = evt.newValue;
                Plugin.Log($"User picked ID={chosen.Path}, Name={chosen.Name}");
                ReskinProfileManager.SetSelectedReskinInCurrentProfile(chosen, "stick_goalie", "red_personal");
                ChangingRoomHelper.UpdateStickDisplayToReskin(chosen, PlayerRole.Goalie);
            })
        );
        goalieRedPersonalStickDropdown.choices = goalieStickReskins;
        goalieRedPersonalStickDropdown.value = ReskinProfileManager.currentProfile.stickGoalieRedPersonal != null
            ? ReskinProfileManager.currentProfile.stickGoalieRedPersonal
            : unchangedEntry;
        goalieRedPersonalStickRow.Add(goalieRedPersonalStickDropdown);
        contentScrollViewContent.Add(goalieRedPersonalStickRow);
        
            
        VisualElement goalieBlueStickRow = UITools.CreateConfigurationRow();
        goalieBlueStickRow.Add(UITools.CreateConfigurationLabel("Blue team"));
        PopupField<ReskinRegistry.ReskinEntry> goalieBlueStickDropdown = UITools.CreateConfigurationDropdownField();
        goalieBlueStickDropdown.RegisterCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(
            new EventCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(evt =>
            {
                ReskinRegistry.ReskinEntry chosen = evt.newValue;
                Plugin.Log($"User picked ID={chosen.Path}, Name={chosen.Name}");
                ReskinProfileManager.SetSelectedReskinInCurrentProfile(chosen, "stick_goalie", "blue_team");
                ChangingRoomHelper.UpdateStickDisplayToReskin(chosen, PlayerRole.Goalie);
            })
        );
        goalieBlueStickDropdown.choices = goalieStickReskins;
        goalieBlueStickDropdown.value = ReskinProfileManager.currentProfile.stickGoalieBlue != null
            ? ReskinProfileManager.currentProfile.stickGoalieBlue
            : unchangedEntry;
        goalieBlueStickRow.Add(goalieBlueStickDropdown);
        contentScrollViewContent.Add(goalieBlueStickRow);
            
        VisualElement goalieRedStickRow = UITools.CreateConfigurationRow();
        goalieRedStickRow.Add(UITools.CreateConfigurationLabel("Red team"));
        PopupField<ReskinRegistry.ReskinEntry> goalieRedStickDropdown = UITools.CreateConfigurationDropdownField();
        goalieRedStickDropdown.RegisterCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(
            new EventCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(evt =>
            {
                ReskinRegistry.ReskinEntry chosen = evt.newValue;
                Plugin.Log($"User picked ID={chosen.Path}, Name={chosen.Name}");
                ReskinProfileManager.SetSelectedReskinInCurrentProfile(chosen, "stick_goalie", "red_team");
                ChangingRoomHelper.UpdateStickDisplayToReskin(chosen, PlayerRole.Goalie);
            })
        );
        goalieRedStickDropdown.choices = goalieStickReskins;
        goalieRedStickDropdown.value = ReskinProfileManager.currentProfile.stickGoalieRed != null
            ? ReskinProfileManager.currentProfile.stickGoalieRed
            : unchangedEntry;
        goalieRedStickRow.Add(goalieRedStickDropdown);
        contentScrollViewContent.Add(goalieRedStickRow);
    }
    
}