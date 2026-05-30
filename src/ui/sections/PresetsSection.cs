using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.ui.sections;

public static class PresetsSection
{
    public static void CreateSection(VisualElement contentScrollViewContent)
    {
        contentScrollViewContent.Clear();

        // ── Track popups for dismissal ───────────────────────────────────
        var activePopups = new List<VisualElement>();
        // This will be assigned regardless of code path:
        VisualElement popupContainer = null;

        // ── Header ──────────────────────────────────────────────────────
        var titleRow = new VisualElement();
        titleRow.style.flexDirection = FlexDirection.Row;
        titleRow.style.justifyContent = Justify.SpaceBetween;
        titleRow.style.alignItems = Align.Center;
        titleRow.style.marginBottom = 16;

        var titleLabel = new Label("Presets");
        titleLabel.style.fontSize = 30;
        titleLabel.style.color = Color.white;
        titleRow.Add(titleLabel);

        contentScrollViewContent.Add(titleRow);

        var descriptionLabel = UITools.CreateConfigurationLabel(
            "Save your entire reskin setup — every swapper, color, arena, and all settings — into a named preset. " +
            "Load it back anytime to switch between setups instantly.");
        descriptionLabel.style.marginBottom = 16;
        descriptionLabel.style.whiteSpace = WhiteSpace.Normal;
        contentScrollViewContent.Add(descriptionLabel);

        // ── Save current ────────────────────────────────────────────────
        var saveRow = new VisualElement();
        saveRow.style.flexDirection = FlexDirection.Row;
        saveRow.style.alignItems = Align.Center;
        saveRow.style.marginBottom = 24;
        saveRow.style.marginTop = 8;

        var nameField = new TextField
        {
            value = "",
            style =
            {
                width = 340,
                minWidth = 340,
                height = 36,
                fontSize = 14,
                color = Color.white,
                backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f)),
                paddingLeft = 10,
                paddingRight = 10,
                marginRight = 10,
            }
        };

        var saveButton = new Button
        {
            text = "Save Current",
            style =
            {
                backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f)),
                unityTextAlign = TextAnchor.MiddleCenter,
                fontSize = 16,
                paddingTop = 6,
                paddingBottom = 6,
                paddingLeft = 16,
                paddingRight = 16,
                marginRight = 8,
                minWidth = 120,
            }
        };
        UITools.AddHoverEffectsForButton(saveButton);

        saveButton.RegisterCallback<ClickEvent>(_ =>
        {
            string name = nameField.value?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                ShowPopup("Preset name cannot be empty.", Color.yellow);
                return;
            }

            if (ToasterReskinLoader.PresetManager.PresetExists(name))
            {
                ShowConfirmation($"A preset named '<b>{name}</b>' already exists. Overwrite?",
                    onConfirm: () =>
                    {
                        ToasterReskinLoader.PresetManager.SavePreset(name);
                        ShowPopup($"Preset '<b>{name}</b>' saved!", new Color(0.3f, 1f, 0.3f));
                        nameField.value = "";
                        CreateSection(contentScrollViewContent);
                    });
                return;
            }

            ToasterReskinLoader.PresetManager.SavePreset(name);
            ShowPopup($"Preset '<b>{name}</b>' saved!", new Color(0.3f, 1f, 0.3f));
            nameField.value = "";
            CreateSection(contentScrollViewContent);
        });

        saveRow.Add(nameField);
        saveRow.Add(saveButton);
        contentScrollViewContent.Add(saveRow);

        // ── Separator ────────────────────────────────────────────────────
        var separator = new VisualElement();
        separator.style.height = 1;
        separator.style.backgroundColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
        separator.style.marginBottom = 16;
        contentScrollViewContent.Add(separator);

        // ── Saved presets ────────────────────────────────────────────────
        var presets = ToasterReskinLoader.PresetManager.GetPresets();

        // Define popup container early so local functions below can reference it
        popupContainer = new VisualElement();
        popupContainer.style.marginBottom = 8;

        if (presets.Count == 0)
        {
            var emptyLabel = UITools.CreateConfigurationLabel("No presets saved yet. Use the form above to save your first preset.");
            emptyLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            contentScrollViewContent.Add(emptyLabel);
            // Still add popupContainer for safety, but nothing to show
            contentScrollViewContent.Add(popupContainer);
            return;
        }

        var listHeader = UITools.CreateConfigurationLabel($"Saved Presets ({presets.Count})");
        listHeader.style.marginBottom = 12;
        contentScrollViewContent.Add(listHeader);
        contentScrollViewContent.Add(popupContainer);

        foreach (var preset in presets)
        {
            var row = UITools.CreateConfigurationRow();
            row.style.alignItems = Align.Center;

            // Left: name + date
            var leftCol = new VisualElement();
            leftCol.style.flexDirection = FlexDirection.Column;

            var nameLabel = UITools.CreateConfigurationLabel($"<b>{preset.Name}</b>");
            nameLabel.enableRichText = true;
            leftCol.Add(nameLabel);

            var dateLabel = UITools.CreateConfigurationLabel(preset.GetDisplayDate());
            dateLabel.style.fontSize = 11;
            dateLabel.style.color = new Color(0.55f, 0.55f, 0.55f);
            leftCol.Add(dateLabel);

            row.Add(leftCol);

            // Right: buttons
            var rightCol = new VisualElement();
            rightCol.style.flexDirection = FlexDirection.Row;
            rightCol.style.alignItems = Align.Center;

            // Rename button
            var renameBtn = new Button { text = "Rename" };
            UITools.StyleConfigButton(renameBtn);
            renameBtn.style.minWidth = 70;
            renameBtn.RegisterCallback<ClickEvent>(_ =>
            {
                ShowRenamePopup(preset, newName =>
                {
                    if (string.IsNullOrWhiteSpace(newName)) return;
                    if (newName == preset.Name) return;
                    if (ToasterReskinLoader.PresetManager.PresetExists(newName))
                    {
                        ShowPopup($"A preset named '<b>{newName}</b>' already exists.", Color.yellow);
                        return;
                    }
                    ToasterReskinLoader.PresetManager.RenamePreset(preset.FilePath, newName);
                    ShowPopup($"Renamed to '<b>{newName}</b>'.", new Color(0.3f, 1f, 0.3f));
                    CreateSection(contentScrollViewContent);
                });
            });
            rightCol.Add(renameBtn);

            // Load button
            var loadBtn = new Button { text = "Load" };
            UITools.StyleConfigButton(loadBtn);
            loadBtn.style.minWidth = 60;
            string capturedName = preset.Name;
            string capturedPath = preset.FilePath;
            loadBtn.RegisterCallback<ClickEvent>(_ =>
            {
                ShowConfirmation(
                    $"Load preset '<b>{capturedName}</b>'?<br><size=12>This will overwrite your current reskin setup.</size>",
                    onConfirm: () =>
                    {
                        if (ToasterReskinLoader.PresetManager.LoadPreset(capturedPath))
                        {
                            ShowPopup($"Preset '<b>{capturedName}</b>' loaded!", new Color(0.3f, 1f, 0.3f));
                            CreateSection(contentScrollViewContent);
                        }
                        else
                        {
                            ShowPopup($"Failed to load preset '<b>{capturedName}</b>'.", Color.red);
                        }
                    });
            });
            rightCol.Add(loadBtn);

            // Delete button
            var deleteBtn = new Button { text = "Delete" };
            UITools.StyleConfigButton(deleteBtn);
            deleteBtn.style.minWidth = 70;
            string delName = preset.Name;
            string delPath = preset.FilePath;
            deleteBtn.RegisterCallback<ClickEvent>(_ =>
            {
                ShowConfirmation(
                    $"Delete preset '<b>{delName}</b>'?<br><size=12>This cannot be undone.</size>",
                    onConfirm: () =>
                    {
                        if (ToasterReskinLoader.PresetManager.DeletePreset(delPath))
                        {
                            ShowPopup($"Preset '<b>{delName}</b>' deleted.", new Color(0.3f, 1f, 0.3f));
                            CreateSection(contentScrollViewContent);
                        }
                        else
                        {
                            ShowPopup($"Failed to delete preset.", Color.red);
                        }
                    });
            });
            rightCol.Add(deleteBtn);

            row.Add(rightCol);
            row.style.marginBottom = 4;
            contentScrollViewContent.Add(row);
        }

        // ─────────────────────────────────────────────────────────────────
        // Local popup helper functions
        // ─────────────────────────────────────────────────────────────────

        void ShowPopup(string message, Color color)
        {
            popupContainer.Clear();
            activePopups.Clear();

            var box = new VisualElement();
            box.style.flexDirection = FlexDirection.Row;
            box.style.alignItems = Align.Center;
            box.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.18f, 0.95f));
            box.style.paddingTop = 8;
            box.style.paddingBottom = 8;
            box.style.paddingLeft = 12;
            box.style.paddingRight = 12;

            var msg = new Label(message);
            msg.enableRichText = true;
            msg.style.color = color;
            msg.style.fontSize = 13;
            msg.style.whiteSpace = WhiteSpace.Normal;
            box.Add(msg);

            popupContainer.Add(box);
            activePopups.Add(box);

            box.schedule.Execute(() =>
            {
                if (box.parent != null) box.RemoveFromHierarchy();
                activePopups.Remove(box);
            }).ExecuteLater(3000);
        }

        void ShowConfirmation(string message, Action onConfirm)
        {
            popupContainer.Clear();
            activePopups.Clear();

            var box = new VisualElement();
            box.style.flexDirection = FlexDirection.Column;
            box.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.18f, 0.95f));
            box.style.paddingTop = 10;
            box.style.paddingBottom = 10;
            box.style.paddingLeft = 12;
            box.style.paddingRight = 12;

            var msg = new Label(message);
            msg.enableRichText = true;
            msg.style.color = Color.white;
            msg.style.fontSize = 13;
            msg.style.whiteSpace = WhiteSpace.Normal;
            msg.style.marginBottom = 8;
            box.Add(msg);

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.alignItems = Align.Center;

            var cancelBtn = new Button { text = "Cancel" };
            UITools.StyleConfigButton(cancelBtn);
            cancelBtn.RegisterCallback<ClickEvent>(_ => box.RemoveFromHierarchy());
            btnRow.Add(cancelBtn);

            var confirmBtn = new Button { text = "Confirm" };
            UITools.StyleConfigButton(confirmBtn);
            confirmBtn.RegisterCallback<ClickEvent>(_ =>
            {
                box.RemoveFromHierarchy();
                onConfirm();
            });
            btnRow.Add(confirmBtn);

            box.Add(btnRow);
            popupContainer.Add(box);
            activePopups.Add(box);
        }

        void ShowRenamePopup(ToasterReskinLoader.PresetManager.PresetInfo presetInfo, Action<string> onSubmit)
        {
            popupContainer.Clear();
            activePopups.Clear();

            var box = new VisualElement();
            box.style.flexDirection = FlexDirection.Column;
            box.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.18f, 0.95f));
            box.style.paddingTop = 10;
            box.style.paddingBottom = 10;
            box.style.paddingLeft = 12;
            box.style.paddingRight = 12;

            var renameField = new TextField
            {
                value = presetInfo.Name,
                style =
                {
                    width = 300,
                    height = 36,
                    fontSize = 14,
                    color = Color.white,
                    backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.12f)),
                    paddingLeft = 10,
                    paddingRight = 10,
                    marginBottom = 8,
                }
            };
            box.Add(renameField);

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.alignItems = Align.Center;

            var cancelBtn = new Button { text = "Cancel" };
            UITools.StyleConfigButton(cancelBtn);
            cancelBtn.RegisterCallback<ClickEvent>(_ => box.RemoveFromHierarchy());
            btnRow.Add(cancelBtn);

            var confirmBtn = new Button { text = "Rename" };
            UITools.StyleConfigButton(confirmBtn);
            confirmBtn.RegisterCallback<ClickEvent>(_ =>
            {
                box.RemoveFromHierarchy();
                onSubmit(renameField.value?.Trim());
            });
            btnRow.Add(confirmBtn);

            box.Add(btnRow);
            popupContainer.Add(box);
            activePopups.Add(box);

            renameField.schedule.Execute(() =>
            {
                renameField.Focus();
            }).ExecuteLater(50);
        }
    }
}
