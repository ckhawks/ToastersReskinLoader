// PresetFieldRegistry.cs
//
// The single description of every "presetable" setting on ReskinProfileManager.Profile.
// Everything the presets system does — the save checkbox-tree, saving only the ticked
// fields, applying a preset, and the blue<->red team swap — loops over this registry
// instead of re-listing settings by hand (which is what Load()/Save() do today).
//
// Design notes:
//  - OPT-IN: only Profile fields tagged with [PresetField] appear here, so nothing leaks
//    into presets accidentally.
//  - Team side (Blue/Red/None) and the opposite-team "swap partner" are auto-derived from
//    the field name's blue/red token (e.g. stickAttackerBlue <-> stickAttackerRed,
//    blueSkaterTorso <-> redSkaterTorso). An explicit Team override is available for the
//    rare field whose name doesn't follow the convention.
//  - Value kind is auto-derived from the field's C# type.
//  - This registry is PRESET-ONLY for now. It deliberately does not touch Load()/Save();
//    migrating those onto it is a later, separately-verified step (see
//    docs/presets-system-design.md "Backwards compatibility").

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ToasterReskinLoader.presets;

public enum PresetTeam
{
    /// Attribute default — registry resolves the real side from the field name.
    Auto,
    None,
    Blue,
    Red,
}

public enum PresetRole
{
    /// Attribute default — registry resolves the role from the field name.
    Auto,
    None,
    Skater,
    Goalie,
}

public enum PresetValueKind
{
    Bool,
    Int,
    Float,
    String,
    Color,
    ReskinRef,
    ReskinRefList,
    Unsupported,
}

/// <summary>
/// Tags a Profile field as included in presets. Group + Display drive the save tree;
/// ReskinType is required for reskin-reference fields so refs can be resolved against
/// installed packs.
/// </summary>
[AttributeUsage(AttributeTargets.Field, Inherited = false)]
public sealed class PresetFieldAttribute : Attribute
{
    public string Group { get; }
    public string Display { get; }

    /// Reskin type key (e.g. "stick_attacker", "jersey_torso", "puck"). Required for
    /// ReskinRef / ReskinRefList fields; leave null for plain-value fields.
    public string ReskinType { get; set; }

    /// Override team detection. Auto (default) derives the side from the field name.
    public PresetTeam Team { get; set; } = PresetTeam.Auto;

    /// Override role detection. Auto (default) derives the role from the field name
    /// (Skater/Attacker -> Skater, Goalie -> Goalie). Set explicitly for role-specific
    /// fields whose name lacks the token (e.g. goalie leg pads).
    public PresetRole Role { get; set; } = PresetRole.Auto;

    public PresetFieldAttribute(string group, string display)
    {
        Group = group;
        Display = display;
    }
}

/// <summary>One presetable setting: its identity, grouping, team side, and live get/set.</summary>
public sealed class PresetField
{
    public string Id { get; }              // the Profile field name, e.g. "blueSkaterTorso"
    public string DisplayName { get; }
    public string Group { get; }
    public PresetTeam Team { get; }
    public PresetRole Role { get; }
    public string SwapPartnerId { get; }   // opposite-team field id, or null
    public PresetValueKind Kind { get; }
    public string ReskinType { get; }      // for ReskinRef/List, else null

    private readonly FieldInfo _field;

    internal PresetField(FieldInfo field, string display, string group, PresetTeam team,
        PresetRole role, string swapPartnerId, PresetValueKind kind, string reskinType)
    {
        _field = field;
        Id = field.Name;
        DisplayName = display;
        Group = group;
        Team = team;
        Role = role;
        SwapPartnerId = swapPartnerId;
        Kind = kind;
        ReskinType = reskinType;
    }

    public object GetValue(ReskinProfileManager.Profile profile) => _field.GetValue(profile);
    public void SetValue(ReskinProfileManager.Profile profile, object value) => _field.SetValue(profile, value);
}

public static class PresetFieldRegistry
{
    private static List<PresetField> _all;
    private static Dictionary<string, PresetField> _byId;

    public static IReadOnlyList<PresetField> All
    {
        get { EnsureBuilt(); return _all; }
    }

    public static PresetField ById(string id)
    {
        EnsureBuilt();
        return id != null && _byId.TryGetValue(id, out var f) ? f : null;
    }

    /// Fields for one team bucket (Blue / Red / None), grouped by category — the shape
    /// the save tree renders.
    public static IEnumerable<IGrouping<string, PresetField>> ByGroup(PresetTeam team)
        => All.Where(f => f.Team == team).GroupBy(f => f.Group);

    private static void EnsureBuilt()
    {
        if (_all != null) return;
        _all = Build();
        _byId = _all.ToDictionary(f => f.Id);
        Validate();
    }

    private static List<PresetField> Build()
    {
        var result = new List<PresetField>();
        var fields = typeof(ReskinProfileManager.Profile)
            .GetFields(BindingFlags.Public | BindingFlags.Instance);

        foreach (var fi in fields)
        {
            var attr = fi.GetCustomAttribute<PresetFieldAttribute>();
            if (attr == null) continue; // opt-in only

            var (team, partner) = ResolveTeam(fi.Name, attr.Team);
            var role = ResolveRole(fi.Name, attr.Role);
            var kind = ResolveKind(fi.FieldType);
            result.Add(new PresetField(fi, attr.Display, attr.Group, team, role, partner, kind, attr.ReskinType));
        }

        return result;
    }

    /// Derive team side + opposite-team partner id from the field name's blue/red token
    /// (case preserved: Blue<->Red, blue<->red). The token may sit anywhere in the name
    /// (prefix like "blueSkaterTorso" or mid-name like "stickAttackerBlue").
    private static (PresetTeam team, string partnerId) ResolveTeam(string name, PresetTeam over)
    {
        if (over == PresetTeam.None) return (PresetTeam.None, null);
        if (over == PresetTeam.Blue) return (PresetTeam.Blue, SwapBlueRed(name));
        if (over == PresetTeam.Red) return (PresetTeam.Red, SwapBlueRed(name));

        // Auto
        if (name.Contains("Blue") || name.Contains("blue")) return (PresetTeam.Blue, SwapBlueRed(name));
        if (name.Contains("Red") || name.Contains("red")) return (PresetTeam.Red, SwapBlueRed(name));
        return (PresetTeam.None, null);
    }

    /// Role from the field name: Skater/Attacker -> Skater, Goalie -> Goalie, else None.
    /// The override handles role-specific fields whose name lacks a token (e.g. leg pads).
    private static PresetRole ResolveRole(string name, PresetRole over)
    {
        if (over != PresetRole.Auto) return over;
        if (name.Contains("Skater") || name.Contains("skater")
            || name.Contains("Attacker") || name.Contains("attacker")) return PresetRole.Skater;
        if (name.Contains("Goalie") || name.Contains("goalie")) return PresetRole.Goalie;
        return PresetRole.None;
    }

    private static string SwapBlueRed(string name)
    {
        if (name.Contains("Blue")) return name.Replace("Blue", "Red");
        if (name.Contains("blue")) return name.Replace("blue", "red");
        if (name.Contains("Red")) return name.Replace("Red", "Blue");
        if (name.Contains("red")) return name.Replace("red", "blue");
        return null;
    }

    private static PresetValueKind ResolveKind(Type t)
    {
        if (t == typeof(bool)) return PresetValueKind.Bool;
        if (t == typeof(int)) return PresetValueKind.Int;
        if (t == typeof(float)) return PresetValueKind.Float;
        if (t == typeof(string)) return PresetValueKind.String;
        if (t == typeof(Color)) return PresetValueKind.Color;
        if (t == typeof(ReskinRegistry.ReskinEntry)) return PresetValueKind.ReskinRef;
        if (typeof(IEnumerable).IsAssignableFrom(t) && t.IsGenericType
            && t.GetGenericArguments().Length == 1
            && t.GetGenericArguments()[0] == typeof(ReskinRegistry.ReskinEntry))
            return PresetValueKind.ReskinRefList;
        return PresetValueKind.Unsupported;
    }

    /// Cheap startup self-check: surface annotation mistakes (unsupported types, missing
    /// reskin type on a ref field, a team field whose computed partner isn't annotated).
    private static void Validate()
    {
        foreach (var f in _all)
        {
            if (f.Kind == PresetValueKind.Unsupported)
                Plugin.LogWarning($"[Presets] Field '{f.Id}' has a type unsupported by presets; it will be skipped.");

            bool isRef = f.Kind == PresetValueKind.ReskinRef || f.Kind == PresetValueKind.ReskinRefList;
            if (isRef && string.IsNullOrEmpty(f.ReskinType))
                Plugin.LogWarning($"[Presets] Reskin field '{f.Id}' is missing ReskinType in its [PresetField] attribute.");
            if (!isRef && !string.IsNullOrEmpty(f.ReskinType))
                Plugin.LogWarning($"[Presets] Field '{f.Id}' sets ReskinType but isn't a reskin field.");

            if (f.Team != PresetTeam.None && f.SwapPartnerId != null && ById(f.SwapPartnerId) == null)
                Plugin.LogWarning($"[Presets] Team field '{f.Id}' ({f.Team}) has no annotated swap partner '{f.SwapPartnerId}'. Team-swap will skip it.");
        }

        Plugin.LogDebug($"[Presets] Registry built: {_all.Count} fields across {_all.Select(f => f.Group).Distinct().Count()} groups "
            + $"(Blue {_all.Count(f => f.Team == PresetTeam.Blue)}, Red {_all.Count(f => f.Team == PresetTeam.Red)}, Global {_all.Count(f => f.Team == PresetTeam.None)}).");
    }
}
