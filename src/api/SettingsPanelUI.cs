// SettingsPanelUI.cs
//
// Public, reflection-friendly wrappers over TRL's internal settings-row builders so a
// registered external panel (see SettingsPanelRegistry) renders with the same visual
// vocabulary as TRL's own Tweaks sections — dark rows, styled toggles/sliders, quiet
// notes. Every method takes only types both mods share, so a soft-dependency consumer
// can call these over reflection without referencing ToasterReskinLoader.dll.

using System;
using UnityEngine.UIElements;
using ToasterReskinLoader.ui;
using ToasterReskinLoader.ui.sections;

namespace ToasterReskinLoader.api;

public static class SettingsPanelUI
{
    /// <summary>Bold sub-heading inside a panel.</summary>
    public static void Header(VisualElement root, string text) => SettingsUI.Header(root, text);

    /// <summary>Dimmed explanatory note (wraps).</summary>
    public static void Note(VisualElement root, string text) => SettingsUI.Note(root, text);

    /// <summary>Full-width horizontal divider between groups of rows.</summary>
    public static void Separator(VisualElement root) => SettingsUI.Separator(root);

    /// <summary>Labelled checkbox row. onChange fires with the new value; the caller persists.</summary>
    public static VisualElement Toggle(VisualElement root, string label, bool value, Action<bool> onChange)
        => SettingsUI.ToggleRow(root, label, value, onChange);

    /// <summary>
    /// Labelled slider row with a debounced commit. <paramref name="onChange"/> fires on every
    /// drag step (use it for live preview); <paramref name="onCommit"/> fires once the value
    /// settles for <paramref name="debounceMs"/> (use it to persist, so a drag doesn't write the
    /// settings file on every tick). Pass a null onCommit to skip the second callback.
    /// </summary>
    public static VisualElement Slider(VisualElement root, string label, float min, float max, float value,
        Action<float> onChange, Action onCommit = null, long debounceMs = 400)
    {
        var row = UITools.CreateConfigurationRow();
        row.Add(UITools.CreateConfigurationLabel(label));
        var slider = UITools.CreateConfigurationSlider(min, max, value, 300);

        IVisualElementScheduledItem pending = null;
        slider.RegisterCallback<ChangeEvent<float>>(evt =>
        {
            onChange?.Invoke(evt.newValue);
            if (onCommit == null) return;
            // Restart the timer on each change; the commit only runs once movement stops.
            // Covers both dragging and typing into the slider's input field.
            pending?.Pause();
            pending = slider.schedule.Execute(() => onCommit());
            pending.ExecuteLater(debounceMs);
        });

        row.Add(slider);
        root.Add(row);
        return row;
    }

    /// <summary>Styled action button that matches the config palette.</summary>
    public static Button Button(VisualElement root, string label, Action onClick)
    {
        var btn = new Button { text = label };
        UITools.StyleConfigButton(btn);
        btn.style.marginTop = 8;
        if (onClick != null) btn.RegisterCallback<ClickEvent>(_ => onClick());
        root.Add(btn);
        return btn;
    }
}
