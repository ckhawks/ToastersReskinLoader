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
    // Order categories appear in (within a role node, or the global bucket).
    private static readonly List<string> CategoryOrder = new()
    {
        "Team identity", "Jersey", "Helmet", "Goalie gear", "Name & number", "Stick & tape",
        "Arena", "Puck", "Skybox", "Puck FX",
    };

    // Maps a field to its editor-aligned category. Mirrors PlayersSection so the save tree
    // reads the same as the Players editor.
    private static string CategoryOf(PresetField f)
    {
        switch (f.Group)
        {
            case "Arena": return "Arena";
            case "Puck": return "Puck";
            case "Skybox": return "Skybox";
            case "Puck FX": return "Puck FX";
            case "Team Colors": return "Team identity";
            case "Sticks":
            case "Tape": return "Stick & tape";
        }
        string id = f.Id;
        if (id.Contains("Torso") || id.Contains("Groin")) return "Jersey";
        if (id.Contains("Mask") || id.Contains("Cage") || id.Contains("LegPad")) return "Goalie gear";
        if (id.Contains("Helmet")) return "Helmet";
        if (id.Contains("Lettering") || id.Contains("NumberOutline")) return "Name & number";
        return f.Group;
    }

    private static VisualElement _root;

    // transient per-row UI state (keyed by preset file path)
    private static string _confirmDeletePath;
    private static string _renamingPath;

    public static void CreateSection(VisualElement contentScrollViewContent)
    {
        ChangingRoomHelper.ShowBaseFocus();
        _root = contentScrollViewContent;
        _confirmDeletePath = _renamingPath = null;
        RenderList();
    }

    private static string Plural(int n, string noun) => $"{n} {noun}{(n == 1 ? "" : "s")}";

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

        var userPresets = PresetStore.LoadUserPresets();
        var packPresets = PresetStore.LoadPackPresets();

        if (userPresets.Count == 0 && packPresets.Count == 0)
        {
            var empty = UITools.CreateConfigurationLabel(
                "No presets yet. Set up your reskins, then click \"Save current as preset\".");
            empty.style.color = new Color(0.6f, 0.6f, 0.6f);
            empty.style.marginTop = 8;
            _root.Add(empty);
            return;
        }

        // Only show group headings when both sources are present.
        bool showHeadings = userPresets.Count > 0 && packPresets.Count > 0;

        if (userPresets.Count > 0)
        {
            if (showHeadings) _root.Add(GroupHeading("My Presets"));
            foreach (var preset in userPresets) _root.Add(BuildPresetRow(preset));
        }

        if (packPresets.Count > 0)
        {
            if (showHeadings) _root.Add(GroupHeading("From Packs"));
            foreach (var preset in packPresets) _root.Add(BuildPresetRow(preset));
        }
    }

    private static Label GroupHeading(string text)
    {
        var label = UITools.CreateConfigurationLabel(text);
        label.style.fontSize = 18;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.marginTop = 10;
        label.style.marginBottom = 4;
        return label;
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
            var field = UITools.CreateConfigurationTextField(preset.PresetName);
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

        var meta = new List<string>
        {
            string.IsNullOrEmpty(preset.SourceLabel) ? "Local" : preset.SourceLabel,
            Plural(preset.FieldIds.Count(), "setting"),
        };
        if (preset.IsTeamScoped) meta.Add($"team preset ({preset.TeamScoped})");
        var missing = PresetStore.GetMissingDependencies(preset);
        var metaLabel = UITools.CreateConfigurationLabel(string.Join("  •  ", meta));
        metaLabel.style.fontSize = 12;
        metaLabel.style.color = new Color(0.65f, 0.65f, 0.65f);
        left.Add(metaLabel);
        if (missing.Count > 0)
        {
            var warn = UITools.CreateConfigurationLabel(
                $"⚠ missing {Plural(missing.Count, "pack")}: {string.Join(", ", missing.Select(m => m.Name))}");
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
        applyBtn.RegisterCallback<ClickEvent>(_ => RenderApplyConfirm(preset));
        right.Add(applyBtn);

        // Pack presets are read-only — apply only, no rename/delete.
        if (!preset.IsReadOnly)
        {
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
        }

        topRow.Add(right);
        container.Add(topRow);
        return container;
    }

    // ───────────────────────── apply confirmation ─────────────────────────

    private static void RenderApplyConfirm(Preset preset)
    {
        _root.Clear();

        var title = new Label($"Apply \"{preset.PresetName}\"");
        title.style.fontSize = 28;
        title.style.color = Color.white;
        title.style.marginBottom = 6;
        _root.Add(title);

        var sub = UITools.CreateConfigurationLabel(
            "This will change the following settings (anything not listed is left as-is):");
        sub.style.color = new Color(0.7f, 0.7f, 0.7f);
        sub.style.marginBottom = 10;
        _root.Add(sub);

        // Group the preset's fields by team bucket -> group for a readable summary.
        var fields = preset.FieldIds
            .Select(PresetFieldRegistry.ById)
            .Where(f => f != null)
            .ToList();

        foreach (var bucket in new[] { PresetTeam.Blue, PresetTeam.Red, PresetTeam.None })
        {
            var bucketFields = fields.Where(f => f.Team == bucket).ToList();
            if (bucketFields.Count == 0) continue;

            if (bucket != PresetTeam.None)
            {
                var b = UITools.CreateConfigurationLabel(bucket == PresetTeam.Blue ? "Blue-team settings" : "Red-team settings");
                b.style.unityFontStyleAndWeight = FontStyle.Bold;
                b.style.marginTop = 6;
                _root.Add(b);
            }

            foreach (var group in bucketFields.GroupBy(CategoryOf).OrderBy(g => CategoryIndex(g.Key)))
            {
                var line = UITools.CreateConfigurationLabel(
                    $"• {group.Key}: {string.Join(", ", group.OrderBy(f => f.DisplayName).Select(f => f.DisplayName))}");
                line.style.fontSize = 13;
                line.style.color = new Color(0.85f, 0.85f, 0.85f);
                line.style.whiteSpace = WhiteSpace.Normal;
                line.style.marginLeft = 8;
                _root.Add(line);
            }
        }

        var missing = PresetStore.GetMissingDependencies(preset);
        if (missing.Count > 0)
        {
            var warn = UITools.CreateConfigurationLabel(
                $"⚠ {Plural(missing.Count, "pack")} not installed — those reskins will be skipped: "
                + string.Join(", ", missing.Select(m => m.Name)));
            warn.style.color = new Color(0.95f, 0.75f, 0.4f);
            warn.style.whiteSpace = WhiteSpace.Normal;
            warn.style.marginTop = 8;
            _root.Add(warn);
        }

        // Action buttons: team choice for team presets, else a single Apply.
        var btnRow = UITools.CreateRow();
        btnRow.style.marginTop = 14;

        if (preset.IsTeamScoped)
        {
            var profile = ReskinProfileManager.currentProfile;
            string blueName = string.IsNullOrWhiteSpace(profile.blueTeamName) ? "Blue" : profile.blueTeamName;
            string redName = string.IsNullOrWhiteSpace(profile.redTeamName) ? "Red" : profile.redTeamName;

            btnRow.Add(UITools.CreateConfigurationLabel("Apply to:"));
            var spacer = new VisualElement(); spacer.style.width = 8; btnRow.Add(spacer);
            btnRow.Add(MakeApplyButton($"{blueName} team", preset, PresetTeam.Blue));
            btnRow.Add(MakeApplyButton($"{redName} team", preset, PresetTeam.Red));
        }
        else
        {
            btnRow.Add(MakeApplyButton("Apply", preset, PresetTeam.None));
        }

        var cancel = new Button { text = "Cancel" };
        UITools.StyleConfigButton(cancel);
        cancel.RegisterCallback<ClickEvent>(_ => RenderList());
        btnRow.Add(cancel);
        _root.Add(btnRow);
    }

    private static Button MakeApplyButton(string label, Preset preset, PresetTeam team)
    {
        var btn = new Button { text = label };
        UITools.StyleConfigButton(btn);
        btn.style.marginRight = 6;
        btn.RegisterCallback<ClickEvent>(_ =>
        {
            var result = PresetApplier.Apply(preset, team);
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
        var nameField = UITools.CreateConfigurationTextField("");
        nameField.style.flexGrow = 1;
        nameField.style.marginLeft = 8;
        nameRow.Add(nameField);
        _root.Add(nameRow);

        // Destination: My Presets, or a local pack's presets/ folder (pack-authoring).
        var destinations = new Dictionary<string, string> { { PresetStore.SourceUser, null } };
        foreach (var (label, dir) in PresetStore.GetLocalPackTargets())
            destinations[label] = dir;

        PopupField<string> destField = null;
        if (destinations.Count > 1)
        {
            var destRow = UITools.CreateRow();
            destRow.style.marginBottom = 10;
            destRow.Add(UITools.CreateConfigurationLabel("Save to:"));
            destField = UITools.CreateStringDropdownField(destinations.Keys.ToList(), PresetStore.SourceUser);
            destField.style.marginLeft = 8;
            destRow.Add(destField);
            _root.Add(destRow);
        }

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
            string targetDir = destField != null && destinations.TryGetValue(destField.value, out var d) ? d : null;
            PresetStore.Save(preset, targetDir);
            string where = destField != null ? destField.value : PresetStore.SourceUser;
            Toast("Saved", $"Preset \"{preset.PresetName}\" saved to {where} ({Plural(selected.Count, "setting")}).");
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
        var (header, body, _) = MakeCollapsible(label, bucketToggle, 16, true);
        _root.Add(header);
        _root.Add(body);

        var childToggles = new List<Toggle>();

        if (team == PresetTeam.None)
        {
            // Global: categories directly under the bucket (Arena / Puck / Skybox / Puck FX).
            foreach (var cat in fields.GroupBy(CategoryOf).OrderBy(g => CategoryIndex(g.Key)))
                childToggles.Add(BuildCategorySection(body, cat.Key, cat.ToList(), 18, toggles));
        }
        else
        {
            // Team: Team identity (team-level), then Skater and Goalie role nodes.
            var identity = fields.Where(f => f.Role == PresetRole.None).ToList();
            if (identity.Count > 0)
                childToggles.Add(BuildCategorySection(body, "Team identity", identity, 18, toggles));

            var skater = fields.Where(f => f.Role == PresetRole.Skater).ToList();
            if (skater.Count > 0)
                childToggles.Add(BuildRoleNode(body, "Skater", skater, 18, toggles));

            var goalie = fields.Where(f => f.Role == PresetRole.Goalie).ToList();
            if (goalie.Count > 0)
                childToggles.Add(BuildRoleNode(body, "Goalie", goalie, 18, toggles));
        }

        WireParent(bucketToggle, childToggles);
    }

    // A role node (Skater / Goalie): its own checkbox + a body of category subsections.
    private static Toggle BuildRoleNode(VisualElement parentBody, string name, List<PresetField> fields,
        int leftMargin, Dictionary<string, Toggle> toggles)
    {
        var nodeToggle = UITools.CreateConfigurationCheckbox(false);
        var (header, body, _) = MakeCollapsible(name, nodeToggle, 14, false);
        header.style.marginLeft = leftMargin;
        body.style.marginLeft = leftMargin + 4;
        parentBody.Add(header);
        parentBody.Add(body);

        var catToggles = new List<Toggle>();
        foreach (var cat in fields.GroupBy(CategoryOf).OrderBy(g => CategoryIndex(g.Key)))
            catToggles.Add(BuildCategorySection(body, cat.Key, cat.ToList(), leftMargin + 18, toggles));

        WireParent(nodeToggle, catToggles);
        return nodeToggle;
    }

    // A leaf category (Jersey / Helmet / …): its own checkbox + field rows. Returns the toggle.
    private static Toggle BuildCategorySection(VisualElement parentBody, string name, List<PresetField> fields,
        int leftMargin, Dictionary<string, Toggle> toggles)
    {
        var catToggle = UITools.CreateConfigurationCheckbox(false);
        var (header, body, _) = MakeCollapsible(name, catToggle, 13, false);
        header.style.marginLeft = leftMargin;
        body.style.marginLeft = leftMargin + 16;
        parentBody.Add(header);
        parentBody.Add(body);

        var leaves = new List<Toggle>();
        foreach (var field in fields.OrderBy(f => f.DisplayName))
        {
            var row = UITools.CreateRow();
            row.style.marginTop = 2;
            row.style.marginBottom = 2;
            var t = UITools.CreateConfigurationCheckbox(false);
            t.style.marginRight = 8;
            row.Add(t);
            var nameLabel = UITools.CreateConfigurationLabel(field.DisplayName);
            nameLabel.style.flexGrow = 1;
            row.Add(nameLabel);
            row.Add(BuildValuePreview(field));
            body.Add(row);
            toggles[field.Id] = t;
            leaves.Add(t);
        }

        WireParent(catToggle, leaves);
        return catToggle;
    }

    // Right-aligned preview of a field's current value: color swatch, texture thumbnail + name,
    // or text.
    private static VisualElement BuildValuePreview(PresetField field)
    {
        object value = field.GetValue(ReskinProfileManager.currentProfile);

        switch (field.Kind)
        {
            case PresetValueKind.Color:
                var swatch = new VisualElement();
                swatch.style.width = 24;
                swatch.style.height = 14;
                swatch.style.flexShrink = 0;
                swatch.style.marginLeft = 8;
                swatch.style.backgroundColor = value is Color col ? col : Color.clear;
                var bc = new Color(0.4f, 0.4f, 0.4f);
                swatch.style.borderTopWidth = 1; swatch.style.borderBottomWidth = 1;
                swatch.style.borderLeftWidth = 1; swatch.style.borderRightWidth = 1;
                swatch.style.borderTopColor = bc; swatch.style.borderBottomColor = bc;
                swatch.style.borderLeftColor = bc; swatch.style.borderRightColor = bc;
                return swatch;

            case PresetValueKind.ReskinRef:
                return BuildRefPreview(value as ReskinRegistry.ReskinEntry);

            case PresetValueKind.ReskinRefList:
                int n = (value as IEnumerable<ReskinRegistry.ReskinEntry>)?.Count(e => e != null) ?? 0;
                return TextPreview(n == 0 ? "none" : $"{n} item{(n == 1 ? "" : "s")}");

            case PresetValueKind.Bool:
                return TextPreview(value is bool b && b ? "On" : "Off");

            case PresetValueKind.Int:
                return TextPreview(value?.ToString() ?? "");

            case PresetValueKind.Float:
                return TextPreview(value is float fl ? fl.ToString("0.###") : "");

            case PresetValueKind.String:
                var s = value as string;
                return TextPreview(string.IsNullOrEmpty(s) ? "(default)" : s);

            default:
                return TextPreview("");
        }
    }

    private static VisualElement BuildRefPreview(ReskinRegistry.ReskinEntry entry)
    {
        var container = UITools.CreateRow();
        container.style.flexShrink = 0;
        container.style.marginLeft = 8;

        if (entry == null || entry.Path == null)
        {
            container.Add(TextPreview("Unchanged"));
            return container;
        }

        var thumb = new VisualElement();
        thumb.style.width = 18;
        thumb.style.height = 18;
        thumb.style.marginRight = 6;
        thumb.style.flexShrink = 0;
        try
        {
            var tex = TextureManager.GetTexture(entry);
            if (tex != null) thumb.style.backgroundImage = new StyleBackground(tex);
        }
        catch { /* preview only — ignore load failures */ }

        container.Add(thumb);
        container.Add(TextPreview(entry.Name));
        return container;
    }

    private static Label TextPreview(string text)
    {
        var l = UITools.CreateConfigurationLabel(text);
        l.style.fontSize = 12;
        l.style.color = new Color(0.6f, 0.6f, 0.6f);
        l.style.unityTextAlign = TextAnchor.MiddleRight;
        l.style.flexShrink = 0;
        l.style.marginLeft = 8;
        return l;
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

        // The ▾/▸ glyphs render small for their point size, so scale them up relative to the
        // row's font size (they otherwise look ~60% too small next to the labels).
        int chevronSize = Mathf.RoundToInt(fontSize * 1.7f);
        var chevron = new Label(startExpanded ? "▾" : "▸");
        chevron.style.color = Color.white;
        chevron.style.fontSize = chevronSize;
        chevron.style.width = chevronSize + 6;
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

    private static int CategoryIndex(string category)
    {
        int i = CategoryOrder.IndexOf(category);
        return i < 0 ? int.MaxValue : i;
    }

    // ───────────────────────── helpers ─────────────────────────

    private static void ToastApplied(Preset preset, PresetApplyResult result)
    {
        string msg = $"{Plural(result.AppliedCount, "setting")} applied"
            + (result.TeamSwapped ? " (team-swapped)" : "")
            + (result.MissingDependencies.Count > 0 ? $". Missing {Plural(result.MissingDependencies.Count, "pack")}." : ".");
        Toast($"Applied \"{preset.PresetName}\"", msg);
        RenderList();
    }

    private static void Toast(string title, string message)
    {
        try { MonoBehaviourSingleton<UIManager>.Instance?.ToastManager?.ShowToast(title, message, 5f); }
        catch { /* UIManager may not be ready */ }
    }
}
