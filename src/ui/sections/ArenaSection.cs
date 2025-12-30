using System.Collections.Generic;
using System.Reflection;
using ToasterReskinLoader.swappers;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.ui.sections;

public static class ArenaSection
{
    static readonly FieldInfo _showPlayerUsernamesToggleField = typeof(UISettings)
        .GetField("showPlayerUsernamesToggle", 
            BindingFlags.Instance | BindingFlags.NonPublic);
    
    public static void CreateSection(VisualElement contentScrollViewContent)
    {
        VisualElement hangarRow = UITools.CreateConfigurationRow();
        hangarRow.Add(UITools.CreateConfigurationLabel("Enable Hangar"));

        Toggle hangarToggle = UITools.CreateConfigurationCheckbox(ReskinProfileManager.currentProfile.hangarEnabled);
        hangarToggle.value = ReskinProfileManager.currentProfile.hangarEnabled;
        hangarToggle.RegisterCallback<ChangeEvent<bool>>(
            new EventCallback<ChangeEvent<bool>>(evt =>
            {
                bool hangarState = evt.newValue;
                Plugin.Log($"User picked hangar: {hangarState}");
                ReskinProfileManager.currentProfile.hangarEnabled = hangarState;
                ReskinProfileManager.SaveProfile();
                ArenaSwapper.UpdateHangarState();
            })
        );
        hangarRow.Add(hangarToggle);
        contentScrollViewContent.Add(hangarRow);
        
        
        VisualElement crowdRow = UITools.CreateConfigurationRow();
        crowdRow.Add(UITools.CreateConfigurationLabel("Enable Crowd"));

        Toggle crowdToggle = UITools.CreateConfigurationCheckbox(ReskinProfileManager.currentProfile.crowdEnabled);
        var spectatorDensitySlider = UITools.CreateConfigurationSlider(0, 1, ReskinProfileManager.currentProfile.spectatorDensity, 300);

        // A variable to hold a reference to our scheduled "final" action.
        IVisualElementScheduledItem finalChangeEvent = null;

        // 1. Register a ChangeEvent callback to update the profile in real-time.
        spectatorDensitySlider.RegisterCallback<ChangeEvent<float>>(evt =>
        {
            // This part is cheap and fine to run continuously.
            ReskinProfileManager.currentProfile.spectatorDensity = evt.newValue;

            // If a "final" action is already scheduled, cancel it.
            // This prevents the action from running while the user is still dragging.
            finalChangeEvent?.Pause();

            // Schedule the expensive action to run after a short delay (e.g., 500ms).
            // This gives the user time to finish their interaction.
            finalChangeEvent = spectatorDensitySlider.schedule.Execute(() =>
            {
                Plugin.Log("Slider value has settled. Saving profile and updating arena.");
                ReskinProfileManager.SaveProfile();
                ArenaSwapper.UpdateSpectators(); // Assuming you have this method now
            }).StartingIn(250); // 500 milliseconds delay
        });
        
        crowdToggle.value = ReskinProfileManager.currentProfile.crowdEnabled;
        crowdToggle.RegisterCallback<ChangeEvent<bool>>(
            new EventCallback<ChangeEvent<bool>>(evt =>
            {
                bool crowdState = evt.newValue;
                Plugin.Log($"User picked crowd: {crowdState}");
                ReskinProfileManager.currentProfile.crowdEnabled = crowdState;
                ReskinProfileManager.SaveProfile();
                ArenaSwapper.UpdateCrowdState();
                UpdateSliderState();
            })
        );

        UpdateSliderState();
        
        crowdRow.Add(crowdToggle);
        contentScrollViewContent.Add(crowdRow);
        
        
        var spectatorDensityRow = UITools.CreateConfigurationRow();
        spectatorDensityRow.Add(UITools.CreateConfigurationLabel("Crowd Density"));
        

        
        spectatorDensityRow.Add(spectatorDensitySlider);
        contentScrollViewContent.Add(spectatorDensityRow);
        
        
        VisualElement glassRow = UITools.CreateConfigurationRow();
        glassRow.Add(UITools.CreateConfigurationLabel("Enable Glass"));

        Toggle glassToggle = UITools.CreateConfigurationCheckbox(ReskinProfileManager.currentProfile.glassEnabled);
        glassToggle.value = ReskinProfileManager.currentProfile.glassEnabled;
        glassToggle.RegisterCallback<ChangeEvent<bool>>(
            new EventCallback<ChangeEvent<bool>>(evt =>
            {
                ReskinProfileManager.currentProfile.glassEnabled = evt.newValue;
                ReskinProfileManager.SaveProfile();
                ArenaSwapper.UpdateGlassState();
            })
        );
        glassRow.Add(glassToggle);
        contentScrollViewContent.Add(glassRow);
        
        VisualElement scoreboardRow = UITools.CreateConfigurationRow();
        scoreboardRow.Add(UITools.CreateConfigurationLabel("Enable Scoreboard"));

        Toggle scoreboardToggle = UITools.CreateConfigurationCheckbox(ReskinProfileManager.currentProfile.scoreboardEnabled);
        scoreboardToggle.value = ReskinProfileManager.currentProfile.scoreboardEnabled;
        scoreboardToggle.RegisterCallback<ChangeEvent<bool>>(
            new EventCallback<ChangeEvent<bool>>(evt =>
            {
                ReskinProfileManager.currentProfile.scoreboardEnabled = evt.newValue;
                ReskinProfileManager.SaveProfile();
                ArenaSwapper.UpdateScoreboardState();
            })
        );
        scoreboardRow.Add(scoreboardToggle);
        contentScrollViewContent.Add(scoreboardRow);
        
        List<ReskinRegistry.ReskinEntry> iceReskins = ReskinRegistry.GetReskinEntriesByType("rink_ice");
        ReskinRegistry.ReskinEntry unchangedEntry = new ReskinRegistry.ReskinEntry();
        unchangedEntry.Name = "Unchanged";
        unchangedEntry.Path = null;
        unchangedEntry.Type = "rink_ice";
        iceReskins.Insert(0, unchangedEntry);
        
        VisualElement iceRow = UITools.CreateConfigurationRow();
        iceRow.Add(UITools.CreateConfigurationLabel("Ice"));
            
        PopupField<ReskinRegistry.ReskinEntry> iceDropdown = UITools.CreateConfigurationDropdownField();
        iceDropdown.RegisterCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(
            new EventCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(evt =>
            {
                ReskinRegistry.ReskinEntry chosen = evt.newValue;
                Plugin.Log($"User picked ID={chosen.Path}, Name={chosen.Name}");
                ReskinProfileManager.SetSelectedReskinInCurrentProfile(chosen, "rink_ice", null);
            })
        );
        // attackerPersonalStickDropdown.index = 0;
        iceDropdown.choices = iceReskins;
        iceDropdown.value = ReskinProfileManager.currentProfile.ice != null
            ? ReskinProfileManager.currentProfile.ice
            : unchangedEntry;
        iceRow.Add(iceDropdown);
        contentScrollViewContent.Add(iceRow);
        
        var iceSmoothnessRow = UITools.CreateConfigurationRow();
        iceSmoothnessRow.Add(UITools.CreateConfigurationLabel("Ice Smoothness"));
        var iceSmoothnessSlider = UITools.CreateConfigurationSlider(0, 1, ReskinProfileManager.currentProfile.iceSmoothness, 300);

        iceSmoothnessSlider.RegisterCallback<ChangeEvent<float>>(evt =>
        {
            ReskinProfileManager.currentProfile.iceSmoothness = evt.newValue;
            IceSwapper.UpdateIceSmoothness();
        });
        iceSmoothnessSlider.RegisterCallback<PointerUpEvent>(evt =>
        {
            ReskinProfileManager.SaveProfile();
        });

        iceSmoothnessRow.Add(iceSmoothnessSlider);
        contentScrollViewContent.Add(iceSmoothnessRow);
        
        
        var boardsBorderTopSection = UITools.CreateColorConfigurationRow(
            "<b>Boards: Border Top</b>",
            ReskinProfileManager.currentProfile.boardsBorderTopColor,
            false,
            newColor =>
            {
                ReskinProfileManager.currentProfile.boardsBorderTopColor =
                    newColor;
                ArenaSwapper.UpdateBoards();
            },
            () => { ReskinProfileManager.SaveProfile(); }
        );
        contentScrollViewContent.Add(boardsBorderTopSection);
        
        
        var boardsMiddleSection = UITools.CreateColorConfigurationRow(
            "<b>Boards: Middle</b>",
            ReskinProfileManager.currentProfile.boardsMiddleColor,
            false,
            newColor =>
            {
                ReskinProfileManager.currentProfile.boardsMiddleColor =
                    newColor;
                ArenaSwapper.UpdateBoards();
            },
            () => { ReskinProfileManager.SaveProfile(); }
        );
        contentScrollViewContent.Add(boardsMiddleSection);
        
        var boardsBorderBottomSection = UITools.CreateColorConfigurationRow(
            "<b>Boards: Border Bottom</b>",
            ReskinProfileManager.currentProfile.boardsBorderBottomColor,
            false,
            newColor =>
            {
                ReskinProfileManager.currentProfile.boardsBorderBottomColor =
                    newColor;
                ArenaSwapper.UpdateBoards();
            },
            () => { ReskinProfileManager.SaveProfile(); }
        );
        contentScrollViewContent.Add(boardsBorderBottomSection);
        
        var pillarsColorSection = UITools.CreateColorConfigurationRow(
            "<b>Glass Pillars</b>",
            ReskinProfileManager.currentProfile.pillarsColor,
            false,
            newColor =>
            {
                ReskinProfileManager.currentProfile.pillarsColor =
                    newColor;
                ArenaSwapper.UpdateGlassAndPillars();
            },
            () => { ReskinProfileManager.SaveProfile(); }
        );
        contentScrollViewContent.Add(pillarsColorSection);
        
        var glassSmoothnessRow = UITools.CreateConfigurationRow();
        glassSmoothnessRow.Add(UITools.CreateConfigurationLabel("Glass Smoothness"));
        var glassSmoothnessSlider = UITools.CreateConfigurationSlider(0, 1, ReskinProfileManager.currentProfile.glassSmoothness, 300);

        glassSmoothnessSlider.RegisterCallback<ChangeEvent<float>>(evt =>
        {
            ReskinProfileManager.currentProfile.glassSmoothness = evt.newValue;
            ArenaSwapper.UpdateGlassAndPillars();
        });
        glassSmoothnessSlider.RegisterCallback<PointerUpEvent>(evt =>
        {
            ReskinProfileManager.SaveProfile();
        });

        glassSmoothnessRow.Add(glassSmoothnessSlider);
        contentScrollViewContent.Add(glassSmoothnessRow);
        
        
        // GOAL NET
        List<ReskinRegistry.ReskinEntry> netReskins = ReskinRegistry.GetReskinEntriesByType("net");
        ReskinRegistry.ReskinEntry unchangedNetEntry = new ReskinRegistry.ReskinEntry
        {
            Name = "Unchanged",
            Path = null,
            Type = "net"
        };
        netReskins.Insert(0, unchangedNetEntry);
        
        VisualElement netRow = UITools.CreateConfigurationRow();
        netRow.Add(UITools.CreateConfigurationLabel("Goal Net"));
            
        PopupField<ReskinRegistry.ReskinEntry> netDropdown = UITools.CreateConfigurationDropdownField();
        netDropdown.RegisterCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(
            new EventCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(evt =>
            {
                ReskinRegistry.ReskinEntry chosen = evt.newValue;
                Plugin.Log($"User picked ID={chosen.Path}, Name={chosen.Name}");
                ReskinProfileManager.SetSelectedReskinInCurrentProfile(chosen, "net", null);
            })
        );
        netDropdown.choices = netReskins;
        netDropdown.value = ReskinProfileManager.currentProfile.net != null
            ? ReskinProfileManager.currentProfile.net
            : unchangedNetEntry;
        netRow.Add(netDropdown);
        contentScrollViewContent.Add(netRow);
        return;

        void UpdateSliderState()
        {
            if (ReskinProfileManager.currentProfile.crowdEnabled)
            {
                spectatorDensitySlider.SetEnabled(true);
                spectatorDensitySlider.style.opacity = 1f;
            }
            else
            {
                spectatorDensitySlider.SetEnabled(false);
                spectatorDensitySlider.style.opacity = 0.5f;
            }
            
        }
    }
}
