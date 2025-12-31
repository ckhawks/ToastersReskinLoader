using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ToasterReskinLoader;

/// <summary>
/// Centralized path resolution for mod and workshop folders.
/// Uses Assembly location and Application.dataPath to derive all paths consistently.
/// </summary>
public static class PathManager
{
    // Cached paths (calculated once at startup)
    private static string _workshopRoot;
    private static string _localReskinFolder;
    private static string _gameRootFolder;

    public static string GameRootFolder
    {
        get
        {
            if (_gameRootFolder == null)
                _gameRootFolder = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return _gameRootFolder;
        }
    }

    /// <summary>
    /// Returns the Steam workshop root folder.
    /// Example: C:\Program Files (x86)\Steam\steamapps\workshop\content\2994020
    /// </summary>
    public static string WorkshopRoot
    {
        get
        {
            if (_workshopRoot == null)
                _workshopRoot = ResolveWorkshopRoot();
            return _workshopRoot;
        }
    }

    /// <summary>
    /// Returns the local reskin packs folder, creating it if needed.
    /// Example: C:\Program Files (x86)\Steam\steamapps\common\Puck\reskinpacks
    /// </summary>
    public static string LocalReskinFolder
    {
        get
        {
            if (_localReskinFolder == null)
            {
                _localReskinFolder = Path.Combine(GameRootFolder, "reskinpacks");
                if (!Directory.Exists(_localReskinFolder))
                {
                    Plugin.Log($"Creating local reskin folder: {_localReskinFolder}");
                    Directory.CreateDirectory(_localReskinFolder);
                }
            }
            return _localReskinFolder;
        }
    }

    /// <summary>
    /// Resolves the workshop root folder by examining the mod DLL's location.
    /// Follows the path: DLL location → parent → parent → workshop\content\2994020
    ///
    /// Example path resolution:
    /// DLL: C:\Program Files (x86)\Steam\steamapps\workshop\content\2994020\3493628417\ToasterReskinLoader.dll
    /// Returns: C:\Program Files (x86)\Steam\steamapps\workshop\content\2994020
    /// </summary>
    private static string ResolveWorkshopRoot()
    {
        string execPath = Assembly.GetExecutingAssembly().Location;
        Plugin.Log($"[PathManager] Mod DLL path: {execPath}");

        // Development override: if DLL is in the game's Plugins folder instead of workshop,
        // point to the workshop location for testing. Ignore if the workshop path doesn't exist.
        if (execPath.Contains(@"steamapps\common"))
        {
            string devOverridePath = @"C:\Program Files (x86)\Steam\steamapps\workshop\content\2994020";
            if (Directory.Exists(devOverridePath))
            {
                Plugin.Log($"[PathManager] Using development override: {devOverridePath}");
                return devOverridePath;
            }
        }

        // Standard path: workshop mods are at steamapps\workshop\content\2994020\<workshopId>\<files>
        // So we go up two levels from the DLL to get the game ID folder
        string workshopRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(execPath), @".."));

        Plugin.Log($"[PathManager] Resolved workshop root: {workshopRoot}");
        return workshopRoot;
    }

    /// <summary>
    /// Finds a specific workshop folder by ID.
    /// Returns the folder path if found, null otherwise.
    ///
    /// Search order:
    /// 1. Direct match: workshopRoot\<workshopId>
    /// 2. Partial match: folder containing workshopId as substring
    /// </summary>
    public static string FindWorkshopFolder(string workshopId)
    {
        if (string.IsNullOrEmpty(workshopId))
            return null;

        // Try direct path first
        string directPath = Path.Combine(WorkshopRoot, workshopId);
        if (Directory.Exists(directPath))
            return directPath;

        // Search by partial match (in case of folder naming variations)
        try
        {
            string[] folders = Directory.GetDirectories(WorkshopRoot);
            foreach (string folder in folders)
            {
                if (Path.GetFileName(folder).Contains(workshopId))
                {
                    Plugin.LogDebug($"[PathManager] Found workshop folder via partial match: {folder}");
                    return folder;
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.LogError($"[PathManager] Error searching workshop folders: {ex.Message}");
        }

        return null;
    }
}
