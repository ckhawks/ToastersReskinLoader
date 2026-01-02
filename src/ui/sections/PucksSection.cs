using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.ui.sections;

public static class PucksSection
{
    public static void CreateSection(VisualElement contentScrollViewContent)
    {
        List<ReskinRegistry.ReskinEntry> attackerStickReskins = ReskinRegistry.GetReskinEntriesByType("puck");
        ReskinRegistry.ReskinEntry unchangedEntry = new ReskinRegistry.ReskinEntry
        {
            Name = "Default",
            Path = null,
            Type = "puck"
        };
        attackerStickReskins.Insert(0, unchangedEntry);
        
        VisualElement puckRow = UITools.CreateConfigurationRow();
        puckRow.Add(UITools.CreateConfigurationLabel("Puck"));
            
        PopupField<ReskinRegistry.ReskinEntry> puckDropdown = UITools.CreateConfigurationDropdownField();
        puckDropdown.RegisterCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(
            new EventCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(evt =>
            {
                ReskinRegistry.ReskinEntry chosen = evt.newValue;
                Plugin.Log($"User picked ID={chosen.Path}, Name={chosen.Name}");
                ReskinProfileManager.SetSelectedReskinInCurrentProfile(chosen, "puck", null);
            })
        );
        // attackerPersonalStickDropdown.index = 0;
        puckDropdown.choices = attackerStickReskins;
        puckDropdown.value = ReskinProfileManager.currentProfile.puck != null
            ? ReskinProfileManager.currentProfile.puck
            : unchangedEntry;
        puckRow.Add(puckDropdown);
        contentScrollViewContent.Add(puckRow);
        Label bumpMapNoticeLabel =
            new Label("<size=14>The puck's bump map will be set to a clean map when any custom reskins are selected.");
        bumpMapNoticeLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
        contentScrollViewContent.Add(bumpMapNoticeLabel);
    }
}