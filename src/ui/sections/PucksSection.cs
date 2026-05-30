using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.ui.sections;

public static class PucksSection
{
    // Held so the Remove handler (in RefreshRandomizerList) can put the removed puck
    // back into the dropdown.
    private static PopupField<ReskinRegistry.ReskinEntry> _puckDropdown;

    public static void CreateSection(VisualElement contentScrollViewContent)
    {
        List<ReskinRegistry.ReskinEntry> allPuckReskins = ReskinRegistry.GetReskinEntriesByType("puck");
        ReskinRegistry.ReskinEntry defaultEntry = new ReskinRegistry.ReskinEntry
        {
            Name = "Default",
            Path = null,
            Type = "puck"
        };

        // Create separate list for dropdown - filter out pucks already in the active list
        List<ReskinRegistry.ReskinEntry> dropdownPuckReskins = new List<ReskinRegistry.ReskinEntry>();
        var activePucks = ReskinProfileManager.currentProfile.puckList ?? new List<ReskinRegistry.ReskinEntry>();

        // Offer the vanilla Default puck as a first-class option (unless it's already in the list)
        if (!activePucks.Any(p => ReskinProfileManager.IsDefaultPuckEntry(p)))
        {
            dropdownPuckReskins.Add(defaultEntry);
        }

        foreach (var puck in allPuckReskins)
        {
            // Only add to dropdown if not already in active list
            if (!activePucks.Any(p => p?.Path == puck.Path))
            {
                dropdownPuckReskins.Add(puck);
            }
        }

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

        // Locker-room 3D preview of the active list, with a motion-style switcher.
        BuildPreviewControls(contentScrollViewContent);
        PuckPreview.Show();

        // Puck selection dropdown + Add button
        VisualElement puckSelectionRow = UITools.CreateConfigurationRow();
        puckSelectionRow.style.alignItems = Align.Center;
        Label selectPuckLabel = UITools.CreateConfigurationLabel("Select Puck");
        selectPuckLabel.style.marginRight = 8;
        puckSelectionRow.Add(selectPuckLabel);

        PopupField<ReskinRegistry.ReskinEntry> puckDropdown = UITools.CreateConfigurationDropdownField();
        puckDropdown.choices = dropdownPuckReskins;
        puckDropdown.value = dropdownPuckReskins.Count > 0 ? dropdownPuckReskins[0] : defaultEntry;
        _puckDropdown = puckDropdown;
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
            // Allow real puck skins (Path set) and the vanilla Default puck; reject empty selections.
            if (chosen != null && (chosen.Path != null || ReskinProfileManager.IsDefaultPuckEntry(chosen)))
            {
                ReskinProfileManager.AddPuckToRandomizer(chosen);
                // Rebuild dropdown to exclude newly added puck
                RefreshPuckDropdown(puckDropdown);
                puckDropdown.value = puckDropdown.choices.Count > 0 ? puckDropdown.choices[0] : defaultEntry;
                RefreshRandomizerList(contentScrollViewContent, dropdownPuckReskins);
                PuckPreview.Refresh();
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

    // Motion-style switcher for the locker-room preview. Only meaningful in the main menu.
    private static void BuildPreviewControls(VisualElement contentScrollViewContent)
    {
        if (!ChangingRoomHelper.IsInMainMenu()) return;

        VisualElement previewRow = UITools.CreateConfigurationRow();
        previewRow.style.alignItems = Align.Center;
        previewRow.style.marginBottom = 12;

        Label previewLabel = UITools.CreateConfigurationLabel("Preview Motion");
        previewLabel.style.marginRight = 8;
        previewRow.Add(previewLabel);

        var buttons = new Dictionary<PuckPreviewMode, Button>();

        void Restyle()
        {
            foreach (var kvp in buttons)
            {
                bool active = kvp.Key == PuckPreview.Mode;
                kvp.Value.style.backgroundColor = new StyleColor(active
                    ? new Color(0.7f, 0.7f, 0.7f)
                    : new Color(0.25f, 0.25f, 0.25f, 0.5f));
                kvp.Value.style.color = active ? Color.black : Color.white;
            }
        }

        void AddModeButton(string text, PuckPreviewMode mode)
        {
            Button b = new Button
            {
                text = text,
                style =
                {
                    unityTextAlign = TextAnchor.MiddleCenter,
                    fontSize = 14,
                    paddingTop = 4,
                    paddingBottom = 4,
                    paddingLeft = 10,
                    paddingRight = 10,
                    marginLeft = 6,
                }
            };
            b.RegisterCallback<ClickEvent>(_ =>
            {
                PuckPreview.SetMode(mode);
                Restyle();
            });
            buttons[mode] = b;
            previewRow.Add(b);
        }

        AddModeButton("Row", PuckPreviewMode.Row);
        AddModeButton("Carousel", PuckPreviewMode.Carousel);
        AddModeButton("Drop", PuckPreviewMode.Drop);
        Restyle();

        contentScrollViewContent.Add(previewRow);
    }

    private static void RefreshPuckDropdown(PopupField<ReskinRegistry.ReskinEntry> puckDropdown)
    {
        List<ReskinRegistry.ReskinEntry> allPuckReskins = ReskinRegistry.GetReskinEntriesByType("puck");
        var activePucks = ReskinProfileManager.currentProfile.puckList ?? new List<ReskinRegistry.ReskinEntry>();

        // Rebuild dropdown choices to exclude pucks in active list
        List<ReskinRegistry.ReskinEntry> dropdownPuckReskins = new List<ReskinRegistry.ReskinEntry>();

        if (!activePucks.Any(p => ReskinProfileManager.IsDefaultPuckEntry(p)))
        {
            dropdownPuckReskins.Add(ReskinProfileManager.CreateDefaultPuckEntry());
        }

        foreach (var puck in allPuckReskins)
        {
            if (!activePucks.Any(p => p?.Path == puck.Path))
            {
                dropdownPuckReskins.Add(puck);
            }
        }

        puckDropdown.choices = dropdownPuckReskins;
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
            puckItemRow.style.justifyContent = Justify.SpaceBetween;
            puckItemRow.style.alignItems = Align.Center;
            puckItemRow.style.marginBottom = 8;
            puckItemRow.style.marginLeft = 15;
            puckItemRow.style.marginRight = 15;

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
                // Put the removed puck back into the dropdown.
                if (_puckDropdown != null)
                {
                    RefreshPuckDropdown(_puckDropdown);
                    if (!_puckDropdown.choices.Contains(_puckDropdown.value))
                        _puckDropdown.value = _puckDropdown.choices.Count > 0 ? _puckDropdown.choices[0] : null;
                }
                RefreshRandomizerList(contentScrollViewContent, puckReskins);
                PuckPreview.Refresh();
            });
            puckItemRow.Add(removeButton);

            container.Add(puckItemRow);
        }
    }
}