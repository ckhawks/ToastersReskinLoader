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
        new List<string>{"stick_attacker", "stick_goalie", "net", "puck", "rink_ice", "jersey_torso", "jersey_groin", "legpad", "goalie_helmet", "skater_helmet", "goalie_mask" }; // , "arena"

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