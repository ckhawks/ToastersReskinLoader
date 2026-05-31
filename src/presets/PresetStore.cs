// PresetStore.cs
//
// File IO for user-saved presets, stored as individual JSON files under
// <game>/reskinprofiles/presets/. Pack-bundled preset discovery is a later phase;
// this covers the user's own create / list / rename / delete plus the missing-pack check.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace ToasterReskinLoader.presets;

public static class PresetStore
{
    public const string SourceUser = "My Presets";

    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        Formatting = Formatting.Indented,
    };

    public static string UserPresetsDir =>
        Path.Combine(PathManager.GameRootFolder, "reskinprofiles", "presets");

    private static void EnsureDir()
    {
        if (!Directory.Exists(UserPresetsDir))
            Directory.CreateDirectory(UserPresetsDir);
    }

    /// All user presets, sorted alphabetically by name. Corrupt files are skipped (logged).
    public static List<Preset> LoadUserPresets()
    {
        var result = new List<Preset>();
        EnsureDir();

        foreach (var path in Directory.GetFiles(UserPresetsDir, "*.json"))
        {
            var preset = LoadFile(path, SourceUser, readOnly: false);
            if (preset != null) result.Add(preset);
        }

        return result.OrderBy(p => p.PresetName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// Presets bundled inside installed packs (<pack>/presets/*.json), read-only. Sorted by name.
    public static List<Preset> LoadPackPresets()
    {
        var result = new List<Preset>();

        foreach (var pack in ReskinRegistry.reskinPacks)
        {
            if (string.IsNullOrEmpty(pack.FolderPath)) continue;
            string dir = Path.Combine(pack.FolderPath, "presets");
            if (!Directory.Exists(dir)) continue;

            foreach (var path in Directory.GetFiles(dir, "*.json"))
            {
                var preset = LoadFile(path, $"Pack: {pack.Name}", readOnly: true);
                if (preset != null) result.Add(preset);
            }
        }

        return result.OrderBy(p => p.PresetName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// Local packs (workshop id 0) the user can save presets into, as (label, presetsDir) pairs.
    /// Workshop packs are excluded — Steam overwrites them on update.
    public static List<(string Label, string Dir)> GetLocalPackTargets()
    {
        return ReskinRegistry.reskinPacks
            .Where(p => p.WorkshopId == 0 && !string.IsNullOrEmpty(p.FolderPath))
            .Select(p => ($"Local pack: {p.Name}", Path.Combine(p.FolderPath, "presets")))
            .ToList();
    }

    public static Preset LoadFile(string path, string sourceLabel, bool readOnly)
    {
        try
        {
            var preset = JsonConvert.DeserializeObject<Preset>(File.ReadAllText(path));
            if (preset == null)
            {
                Plugin.LogWarning($"[Presets] Empty or invalid preset file: {path}");
                return null;
            }

            preset.FilePath = path;
            preset.SourceLabel = sourceLabel;
            preset.IsReadOnly = readOnly;
            if (string.IsNullOrEmpty(preset.PresetName))
                preset.PresetName = Path.GetFileNameWithoutExtension(path);
            return preset;
        }
        catch (Exception ex)
        {
            Plugin.LogError($"[Presets] Failed to read preset '{path}': {ex.Message}");
            return null;
        }
    }

    /// Write a preset to the user presets folder. Returns the file path, or null on failure.
    /// Avoids clobbering a different preset that happens to sanitize to the same file name.
    public static string Save(Preset preset, string targetDir = null)
    {
        try
        {
            string dir = targetDir ?? UserPresetsDir;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string fileName = UniqueFileName(dir, preset.PresetName, preset.FilePath);
            string path = Path.Combine(dir, fileName);

            File.WriteAllText(path, JsonConvert.SerializeObject(preset, JsonSettings));
            preset.FilePath = path;
            Plugin.Log($"[Presets] Saved preset '{preset.PresetName}' to {path}");
            return path;
        }
        catch (Exception ex)
        {
            Plugin.LogError($"[Presets] Failed to save preset '{preset.PresetName}': {ex.Message}");
            return null;
        }
    }

    public static bool Delete(Preset preset)
    {
        if (preset?.FilePath == null) return false;
        if (preset.IsReadOnly)
        {
            Plugin.LogWarning($"[Presets] Refusing to delete read-only preset '{preset.PresetName}'.");
            return false;
        }

        try
        {
            if (File.Exists(preset.FilePath)) File.Delete(preset.FilePath);
            return true;
        }
        catch (Exception ex)
        {
            Plugin.LogError($"[Presets] Failed to delete preset '{preset.PresetName}': {ex.Message}");
            return false;
        }
    }

    /// Rename a user preset (updates the name and the backing file). Returns the new path or null.
    public static string Rename(Preset preset, string newName)
    {
        if (preset?.FilePath == null || preset.IsReadOnly || string.IsNullOrWhiteSpace(newName))
            return null;

        try
        {
            string dir = Path.GetDirectoryName(preset.FilePath);
            string oldPath = preset.FilePath;
            preset.PresetName = newName.Trim();

            string newPath = Path.Combine(dir, UniqueFileName(dir, preset.PresetName, oldPath));
            File.WriteAllText(newPath, JsonConvert.SerializeObject(preset, JsonSettings));
            if (!string.Equals(newPath, oldPath, StringComparison.OrdinalIgnoreCase) && File.Exists(oldPath))
                File.Delete(oldPath);

            preset.FilePath = newPath;
            return newPath;
        }
        catch (Exception ex)
        {
            Plugin.LogError($"[Presets] Failed to rename preset to '{newName}': {ex.Message}");
            return null;
        }
    }

    /// Dependencies whose pack isn't currently installed — drives the "missing reskins" warning.
    public static List<PresetDependency> GetMissingDependencies(Preset preset)
    {
        if (preset?.Dependencies == null) return new List<PresetDependency>();
        return preset.Dependencies
            .Where(d => ReskinRegistry.reskinPacks.All(p => p.UniqueId != d.PackId))
            .ToList();
    }

    // Pick a file name based on the preset name, keeping an existing file's name if it already
    // maps to this preset (so re-saving/renaming doesn't spawn "Name (1).json" duplicates).
    private static string UniqueFileName(string dir, string presetName, string existingPath)
    {
        string baseName = Sanitize(presetName);
        if (string.IsNullOrEmpty(baseName)) baseName = "preset";

        string candidate = baseName + ".json";
        string candidatePath = Path.Combine(dir, candidate);

        int n = 1;
        while (File.Exists(candidatePath)
               && !string.Equals(candidatePath, existingPath, StringComparison.OrdinalIgnoreCase))
        {
            candidate = $"{baseName} ({n++}).json";
            candidatePath = Path.Combine(dir, candidate);
        }

        return candidate;
    }

    private static string Sanitize(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }
}
