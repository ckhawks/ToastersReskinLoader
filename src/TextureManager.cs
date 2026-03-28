// TextureManager.cs

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ToasterReskinLoader;

public static class TextureManager
{
    // The cache remains the same, but its key is now always the file path.
    private static readonly Dictionary<string, Texture2D> _loadedTextures = new Dictionary<string, Texture2D>();
    
    /// <summary>
    /// Gets a texture for a given ReskinEntry.
    /// This is the primary access point for all textures.
    /// It will load the texture from disk if it's not already in the cache.
    /// </summary>
    /// <param name="reskinEntry">The reskin entry to get the texture for.</param>
    /// <returns>The loaded Texture2D, or null if the entry is null or loading fails.</returns>
    public static Texture2D GetTexture(ReskinRegistry.ReskinEntry reskinEntry)
    {
        if (reskinEntry == null || string.IsNullOrEmpty(reskinEntry.Path))
            return null;

        return GetTextureByPath(reskinEntry.Path, reskinEntry.Name);
    }

    /// <summary>
    /// Shared cache-check-and-load logic used by both GetTexture and GetTextureFromFilePath.
    /// </summary>
    private static Texture2D GetTextureByPath(string path, string displayName)
    {
        // Check if the texture is already cached
        if (_loadedTextures.TryGetValue(path, out var cachedTexture))
        {
            if (cachedTexture != null)
                return cachedTexture;

            // Texture was destroyed but still in dictionary - remove and reload
            _loadedTextures.Remove(path);
        }

        Plugin.LogDebug($"Cache miss for '{displayName}'. Loading texture from: {path}");
        Texture2D newTexture = LoadTextureFromFile(path);

        if (newTexture != null)
            _loadedTextures[path] = newTexture;

        return newTexture;
    }

    public static void ClearTextureCache()
    {
        _loadedTextures.Clear();
    }
    
    /// <summary>
    /// The core file-to-texture loading logic. Kept private.
    /// </summary>
    private static Texture2D LoadTextureFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Plugin.LogError($"File not found: {filePath}");
            return null;
        }

        try
        {
            byte[] fileData = File.ReadAllBytes(filePath);
            // Create a non-readable texture for performance unless you need to access its pixels later.
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            
            if (texture.LoadImage(fileData))
            {
                // Set filter mode to take advantage of mipmaps
                texture.filterMode = FilterMode.Trilinear;
                
                // Apply changes and generate mipmaps (true parameter generates mipmaps)
                texture.Apply(true);
                
                return texture;
            }
            else
            {
                Plugin.LogError($"Failed to load image data from: {filePath}");
                return null;
            }
        }
        catch (System.Exception ex)
        {
            Plugin.LogError($"Exception while loading texture from {filePath}: {ex.Message}");
            return null;
        }
    }

    public static Texture2D GetTextureFromFilePath(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return null;
        return GetTextureByPath(filePath, filePath);
    }
    
    /// <summary>
    /// Unloads all textures that are not present in the provided list of active entries.
    /// This is the "garbage collection" step to free up memory.
    /// </summary>
    /// <param name="activeEntries">An enumerable of all currently active ReskinEntry objects.</param>
    public static void UnloadUnusedTextures(IEnumerable<ReskinRegistry.ReskinEntry> activeEntries)
    {
        // Create a HashSet of active file paths for efficient O(1) lookups.
        var activePaths = new HashSet<string>(activeEntries
            .Where(entry => entry != null && !string.IsNullOrEmpty(entry.Path))
            .Select(entry => entry.Path));

        // Find all cached texture paths that are NOT in the active list.
        // We collect keys to a new list to avoid modifying the dictionary while iterating over it.
        var pathsToUnload = _loadedTextures.Keys.Where(path => !activePaths.Contains(path)).ToList();

        if (pathsToUnload.Count == 0) return;

        Plugin.LogDebug($"Unloading {pathsToUnload.Count} unused textures...");

        foreach (var path in pathsToUnload)
        {
            if (_loadedTextures.TryGetValue(path, out var textureToUnload))
            {
                // Destroy the Unity Texture2D object to free GPU memory.
                if (textureToUnload != null)
                {
                    Object.Destroy(textureToUnload);
                }
                
                // Remove the entry from our cache dictionary.
                _loadedTextures.Remove(path);
                Plugin.LogDebug($" - Unloaded and removed '{path}' from cache.");
            }
        }
    }
}