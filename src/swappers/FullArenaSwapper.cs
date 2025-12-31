using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ToasterReskinLoader.swappers
{
    /// <summary>
    /// Handles full arena replacement functionality using the SceneryChanger mod.
    /// Discovers available arena asset bundles and coordinates their loading into the game.
    /// </summary>
    public static class FullArenaSwapper
    {
        // Reflection references to SceneryChanger mod's internal types/methods
        private static Type sceneInformationType;
        private static MethodInfo requestLoadMethod;
        public static bool isInitialized = false;

        /// <summary>
        /// Metadata about a discoverable arena asset bundle.
        /// </summary>
        public class ArenaInfo
        {
            /// <summary>Arena asset bundle filename (without extension)</summary>
            public string Name { get; set; }

            /// <summary>Full filesystem path to the asset bundle file</summary>
            public string BundlePath { get; set; }

            /// <summary>Name of the prefab object within the bundle to instantiate</summary>
            public string PrefabName { get; set; }

            /// <summary>Workshop ID if from Steam Workshop, null if local</summary>
            public string WorkshopId { get; set; }

            /// <summary>Folder containing this arena's asset bundle</summary>
            public string FolderPath { get; set; }

            public override string ToString()
            {
                return $"{Name} (prefab: {PrefabName})";
            }
        }

        /// Cache of all discovered arenas from workshop and local folders
        private static List<ArenaInfo> availableArenas = new List<ArenaInfo>();
        
        /// <summary>
        /// Initializes the arena swapping system by loading and reflecting on the SceneryChanger mod.
        /// Must be called once before LoadArena() can be used.
        /// Safe to call multiple times - only runs once.
        /// </summary>
        public static void Initialize()
        {
            try
            {
                if (isInitialized) return;

                Plugin.Log("[FullArena] Initializing arena swapping system...");

                // Step 1: Locate SceneryChanger.dll (a dependency mod)
                string sceneryChangerPath = FindSceneryChangerDll();
                if (string.IsNullOrEmpty(sceneryChangerPath))
                {
                    Plugin.LogError("[FullArena] SceneryChanger.dll not found! This feature requires the SceneryChanger mod.");
                    return;
                }

                // Step 2: Load the assembly and use reflection to access its internal types
                Assembly assembly = Assembly.LoadFrom(sceneryChangerPath);
                if (assembly == null)
                {
                    Plugin.LogError("[FullArena] Failed to load SceneryChanger assembly!");
                    return;
                }

                // Step 3: Get required types for arena loading
                // SceneLoadCoordinator.RequestLoad() is the method we'll invoke to swap arenas
                Type sceneLoadCoordinatorType = assembly.GetType("SceneryChanger.Services.SceneLoadCoordinator");
                if (sceneLoadCoordinatorType == null)
                {
                    Plugin.LogError("[FullArena] SceneLoadCoordinator type not found in SceneryChanger.dll!");
                    return;
                }

                // SceneInformation is the data class we'll populate with arena details
                sceneInformationType = assembly.GetType("SceneryChanger.Model.SceneInformation");
                if (sceneInformationType == null)
                {
                    Plugin.LogError("[FullArena] SceneInformation type not found in SceneryChanger.dll!");
                    return;
                }

                // Step 4: Get the RequestLoad method via reflection (it's private, so we use NonPublic)
                requestLoadMethod = sceneLoadCoordinatorType.GetMethod("RequestLoad",
                    BindingFlags.NonPublic | BindingFlags.Static);

                if (requestLoadMethod == null)
                {
                    Plugin.LogError("[FullArena] RequestLoad method not found on SceneLoadCoordinator!");
                    return;
                }

                // Step 5: Scan for available arena asset bundles in workshop and local folders
                ScanAvailableArenas();

                isInitialized = true;
                Plugin.Log("[FullArena] Initialization successful!");
            }
            catch (Exception ex)
            {
                Plugin.LogError($"[FullArena] Initialization error: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Loads an arena asset bundle and swaps it into the current game scene.
        /// Can load from built-in paths or a specific Workshop folder.
        /// </summary>
        /// <param name="bundleName">Name of the asset bundle (without .abx/.unity3d extension)</param>
        /// <param name="prefabName">Name of the prefab object to instantiate from the bundle</param>
        /// <param name="workshopId">Optional: Steam Workshop ID to load from. If provided, searches that workshop folder.</param>
        public static void LoadArena(string bundleName, string prefabName, string workshopId = null)
        {
            try
            {
                Plugin.Log($"[FullArena] Loading arena: bundle={bundleName}, prefab={prefabName}, workshop={workshopId}");

                if (!isInitialized)
                {
                    Plugin.LogError("[FullArena] System not initialized! Call Initialize() first.");
                    return;
                }

                // If Workshop ID is provided, verify the folder exists
                if (!string.IsNullOrEmpty(workshopId))
                {
                    string workshopPath = FindWorkshopFolder(workshopId);
                    if (string.IsNullOrEmpty(workshopPath))
                    {
                        Plugin.LogError($"[FullArena] Workshop folder {workshopId} not found!");
                        return;
                    }
                    // Note: SceneryChanger will search for the bundle relative to its installation,
                    // so we can't directly point it to a specific folder. This is a limitation.
                    Plugin.LogWarning($"[FullArena] Found workshop folder, but SceneryChanger may not load from it directly.");
                }

                // Verify we're in a game scene (not menu/changing_room)
                Scene currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (!currentScene.name.Equals("level_1", StringComparison.OrdinalIgnoreCase))
                {
                    Plugin.LogWarning($"[FullArena] Not in expected game scene 'level_1'. Current scene: {currentScene.name}");
                    // Note: Could extend this to support other scene names if needed
                }

                // Create a SceneInformation instance and populate it via reflection
                object sceneInfo = Activator.CreateInstance(sceneInformationType);
                if (sceneInfo == null)
                {
                    Plugin.LogError("[FullArena] Failed to create SceneInformation instance!");
                    return;
                }

                // Set the fields that SceneryChanger's RequestLoad method expects
                // These correspond to the SceneInformation class properties
                try
                {
                    sceneInformationType.GetField("bundleName")?.SetValue(sceneInfo, bundleName);
                    sceneInformationType.GetField("prefabName")?.SetValue(sceneInfo, prefabName);
                    sceneInformationType.GetField("useSceneLocally")?.SetValue(sceneInfo, true);
                    sceneInformationType.GetField("skyboxName")?.SetValue(sceneInfo, "");
                    sceneInformationType.GetField("contentKey64")?.SetValue(sceneInfo, "");
                }
                catch (Exception ex)
                {
                    Plugin.LogError($"[FullArena] Failed to set SceneInformation fields: {ex.Message}");
                    return;
                }

                // Invoke SceneryChanger's RequestLoad to trigger the arena swap
                // Parameters: (currentScene, sceneInfo, sourceMod)
                requestLoadMethod?.Invoke(null, new object[]
                {
                    currentScene,
                    sceneInfo,
                    "ToasterReskinLoader"
                });

                // Persist the selected arena to the profile so it reloads on next game load
                ReskinProfileManager.currentProfile.fullArenaEnabled = true;
                ReskinProfileManager.currentProfile.fullArenaBundle = bundleName;
                ReskinProfileManager.currentProfile.fullArenaPrefab = prefabName;
                ReskinProfileManager.currentProfile.fullArenaWorkshopId = workshopId ?? "";
                ReskinProfileManager.SaveProfile();

                Plugin.Log($"[FullArena] Arena loaded: {bundleName}/{prefabName}");
            }
            catch (Exception ex)
            {
                Plugin.LogError($"[FullArena] Error loading arena: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Finds a workshop folder by its ID. Used to verify workshop content is installed.
        /// </summary>
        private static string FindWorkshopFolder(string workshopId)
        {
            return PathManager.FindWorkshopFolder(workshopId);
        }

        /// <summary>Disables full arena replacement and saves the change to profile.</summary>
        public static void DisableFullArena()
        {
            ReskinProfileManager.currentProfile.fullArenaEnabled = false;
            ReskinProfileManager.SaveProfile();
            Plugin.Log("[FullArena] Full arena replacement disabled");
        }

        /// <summary>Returns a copy of the list of all discovered arenas.</summary>
        public static List<ArenaInfo> GetAvailableArenas()
        {
            return new List<ArenaInfo>(availableArenas);
        }

        /// <summary>
        /// Applies the arena that was previously saved to the user's profile.
        /// Called during mod initialization to restore the user's selected arena.
        /// </summary>
        public static void ApplyFromProfile()
        {
            if (!ReskinProfileManager.currentProfile.fullArenaEnabled ||
                string.IsNullOrEmpty(ReskinProfileManager.currentProfile.fullArenaBundle))
            {
                return;
            }

            string workshopId = ReskinProfileManager.currentProfile.fullArenaWorkshopId;
            LoadArena(
                ReskinProfileManager.currentProfile.fullArenaBundle,
                ReskinProfileManager.currentProfile.fullArenaPrefab,
                string.IsNullOrEmpty(workshopId) ? null : workshopId
            );
        }
        
        /// <summary>
        /// Discovers all available arena asset bundles by scanning workshop and local folders.
        /// Called during Initialize() to populate the availableArenas list.
        /// </summary>
        public static void ScanAvailableArenas()
        {
            availableArenas.Clear();

            // Scan Steam Workshop folders for arena mods
            ScanWorkshopArenas();

            // Scan standard installation paths (game folder, local directory, etc)
            ScanStandardPaths();

            Plugin.Log($"[FullArena] Arena discovery complete: found {availableArenas.Count} arenas");
        }

        /// <summary>
        /// Scans all Steam Workshop folders for arena asset bundles.
        /// Workshop structure: steamapps/workshop/content/2994020/{workshopId}/AssetBundles/
        /// </summary>
        private static void ScanWorkshopArenas()
        {
            string workshopRoot = PathManager.WorkshopRoot;
            if (!Directory.Exists(workshopRoot))
            {
                Plugin.LogWarning($"[FullArena] Workshop folder not found: {workshopRoot}");
                return;
            }

            string[] folders = Directory.GetDirectories(workshopRoot);
            foreach (string folder in folders)
            {
                string workshopId = Path.GetFileName(folder);

                // Skip specific workshop folders
                if (workshopId == "3495059812") // Toaster's Rink companion - ignore these prefabs
                {
                    Plugin.LogDebug($"[FullArena] Skipping workshop folder: {workshopId}");
                    continue;
                }

                ScanArenasInFolder(folder, workshopId);
            }
        }

        /// <summary>
        /// Scans standard game installation paths for arena asset bundles.
        /// Checks the game folder's AssetBundles directory and other common locations.
        /// </summary>
        private static void ScanStandardPaths()
        {
            string[] paths =
            {
                Path.Combine(PathManager.GameRootFolder, "AssetBundles"),
                Path.Combine(Application.dataPath, "..", "AssetBundles"),
                Path.Combine(Directory.GetCurrentDirectory(), "AssetBundles"),
            };

            foreach (string path in paths)
            {
                if (Directory.Exists(path))
                {
                    ScanArenasInFolder(path, null);
                }
            }
        }
        
        /// <summary>
        /// Scans a specific folder for arena asset bundle files.
        /// Asset bundles can have .abx, .unity3d, or other extensions.
        /// We skip .manifest files (metadata) and .abx if a .unity3d version exists (newer format).
        /// </summary>
        /// <param name="folderPath">Path to scan (can be an ArenaBundle folder or an AssetBundles subfolder)</param>
        /// <param name="workshopId">Workshop ID if from Steam Workshop, null if local</param>
        private static void ScanArenasInFolder(string folderPath, string workshopId)
        {
            try
            {
                string assetBundlesPath = Path.Combine(folderPath, "AssetBundles");
                if (!Directory.Exists(assetBundlesPath))
                {
                    // If the provided path IS already an AssetBundles folder, use it directly
                    if (folderPath.EndsWith("AssetBundles", StringComparison.OrdinalIgnoreCase))
                    {
                        assetBundlesPath = folderPath;
                    }
                    else
                    {
                        return;
                    }
                }

                string[] files = Directory.GetFiles(assetBundlesPath);
                foreach (string file in files)
                {
                    string fileName = Path.GetFileName(file);

                    // Skip .manifest files (these are metadata/mapping files for asset bundles)
                    if (fileName.EndsWith(".manifest")) continue;

                    string arenaName = Path.GetFileNameWithoutExtension(file);
                    string extension = Path.GetExtension(file).ToLower();

                    // Asset bundle format preference: .unity3d is newer format, .abx is older
                    // If both exist for the same bundle, skip the .abx version
                    if (extension == ".abx")
                    {
                        string unity3dVersion = file.Replace(".abx", ".unity3d");
                        if (File.Exists(unity3dVersion)) continue; // Use the .unity3d instead
                    }

                    // Determine the prefab name based on arena filename patterns
                    string prefabName = DeterminePrefabName(arenaName);

                    ArenaInfo arena = new ArenaInfo
                    {
                        Name = arenaName,
                        BundlePath = file,
                        PrefabName = prefabName,
                        WorkshopId = workshopId,
                        FolderPath = folderPath
                    };

                    availableArenas.Add(arena);
                    Plugin.LogDebug($"[FullArena] Discovered arena: {arenaName} ({prefabName})");
                }
            }
            catch (Exception ex)
            {
                Plugin.LogError($"[FullArena] Error scanning {folderPath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Infers the prefab name to instantiate based on the arena asset bundle filename.
        /// This is a heuristic - ideally this should be specified in a manifest file.
        ///
        /// Pattern matching:
        /// - If filename contains "outdoor" AND "hockey": use "OutdoorHockey" prefab
        /// - Otherwise: use default "Arena" prefab
        ///
        /// Warning: This may cause incorrect arena loading if the filename doesn't match the pattern.
        /// Consider adding a manifest file to explicitly specify prefab names.
        /// </summary>
        private static string DeterminePrefabName(string arenaName)
        {
            arenaName = arenaName.ToLower();

            // Check for outdoor hockey arena (less common)
            if (arenaName.Contains("outdoor") && arenaName.Contains("hockey"))
                return "OutdoorHockey";

            // Default to standard Arena prefab for all others
            // This includes "tymb" 
            return "Arena";
        }
        
        /// <summary>
        /// Locates the SceneryChanger.dll mod dependency in various possible locations.
        /// SceneryChanger is a required dependency for full arena replacement to work.
        ///
        /// Search order:
        /// 1. Game Plugins folder (typical installation)
        /// 2. Application data Plugins folder
        /// 3. Current working directory (fallback)
        ///
        /// Returns: Full path to SceneryChanger.dll if found, null otherwise
        /// </summary>
        private static string FindSceneryChangerDll()
        {
            string[] paths =
            {
                // Workshop mod location (item 3566470321)
                Path.Combine(PathManager.WorkshopRoot, "3566470321", "SceneryChanger.dll"),
                // Game Plugins folder
                Path.Combine(PathManager.GameRootFolder, "Plugins", "SceneryChanger.dll"),
                // Application data Plugins folder
                Path.Combine(Application.dataPath, "Plugins", "SceneryChanger.dll"),
                // Current working directory (fallback)
                Path.Combine(Directory.GetCurrentDirectory(), "SceneryChanger.dll"),
            };

            foreach (string path in paths)
            {
                if (File.Exists(path))
                {
                    Plugin.Log($"[FullArena] Found SceneryChanger.dll at: {path}");
                    return path;
                }
            }

            return null;
        }
    }
}