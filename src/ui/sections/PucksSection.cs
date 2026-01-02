using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.ui.sections;

public static class PucksSection
{
    public static void CreateSection(VisualElement contentScrollViewContent)
    {
        List<ReskinRegistry.ReskinEntry> allPuckReskins = ReskinRegistry.GetReskinEntriesByType("puck");
        ReskinRegistry.ReskinEntry defaultEntry = new ReskinRegistry.ReskinEntry
        {
            Name = "Default",
            Path = null,
            Type = "puck"
        };

        // Create separate list for dropdown (without default entry)
        List<ReskinRegistry.ReskinEntry> dropdownPuckReskins = new List<ReskinRegistry.ReskinEntry>(allPuckReskins);

        // Title
        Label title = new Label("Puck Randomizer");
        title.style.fontSize = 18;
        title.style.marginBottom = 8;
        contentScrollViewContent.Add(title);

        // Description
        Label description = new Label("Select pucks to randomize between. Each spawned puck will use a random texture from your selected list.");
        description.style.color = new Color(0.7f, 0.7f, 0.7f);
        description.style.fontSize = 12;
        description.style.marginBottom = 16;
        contentScrollViewContent.Add(description);

        // Puck selection dropdown + Add button
        VisualElement puckSelectionRow = UITools.CreateConfigurationRow();
        puckSelectionRow.style.alignItems = Align.Center;
        Label selectPuckLabel = UITools.CreateConfigurationLabel("Select Puck");
        selectPuckLabel.style.marginRight = 8;
        puckSelectionRow.Add(selectPuckLabel);

        PopupField<ReskinRegistry.ReskinEntry> puckDropdown = UITools.CreateConfigurationDropdownField();
        puckDropdown.choices = dropdownPuckReskins;
        puckDropdown.value = dropdownPuckReskins.Count > 0 ? dropdownPuckReskins[0] : defaultEntry;
        puckSelectionRow.Add(puckDropdown);
        contentScrollViewContent.Add(puckSelectionRow);

        // Add button
        Button addButton = new Button
        {
            text = "Add",
            style =
            {
                backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f, 0.5f)),
                unityTextAlign = TextAnchor.MiddleCenter,
                fontSize = 16,
                // marginBottom = 16,
                paddingTop = 4,
                paddingBottom = 4,
                width = new StyleLength(new Length(80)),
                marginLeft = new StyleLength(new Length(8))
            }
        };
        UITools.AddHoverEffectsForButton(addButton);
        addButton.RegisterCallback<ClickEvent>(evt =>
        {
            ReskinRegistry.ReskinEntry chosen = puckDropdown.value;
            if (chosen != null && chosen.Path != null) // Don't add null entries
            {
                ReskinProfileManager.AddPuckToRandomizer(chosen);
                puckDropdown.value = dropdownPuckReskins.Count > 0 ? dropdownPuckReskins[0] : defaultEntry;
                RefreshRandomizerList(contentScrollViewContent, dropdownPuckReskins);
            }
        });
        puckSelectionRow.Add(addButton);
        puckSelectionRow.style.marginBottom = 12;

        // Container for randomizer list
        VisualElement randomizerListContainer = new VisualElement();
        randomizerListContainer.name = "randomizerListContainer";
        contentScrollViewContent.Add(randomizerListContainer);

        // Initial population of randomizer list
        RefreshRandomizerList(contentScrollViewContent, dropdownPuckReskins);

        // Bump map notice
        Label bumpMapNoticeLabel = new Label("The puck's bump map will be set to a clean map when any custom reskins are selected.");
        bumpMapNoticeLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
        bumpMapNoticeLabel.style.fontSize = 12;
        bumpMapNoticeLabel.style.marginTop = 16;
        contentScrollViewContent.Add(bumpMapNoticeLabel);
    }

    private static void RefreshRandomizerList(VisualElement contentScrollViewContent, List<ReskinRegistry.ReskinEntry> puckReskins)
    {
        var container = contentScrollViewContent.Q("randomizerListContainer");
        if (container == null) return;

        container.Clear();

        var puckList = ReskinProfileManager.currentProfile.puckList;

        if (puckList == null || puckList.Count == 0)
        {
            Label emptyLabel = new Label("No pucks selected for randomization");
            emptyLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            emptyLabel.style.fontSize = 14;
            emptyLabel.style.marginBottom = 16;
            container.Add(emptyLabel);
            return;
        }

        Label listTitle = new Label($"<b>Active Pucks</b> ({puckList.Count} selected)");
        listTitle.style.fontSize = 14;
        listTitle.style.marginBottom = 8;
        listTitle.style.marginTop = 8;
        container.Add(listTitle);

        foreach (var puck in puckList)
        {
            VisualElement puckItemRow = new VisualElement();
            puckItemRow.style.flexDirection = FlexDirection.Row;
            puckItemRow.style.marginBottom = 8;
            puckItemRow.style.marginLeft = 15;

            Label puckLabel = new Label(puck.Name);
            puckLabel.style.marginRight = 15;
            puckLabel.style.fontSize = 14;
            puckItemRow.Add(puckLabel);

            Button removeButton = new Button
            {
                text = "Remove",
                style =
                {
                    backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f, 0.5f)),
                    unityTextAlign = TextAnchor.MiddleCenter,
                    width = 80,
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 2,
                    paddingBottom = 2,
                    fontSize = 12
                }
            };
            UITools.AddHoverEffectsForButton(removeButton);
            removeButton.RegisterCallback<ClickEvent>(evt =>
            {
                ReskinProfileManager.RemovePuckFromRandomizer(puck);
                RefreshRandomizerList(contentScrollViewContent, puckReskins);
            });
            puckItemRow.Add(removeButton);

            container.Add(puckItemRow);
        }
    }
}