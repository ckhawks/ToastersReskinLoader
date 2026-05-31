// Preset.cs
//
// The preset data model + codec. A preset is a sparse set of saved settings keyed by
// PresetFieldRegistry descriptor id, plus a small header (name, team marker, dependencies).
//
// Encoding is per field kind:
//   primitives  -> raw JSON value
//   Color       -> { r, g, b, a }
//   ReskinRef   -> { packId, entryName, reskinType, workshopId }   (null if unset)
//   RefList     -> array of the above
//
// This file owns serialization (Encode) and the inverse (TryDecode + ref resolution).
// Wiring decoded values onto the live Profile is the apply step (phase 3).

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ToasterReskinLoader.presets;

/// <summary>A pack a preset depends on, for missing-reskin warnings.</summary>
public sealed class PresetDependency
{
    [JsonProperty("packId")] public string PackId { get; set; }
    [JsonProperty("workshopId")] public ulong WorkshopId { get; set; }
    [JsonProperty("name")] public string Name { get; set; }
}

public sealed class Preset
{
    [JsonProperty("presetName")] public string PresetName { get; set; }
    [JsonProperty("presetFormatVersion")] public int PresetFormatVersion { get; set; } = 1;

    /// "blue" | "red" | null. Set only when every team-scoped saved field is from one side.
    [JsonProperty("teamScoped")] public string TeamScoped { get; set; }

    [JsonProperty("dependencies")] public List<PresetDependency> Dependencies { get; set; } = new();

    /// Sparse map: descriptor id -> encoded value. Only saved fields appear.
    [JsonProperty("fields")] public JObject Fields { get; set; } = new();

    // ---- runtime-only (not serialized) ----
    [JsonIgnore] public string SourceLabel { get; set; } // "My Presets" or "Pack: <name>"
    [JsonIgnore] public string FilePath { get; set; }
    [JsonIgnore] public bool IsReadOnly { get; set; }

    [JsonIgnore] public bool IsTeamScoped => TeamScoped == "blue" || TeamScoped == "red";

    public IEnumerable<string> FieldIds => Fields?.Properties().Select(p => p.Name) ?? Enumerable.Empty<string>();
}

public static class PresetCodec
{
    // ---------- Encode (live value -> JToken) ----------

    public static JToken Encode(PresetField field, object value)
    {
        switch (field.Kind)
        {
            case PresetValueKind.Bool:
            case PresetValueKind.Int:
            case PresetValueKind.Float:
            case PresetValueKind.String:
                return value == null ? JValue.CreateNull() : JToken.FromObject(value);

            case PresetValueKind.Color:
                return value is Color c ? EncodeColor(c) : JValue.CreateNull();

            case PresetValueKind.ReskinRef:
                return EncodeRef(value as ReskinRegistry.ReskinEntry, field.ReskinType);

            case PresetValueKind.ReskinRefList:
                var arr = new JArray();
                if (value is IEnumerable<ReskinRegistry.ReskinEntry> list)
                    foreach (var e in list)
                    {
                        var t = EncodeRef(e, field.ReskinType);
                        if (t.Type != JTokenType.Null) arr.Add(t);
                    }
                return arr;

            default:
                return JValue.CreateNull();
        }
    }

    private static JToken EncodeColor(Color c)
        => new JObject { ["r"] = c.r, ["g"] = c.g, ["b"] = c.b, ["a"] = c.a };

    private static JToken EncodeRef(ReskinRegistry.ReskinEntry e, string reskinType)
    {
        if (e?.ParentPack == null) return JValue.CreateNull();
        return new JObject
        {
            ["packId"] = e.ParentPack.UniqueId,
            ["entryName"] = e.Name,
            ["reskinType"] = reskinType,
            ["workshopId"] = e.ParentPack.WorkshopId,
        };
    }

    // ---------- Decode (stored JToken -> value to apply) ----------
    // Reskin refs resolve against installed packs; unresolved refs are recorded in `missing`
    // (if provided) and decode to null so the rest of the preset still applies.

    public static bool TryDecode(PresetField field, JToken token, out object value,
        ICollection<PresetDependency> missing = null)
    {
        value = null;
        if (token == null) return false;

        switch (field.Kind)
        {
            case PresetValueKind.Bool:
                value = token.Type == JTokenType.Null ? (object)false : token.Value<bool>();
                return true;
            case PresetValueKind.Int:
                value = token.Type == JTokenType.Null ? (object)0 : token.Value<int>();
                return true;
            case PresetValueKind.Float:
                value = token.Type == JTokenType.Null ? (object)0f : token.Value<float>();
                return true;
            case PresetValueKind.String:
                value = token.Type == JTokenType.Null ? null : token.Value<string>();
                return true;
            case PresetValueKind.Color:
                value = DecodeColor(token);
                return true;
            case PresetValueKind.ReskinRef:
                value = DecodeRef(token, field.ReskinType, missing);
                return true;
            case PresetValueKind.ReskinRefList:
                var l = new List<ReskinRegistry.ReskinEntry>();
                if (token is JArray ja)
                    foreach (var t in ja)
                    {
                        var r = DecodeRef(t, field.ReskinType, missing);
                        if (r != null) l.Add(r);
                    }
                value = l;
                return true;
            default:
                return false;
        }
    }

    private static Color DecodeColor(JToken token)
    {
        if (token is JObject o)
            return new Color(
                o.Value<float?>("r") ?? 1f,
                o.Value<float?>("g") ?? 1f,
                o.Value<float?>("b") ?? 1f,
                o.Value<float?>("a") ?? 1f);
        return Color.white;
    }

    /// Resolve a stored reskin reference to a live entry, or null if its pack/entry isn't
    /// installed (recording the gap in `missing`).
    private static ReskinRegistry.ReskinEntry DecodeRef(JToken token, string fallbackType,
        ICollection<PresetDependency> missing)
    {
        if (!(token is JObject o)) return null;

        string packId = o.Value<string>("packId");
        string entryName = o.Value<string>("entryName");
        string type = o.Value<string>("reskinType") ?? fallbackType;
        if (string.IsNullOrEmpty(packId) || string.IsNullOrEmpty(entryName)) return null;

        var pack = ReskinRegistry.reskinPacks.FirstOrDefault(p => p.UniqueId == packId);
        var entry = pack?.Reskins?.FirstOrDefault(e => e.Name == entryName && e.Type == type);

        if (entry == null && missing != null && missing.All(d => d.PackId != packId))
        {
            missing.Add(new PresetDependency
            {
                PackId = packId,
                WorkshopId = o.Value<ulong?>("workshopId") ?? 0,
                Name = pack?.Name ?? packId,
            });
        }

        return entry;
    }
}

public static class PresetBuilder
{
    /// Build a preset from the current profile, capturing only the selected fields.
    /// Computes teamScoped (one-sided => that side) and the reskin dependency list.
    public static Preset FromProfile(ReskinProfileManager.Profile profile,
        IEnumerable<string> selectedFieldIds, string name)
    {
        var preset = new Preset { PresetName = name };
        var deps = new Dictionary<string, PresetDependency>();
        var teams = new HashSet<PresetTeam>();

        foreach (var id in selectedFieldIds.Distinct())
        {
            var field = PresetFieldRegistry.ById(id);
            if (field == null)
            {
                Plugin.LogWarning($"[Presets] Unknown field id '{id}' while saving preset; skipped.");
                continue;
            }

            var value = field.GetValue(profile);
            preset.Fields[id] = PresetCodec.Encode(field, value);

            if (field.Team == PresetTeam.Blue || field.Team == PresetTeam.Red)
                teams.Add(field.Team);

            CollectDependencies(value, deps);
        }

        // Team-scoped only when exactly one side is represented.
        if (teams.Count == 1)
            preset.TeamScoped = teams.Contains(PresetTeam.Blue) ? "blue" : "red";

        preset.Dependencies = deps.Values.ToList();
        return preset;
    }

    private static void CollectDependencies(object value, Dictionary<string, PresetDependency> deps)
    {
        switch (value)
        {
            case ReskinRegistry.ReskinEntry e:
                AddDep(e, deps);
                break;
            case IEnumerable<ReskinRegistry.ReskinEntry> list:
                foreach (var item in list) AddDep(item, deps);
                break;
        }
    }

    private static void AddDep(ReskinRegistry.ReskinEntry e, Dictionary<string, PresetDependency> deps)
    {
        var pack = e?.ParentPack;
        if (pack == null || string.IsNullOrEmpty(pack.UniqueId)) return;
        if (deps.ContainsKey(pack.UniqueId)) return;
        deps[pack.UniqueId] = new PresetDependency
        {
            PackId = pack.UniqueId,
            WorkshopId = pack.WorkshopId,
            Name = pack.Name,
        };
    }
}
