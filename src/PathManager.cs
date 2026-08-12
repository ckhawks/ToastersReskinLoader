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
    // Steam Workshop app id for Puck. Subscribed workshop items install under
    // <Steam library>\steamapps\workshop\content\<APP_ID>\<itemId>\.
    public const string WorkshopAppId = "2994020";

    // Cached paths (calculated once at startup)
    private static string _workshopRoot;
    private static string _localReskinFolder;
    private static string _gameRootFolder;

    /// <summary>
    /// Returns the game install folder (the one Steam creates under steamapps\common).
    /// Example: C:\Program Files (x86)\Steam\steamapps\common\Puck
    /// </summary>
    public static string GameRootFolder
    {
        get
        {
            if (_gameRootFolder == null)
                _gameRootFolder = ResolveGameRootFolder();
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
    /// Resolves the game install folder from Application.dataPath.
    ///
    /// dataPath sits at a different depth per platform, so a fixed ".." is wrong on macOS:
    ///   Windows/Linux: &lt;install&gt;\Puck_Data                              -> up 1
    ///   macOS:         &lt;install&gt;/Puck.app/Contents/Resources/Data       -> up 4
    ///
    /// Rather than hardcode a per-platform depth, walk up from dataPath looking for a
    /// ".app" bundle component; the game install folder is that bundle's parent. When no
    /// bundle is in the path (Windows/Linux) this falls back to the single-level parent.
    /// </summary>
    private static string ResolveGameRootFolder()
    {
        string dataPath = Path.GetFullPath(Application.dataPath);

        // Walk up looking for the .app bundle (macOS). Bounded by the loop condition
        // reaching the filesystem root, where GetDirectoryName returns null.
        for (string dir = dataPath; !string.IsNullOrEmpty(dir); dir = Path.GetDirectoryName(dir))
        {
            if (!Path.GetFileName(dir).EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                continue;

            string bundleParent = Path.GetDirectoryName(dir);
            if (string.IsNullOrEmpty(bundleParent))
                break;

            Plugin.Log($"[PathManager] Resolved game root from .app bundle: {bundleParent}");
            return bundleParent;
        }

        string gameRoot = Path.GetFullPath(Path.Combine(dataPath, ".."));
        Plugin.Log($"[PathManager] Resolved game root: {gameRoot}");
        return gameRoot;
    }

    /// <summary>
    /// Resolves the workshop root folder for this game's subscribed items.
    ///
    /// Steam stores a game's workshop content in the SAME library as the game, so we can
    /// derive it from the game install regardless of which drive/library Steam used and
    /// regardless of whether this mod is running from the workshop or a local Plugins folder:
    ///   &lt;library&gt;\steamapps\common\Puck      (GameRootFolder)
    ///   &lt;library&gt;\steamapps\workshop\content\2994020   (workshop root)
    ///
    /// This avoids any hardcoded machine-specific path and works identically for:
    ///  - Workshop install: DLL at ...\workshop\content\2994020\&lt;workshopId&gt;\ToasterReskinLoader.dll
    ///  - Local/dev install: DLL at ...\common\Puck\Plugins\ToasterReskinLoader\ToasterReskinLoader.dll
    /// </summary>
    private static string ResolveWorkshopRoot()
    {
        Plugin.LogDebug($"[PathManager] Mod DLL path: {Assembly.GetExecutingAssembly().Location}");

        // GameRootFolder is ...\steamapps\common\Puck; go up two levels to ...\steamapps,
        // then down into workshop\content\<appId>.
        string workshopRoot = Path.GetFullPath(
            Path.Combine(GameRootFolder, "..", "..", "workshop", "content", WorkshopAppId));

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
