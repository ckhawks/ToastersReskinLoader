using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.Text;

namespace ToasterReskinLoader.emotes;

public class EmoteSlot
{
    public string Id;
    // Name the emote had in the first set it arrived from; used for hover text.
    public string Name;
    public RectInt Rect;
    public string SpriteName => "e" + Id;
}

// One atlas page that emotes are added to as their sets arrive, never rebuilt.
// Each emote gets one 64px cell and a sprite named by its puckstats id, so two players'
// :pog: are different sprites. Animated emotes keep their cell and EmoteAnimator copies
// later frames into it from the emote's sprite sheet.
public class EmoteAtlas
{
    public const string NAME = "TRLEmotes";
    const int SIZE = 2048;
    // 64px frames plus a 1px gutter on each side so bilinear sampling never bleeds.
    const int CELL = 66;
    const int CELLS_PER_ROW = SIZE / CELL;
    const int CAPACITY = CELLS_PER_ROW * CELLS_PER_ROW;
    // How far the emote sits above the baseline, as a fraction of its height.
    const float BEARING_Y = 0.85f;

    static readonly PropertyInfo spriteSheetProperty = typeof(SpriteAsset).GetProperty("spriteSheet");

    public Texture2D Texture { get; private set; }
    public SpriteAsset SpriteAsset { get; private set; }

    readonly Dictionary<string, EmoteSlot> byId = new Dictionary<string, EmoteSlot>();
    readonly Dictionary<Vector2Int, EmoteSlot> byOrigin = new Dictionary<Vector2Int, EmoteSlot>();
    readonly List<EmoteSlot> byIndex = new List<EmoteSlot>();
    bool loggedFull;

    public static EmoteAtlas Create()
    {
        var atlas = new EmoteAtlas();
        atlas.Texture = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false) { name = NAME };
        atlas.Texture.SetPixels32(new Color32[SIZE * SIZE]);
        atlas.Texture.filterMode = FilterMode.Bilinear;
        atlas.Texture.wrapMode = TextureWrapMode.Clamp;
        // Cells are filled on the GPU, so the CPU copy is never needed.
        atlas.Texture.Apply(false, true);

        var spriteAsset = ScriptableObject.CreateInstance<SpriteAsset>();
        spriteAsset.name = NAME;
        spriteAsset.hashCode = TextHash.CaseInsensitive(NAME);
        spriteSheetProperty.GetSetMethod(true).Invoke(spriteAsset, new object[] { atlas.Texture });
        spriteAsset.material = CreateMaterial(atlas.Texture);
        spriteAsset.UpdateLookupTables();
        atlas.SpriteAsset = spriteAsset;
        return atlas;
    }

    public int Count => byId.Count;

    public EmoteSlot Get(string id)
    {
        return id != null && byId.TryGetValue(id, out EmoteSlot slot) ? slot : null;
    }

    // Laid-out text holds each sprite as U+E000 + its index, which is what a selection copies.
    public const char FIRST_SPRITE_CHAR = '';

    public EmoteSlot GetBySpriteChar(char c)
    {
        int index = c - FIRST_SPRITE_CHAR;
        return index >= 0 && index < byIndex.Count ? byIndex[index] : null;
    }

    public bool TryGetByOrigin(Vector2Int origin, out EmoteSlot slot)
    {
        return byOrigin.TryGetValue(origin, out slot);
    }

    public string Tag(EmoteSlot slot)
    {
        return "<sprite=\"" + NAME + "\" name=\"" + slot.SpriteName + "\">";
    }

    // Copies the emote's first frame into a free cell and registers its sprite.
    public EmoteSlot Add(RemoteEmote emote, Texture2D sheet)
    {
        EmoteSlot existing = Get(emote.Id);
        if (existing != null)
        {
            return existing;
        }
        if (byId.Count >= CAPACITY)
        {
            if (!loggedFull)
            {
                loggedFull = true;
                Plugin.LogError($"[Emotes] Atlas is full ({CAPACITY} emotes); new emotes will show as text.");
            }
            return null;
        }

        int index = byId.Count;
        var rect = new RectInt(
            (index % CELLS_PER_ROW) * CELL + 1,
            (index / CELLS_PER_ROW) * CELL + 1,
            emote.FrameWidth,
            emote.FrameHeight);
        RectInt source = emote.FrameRect(0, sheet.height);
        Graphics.CopyTexture(sheet, 0, 0, source.x, source.y, source.width, source.height, Texture, 0, 0, rect.x, rect.y);

        var metrics = new GlyphMetrics(rect.width, rect.height, 0f, rect.height * BEARING_Y, rect.width);
        var glyph = new SpriteGlyph((uint)index, metrics, new GlyphRect(rect.x, rect.y, rect.width, rect.height), 1f, 0);
        var slot = new EmoteSlot { Id = emote.Id, Name = emote.Name, Rect = rect };
        var character = new SpriteCharacter(0xE000u + (uint)index, SpriteAsset, glyph) { name = slot.SpriteName };
        SpriteAsset.spriteGlyphTable.Add(glyph);
        SpriteAsset.spriteCharacterTable.Add(character);
        SpriteAsset.UpdateLookupTables();

        byId[emote.Id] = slot;
        byOrigin[new Vector2Int(rect.x, rect.y)] = slot;
        byIndex.Add(slot);
        return slot;
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
        byId.Clear();
        byOrigin.Clear();
        byIndex.Clear();
        if (SpriteAsset != null)
        {
            Object.Destroy(SpriteAsset.material);
            Object.Destroy(SpriteAsset);
        }
        Object.Destroy(Texture);
    }
}
