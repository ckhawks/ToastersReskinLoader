using System.Collections.Generic;
using System.Linq;
using ToasterReskinLoader.presets;
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
    private static readonly string[] HiddenGroups = { "Sticks", "Tape" };

    private static PresetTeam _team = PresetTeam.Blue;
    private static PresetRole _role = PresetRole.Skater;
    private static VisualElement _root;

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

        // Team + role toggles
        _root.Add(ToggleRow("Team", new[]
        {
            (TeamName(PresetTeam.Blue), (object)PresetTeam.Blue, _team == PresetTeam.Blue),
            (TeamName(PresetTeam.Red), (object)PresetTeam.Red, _team == PresetTeam.Red),
        }, sel => { _team = (PresetTeam)sel; Render(); }));

        _root.Add(ToggleRow("Role", new[]
        {
            ("Skater", (object)PresetRole.Skater, _role == PresetRole.Skater),
            ("Goalie", (object)PresetRole.Goalie, _role == PresetRole.Goalie),
        }, sel => { _role = (PresetRole)sel; Render(); }));

        _root.Add(BuildCopyRow());

        var divider = new VisualElement();
        divider.style.height = 1;
        divider.style.backgroundColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
        divider.style.marginTop = 8;
        divider.style.marginBottom = 10;
        _root.Add(divider);

        var heading = new Label($"Editing: {TeamName(_team)} {(_role == PresetRole.Goalie ? "Goalie" : "Skater")}");
        heading.style.fontSize = 18;
        heading.style.unityFontStyleAndWeight = FontStyle.Bold;
        heading.style.color = Color.white;
        heading.style.marginBottom = 8;
        _root.Add(heading);

        var fields = CellFields(_team, _role);
        if (fields.Count == 0)
        {
            _root.Add(UITools.CreateConfigurationLabel("(no editable settings for this cell)"));
        }
        else
        {
            foreach (var field in fields)
                RenderField(field);
        }

        Preview();
    }

    // ───────────────────────── field rendering ─────────────────────────

    private static List<PresetField> CellFields(PresetTeam team, PresetRole role)
        => PresetFieldRegistry.All
            .Where(f => f.Team == team && f.Role == role && !HiddenGroups.Contains(f.Group))
            .ToList();

    private static void RenderField(PresetField field)
    {
        var profile = ReskinProfileManager.currentProfile;

        switch (field.Kind)
        {
            case PresetValueKind.ReskinRef:
                RenderRefDropdown(field, profile);
                break;

            case PresetValueKind.Color:
                var colorRow = UITools.CreateColorConfigurationRow(
                    field.DisplayName,
                    (Color)field.GetValue(profile),
                    false,
                    c => { field.SetValue(profile, c); Preview(); },
                    ReskinProfileManager.SaveProfile);
                _root.Add(colorRow);
                break;

            case PresetValueKind.Float:
                var row = UITools.CreateConfigurationRow();
                row.Add(UITools.CreateConfigurationLabel(field.DisplayName));
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

    private static void RenderRefDropdown(PresetField field, ReskinProfileManager.Profile profile)
    {
        var row = UITools.CreateConfigurationRow();
        row.Add(UITools.CreateConfigurationLabel(field.DisplayName));

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

        var dropdown = UITools.CreateStringDropdownField(labels, labels[0]);
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

    private static VisualElement ToggleRow(string label, (string Text, object Value, bool Active)[] options,
        System.Action<object> onSelect)
    {
        var row = UITools.CreateRow();
        row.style.marginTop = 4;
        var lbl = UITools.CreateConfigurationLabel(label + ":");
        lbl.style.width = 60;
        row.Add(lbl);

        foreach (var opt in options)
        {
            var btn = new Button { text = opt.Text };
            UITools.StyleConfigButton(btn);
            btn.style.marginRight = 6;
            if (opt.Active)
            {
                btn.style.backgroundColor = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
                btn.style.color = Color.black;
            }
            var value = opt.Value;
            btn.RegisterCallback<ClickEvent>(_ => onSelect(value));
            row.Add(btn);
        }

        return row;
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
