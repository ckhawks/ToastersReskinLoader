using System.Collections.Generic;
using System.Linq;
using ToasterReskinLoader.presets;
using ToasterReskinLoader.swappers;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.ui.sections;

/// <summary>
/// The 2x2 player editor: a {Blue/Red} x {Skater/Goalie} grid. You pick a cell with the team
/// and role toggles, edit just that cell's appearance, and "copy from" another cell to clone
/// settings across teams or roles. Controls are generated from the field registry and applied
/// via the locker-room preview, which re-drives the whole cell on each change.
///
/// Scope: appearance only (jersey, helmet, colors, lettering, outline, goalie gear). Sticks and
/// tape keep their own sections for now.
/// </summary>
public static class PlayersSection
{
    private static PresetTeam _team = PresetTeam.Blue;
    private static PresetRole _role = PresetRole.Skater;
    private static VisualElement _root;

    // Profile defaults, for the team colors used when custom team colors are turned off.
    private static readonly ReskinProfileManager.Profile Defaults = new ReskinProfileManager.Profile();

    // The team color actually in effect: the custom one only when team colors are enabled,
    // otherwise the default. (Reusing the custom value while disabled is a recurring trap.)
    private static Color EffectiveTeamColor(PresetTeam team)
    {
        var p = ReskinProfileManager.currentProfile;
        bool enabled = team == PresetTeam.Blue ? p.blueTeamColorEnabled : p.redTeamColorEnabled;
        if (enabled)
            return team == PresetTeam.Blue ? p.blueTeamColor : p.redTeamColor;
        return team == PresetTeam.Blue ? Defaults.blueTeamColor : Defaults.redTeamColor;
    }

    public static void CreateSection(VisualElement contentScrollViewContent)
    {
        _root = contentScrollViewContent;
        contentScrollViewContent.schedule.Execute(ChangingRoomHelper.ShowBody).ExecuteLater(2);
        Render();
    }

    private static void Render()
    {
        _root.Clear();

        var title = new Label("Players");
        title.style.fontSize = 30;
        title.style.color = Color.white;
        title.style.marginBottom = 8;
        _root.Add(title);

        // Team + role toggles in one row
        var toggles = UITools.CreateRow();
        toggles.style.flexWrap = Wrap.Wrap;

        var teamLabel = UITools.CreateConfigurationLabel("Team:");
        teamLabel.style.width = 55;
        toggles.Add(teamLabel);
        toggles.Add(MakeToggleButton(TeamName(PresetTeam.Blue), _team == PresetTeam.Blue,
            EffectiveTeamColor(PresetTeam.Blue), () => { _team = PresetTeam.Blue; Render(); }));
        toggles.Add(MakeToggleButton(TeamName(PresetTeam.Red), _team == PresetTeam.Red,
            EffectiveTeamColor(PresetTeam.Red), () => { _team = PresetTeam.Red; Render(); }));

        var roleLabel = UITools.CreateConfigurationLabel("Role:");
        roleLabel.style.width = 55;
        roleLabel.style.marginLeft = 20;
        toggles.Add(roleLabel);
        toggles.Add(MakeToggleButton("Skater", _role == PresetRole.Skater,
            null, () => { _role = PresetRole.Skater; Render(); }));
        toggles.Add(MakeToggleButton("Goalie", _role == PresetRole.Goalie,
            null, () => { _role = PresetRole.Goalie; Render(); }));
        _root.Add(toggles);

        _root.Add(BuildCopyRow());

        var divider = new VisualElement();
        divider.style.height = 1;
        divider.style.backgroundColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
        divider.style.marginTop = 8;
        divider.style.marginBottom = 10;
        _root.Add(divider);

        var heading = new Label($"Editing: {TeamName(_team)} {(_role == PresetRole.Goalie ? "Goalie" : "Skater")}");
        heading.style.fontSize = 24;
        heading.style.unityFontStyleAndWeight = FontStyle.Bold;
        heading.style.color = Color.white;
        heading.style.marginBottom = 8;
        _root.Add(heading);

        RenderTeamIdentity();

        var cellFields = CellFields(_team, _role);
        var appearance = cellFields.Where(f => f.Group != "Sticks" && f.Group != "Tape").ToList();
        var teamStick = cellFields.FirstOrDefault(f => f.Group == "Sticks" && !f.Id.Contains("Personal"));
        var personalStick = cellFields.FirstOrDefault(f => f.Group == "Sticks" && f.Id.Contains("Personal"));

        // Appearance, grouped into categories with sub-headings.
        foreach (var category in new[] { "Jersey", "Helmet", "Goalie gear", "Name & number" })
        {
            var catFields = appearance.Where(f => CategoryOf(f) == category).ToList();
            if (catFields.Count == 0) continue;
            _root.Add(SubHeading(category, null));
            foreach (var f in catFields) RenderField(f);
        }

        // Team stick — everyone on this team/role gets it.
        _root.Add(SubHeading("Team stick", "Applies to the whole team."));
        if (teamStick != null) RenderField(teamStick, "Team stick");

        // Personal — only the local player sees their own stick + tape. (Tape is local-only until
        // the team/personal tape split lands — see docs/presets-backlog.md.)
        _root.Add(SubHeading("Your stick (personal)", "Only you see this — your own stick skin and tape."));
        if (personalStick != null) RenderField(personalStick, "Your stick skin");
        RenderTapeControl(_team, _role, "Blade");
        RenderTapeControl(_team, _role, "Shaft");

        Preview();
    }

    // Team-level identity (name + custom color) — shared by both roles, part of the
    // shareable reskin profile.
    private static void RenderTeamIdentity()
    {
        var profile = ReskinProfileManager.currentProfile;
        bool blue = _team == PresetTeam.Blue;

        _root.Add(SubHeading("Team identity", "Shared by both roles — name and custom UI color for this team."));

        var nameRow = UITools.CreateConfigurationRow();
        nameRow.Add(UITools.CreateConfigurationLabel("Team name"));
        var nameField = UITools.CreateConfigurationTextField(blue ? profile.blueTeamName : profile.redTeamName);
        nameField.style.flexGrow = 1;
        nameField.style.marginLeft = 8;
        nameField.RegisterValueChangedCallback(evt =>
        {
            if (blue) profile.blueTeamName = evt.newValue; else profile.redTeamName = evt.newValue;
            ReskinProfileManager.SaveProfile();
            try { TeamColorSwapper.RefreshAll(); } catch { }
        });
        nameRow.Add(nameField);
        _root.Add(nameRow);

        var enableRow = UITools.CreateConfigurationRow();
        enableRow.Add(UITools.CreateConfigurationLabel("Custom team color"));
        var enableToggle = UITools.CreateConfigurationCheckbox(blue ? profile.blueTeamColorEnabled : profile.redTeamColorEnabled);
        enableToggle.RegisterValueChangedCallback(evt =>
        {
            if (blue) profile.blueTeamColorEnabled = evt.newValue; else profile.redTeamColorEnabled = evt.newValue;
            RefreshTeamColors();
            Render(); // refresh the team toggle button color
        });
        enableRow.Add(enableToggle);
        _root.Add(enableRow);

        var colorRow = UITools.CreateColorConfigurationRow(
            "Team color",
            blue ? profile.blueTeamColor : profile.redTeamColor,
            false,
            c => { if (blue) profile.blueTeamColor = c; else profile.redTeamColor = c; RefreshTeamColors(); },
            ReskinProfileManager.SaveProfile);
        _root.Add(colorRow);

        // Gray out / disable the color picker unless the custom-color checkbox is on.
        // (The enable toggle re-renders the section, so this reflects the current state.)
        bool colorEnabled = blue ? profile.blueTeamColorEnabled : profile.redTeamColorEnabled;
        colorRow.SetEnabled(colorEnabled);
        colorRow.style.opacity = colorEnabled ? 1f : 0.5f;
    }

    private static void RefreshTeamColors()
    {
        ReskinProfileManager.SaveProfile();
        try { TeamColorSwapper.RefreshAll(); } catch { }
        try { ArenaSwapper.UpdateGoalFrameColors(); } catch { }
        try { TeamIndicatorSwapper.UpdateVisibility(); } catch { }
        try { ToasterReskinLoaderAPI.NotifyTeamColorsChanged(); } catch { }
    }

    // Coarse category for a cell field, used for the in-editor sub-headings.
    private static string CategoryOf(PresetField f)
    {
        string id = f.Id;
        if (id.Contains("Torso") || id.Contains("Groin")) return "Jersey";
        if (id.Contains("Mask") || id.Contains("Cage") || id.Contains("LegPad")) return "Goalie gear";
        if (id.Contains("Helmet")) return "Helmet";
        if (id.Contains("Lettering") || id.Contains("NumberOutline")) return "Name & number";
        return "Jersey";
    }

    // ───────────────────────── field rendering ─────────────────────────

    private static List<PresetField> CellFields(PresetTeam team, PresetRole role)
        => PresetFieldRegistry.All
            .Where(f => f.Team == team && f.Role == role)
            .ToList();

    private static VisualElement SubHeading(string title, string subtitle)
    {
        var c = new VisualElement();
        c.style.marginTop = 16;
        c.style.marginBottom = 4;
        var t = new Label(title);
        t.style.fontSize = 18;
        t.style.unityFontStyleAndWeight = FontStyle.Bold;
        t.style.color = Color.white;
        c.Add(t);
        if (!string.IsNullOrEmpty(subtitle))
        {
            var s = UITools.CreateConfigurationLabel(subtitle);
            s.style.fontSize = 12;
            s.style.color = new Color(0.6f, 0.6f, 0.6f);
            c.Add(s);
        }
        return c;
    }

    // Tape is a mode (Unchanged / RGB / Textured) plus a color (RGB) and a texture (Textured).
    // The three live as separate registry fields; we find them by kind for the chosen blade/shaft.
    private static void RenderTapeControl(PresetTeam team, PresetRole role, string which)
    {
        var profile = ReskinProfileManager.currentProfile;
        var fields = PresetFieldRegistry.All
            .Where(f => f.Team == team && f.Role == role && f.Group == "Tape" && f.Id.Contains(which))
            .ToList();
        var modeField = fields.FirstOrDefault(f => f.Kind == PresetValueKind.String);
        var texField = fields.FirstOrDefault(f => f.Kind == PresetValueKind.ReskinRef);
        var colorField = fields.FirstOrDefault(f => f.Kind == PresetValueKind.Color);
        if (modeField == null || texField == null || colorField == null) return;

        string label = $"{which} tape";

        var modeRow = UITools.CreateConfigurationRow();
        modeRow.Add(UITools.CreateConfigurationLabel($"{label} mode"));
        var modeDropdown = UITools.CreateStringDropdownField(
            new List<string> { "Unchanged", "RGB", "Textured" },
            (string)modeField.GetValue(profile) ?? "Unchanged");
        modeRow.Add(modeDropdown);
        _root.Add(modeRow);

        var colorSection = UITools.CreateColorConfigurationRow(
            $"{label} color",
            (Color)colorField.GetValue(profile),
            false,
            c => { colorField.SetValue(profile, c); Preview(); },
            ReskinProfileManager.SaveProfile);
        _root.Add(colorSection);

        var texRow = UITools.CreateConfigurationRow();
        texRow.Add(UITools.CreateConfigurationLabel($"{label} texture"));
        var unchanged = new ReskinRegistry.ReskinEntry { Name = "Unchanged", Path = null, Type = texField.ReskinType };
        var choices = ReskinRegistry.GetReskinEntriesByType(texField.ReskinType);
        choices.Insert(0, unchanged);
        var texDropdown = UITools.CreateConfigurationDropdownField();
        texDropdown.choices = choices;
        texDropdown.value = (texField.GetValue(profile) as ReskinRegistry.ReskinEntry) ?? unchanged;
        texDropdown.RegisterCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(evt =>
        {
            var chosen = evt.newValue;
            texField.SetValue(profile, chosen != null && chosen.Path != null ? chosen : null);
            ReskinProfileManager.SaveProfile();
            Preview();
        });
        texRow.Add(texDropdown);
        _root.Add(texRow);

        void UpdateVisibility(string mode)
        {
            colorSection.style.display = mode == "RGB" ? DisplayStyle.Flex : DisplayStyle.None;
            texRow.style.display = mode == "Textured" ? DisplayStyle.Flex : DisplayStyle.None;
        }
        modeDropdown.RegisterValueChangedCallback(evt =>
        {
            modeField.SetValue(profile, evt.newValue);
            ReskinProfileManager.SaveProfile();
            UpdateVisibility(evt.newValue);
            Preview();
        });
        UpdateVisibility(modeDropdown.value);
    }

    private static void RenderField(PresetField field, string labelOverride = null)
    {
        var profile = ReskinProfileManager.currentProfile;
        string label = labelOverride ?? field.DisplayName;

        switch (field.Kind)
        {
            case PresetValueKind.ReskinRef:
                RenderRefDropdown(field, profile, label);
                break;

            case PresetValueKind.Color:
                var colorRow = UITools.CreateColorConfigurationRow(
                    label,
                    (Color)field.GetValue(profile),
                    false,
                    c => { field.SetValue(profile, c); Preview(); },
                    ReskinProfileManager.SaveProfile);
                _root.Add(colorRow);
                break;

            case PresetValueKind.Float:
                var row = UITools.CreateConfigurationRow();
                row.Add(UITools.CreateConfigurationLabel(label));
                var slider = UITools.CreateConfigurationSlider(0f, 1f, (float)field.GetValue(profile), 300);
                slider.RegisterCallback<ChangeEvent<float>>(evt =>
                {
                    field.SetValue(profile, evt.newValue);
                    Preview();
                });
                slider.RegisterCallback<PointerUpEvent>(_ => ReskinProfileManager.SaveProfile());
                row.Add(slider);
                _root.Add(row);
                break;
        }
    }

    private static void RenderRefDropdown(PresetField field, ReskinProfileManager.Profile profile, string label)
    {
        var row = UITools.CreateConfigurationRow();
        row.Add(UITools.CreateConfigurationLabel(label));

        var unchanged = new ReskinRegistry.ReskinEntry { Name = "Unchanged", Path = null, Type = field.ReskinType };
        var choices = ReskinRegistry.GetReskinEntriesByType(field.ReskinType);
        choices.Insert(0, unchanged);

        var dropdown = UITools.CreateConfigurationDropdownField();
        dropdown.choices = choices;
        var current = field.GetValue(profile) as ReskinRegistry.ReskinEntry;
        dropdown.value = current ?? unchanged;
        dropdown.RegisterCallback<ChangeEvent<ReskinRegistry.ReskinEntry>>(evt =>
        {
            var chosen = evt.newValue;
            field.SetValue(profile, chosen != null && chosen.Path != null ? chosen : null);
            ReskinProfileManager.SaveProfile();
            Preview();
        });

        row.Add(dropdown);
        _root.Add(row);
    }

    // ───────────────────────── copy-from ─────────────────────────

    private static VisualElement BuildCopyRow()
    {
        var row = UITools.CreateRow();
        row.style.marginTop = 6;
        row.Add(UITools.CreateConfigurationLabel("Copy from:"));

        var others = ProfileTeamTools.Cells
            .Where(c => !(c.Team == _team && c.Role == _role))
            .ToList();
        var labels = others.Select(CellLabel).ToList();

        // Default to the same team's other role (the most common copy, e.g. Skater <-> Goalie).
        var otherRole = _role == PresetRole.Goalie ? PresetRole.Skater : PresetRole.Goalie;
        string defaultLabel = CellLabel((_team, otherRole));
        if (!labels.Contains(defaultLabel)) defaultLabel = labels[0];

        var dropdown = UITools.CreateStringDropdownField(labels, defaultLabel);
        dropdown.style.marginLeft = 8;
        dropdown.style.marginRight = 8;
        row.Add(dropdown);

        var copyBtn = new Button { text = "Copy" };
        UITools.StyleConfigButton(copyBtn);
        copyBtn.RegisterCallback<ClickEvent>(_ =>
        {
            int idx = labels.IndexOf(dropdown.value);
            if (idx < 0) return;
            var from = others[idx];
            int n = ProfileTeamTools.CopyCell(from.Team, from.Role, _team, _role);
            ReskinProfileManager.SaveProfile();
            Toast("Copied", $"Copied {n} setting{(n == 1 ? "" : "s")} from {CellLabel(from)}.");
            Render(); // refresh control values + preview
        });
        row.Add(copyBtn);

        return row;
    }

    private static string CellLabel((PresetTeam Team, PresetRole Role) cell)
        => $"{TeamName(cell.Team)} {(cell.Role == PresetRole.Goalie ? "Goalie" : "Skater")}";

    // ───────────────────────── helpers ─────────────────────────

    // A toggle button that keeps its active styling when the mouse leaves. For team buttons,
    // pass the team color (shown full when active, dimmed when not); role buttons pass null
    // and use the neutral light/dark active scheme.
    private static Button MakeToggleButton(string text, bool active, Color? teamColor, System.Action onClick)
    {
        var btn = new Button { text = text };
        btn.style.unityTextAlign = TextAnchor.MiddleCenter;
        btn.style.fontSize = 16;
        btn.style.paddingTop = 6;
        btn.style.paddingBottom = 6;
        btn.style.paddingLeft = 16;
        btn.style.paddingRight = 16;
        btn.style.marginRight = 6;
        btn.style.borderTopWidth = 0;
        btn.style.borderBottomWidth = 0;
        btn.style.borderLeftWidth = 0;
        btn.style.borderRightWidth = 0;

        Color resting, textColor;
        if (teamColor.HasValue)
        {
            var c = teamColor.Value;
            resting = active ? c : new Color(c.r * 0.3f, c.g * 0.3f, c.b * 0.3f, 1f);
            textColor = Color.white;
        }
        else
        {
            resting = active ? new Color(0.7f, 0.7f, 0.7f) : new Color(0.25f, 0.25f, 0.25f);
            textColor = active ? Color.black : Color.white;
        }

        btn.style.backgroundColor = resting;
        btn.style.color = textColor;
        btn.RegisterCallback<MouseEnterEvent>(_ => btn.style.backgroundColor = Color.Lerp(resting, Color.white, 0.3f));
        btn.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            btn.style.backgroundColor = resting;
            btn.style.color = textColor;
        });
        btn.RegisterCallback<ClickEvent>(_ => onClick());
        return btn;
    }

    private static string TeamName(PresetTeam team)
    {
        var profile = ReskinProfileManager.currentProfile;
        if (team == PresetTeam.Blue)
            return string.IsNullOrWhiteSpace(profile.blueTeamName) ? "Blue" : profile.blueTeamName;
        return string.IsNullOrWhiteSpace(profile.redTeamName) ? "Red" : profile.redTeamName;
    }

    private static PlayerTeam ToPlayerTeam(PresetTeam t) => t == PresetTeam.Red ? PlayerTeam.Red : PlayerTeam.Blue;
    private static PlayerRole ToPlayerRole(PresetRole r) => r == PresetRole.Goalie ? PlayerRole.Goalie : PlayerRole.Attacker;

    private static void Preview()
    {
        ChangingRoomHelper.SetPreviewContext(ToPlayerTeam(_team), ToPlayerRole(_role));
        ChangingRoomHelper.RefreshPreview();
    }

    private static void Toast(string title, string message)
    {
        try { MonoBehaviourSingleton<UIManager>.Instance?.ToastManager?.ShowToast(title, message, 4f); }
        catch { /* UIManager may not be ready */ }
    }
}
