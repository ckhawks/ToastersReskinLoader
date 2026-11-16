using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine.TextCore.Text;

namespace ToasterReskinLoader.emotes;

// UI Toolkit has two text paths and each resolves <sprite="name"> differently:
// - advanced (ATG): RichTextTagParser.s_SpriteAssetCache, keyed by name
// - legacy: MaterialReferenceManager, keyed by the case-insensitive name hash
// Both normally fill themselves from Resources, which a runtime asset is not in,
// so we insert ours directly.
public static class SpriteAssetRegistry
{
    static readonly Dictionary<string, WeakReference<SpriteAsset>> atgCache = FindAtgCache();
    static readonly IDictionary<int, SpriteAsset> legacyLookup = FindLegacyLookup();

    public static void Register(SpriteAsset asset)
    {
        if (atgCache != null)
        {
            atgCache[asset.name] = new WeakReference<SpriteAsset>(asset);
        }
        if (legacyLookup != null)
        {
            legacyLookup[TextHash.CaseInsensitive(asset.name)] = asset;
        }
    }

    public static void Unregister(SpriteAsset asset)
    {
        atgCache?.Remove(asset.name);
        legacyLookup?.Remove(TextHash.CaseInsensitive(asset.name));
    }

    static Dictionary<string, WeakReference<SpriteAsset>> FindAtgCache()
    {
        Type parser = AccessTools.TypeByName("UnityEngine.TextCore.RichTextTagParser");
        FieldInfo field = parser == null ? null : AccessTools.Field(parser, "s_SpriteAssetCache");
        var cache = field?.GetValue(null) as Dictionary<string, WeakReference<SpriteAsset>>;
        if (cache == null)
        {
            Plugin.LogError("[Emotes] RichTextTagParser.s_SpriteAssetCache not found. Emotes will not render on the advanced text path.");
        }
        return cache;
    }

    static IDictionary<int, SpriteAsset> FindLegacyLookup()
    {
        Type manager = AccessTools.TypeByName("UnityEngine.TextCore.Text.MaterialReferenceManager");
        object instance = manager == null ? null : AccessTools.Property(manager, "instance")?.GetValue(null);
        FieldInfo field = manager == null ? null : AccessTools.Field(manager, "m_SpriteAssetReferenceLookup");
        var lookup = instance == null ? null : field?.GetValue(instance) as IDictionary<int, SpriteAsset>;
        if (lookup == null)
        {
            Plugin.LogError("[Emotes] MaterialReferenceManager sprite lookup not found. Emotes will not render on the legacy text path.");
        }
        return lookup;
    }
}
