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

        var profile = ReskinProfileManager.currentProfile;
        var jerseyTorsos = ReskinRegistry.GetReskinChoices("jersey_torso", out var unchangedJerseyTorso);
        var jerseyGroins = ReskinRegistry.GetReskinChoices("jersey_groin", out var unchangedJerseyGroin);
        var skaterHelmets = ReskinRegistry.GetReskinChoices("helmet", out var unchangedSkaterHelmet);

        // BLUE TEAM
        Label blueTeamTitle = new Label("Blue");
        blueTeamTitle.style.fontSize = 24;
        blueTeamTitle.style.color = Color.white;
        contentScrollViewContent.Add(blueTeamTitle);

        UITools.AddReskinDropdownRow(contentScrollViewContent, "Skater Torso", jerseyTorsos,
            profile.blueSkaterTorso, unchangedJerseyTorso, "jersey_torso", "blue_skater", PlayerTeam.Blue, PlayerRole.Attacker);
        UITools.AddReskinDropdownRow(contentScrollViewContent, "Skater Groin", jerseyGroins,
            profile.blueSkaterGroin, unchangedJerseyGroin, "jersey_groin", "blue_skater", PlayerTeam.Blue, PlayerRole.Attacker);

        // Blue Skater Helmet Color section created first so the dropdown callback can reference it
        var blueSkaterHelmetColorSection = UITools.CreateColorConfigurationRow(
            "Blue Helmet Color (Unchanged)",
            profile.blueSkaterHelmetColor,
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

        var blueSkaterHelmetDropdown = UITools.AddReskinDropdownRow(contentScrollViewContent, "Skater Helmet", skaterHelmets,
            profile.blueSkaterHelmet, unchangedSkaterHelmet, "helmet", "skater_blue", PlayerTeam.Blue, PlayerRole.Attacker,
            chosen => SetColorSliderEnabled(blueSkaterHelmetColorSection, chosen.Path == null));

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

        var blueNumberOutlineSection = UITools.CreateNumberOutlineConfigurationRow(
            "Blue Number Outline",
            ReskinProfileManager.currentProfile.blueSkaterNumberOutlineColor,
            ReskinProfileManager.currentProfile.blueSkaterNumberOutlineWidth,
            newColor =>
            {
                ReskinProfileManager.currentProfile.blueSkaterNumberOutlineColor = newColor;
                PlayerTextSwapper.OnBlueSkaterNumberOutlineChanged();
                ChangingRoomHelper.SetPreviewContext(PlayerTeam.Blue, PlayerRole.Attacker);
                ChangingRoomHelper.RefreshPreview();
            },
            newWidth =>
            {
                ReskinProfileManager.currentProfile.blueSkaterNumberOutlineWidth = newWidth;
                PlayerTextSwapper.OnBlueSkaterNumberOutlineChanged();
                ChangingRoomHelper.SetPreviewContext(PlayerTeam.Blue, PlayerRole.Attacker);
                ChangingRoomHelper.RefreshPreview();
            },
            () => { ReskinProfileManager.SaveProfile(); }
        );
        contentScrollViewContent.Add(blueNumberOutlineSection);

        // RED TEAM
        Label redTeamTitle = new Label("Red");
        redTeamTitle.style.fontSize = 24;
        redTeamTitle.style.color = Color.white;
        contentScrollViewContent.Add(redTeamTitle);
        
        UITools.AddReskinDropdownRow(contentScrollViewContent, "Skater Torso", jerseyTorsos,
            profile.redSkaterTorso, unchangedJerseyTorso, "jersey_torso", "red_skater", PlayerTeam.Red, PlayerRole.Attacker);
        UITools.AddReskinDropdownRow(contentScrollViewContent, "Skater Groin", jerseyGroins,
            profile.redSkaterGroin, unchangedJerseyGroin, "jersey_groin", "red_skater", PlayerTeam.Red, PlayerRole.Attacker);

        // Red Skater Helmet Color section created first so the dropdown callback can reference it
        var redSkaterHelmetColorSection = UITools.CreateColorConfigurationRow(
            "Red Helmet Color (Unchanged)",
            profile.redSkaterHelmetColor,
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

        var redSkaterHelmetDropdown = UITools.AddReskinDropdownRow(contentScrollViewContent, "Skater Helmet", skaterHelmets,
            profile.redSkaterHelmet, unchangedSkaterHelmet, "helmet", "skater_red", PlayerTeam.Red, PlayerRole.Attacker,
            chosen => SetColorSliderEnabled(redSkaterHelmetColorSection, chosen.Path == null));

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

        var redNumberOutlineSection = UITools.CreateNumberOutlineConfigurationRow(
            "Red Number Outline",
            ReskinProfileManager.currentProfile.redSkaterNumberOutlineColor,
            ReskinProfileManager.currentProfile.redSkaterNumberOutlineWidth,
            newColor =>
            {
                ReskinProfileManager.currentProfile.redSkaterNumberOutlineColor = newColor;
                PlayerTextSwapper.OnRedSkaterNumberOutlineChanged();
                ChangingRoomHelper.SetPreviewContext(PlayerTeam.Red, PlayerRole.Attacker);
                ChangingRoomHelper.RefreshPreview();
            },
            newWidth =>
            {
                ReskinProfileManager.currentProfile.redSkaterNumberOutlineWidth = newWidth;
                PlayerTextSwapper.OnRedSkaterNumberOutlineChanged();
                ChangingRoomHelper.SetPreviewContext(PlayerTeam.Red, PlayerRole.Attacker);
                ChangingRoomHelper.RefreshPreview();
            },
            () => { ReskinProfileManager.SaveProfile(); }
        );
        contentScrollViewContent.Add(redNumberOutlineSection);
    }
}