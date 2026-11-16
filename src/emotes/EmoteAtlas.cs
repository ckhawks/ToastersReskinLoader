using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.Text;

namespace ToasterReskinLoader.emotes;

public class EmoteSlot
{
    public EmoteSource Source;
    public RectInt Rect;
}

// Packs the first frame of every emote into one texture and wraps it in a TextCore
// SpriteAsset, so chat labels can draw them with <sprite="ATLAS" name="emote">.
// Animated emotes keep their slot and get later frames copied in by EmoteAnimator.
public class EmoteAtlas
{
    const int PADDING = 2;
    const int MAX_ATLAS_SIZE = 4096;
    // How far the emote sits above the baseline, as a fraction of its height.
    const float BEARING_Y = 0.85f;

    static readonly PropertyInfo spriteSheetProperty = typeof(SpriteAsset).GetProperty("spriteSheet");

    public string Name;
    public Texture2D Texture;
    public SpriteAsset SpriteAsset;
    public readonly Dictionary<string, EmoteSlot> Slots = new Dictionary<string, EmoteSlot>(System.StringComparer.OrdinalIgnoreCase);

    public static EmoteAtlas Build(string atlasName, List<EmoteSource> sources)
    {
        var atlas = new EmoteAtlas { Name = atlasName };

        var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        var unique = new List<EmoteSource>();
        foreach (EmoteSource source in sources)
        {
            if (!seen.Add(source.Name))
            {
                Plugin.LogError($"[Emotes] Duplicate emote name '{source.Name}' (names are case-insensitive). Skipped.");
                continue;
            }
            unique.Add(source);
        }

        var firstFrames = new Texture2D[unique.Count];
        for (int i = 0; i < unique.Count; i++)
        {
            firstFrames[i] = unique[i].Frames[0];
        }

        atlas.Texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        atlas.Texture.name = atlasName;
        Rect[] uvRects = atlas.Texture.PackTextures(firstFrames, PADDING, MAX_ATLAS_SIZE, false);
        atlas.Texture.filterMode = FilterMode.Bilinear;
        atlas.Texture.wrapMode = TextureWrapMode.Clamp;

        var spriteAsset = ScriptableObject.CreateInstance<SpriteAsset>();
        spriteAsset.name = atlasName;
        spriteAsset.hashCode = TextHash.CaseInsensitive(atlasName);
        spriteSheetProperty.GetSetMethod(true).Invoke(spriteAsset, new object[] { atlas.Texture });
        spriteAsset.material = CreateMaterial(atlas.Texture);

        for (int i = 0; i < unique.Count; i++)
        {
            EmoteSource source = unique[i];
            var rect = new RectInt(
                Mathf.RoundToInt(uvRects[i].x * atlas.Texture.width),
                Mathf.RoundToInt(uvRects[i].y * atlas.Texture.height),
                source.Width,
                source.Height);

            var metrics = new GlyphMetrics(source.Width, source.Height, 0f, source.Height * BEARING_Y, source.Width);
            var glyphRect = new GlyphRect(rect.x, rect.y, rect.width, rect.height);
            var glyph = new SpriteGlyph((uint)i, metrics, glyphRect, 1f, 0);
            var character = new SpriteCharacter(0xE000u + (uint)i, spriteAsset, glyph) { name = source.Name };

            spriteAsset.spriteGlyphTable.Add(glyph);
            spriteAsset.spriteCharacterTable.Add(character);
            atlas.Slots[source.Name] = new EmoteSlot { Source = source, Rect = rect };
        }

        spriteAsset.UpdateLookupTables();
        atlas.SpriteAsset = spriteAsset;

        // Animation copies GPU-side only, so drop the CPU copies of every frame and the atlas.
        foreach (EmoteSource source in unique)
        {
            foreach (Texture2D frame in source.Frames)
            {
                frame.Apply(false, true);
            }
        }
        atlas.Texture.Apply(false, true);

        Plugin.Log($"[Emotes] Built atlas '{atlasName}': {unique.Count} emotes, {atlas.Texture.width}x{atlas.Texture.height}");
        return atlas;
    }

    static Material CreateMaterial(Texture2D texture)
    {
        Shader shader = Shader.Find("Hidden/TextCore/Sprite")
            ?? Shader.Find("TextMeshPro/Sprite")
            ?? Shader.Find("UI/Default");
        if (shader == null)
        {
            Plugin.LogError("[Emotes] No sprite shader found. Emotes may not render.");
        }
        var material = new Material(shader) { name = texture.name };
        material.mainTexture = texture;
        return material;
    }

    public void Destroy()
    {
        foreach (EmoteSlot slot in Slots.Values)
        {
            foreach (Texture2D frame in slot.Source.Frames)
            {
                Object.Destroy(frame);
            }
        }
        Slots.Clear();
        if (SpriteAsset != null)
        {
            Object.Destroy(SpriteAsset.material);
            Object.Destroy(SpriteAsset);
        }
        Object.Destroy(Texture);
    }
}
