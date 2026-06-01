// ReskinRegistry.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using ToasterReskinLoader.swappers;
using UnityEngine;

namespace ToasterReskinLoader;

public static class ReskinRegistry
{
    public static List<ReskinPack> reskinPacks = new List<ReskinPack>();
    public readonly static List<string> ReskinTypes =
        new List<string>{"stick_attacker", "stick_goalie", "net", "puck", "rink_ice", "jersey_torso", "jersey_groin", "legpad", "helmet", "goalie_mask", "tape_attacker_blade", "tape_attacker_shaft", "tape_goalie_blade", "tape_goalie_shaft" }; // , "arena"

    public static void ReloadPacks()
    {
        reskinPacks.Clear();
        LoadPacks();
        FullArenaSwapper.ScanAvailableArenas();
    }
    
    public static void LoadPacks()
    {
        Plugin.Log($"Loading reskin packs...");

        // Workshop packs
        Plugin.Log($"Looking for packs in workshop: {PathManager.WorkshopRoot}");
        if (Directory.Exists(PathManager.WorkshopRoot))
        {
            foreach (var dir in Directory.GetDirectories(PathManager.WorkshopRoot))
            {
                LoadPackDirectory(dir);
            }
        }
        else
        {
            Plugin.LogWarning($"Workshop folder not found: {PathManager.WorkshopRoot}");
        }

        // Local packs
        Plugin.Log($"Looking for packs in: {PathManager.LocalReskinFolder}");
        foreach (var dir in Directory.GetDirectories(PathManager.LocalReskinFolder))
        {
            LoadPackDirectory(dir);
        }

        Plugin.Log($"Loaded {reskinPacks.Count} packs");

        WarnDuplicateUniqueIds();
    }

    // Returns groups of packs that share a (non-empty) unique-id.
    // Each item: (id, list of packs colliding on that id).
    public static List<(string Id, List<ReskinPack> Packs)> FindDuplicateUniqueIds()
    {
        return reskinPacks
            .Where(p => !string.IsNullOrWhiteSpace(p.UniqueId))
            .GroupBy(p => p.UniqueId.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => (g.Key, g.ToList()))
            .ToList();
    }

    private static void WarnDuplicateUniqueIds()
    {
        // Two packs sharing a unique-id will silently clobber each other when
        // ReskinProfileManager keys profiles or lookups by UniqueId. Surface
        // it loudly so the author can fix the manifest.
        var dupes = FindDuplicateUniqueIds();
        if (dupes.Count == 0) return;

        foreach (var (id, packs) in dupes)
        {
            string names = string.Join(", ", packs.Select(p => $"\"{p.Name}\""));
            Plugin.LogWarning($"[ReskinRegistry] Duplicate unique-id '{id}' shared by {packs.Count} packs: {names}");
        }

        try
        {
            int worstCount = dupes.Max(g => g.Packs.Count);
            string firstId = dupes[0].Id;
            string msg = dupes.Count == 1
                ? $"{worstCount} packs share unique-id '{firstId}'. Check logs."
                : $"{dupes.Count} unique-ids are duplicated across packs. Check logs.";
            MonoBehaviourSingleton<UIManager>.Instance?.ToastManager?.ShowToast(
                "Duplicate Pack IDs", msg, 8f);
        }
        catch { /* UIManager may not exist yet at startup; log already covers it */ }
    }

    public static void LoadPackDirectory(string dir)
    {
        string manifestPath = Path.Combine(dir, "reskinpack.json");
        if (!File.Exists(manifestPath))
        {
            Plugin.Log($" - Missing reskinpack.json in {dir}");
            return;
        }

        // *** NEW LOGIC: Extract Workshop ID from folder name ***
        string folderName = Path.GetFileName(dir);
        // ulong.TryParse is perfect here. If it fails (e.g., for a local pack with a text name),
        // workshopId will remain 0, which is exactly what we want.
        ulong.TryParse(folderName, out ulong workshopId);
        
        try
        {
            string json = File.ReadAllText(manifestPath);
            var pack = JsonConvert.DeserializeObject<ReskinPack>(json);
            if (pack != null)
            {
                // Assign the captured ID to the pack object
                pack.WorkshopId = workshopId;
                pack.FolderPath = dir;
                
                // make paths absolute
                foreach (var skin in pack.Reskins)
                {
                    if (!ReskinTypes.Contains(skin.Type))
                    {
                        Plugin.Log($"   - Unknown reskin type: {skin.Type}");
                        continue;
                    };
                    skin.Path = Path.GetFullPath(Path.Combine(dir, skin.Path));
            
                    // *** ADD THIS LINE ***
                    skin.ParentPack = pack; // Set the back-reference to the parent pack
                }
                reskinPacks.Add(pack);
                Plugin.Log($" - Loaded pack: {pack.Name} v{pack.Version} with {pack.Reskins.Count} reskins.");
            }
        }
        catch (Exception ex)
        {
            Plugin.LogError($" - Failed to load reskinpack.json in {dir}: {ex}");
        }
    }
    
    public static List<ReskinEntry> GetReskinEntriesByType(string reskinType)
    {
        if (string.IsNullOrEmpty(reskinType))
            return new List<ReskinEntry>();

        return reskinPacks
            .Where(pack => pack.Reskins != null)
            .SelectMany(pack => pack.Reskins)
            .Where(entry => string.Equals(entry.Type, reskinType,
                StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// The "Unchanged" sentinel entry for a reskin type (no pack, null path). Used as the
    /// first dropdown choice and the fallback value when no reskin is selected. Centralizes
    /// what was a hand-built ReskinEntry repeated across every section.
    /// </summary>
    public static ReskinEntry UnchangedEntry(string reskinType) =>
        new ReskinEntry { Name = "Unchanged", Path = null, Type = reskinType };

    /// <summary>
    /// Convenience for dropdowns: the reskins of a type with the "Unchanged" sentinel
    /// prepended at index 0. Returns the sentinel via <paramref name="unchanged"/> so callers
    /// can reuse it as the fallback selection.
    /// </summary>
    public static List<ReskinEntry> GetReskinChoices(string reskinType, out ReskinEntry unchanged)
    {
        unchanged = UnchangedEntry(reskinType);
        var choices = GetReskinEntriesByType(reskinType);
        choices.Insert(0, unchanged);
        return choices;
    }


    public class ReskinPack
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        
        [JsonProperty("unique-id")]
        public string UniqueId { get; set; }

        [JsonProperty("pack-version")]
        public string PackVersion { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }
        
        // This is not part of the JSON file, it's derived from the folder structure at runtime.
        [JsonIgnore]
        public ulong WorkshopId { get; set; }

        // Absolute path to the pack's folder on disk (set at load). Used to discover
        // bundled presets in <folder>/presets/. Not serialized.
        [JsonIgnore]
        public string FolderPath { get; set; }

        [JsonProperty("reskins")]
        public List<ReskinEntry> Reskins { get; set; } = new List<ReskinEntry>();
    }

    public class ReskinEntry
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        // Path to asset file (relative in JSON; converted to absolute in LoadPacks())
        [JsonProperty("path")]
        public string Path { get; set; }

        // For arena type: the prefab name within the asset bundle
        [JsonProperty("prefabName")]
        public string PrefabName { get; set; }

        // Runtime-only reference to the pack this entry belongs to
        [JsonIgnore]
        public ReskinPack ParentPack { get; set; }
    }
}