using System.Collections.Generic;
using ToasterReskinLoader.swappers;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.ui.sections;

public static class SkaterSection
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
        void showBody() { ChangingRoomHelper.ShowBody(); }
        contentScrollViewContent.schedule.Execute(showBody).ExecuteLater(2);

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

        List<ReskinRegistry.ReskinEntry> skaterHelmets = ReskinRegistry.GetReskinEntriesByType("helmet");
        ReskinRegistry.ReskinEntry unchangedSkaterHelmetEntry = new ReskinRegistry.ReskinEntry
        {
            Name = "Unchanged",
            Path = null,
            Type = "helmet"
        };
        skaterHelmets.Insert(0, unchangedSkaterHelmetEntry);

        // BLUE TEAM
        Label blueTeamTitle = new Label("Blue");
        blueTeamTitle.style.fontSize = 24;
        blueTeamTitle.style.color = Color.white;
        contentScrollViewContent.Add(blueTeamTitle);
        
        VisualElement blueSkaterTorsoRow = UITools.CreateConfigurationRow();
        blueSkaterTorsoRow.Add(UITools.CreateConfigurationLabel("Skater Torso"));
            
        PopupField<ReskinRegistry.ReskinEntry> blueSkaterTorsoDropdown = UITools.CreateConfigurationDropdownField();
        blueSkaterTorsoDropdown.RegisterCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(
            new EventCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(evt =>
            {
                ReskinRegistry.ReskinEntry chosen = evt.newValue;
                Plugin.Log($"User picked ID={chosen.Path}, Name={chosen.Name}");
                ReskinProfileManager.SetSelectedReskinInCurrentProfile(chosen, "jersey_torso", "blue_skater");
                ChangingRoomHelper.SetPreviewContext(PlayerTeam.Blue, PlayerRole.Attacker);
                ChangingRoomHelper.RefreshPreview();
            })
        );
        // attackerPersonalStickDropdown.index = 0;
        blueSkaterTorsoDropdown.choices = jerseyTorsos;
        blueSkaterTorsoDropdown.value = ReskinProfileManager.currentProfile.blueSkaterTorso != null
            ? ReskinProfileManager.currentProfile.blueSkaterTorso
            : unchangedJerseyTorsoEntry;
        blueSkaterTorsoRow.Add(blueSkaterTorsoDropdown);
        contentScrollViewContent.Add(blueSkaterTorsoRow);
        
       
        VisualElement blueSkaterGroinRow = UITools.CreateConfigurationRow();
        blueSkaterGroinRow.Add(UITools.CreateConfigurationLabel("Skater Groin"));
            
        PopupField<ReskinRegistry.ReskinEntry> blueSkaterGroinDropdown = UITools.CreateConfigurationDropdownField();
        blueSkaterGroinDropdown.RegisterCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(
            new EventCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(evt =>
            {
                ReskinRegistry.ReskinEntry chosen = evt.newValue;
                Plugin.Log($"User picked ID={chosen.Path}, Name={chosen.Name}");
                ReskinProfileManager.SetSelectedReskinInCurrentProfile(chosen, "jersey_groin", "blue_skater");
                ChangingRoomHelper.SetPreviewContext(PlayerTeam.Blue, PlayerRole.Attacker);
                ChangingRoomHelper.RefreshPreview();
            })
        );
        // attackerPersonalStickDropdown.index = 0;
        blueSkaterGroinDropdown.choices = jerseyGroins;
        blueSkaterGroinDropdown.value = ReskinProfileManager.currentProfile.blueSkaterGroin != null
            ? ReskinProfileManager.currentProfile.blueSkaterGroin
            : unchangedJerseyGroinEntry;
        blueSkaterGroinRow.Add(blueSkaterGroinDropdown);
        contentScrollViewContent.Add(blueSkaterGroinRow);


        VisualElement blueSkaterHelmetRow = UITools.CreateConfigurationRow();
        blueSkaterHelmetRow.Add(UITools.CreateConfigurationLabel("Skater Helmet"));

        PopupField<ReskinRegistry.ReskinEntry> blueSkaterHelmetDropdown = UITools.CreateConfigurationDropdownField();

        // Blue Skater Helmet Color section created first so we can reference it
        var blueSkaterHelmetColorSection = UITools.CreateColorConfigurationRow(
            "Blue Helmet Color (Unchanged)",
            ReskinProfileManager.currentProfile.blueSkaterHelmetColor,
            false,
            newColor =>
            {
                ReskinProfileManager.currentProfile.blueSkaterHelmetColor = newColor;
                ReskinProfileManager.SaveProfile();
                SkaterHelmetSwapper.OnBlueHelmetColorChanged();
                ChangingRoomHelper.SetPreviewContext(PlayerTeam.Blue, PlayerRole.Attacker);
                ChangingRoomHelper.RefreshPreview();
            },
            () => { ReskinProfileManager.SaveProfile(); }
        );

        blueSkaterHelmetDropdown.RegisterCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(
            new EventCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(evt =>
            {
                ReskinRegistry.ReskinEntry chosen = evt.newValue;
                Plugin.Log($"User picked ID={chosen.Path}, Name={chosen.Name}");
                ReskinProfileManager.SetSelectedReskinInCurrentProfile(chosen, "helmet", "skater_blue");
                ChangingRoomHelper.SetPreviewContext(PlayerTeam.Blue, PlayerRole.Attacker);
                ChangingRoomHelper.RefreshPreview();
                // Enable/disable color sliders based on whether "Unchanged" is selected
                SetColorSliderEnabled(blueSkaterHelmetColorSection, chosen.Path == null);
            })
        );
        blueSkaterHelmetDropdown.choices = skaterHelmets;
        blueSkaterHelmetDropdown.value = ReskinProfileManager.currentProfile.blueSkaterHelmet != null
            ? ReskinProfileManager.currentProfile.blueSkaterHelmet
            : unchangedSkaterHelmetEntry;
        blueSkaterHelmetRow.Add(blueSkaterHelmetDropdown);
        contentScrollViewContent.Add(blueSkaterHelmetRow);

        // Set initial enabled state
        SetColorSliderEnabled(blueSkaterHelmetColorSection, blueSkaterHelmetDropdown.value.Path == null);
        contentScrollViewContent.Add(blueSkaterHelmetColorSection);

        // Lettering color
        var blueLetteringColorSection = UITools.CreateColorConfigurationRow(
            "Blue Lettering Color",
            ReskinProfileManager.currentProfile.blueSkaterLetteringColor,
            false,
            newColor =>
            {
                ReskinProfileManager.currentProfile.blueSkaterLetteringColor = newColor;
                PlayerTextSwapper.OnBlueSkaterLetteringColorChanged();
                ChangingRoomHelper.SetPreviewContext(PlayerTeam.Blue, PlayerRole.Attacker);
                ChangingRoomHelper.RefreshPreview();
                ReskinProfileManager.SaveProfile();
            },
            () => { ReskinProfileManager.SaveProfile(); }
        );
        contentScrollViewContent.Add(blueLetteringColorSection);

        // RED TEAM
        Label redTeamTitle = new Label("Red");
        redTeamTitle.style.fontSize = 24;
        redTeamTitle.style.color = Color.white;
        contentScrollViewContent.Add(redTeamTitle);
        
        VisualElement redSkaterTorsoRow = UITools.CreateConfigurationRow();
        redSkaterTorsoRow.Add(UITools.CreateConfigurationLabel("Skater Torso"));
            
        PopupField<ReskinRegistry.ReskinEntry> redSkaterTorsoDropdown = UITools.CreateConfigurationDropdownField();
        redSkaterTorsoDropdown.RegisterCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(
            new EventCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(evt =>
            {
                ReskinRegistry.ReskinEntry chosen = evt.newValue;
                Plugin.Log($"User picked ID={chosen.Path}, Name={chosen.Name}");
                ReskinProfileManager.SetSelectedReskinInCurrentProfile(chosen, "jersey_torso", "red_skater");
                ChangingRoomHelper.SetPreviewContext(PlayerTeam.Red, PlayerRole.Attacker);
                ChangingRoomHelper.RefreshPreview();
            })
        );
        // attackerPersonalStickDropdown.index = 0;
        redSkaterTorsoDropdown.choices = jerseyTorsos;
        redSkaterTorsoDropdown.value = ReskinProfileManager.currentProfile.redSkaterTorso != null
            ? ReskinProfileManager.currentProfile.redSkaterTorso
            : unchangedJerseyTorsoEntry;
        redSkaterTorsoRow.Add(redSkaterTorsoDropdown);
        contentScrollViewContent.Add(redSkaterTorsoRow);
        
       
        VisualElement redSkaterGroinRow = UITools.CreateConfigurationRow();
        redSkaterGroinRow.Add(UITools.CreateConfigurationLabel("Skater Groin"));
            
        PopupField<ReskinRegistry.ReskinEntry> redSkaterGroinDropdown = UITools.CreateConfigurationDropdownField();
        redSkaterGroinDropdown.RegisterCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(
            new EventCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(evt =>
            {
                ReskinRegistry.ReskinEntry chosen = evt.newValue;
                Plugin.Log($"User picked ID={chosen.Path}, Name={chosen.Name}");
                ReskinProfileManager.SetSelectedReskinInCurrentProfile(chosen, "jersey_groin", "red_skater");
                ChangingRoomHelper.SetPreviewContext(PlayerTeam.Red, PlayerRole.Attacker);
                ChangingRoomHelper.RefreshPreview();
            })
        );
        // attackerPersonalStickDropdown.index = 0;
        redSkaterGroinDropdown.choices = jerseyGroins;
        redSkaterGroinDropdown.value = ReskinProfileManager.currentProfile.redSkaterGroin != null
            ? ReskinProfileManager.currentProfile.redSkaterGroin
            : unchangedJerseyGroinEntry;
        redSkaterGroinRow.Add(redSkaterGroinDropdown);
        contentScrollViewContent.Add(redSkaterGroinRow);


        VisualElement redSkaterHelmetRow = UITools.CreateConfigurationRow();
        redSkaterHelmetRow.Add(UITools.CreateConfigurationLabel("Skater Helmet"));

        PopupField<ReskinRegistry.ReskinEntry> redSkaterHelmetDropdown = UITools.CreateConfigurationDropdownField();

        // Red Skater Helmet Color section created first so we can reference it
        var redSkaterHelmetColorSection = UITools.CreateColorConfigurationRow(
            "Red Helmet Color (Unchanged)",
            ReskinProfileManager.currentProfile.redSkaterHelmetColor,
            false,
            newColor =>
            {
                ReskinProfileManager.currentProfile.redSkaterHelmetColor = newColor;
                ReskinProfileManager.SaveProfile();
                SkaterHelmetSwapper.OnRedHelmetColorChanged();
                ChangingRoomHelper.SetPreviewContext(PlayerTeam.Red, PlayerRole.Attacker);
                ChangingRoomHelper.RefreshPreview();
            },
            () => { ReskinProfileManager.SaveProfile(); }
        );

        redSkaterHelmetDropdown.RegisterCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(
            new EventCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(evt =>
            {
                ReskinRegistry.ReskinEntry chosen = evt.newValue;
                Plugin.Log($"User picked ID={chosen.Path}, Name={chosen.Name}");
                ReskinProfileManager.SetSelectedReskinInCurrentProfile(chosen, "helmet", "skater_red");
                ChangingRoomHelper.SetPreviewContext(PlayerTeam.Red, PlayerRole.Attacker);
                ChangingRoomHelper.RefreshPreview();
                // Enable/disable color sliders based on whether "Unchanged" is selected
                SetColorSliderEnabled(redSkaterHelmetColorSection, chosen.Path == null);
            })
        );
        redSkaterHelmetDropdown.choices = skaterHelmets;
        redSkaterHelmetDropdown.value = ReskinProfileManager.currentProfile.redSkaterHelmet != null
            ? ReskinProfileManager.currentProfile.redSkaterHelmet
            : unchangedSkaterHelmetEntry;
        redSkaterHelmetRow.Add(redSkaterHelmetDropdown);
        contentScrollViewContent.Add(redSkaterHelmetRow);

        // Set initial enabled state
        SetColorSliderEnabled(redSkaterHelmetColorSection, redSkaterHelmetDropdown.value.Path == null);
        contentScrollViewContent.Add(redSkaterHelmetColorSection);

        // Lettering color
        var redLetteringColorSection = UITools.CreateColorConfigurationRow(
            "Red Lettering Color",
            ReskinProfileManager.currentProfile.redSkaterLetteringColor,
            false,
            newColor =>
            {
                ReskinProfileManager.currentProfile.redSkaterLetteringColor = newColor;
                PlayerTextSwapper.OnRedSkaterLetteringColorChanged();
                ChangingRoomHelper.SetPreviewContext(PlayerTeam.Red, PlayerRole.Attacker);
                ChangingRoomHelper.RefreshPreview();
                ReskinProfileManager.SaveProfile();
            },
            () => { ReskinProfileManager.SaveProfile(); }
        );
        contentScrollViewContent.Add(redLetteringColorSection);
    }
}