// SettingsPanelRegistry.cs
//
// Lets other mods contribute their own settings page to the Reskin Manager menu.
// A registered panel shows up as a sidebar button under the "Plugins" bucket; when
// selected, TRL hands the panel an empty content container and the owning mod fills
// it with its own widgets (typically via the SettingsPanelUI helpers).
//
// The contract crosses the assembly boundary using only types both mods already
// share (string / int / Action<VisualElement>), so a consumer can register over
// reflection without referencing ToasterReskinLoader.dll — see
// ToasterReskinLoaderAPI for the recommended soft-dependency call shape.

using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.api;

/// <summary>A settings page contributed by another mod.</summary>
public sealed class SettingsPanelRegistration
{
    /// <summary>Stable unique key (e.g. the mod name). Re-registering the same id replaces.</summary>
    public string Id;

    /// <summary>Sidebar label. Should be distinct from TRL's built-in section names.</summary>
    public string Title;

    /// <summary>Optional right-aligned bucket tag (e.g. "Cameras"); null renders a plain row.</summary>
    public string Group;

    /// <summary>Sort key within the Plugins bucket (lower first).</summary>
    public int Order;

    /// <summary>Populates the content container when the page is opened. Called every time
    /// the page is shown, so it should rebuild from the mod's current settings each call.</summary>
    public Action<VisualElement> Build;
}

public static class SettingsPanelRegistry
{
    private static readonly List<SettingsPanelRegistration> _panels = new();

    /// <summary>Fired when a panel is added or removed so an open menu can rebuild its sidebar.</summary>
    public static event Action OnChanged;

    public static IReadOnlyList<SettingsPanelRegistration> Panels => _panels;

    public static void Register(string id, string title, string group, Action<VisualElement> build, int order)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(title) || build == null)
        {
            Plugin.LogError("SettingsPanelRegistry.Register ignored: id, title and build are required.");
            return;
        }

        _panels.RemoveAll(p => p.Id == id);
        _panels.Add(new SettingsPanelRegistration
        {
            Id = id,
            Title = title,
            Group = group,
            Order = order,
            Build = build,
        });

        // Order by the caller's Order, then by Id so ties are deterministic across runs.
        _panels.Sort((a, b) =>
        {
            int byOrder = a.Order.CompareTo(b.Order);
            return byOrder != 0 ? byOrder : string.CompareOrdinal(a.Id, b.Id);
        });

        Plugin.Log($"Registered external settings panel '{title}' (id={id}).");
        Invoke();
    }

    public static void Unregister(string id)
    {
        if (_panels.RemoveAll(p => p.Id == id) > 0)
            Invoke();
    }

    private static void Invoke()
    {
        try { OnChanged?.Invoke(); }
        catch (Exception e) { Plugin.LogError($"SettingsPanelRegistry.OnChanged handler error: {e}"); }
    }
}
