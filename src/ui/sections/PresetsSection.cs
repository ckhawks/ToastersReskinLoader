using System.Collections.Generic;
using System.Linq;
using ToasterReskinLoader.presets;
using ToasterReskinLoader.ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.ui.sections;

/// <summary>
/// The Presets tab: list / apply / rename / delete user presets, reset all to defaults,
/// and a "save current as preset" view with a Blue Team / Red Team / Global checkbox tree.
/// Pack-bundled presets are a later phase.
/// </summary>
public static class PresetsSection
{
    // Order groups appear in within each team bucket / the global bucket.
    private static readonly List<string> GroupOrder = new()
    {
        "Skaters", "Goalies", "Sticks", "Tape", "Team Colors", "Minimap",
        "Puck", "Arena", "Skybox", "Shadows", "Puck FX", "Gloss", "Chat",
    };

    private static VisualElement _root;

    // transient per-row UI state (keyed by preset file path)
    private static string _confirmDeletePath;
    private static string _renamingPath;
    private static string _applyTeamPath;

    public static void CreateSection(VisualElement contentScrollViewContent)
    {
        ChangingRoomHelper.ShowBaseFocus();
        _root = contentScrollViewContent;
        _confirmDeletePath = _renamingPath = _applyTeamPath = null;
        RenderList();
    }

    // ───────────────────────── list view ─────────────────────────

    private static void RenderList()
    {
        _root.Clear();

        var titleRow = new VisualElement();
        titleRow.style.flexDirection = FlexDirection.Row;
        titleRow.style.justifyContent = Justify.SpaceBetween;
        titleRow.style.alignItems = Align.Center;
        titleRow.style.marginBottom = 8;

        var title = new Label("Presets");
        title.style.fontSize = 30;
        title.style.color = Color.white;
        titleRow.Add(title);

        var saveBtn = new Button { text = "Save current as preset" };
        UITools.StyleConfigButton(saveBtn);
        saveBtn.RegisterCallback<ClickEvent>(_ => RenderSaveView());
        titleRow.Add(saveBtn);
        _root.Add(titleRow);

        var sub = UITools.CreateConfigurationLabel(
            "Apply a saved look, or save your current settings as a new preset. "
            + "Partial presets only change the settings they contain.");
        sub.style.color = new Color(0.7f, 0.7f, 0.7f);
        sub.style.marginBottom = 12;
        _root.Add(sub);

        // top action row: reset-all + open folder
        var actions = new VisualElement();
        actions.style.flexDirection = FlexDirection.Row;
        actions.style.marginBottom = 14;

        var resetBtn = new Button { text = "Reset all to defaults" };
        UITools.StyleConfigButton(resetBtn);
        resetBtn.style.marginRight = 8;
        bool resetArmed = false;
        resetBtn.RegisterCallback<ClickEvent>(_ =>
        {
            if (!resetArmed)
            {
                resetArmed = true;
                resetBtn.text = "Click again to confirm";
                resetBtn.schedule.Execute(() => { resetArmed = false; resetBtn.text = "Reset all to defaults"; }).ExecuteLater(3000);
                return;
            }
            ReskinProfileManager.ResetAllToDefault();
            PresetApplier.RefreshWorld();
            Toast("Reset", "All reskin settings restored to defaults.");
            RenderList();
        });
        actions.Add(resetBtn);

        var folderBtn = new Button { text = "Open presets folder" };
        UITools.StyleConfigButton(folderBtn);
        folderBtn.RegisterCallback<ClickEvent>(_ =>
        {
            var dir = PresetStore.UserPresetsDir;
            if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
            Application.OpenURL("file:///" + dir.Replace('\\', '/'));
        });
        actions.Add(folderBtn);
        _root.Add(actions);

        var presets = PresetStore.LoadUserPresets();
        if (presets.Count == 0)
        {
            var empty = UITools.CreateConfigurationLabel(
                "No presets yet. Set up your reskins, then click \"Save current as preset\".");
            empty.style.color = new Color(0.6f, 0.6f, 0.6f);
            empty.style.marginTop = 8;
            _root.Add(empty);
            return;
        }

        foreach (var preset in presets)
            _root.Add(BuildPresetRow(preset));
    }

    private static VisualElement BuildPresetRow(Preset preset)
    {
        var container = new VisualElement();
        container.style.flexDirection = FlexDirection.Column;
        container.style.marginBottom = 6;
        container.style.paddingTop = 8;
        container.style.paddingBottom = 8;
        container.style.paddingLeft = 10;
        container.style.paddingRight = 10;
        container.style.backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.12f, 0.6f));
        container.style.borderTopLeftRadius = 4;
        container.style.borderTopRightRadius = 4;
        container.style.borderBottomLeftRadius = 4;
        container.style.borderBottomRightRadius = 4;

        // ── rename mode ──
        if (_renamingPath == preset.FilePath)
        {
            var row = UITools.CreateRow();
            var field = new TextField { value = preset.PresetName };
            field.style.flexGrow = 1;
            field.style.marginRight = 8;
            row.Add(field);

            var ok = new Button { text = "Save" };
            UITools.StyleConfigButton(ok);
            ok.style.marginRight = 6;
            ok.RegisterCallback<ClickEvent>(_ =>
            {
                if (!string.IsNullOrWhiteSpace(field.value))
                    PresetStore.Rename(preset, field.value);
                _renamingPath = null;
                RenderList();
            });
            row.Add(ok);

            var cancel = new Button { text = "Cancel" };
            UITools.StyleConfigButton(cancel);
            cancel.RegisterCallback<ClickEvent>(_ => { _renamingPath = null; RenderList(); });
            row.Add(cancel);

            container.Add(row);
            return container;
        }

        var topRow = new VisualElement();
        topRow.style.flexDirection = FlexDirection.Row;
        topRow.style.justifyContent = Justify.SpaceBetween;
        topRow.style.alignItems = Align.Center;

        // left: name + badges
        var left = new VisualElement();
        left.style.flexDirection = FlexDirection.Column;
        var nameLabel = UITools.CreateConfigurationLabel(preset.PresetName);
        nameLabel.style.fontSize = 17;
        left.Add(nameLabel);

        var meta = new List<string> { $"{preset.FieldIds.Count()} setting(s)" };
        if (preset.IsTeamScoped) meta.Add($"team preset ({preset.TeamScoped})");
        var missing = PresetStore.GetMissingDependencies(preset);
        var metaLabel = UITools.CreateConfigurationLabel(string.Join("  •  ", meta));
        metaLabel.style.fontSize = 12;
        metaLabel.style.color = new Color(0.65f, 0.65f, 0.65f);
        left.Add(metaLabel);
        if (missing.Count > 0)
        {
            var warn = UITools.CreateConfigurationLabel(
                $"⚠ missing {missing.Count} pack(s): {string.Join(", ", missing.Select(m => m.Name))}");
            warn.style.fontSize = 12;
            warn.style.color = new Color(0.95f, 0.75f, 0.4f);
            warn.style.whiteSpace = WhiteSpace.Normal;
            left.Add(warn);
        }
        topRow.Add(left);

        // right: actions
        var right = new VisualElement();
        right.style.flexDirection = FlexDirection.Row;
        right.style.alignItems = Align.Center;

        var applyBtn = new Button { text = "Apply" };
        UITools.StyleConfigButton(applyBtn);
        applyBtn.style.marginRight = 6;
        applyBtn.RegisterCallback<ClickEvent>(_ =>
        {
            if (preset.IsTeamScoped)
            {
                _applyTeamPath = _applyTeamPath == preset.FilePath ? null : preset.FilePath;
                RenderList();
            }
            else
            {
                var result = PresetApplier.Apply(preset);
                ToastApplied(preset, result);
            }
        });
        right.Add(applyBtn);

        var renameBtn = new Button { text = "Rename" };
        UITools.StyleConfigButton(renameBtn);
        renameBtn.style.marginRight = 6;
        renameBtn.RegisterCallback<ClickEvent>(_ => { _renamingPath = preset.FilePath; RenderList(); });
        right.Add(renameBtn);

        var deleteBtn = new Button { text = _confirmDeletePath == preset.FilePath ? "Confirm?" : "Delete" };
        UITools.StyleConfigButton(deleteBtn);
        deleteBtn.RegisterCallback<ClickEvent>(_ =>
        {
            if (_confirmDeletePath != preset.FilePath)
            {
                _confirmDeletePath = preset.FilePath;
                RenderList();
                return;
            }
            PresetStore.Delete(preset);
            _confirmDeletePath = null;
            RenderList();
        });
        right.Add(deleteBtn);

        topRow.Add(right);
        container.Add(topRow);

        // ── inline "apply to which team?" row ──
        if (_applyTeamPath == preset.FilePath && preset.IsTeamScoped)
        {
            var profile = ReskinProfileManager.currentProfile;
            string blueName = string.IsNullOrWhiteSpace(profile.blueTeamName) ? "Blue" : profile.blueTeamName;
            string redName = string.IsNullOrWhiteSpace(profile.redTeamName) ? "Red" : profile.redTeamName;

            var teamRow = UITools.CreateRow();
            teamRow.style.marginTop = 8;
            var prompt = UITools.CreateConfigurationLabel("Apply to which team?");
            prompt.style.marginRight = 10;
            teamRow.Add(prompt);

            teamRow.Add(MakeTeamButton(blueName, preset, PresetTeam.Blue));
            teamRow.Add(MakeTeamButton(redName, preset, PresetTeam.Red));
            container.Add(teamRow);
        }

        return container;
    }

    private static Button MakeTeamButton(string label, Preset preset, PresetTeam team)
    {
        var btn = new Button { text = label };
        UITools.StyleConfigButton(btn);
        btn.style.marginRight = 6;
        btn.RegisterCallback<ClickEvent>(_ =>
        {
            var result = PresetApplier.Apply(preset, team);
            _applyTeamPath = null;
            ToastApplied(preset, result);
        });
        return btn;
    }

    // ───────────────────────── save view (checkbox tree) ─────────────────────────

    private static void RenderSaveView()
    {
        _root.Clear();
        var toggles = new Dictionary<string, Toggle>();

        var title = new Label("Save preset");
        title.style.fontSize = 30;
        title.style.color = Color.white;
        title.style.marginBottom = 8;
        _root.Add(title);

        var nameRow = UITools.CreateRow();
        nameRow.style.marginBottom = 10;
        nameRow.Add(UITools.CreateConfigurationLabel("Name:"));
        var nameField = new TextField { value = "" };
        nameField.style.flexGrow = 1;
        nameField.style.marginLeft = 8;
        nameRow.Add(nameField);
        _root.Add(nameRow);

        // quick-fill buttons
        var quick = new VisualElement();
        quick.style.flexDirection = FlexDirection.Row;
        quick.style.flexWrap = Wrap.Wrap;
        quick.style.marginBottom = 10;
        quick.Add(QuickFill("Blue Team", toggles, f => f.Team == PresetTeam.Blue, true));
        quick.Add(QuickFill("Red Team", toggles, f => f.Team == PresetTeam.Red, true));
        quick.Add(QuickFill("All Global", toggles, f => f.Team == PresetTeam.None, true));
        quick.Add(QuickFill("Everything", toggles, _ => true, true));
        quick.Add(QuickFill("Clear", toggles, _ => true, false));
        _root.Add(quick);

        // tree: bucket -> group -> field
        BuildBucket("Blue Team", PresetTeam.Blue, toggles);
        BuildBucket("Red Team", PresetTeam.Red, toggles);
        BuildBucket("Global", PresetTeam.None, toggles);

        // save / cancel
        var btnRow = UITools.CreateRow();
        btnRow.style.marginTop = 14;
        var save = new Button { text = "Save preset" };
        UITools.StyleConfigButton(save);
        save.style.marginRight = 8;
        save.RegisterCallback<ClickEvent>(_ =>
        {
            var selected = toggles.Where(kv => kv.Value.value).Select(kv => kv.Key).ToList();
            if (string.IsNullOrWhiteSpace(nameField.value))
            {
                Toast("Name required", "Give the preset a name first.");
                return;
            }
            if (selected.Count == 0)
            {
                Toast("Nothing selected", "Tick at least one setting to save.");
                return;
            }
            var preset = PresetBuilder.FromProfile(ReskinProfileManager.currentProfile, selected, nameField.value.Trim());
            PresetStore.Save(preset);
            Toast("Saved", $"Preset \"{preset.PresetName}\" saved ({selected.Count} setting(s)).");
            RenderList();
        });
        btnRow.Add(save);

        var cancel = new Button { text = "Cancel" };
        UITools.StyleConfigButton(cancel);
        cancel.RegisterCallback<ClickEvent>(_ => RenderList());
        btnRow.Add(cancel);
        _root.Add(btnRow);
    }

    private static Button QuickFill(string label, Dictionary<string, Toggle> toggles,
        System.Func<PresetField, bool> match, bool value)
    {
        var btn = new Button { text = label };
        UITools.StyleConfigButton(btn);
        btn.style.marginRight = 6;
        btn.style.marginBottom = 6;
        btn.RegisterCallback<ClickEvent>(_ =>
        {
            foreach (var f in PresetFieldRegistry.All.Where(match))
                if (toggles.TryGetValue(f.Id, out var t)) t.value = value;
        });
        return btn;
    }

    private static void BuildBucket(string label, PresetTeam team, Dictionary<string, Toggle> toggles)
    {
        var fields = PresetFieldRegistry.All.Where(f => f.Team == team).ToList();
        if (fields.Count == 0) return;

        var bucketToggle = UITools.CreateConfigurationCheckbox(false);
        var (header, body, childToggles) = MakeCollapsible(label, bucketToggle, 16, true);
        _root.Add(header);
        _root.Add(body);

        var groups = fields.GroupBy(f => f.Group)
            .OrderBy(g => GroupIndex(g.Key));

        foreach (var group in groups)
        {
            var groupToggle = UITools.CreateConfigurationCheckbox(false);
            var (gHeader, gBody, gChildToggles) = MakeCollapsible(group.Key, groupToggle, 14, false);
            gHeader.style.marginLeft = 18;
            gBody.style.marginLeft = 34;
            body.Add(gHeader);
            body.Add(gBody);

            var fieldToggleList = new List<Toggle>();
            foreach (var field in group.OrderBy(f => f.DisplayName))
            {
                var row = UITools.CreateRow();
                row.style.marginTop = 2;
                row.style.marginBottom = 2;
                var t = UITools.CreateConfigurationCheckbox(false);
                t.style.marginRight = 8;
                row.Add(t);
                row.Add(UITools.CreateConfigurationLabel(field.DisplayName));
                gBody.Add(row);
                toggles[field.Id] = t;
                fieldToggleList.Add(t);
            }

            WireParent(groupToggle, fieldToggleList);
            childToggles.Add(groupToggle);
        }

        WireParent(bucketToggle, childToggles);
    }

    // Header row with a selection checkbox + a click-to-expand label/chevron, plus the body
    // container it shows/hides. Returns (header, body, childTogglesAccumulator).
    private static (VisualElement header, VisualElement body, List<Toggle> children) MakeCollapsible(
        string label, Toggle selectionToggle, int fontSize, bool startExpanded)
    {
        var header = UITools.CreateRow();
        header.style.marginTop = 4;

        selectionToggle.style.marginRight = 8;
        header.Add(selectionToggle);

        var chevron = new Label(startExpanded ? "▾" : "▸");
        chevron.style.color = Color.white;
        chevron.style.fontSize = fontSize;
        chevron.style.width = 16;
        chevron.style.unityTextAlign = TextAnchor.MiddleCenter;
        header.Add(chevron);

        var text = UITools.CreateConfigurationLabel(label);
        text.style.fontSize = fontSize;
        text.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.Add(text);

        var body = new VisualElement();
        body.style.flexDirection = FlexDirection.Column;
        body.style.display = startExpanded ? DisplayStyle.Flex : DisplayStyle.None;

        void Toggle()
        {
            bool open = body.style.display == DisplayStyle.None;
            body.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            chevron.text = open ? "▾" : "▸";
        }
        chevron.RegisterCallback<ClickEvent>(_ => Toggle());
        text.RegisterCallback<ClickEvent>(_ => Toggle());

        return (header, body, new List<Toggle>());
    }

    // Parent reflects "all children ticked"; ticking the parent sets all children. A guard
    // prevents the parent<->child callbacks from re-triggering each other.
    private static void WireParent(Toggle parent, List<Toggle> children)
    {
        bool suppress = false;
        parent.RegisterValueChangedCallback(evt =>
        {
            if (suppress) return;
            suppress = true;
            foreach (var c in children) c.value = evt.newValue;
            suppress = false;
        });
        foreach (var c in children)
        {
            c.RegisterValueChangedCallback(_ =>
            {
                if (suppress) return;
                suppress = true;
                parent.value = children.All(x => x.value);
                suppress = false;
            });
        }
    }

    private static int GroupIndex(string group)
    {
        int i = GroupOrder.IndexOf(group);
        return i < 0 ? int.MaxValue : i;
    }

    // ───────────────────────── helpers ─────────────────────────

    private static void ToastApplied(Preset preset, PresetApplyResult result)
    {
        string msg = $"{result.AppliedCount} setting(s) applied"
            + (result.TeamSwapped ? " (team-swapped)" : "")
            + (result.MissingDependencies.Count > 0 ? $". Missing {result.MissingDependencies.Count} pack(s)." : ".");
        Toast($"Applied \"{preset.PresetName}\"", msg);
        RenderList();
    }

    private static void Toast(string title, string message)
    {
        try { MonoBehaviourSingleton<UIManager>.Instance?.ToastManager?.ShowToast(title, message, 5f); }
        catch { /* UIManager may not be ready */ }
    }
}
