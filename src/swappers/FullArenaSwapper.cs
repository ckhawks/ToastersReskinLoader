using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ToasterReskinLoader.swappers
{
    public static class FullArenaSwapper
    {
        private static Type sceneInformationType;
        private static MethodInfo requestLoadMethod;
        private static bool isInitialized = false;
        
        // Arena information container class
        public class ArenaInfo
        {
            public string Name { get; set; }
            public string BundlePath { get; set; }
            public string PrefabName { get; set; }
            public string WorkshopId { get; set; }
            public string FolderPath { get; set; }
            
            public override string ToString()
            {
                return $"{Name} (prefab: {PrefabName})";
            }
        }
        
        // List of all discovered arenas
        private static List<ArenaInfo> availableArenas = new List<ArenaInfo>();
        
        // Initialize the arena swapping system
        public static void Initialize()
        {
            try
            {
                if (isInitialized) return;
                
                Plugin.Log("[FullArena] Initializing arena swapping system...");
                
                // Find SceneryChanger.dll
                string sceneryChangerPath = FindSceneryChangerDll();
                if (string.IsNullOrEmpty(sceneryChangerPath))
                {
                    Plugin.LogError("[FullArena] SceneryChanger.dll not found!");
                    return;
                }
                
                // Load the assembly
                Assembly assembly = Assembly.LoadFrom(sceneryChangerPath);
                if (assembly == null)
                {
                    Plugin.LogError("[FullArena] Failed to load SceneryChanger.dll!");
                    return;
                }
                
                // Get required types from SceneryChanger
                Type sceneLoadCoordinatorType = assembly.GetType("SceneryChanger.Services.SceneLoadCoordinator");
                if (sceneLoadCoordinatorType == null)
                {
                    Plugin.LogError("[FullArena] SceneLoadCoordinator not found!");
                    return;
                }
                
                sceneInformationType = assembly.GetType("SceneryChanger.Model.SceneInformation");
                if (sceneInformationType == null)
                {
                    Plugin.LogError("[FullArena] SceneInformation not found!");
                    return;
                }
                
                // Get the RequestLoad method
                requestLoadMethod = sceneLoadCoordinatorType.GetMethod("RequestLoad", 
                    BindingFlags.NonPublic | BindingFlags.Static);
                    
                if (requestLoadMethod == null)
                {
                    Plugin.LogError("[FullArena] RequestLoad method not found!");
                    return;
                }
                
                // Scan for available arena files
                ScanAvailableArenas();
                
                isInitialized = true;
                Plugin.Log("[FullArena] Initialization successful!");
            }
            catch (Exception ex)
            {
                Plugin.LogError($"[FullArena] Initialization error: {ex.Message}");
            }
        }
        
        // Main method to load an arena
        public static void LoadArena(string bundleName, string prefabName, string workshopId = null)
        {
            try
            {
                Plugin.Log($"[FullArena] Loading arena: bundle={bundleName}, prefab={prefabName}, workshop={workshopId}");
                
                if (!isInitialized)
                {
                    Plugin.LogError("[FullArena] System not initialized!");
                    return;
                }
                
                // If Workshop ID is provided, load from that folder
                if (!string.IsNullOrEmpty(workshopId))
                {
                    LoadArenaFromWorkshop(bundleName, prefabName, workshopId);
                    return;
                }
                
                // Check if we're in the game scene
                Scene currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (!currentScene.name.Equals("level_1", StringComparison.OrdinalIgnoreCase))
                {
                    Plugin.LogError($"[FullArena] Not in game scene! Current scene: {currentScene.name}");
                    return;
                }
                
                // Create SceneInformation instance
                object sceneInfo = Activator.CreateInstance(sceneInformationType);
                
                // Set required fields
                sceneInformationType.GetField("bundleName").SetValue(sceneInfo, bundleName);
                sceneInformationType.GetField("prefabName").SetValue(sceneInfo, prefabName);
                sceneInformationType.GetField("useSceneLocally").SetValue(sceneInfo, true);
                sceneInformationType.GetField("skyboxName").SetValue(sceneInfo, "");
                sceneInformationType.GetField("contentKey64").SetValue(sceneInfo, "");
                
                // Call RequestLoad to trigger arena swap
                requestLoadMethod.Invoke(null, new object[] { 
                    currentScene, 
                    sceneInfo, 
                    "ToasterReskinLoader" 
                });
                
                // Save to profile
                ReskinProfileManager.currentProfile.fullArenaEnabled = true;
                ReskinProfileManager.currentProfile.fullArenaBundle = bundleName;
                ReskinProfileManager.currentProfile.fullArenaPrefab = prefabName;
                ReskinProfileManager.currentProfile.fullArenaWorkshopId = workshopId ?? "";
                ReskinProfileManager.SaveProfile();
                
                Plugin.Log($"[FullArena] ✅ Arena loaded: {bundleName}/{prefabName}");
            }
            catch (Exception ex)
            {
                Plugin.LogError($"[FullArena] Error loading arena: {ex.Message}");
            }
        }
        
        // Load arena from a specific Workshop folder
        private static void LoadArenaFromWorkshop(string bundleName, string prefabName, string workshopId)
        {
            try
            {
                string workshopPath = FindWorkshopFolder(workshopId);
                if (string.IsNullOrEmpty(workshopPath))
                {
                    Plugin.LogError($"[FullArena] Workshop folder {workshopId} not found!");
                    return;
                }
                
                // Load normally - SceneryChanger will find the file
                LoadArena(bundleName, prefabName);
            }
            catch (Exception ex)
            {
                Plugin.LogError($"[FullArena] Error loading from Workshop: {ex.Message}");
            }
        }
        
        // Find Workshop folder by ID
        private static string FindWorkshopFolder(string workshopId)
        {
            string workshopRoot = @"D:\SteamLibrary\steamapps\workshop\content\2994020";
            if (!Directory.Exists(workshopRoot)) return null;
            
            // Direct path
            string directPath = Path.Combine(workshopRoot, workshopId);
            if (Directory.Exists(directPath)) return directPath;
            
            // Search by partial match
            string[] folders = Directory.GetDirectories(workshopRoot);
            foreach (string folder in folders)
            {
                if (Path.GetFileName(folder).Contains(workshopId))
                {
                    return folder;
                }
            }
            
            return null;
        }
        
        // Disable full arena replacement
        public static void DisableFullArena()
        {
            ReskinProfileManager.currentProfile.fullArenaEnabled = false;
            ReskinProfileManager.SaveProfile();
            Plugin.Log("[FullArena] Full arena replacement disabled");
        }
        
        // Get list of discovered arenas
        public static List<ArenaInfo> GetAvailableArenas()
        {
            return new List<ArenaInfo>(availableArenas);
        }
        
        // Apply arena from saved profile
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
        
        // Scan for arena files in various locations
        private static void ScanAvailableArenas()
        {
            availableArenas.Clear();
            
            // 1. Workshop folders
            ScanWorkshopArenas();
            
            // 2. Standard paths
            ScanStandardPaths();
            
            Plugin.Log($"[FullArena] Found arenas: {availableArenas.Count}");
        }
        
        // Scan Workshop folders for arenas
        private static void ScanWorkshopArenas()
        {
            string workshopRoot = @"D:\SteamLibrary\steamapps\workshop\content\2994020";
            if (!Directory.Exists(workshopRoot)) return;
            
            string[] folders = Directory.GetDirectories(workshopRoot);
            foreach (string folder in folders)
            {
                string workshopId = Path.GetFileName(folder);
                ScanArenasInFolder(folder, workshopId);
            }
        }
        
        // Scan standard installation paths
        private static void ScanStandardPaths()
        {
            string[] paths = 
            {
                Path.Combine(Application.dataPath, "..", "AssetBundles"),
                Path.Combine(Directory.GetCurrentDirectory(), "AssetBundles"),
                @"D:\SteamLibrary\steamapps\workshop\content\2994020\3566470321\AssetBundles"
            };
            
            foreach (string path in paths)
            {
                if (Directory.Exists(path))
                {
                    ScanArenasInFolder(path, null);
                }
            }
        }
        
        // Scan a specific folder for arena files
        private static void ScanArenasInFolder(string folderPath, string workshopId)
        {
            try
            {
                string assetBundlesPath = Path.Combine(folderPath, "AssetBundles");
                if (!Directory.Exists(assetBundlesPath))
                {
                    // If this is already an AssetBundles folder
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
                    if (fileName.EndsWith(".manifest")) continue;
                    
                    string arenaName = Path.GetFileNameWithoutExtension(file);
                    string extension = Path.GetExtension(file).ToLower();
                    
                    // Skip .abx if .unity3d exists
                    if (extension == ".abx")
                    {
                        string unity3dVersion = file.Replace(".abx", ".unity3d");
                        if (File.Exists(unity3dVersion)) continue;
                    }
                    
                    // Determine prefab name
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
                }
            }
            catch (Exception ex)
            {
                Plugin.LogError($"[FullArena] Error scanning {folderPath}: {ex.Message}");
            }
        }
        
        // Determine prefab name based on arena filename
        private static string DeterminePrefabName(string arenaName)
        {
            arenaName = arenaName.ToLower();
            
            if (arenaName.Contains("outdoor") && arenaName.Contains("hockey"))
                return "OutdoorHockey";
            if (arenaName.Contains("tymb"))
                return "Arena";
                
            return "Arena"; // Default
        }
        
        // Find SceneryChanger.dll in various locations
        private static string FindSceneryChangerDll()
        {
            string[] paths = 
            {
                @"D:\SteamLibrary\steamapps\workshop\content\2994020\3566470321\SceneryChanger.dll",
                Path.Combine(Application.dataPath, "Plugins", "SceneryChanger.dll"),
                Path.Combine(Directory.GetCurrentDirectory(), "SceneryChanger.dll"),
            };
            
            foreach (string path in paths)
            {
                if (File.Exists(path)) return path;
            }
            
            return null;
        }
    }
}