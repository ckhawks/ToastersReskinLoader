using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ToasterReskinLoader;

public static class TestArena
{
    public static bool ChangeLevel()
    {
        // string assetBundlePath = System.IO.Path.Combine(Paths.GameRootPath, "reskinpacks/test1/arenas/city/cityarena.test");
        // Locals feed the disabled LoadAndPlaceGameObject/scene scaffolding below.
#pragma warning disable CS0219
        string assetName = "arena";
        Vector3 position = new Vector3(0, 0, 0);
        Quaternion rotation = Quaternion.identity;
#pragma warning restore CS0219

        // TODO this is disabled currently
        
        // bool ok = LoadAndPlaceScene(
        //     System.IO.Path.Combine(
        //         Paths.GameRootPath,
        //         "reskinpacks/test1/arenas/city/cityarena.test"
        //     ),
        //     sceneName: "SampleScene"  // omit or pass null to use the first scene
        // );
        // if (ok)
        // {
        //     Plugin.Log("scene loaded and placed successfully!");
        //     return true;
        // } else
        // {
        //     Plugin.LogError("Failed to load and place scene.");
        //     return false;
        // }
        // GameObject loadedObject = LoadAndPlaceGameObject(assetBundlePath, assetName, position, rotation);
        // if (loadedObject != null)
        // {
        //     Plugin.Log.LogInfo("GameObject loaded and placed successfully!");
        //     return true;
        // }
        // else
        // {
        //     Plugin.Log.LogError("Failed to load and place GameObject.");
        //     return false;
        // }

        return true;
    }
    
    public static GameObject LoadAndPlaceGameObject(string assetBundlePath, string assetName, Vector3 position, Quaternion rotation)
    {
        // Load the AssetBundle
        Plugin.Log($"Loading AssetBundle from path: {assetBundlePath}");
        AssetBundle assetBundle = AssetBundle.LoadFromFile(assetBundlePath);
        if (assetBundle == null)
        {
            Plugin.LogError($"Failed to load AssetBundle from path: {assetBundlePath}");
            return null;
        }

        // Load the GameObject from the AssetBundle
        Plugin.Log($"Loading asset: {assetName}");
        GameObject loadedObject = assetBundle.LoadAsset<GameObject>(assetName);
        if (loadedObject == null)
        {
            Plugin.LogError($"Failed to load asset: {assetName} from AssetBundle");
            assetBundle.Unload(false); // Unload the AssetBundle
            return null;
        }

        // Instantiate the GameObject
        Plugin.Log($"Instantiating GameObject: {assetName}");
        GameObject instantiatedObject = Object.Instantiate(loadedObject, position, rotation);

        // Optionally, unload the AssetBundle to free memory
        assetBundle.Unload(false);

        // Return the instantiated GameObject
        Plugin.Log($"GameObject instantiated successfully at position: {position}");
        return instantiatedObject;
    }

    // public static GameObject LoadAndAddPlaceScene(string assetBundlePath, string assetName, Vector3 position,
    //     Quaternion rotation)
    // {
    //     // Load the AssetBundle
    //     Plugin.Log.LogInfo($"Loading AssetBundle from path: {assetBundlePath}");
    //     AssetBundle assetBundle = AssetBundle.LoadFromFile(assetBundlePath);
    //     if (assetBundle == null)
    //     {
    //         Plugin.Log.LogError($"Failed to load AssetBundle from path: {assetBundlePath}");
    //         return null;
    //     }
    //     
    //     // get list of contained scenes
    //     string[] scenes = assetBundle.GetAllScenePaths();
    //     if (scenes.Length == 0)
    //     {
    //         Plugin.Log.LogError("Bundle contains no scenes.");
    //         assetBundle.Unload(false);
    //         return null;
    //     }
    //     
    //     // pick first scene (or find by name)
    //     string scenePath = scenes[0];
    //     Plugin.Log.LogInfo($"Loading scene '{scenePath}' from bundle…");
    //
    //     // Async load the scene (additive or single)
    //     var op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(scenePath, 
    //         LoadSceneMode.Additive);
    //     while (!op.isDone)
    //         return null;
    //
    //     Plugin.Log.LogInfo("Scene loaded successfully!");
    //     return o
    // }
    
    // public static IEnumerator LoadAndPlaceScene(
    //         string bundlePath,
    //         string sceneName = null
    //     )
    // {
    //     Plugin.Log.LogInfo($"Loading scene-bundle from: {bundlePath}");
    //     var bundle = AssetBundle.LoadFromFile(bundlePath);
    //     if (bundle == null)
    //     {
    //         Plugin.Log.LogError($"Failed to load AssetBundle at {bundlePath}");
    //         yield break;
    //     }
    //
    //     // Get all scene paths contained in this bundle
    //     string[] scenePaths = bundle.GetAllScenePaths();
    //     if (scenePaths == null || scenePaths.Length == 0)
    //     {
    //         Plugin.Log.LogError(
    //             "Scene bundle contains no scenes: " + bundlePath
    //         );
    //         bundle.Unload(false);
    //         yield break;
    //     }
    //
    //     // Pick the correct scene path
    //     string chosenPath = null;
    //     if (!string.IsNullOrEmpty(sceneName))
    //     {
    //         // find the one whose file name matches sceneName
    //         foreach (var path in scenePaths)
    //         {
    //             // e.g. path = "Assets/Scenes/cityarena.unity"
    //             var file = System.IO.Path.GetFileNameWithoutExtension(path);
    //             if (string.Equals(file, sceneName,
    //                               System.StringComparison.OrdinalIgnoreCase))
    //             {
    //                 chosenPath = path;
    //                 break;
    //             }
    //         }
    //         if (chosenPath == null)
    //         {
    //             Plugin.Log.LogWarning(
    //                 $"Scene '{sceneName}' not found in bundle; " +
    //                 "defaulting to first scene."
    //             );
    //         }
    //     }
    //     // fallback to first
    //     if (chosenPath == null)
    //         chosenPath = scenePaths[0];
    //
    //     Plugin.Log.LogInfo($"Loading scene '{chosenPath}' additively…");
    //     var loadOp = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(
    //         chosenPath, LoadSceneMode.Additive
    //     );
    //     if (loadOp == null)
    //     {
    //         Plugin.Log.LogError("Failed to start scene-load operation.");
    //         bundle.Unload(false);
    //         yield break;
    //     }
    //
    //     // wait for it to finish
    //     while (!loadOp.isDone)
    //         yield return null;
    //
    //     Plugin.Log.LogInfo($"Scene loaded: {chosenPath}");
    //
    //     // You can now unload the bundle data – the scene stays in memory
    //     bundle.Unload(false);
    //     Plugin.Log.LogInfo("AssetBundle unloaded.");
    // }
    
    /// <summary>
        /// Synchronously loads a scene‐bundle from disk and adds its scene
        /// to the current scenes (additive). Blocks until the load is complete.
        /// </summary>
        /// <param name="bundlePath">
        /// Full path to the .test (scene) AssetBundle file.
        /// </param>
        /// <param name="sceneName">
        /// Optional: the name of the scene to load (filename without .unity);
        /// if null/empty, the first scene in the bundle is used.
        /// </param>
        /// <returns>true on success, false on failure.</returns>
    public static bool LoadAndPlaceScene(string bundlePath, string sceneName = null)
    {
        try
        {
            Plugin.Log($"Loading scene-bundle from: {bundlePath}");
            var bundle = AssetBundle.LoadFromFile(bundlePath);
            if (bundle == null)
            {
                Plugin.LogError($"Failed to load AssetBundle at {bundlePath}");
                return false;
            }

            // Grab all scenes in the bundle
            var scenePaths = bundle.GetAllScenePaths();
            if (scenePaths == null || scenePaths.Length == 0)
            {
                Plugin.LogError("Scene bundle contains no scenes.");
                bundle.Unload(false);
                return false;
            }

            // Pick the right path
            string chosenPath = null;
            if (!string.IsNullOrEmpty(sceneName))
            {
                foreach (var path in scenePaths)
                {
                    var file = System.IO.Path.GetFileNameWithoutExtension(path);
                    if (string.Equals(file, sceneName,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        chosenPath = path;
                        break;
                    }
                }
                if (chosenPath == null)
                {
                    Plugin.LogError(
                      $"Scene '{sceneName}' not found; using first scene."
                    );
                }
            }
            if (chosenPath == null)
                chosenPath = scenePaths[0];

            Plugin.Log($"Synchronously loading scene '{chosenPath}' additively…");
            // This will block until the scene is loaded
            UnityEngine.SceneManagement.SceneManager.LoadScene(chosenPath, LoadSceneMode.Additive);
            Plugin.Log("Scene loaded successfully!");

            bundle.Unload(false);
            Plugin.Log("AssetBundle unloaded.");
            return true;
        }
        catch (Exception ex)
        {
            Plugin.LogError($"Exception in LoadAndPlaceScene: {ex}");
            return false;
        }
    }
}