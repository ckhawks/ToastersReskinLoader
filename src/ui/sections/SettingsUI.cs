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

    // Shared guard: returns the live QoLConfig, or null (after rendering a
    // "not ready yet" notice) if the runtime hasn't booted. Every Tweaks
    // section calls this first.
    public static ToasterReskinLoader.core.QoLConfig RequireConfig(VisualElement parent, string blurb)
    {
        var description = UITools.CreateConfigurationLabel(blurb);
        description.style.marginBottom = 12;
        description.style.whiteSpace = WhiteSpace.Normal;
        parent.Add(description);

        var runner = ToasterReskinLoader.core.QoLRunner.Instance;
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
