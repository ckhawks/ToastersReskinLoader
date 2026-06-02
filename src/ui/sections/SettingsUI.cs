// Shared row builders for the settings sections under the "Tweaks" group
// (General, Chat & Scoreboard, Server Browser, Multiplayer, Input & Camera,
// Developer). Extracted from the former single PlayerQoLSection so each
// settings page can be its own file while sharing one visual vocabulary.

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.ui.sections;

internal static class SettingsUI
{
    public static void Header(VisualElement parent, string text)
    {
        var h = new Label($"<b>{text}</b>");
        h.style.fontSize = 20;
        h.style.color = Color.white;
        h.style.marginTop = 4;
        h.style.marginBottom = 8;
        parent.Add(h);
    }

    public static void Note(VisualElement parent, string text)
    {
        var n = UITools.CreateConfigurationLabel(text);
        n.style.marginBottom = 8;
        n.style.fontSize = 13;
        n.style.color = new Color(0.7f, 0.7f, 0.7f);
        n.style.whiteSpace = WhiteSpace.Normal;
        parent.Add(n);
    }

    public static void Separator(VisualElement parent)
    {
        var sep = new VisualElement();
        sep.style.height = 1;
        sep.style.backgroundColor = new Color(0.4f, 0.4f, 0.4f);
        sep.style.marginTop = 16;
        sep.style.marginBottom = 16;
        parent.Add(sep);
    }

    public static VisualElement ToggleRow(VisualElement parent, string label, bool initial, Action<bool> onChange)
    {
        var row = UITools.CreateConfigurationRow();
        row.Add(UITools.CreateConfigurationLabel(label));
        var t = UITools.CreateConfigurationCheckbox(initial);
        t.RegisterCallback<ChangeEvent<bool>>(evt => onChange(evt.newValue));
        row.Add(t);
        parent.Add(row);
        return row;
    }

    // Generic labeled slider. onChange fires on every drag step; the caller's
    // setter is responsible for persisting (typically SettingsRunner.SaveAndRefresh).
    public static VisualElement SliderRow(VisualElement parent, string label, float min, float max,
        float current, Action<float> onChange)
    {
        var row = UITools.CreateConfigurationRow();
        row.Add(UITools.CreateConfigurationLabel(label));
        var slider = UITools.CreateConfigurationSlider(min, max, current, 300);
        slider.RegisterCallback<ChangeEvent<float>>(evt => onChange(evt.newValue));
        row.Add(slider);
        parent.Add(row);
        return row;
    }

    // A styled "Reset to default" button that, on click, runs resetState then
    // rebuilds the section in place (preserving the section-title label that
    // ReskinManagerMenu adds as the first child).
    public static Button RebuildButton(VisualElement root, string text, Action resetState,
        Action<VisualElement> rebuild)
    {
        var btn = new Button
        {
            text = text,
            style =
            {
                backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f)),
                unityTextAlign = TextAnchor.MiddleLeft,
                fontSize = 18,
                marginTop = 8,
                paddingTop = 8,
                paddingBottom = 8,
                paddingLeft = 15,
            },
        };
        UITools.AddHoverEffectsForButton(btn);
        btn.RegisterCallback<ClickEvent>(_ =>
        {
            resetState?.Invoke();
            var title = root.childCount > 0 ? root[0] : null;
            root.Clear();
            if (title != null) root.Add(title);
            rebuild(root);
        });
        return btn;
    }

    // Shared guard: returns the live SettingsConfig, or null (after rendering a
    // "not ready yet" notice) if the runtime hasn't booted. Every Tweaks
    // section calls this first.
    public static ToasterReskinLoader.core.SettingsConfig RequireConfig(VisualElement parent, string blurb)
    {
        var description = UITools.CreateConfigurationLabel(blurb);
        description.style.marginBottom = 12;
        description.style.whiteSpace = WhiteSpace.Normal;
        parent.Add(description);

        var runner = ToasterReskinLoader.core.SettingsRunner.Instance;
        if (runner == null)
        {
            var warn = UITools.CreateConfigurationLabel(
                "Settings runtime is not initialized yet. Reopen this menu after the game finishes loading.");
            warn.style.color = new Color(1f, 0.7f, 0.4f);
            warn.style.whiteSpace = WhiteSpace.Normal;
            parent.Add(warn);
            return null;
        }
        return runner.Config;
    }
}
