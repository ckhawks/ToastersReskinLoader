using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.emotes;

// Shows ":name:" above the emote under the mouse.
// Emote rects come from PostProcessTextVertices: any glyph quad whose UVs exactly match an atlas
// slot is that emote. It is recomputed only when the label re-lays out. Hover is polled from the
// mouse position rather than pointer events, so it works even when chat labels are not pickable.
public class EmoteHover : MonoBehaviour
{
    // A glyph's UV corners must land within this many texels of a slot's corners.
    const float UV_TOLERANCE_TEXELS = 0.5f;
    // Sprites scale to the font's ascent, so this shows the emote at roughly 48px.
    const float PREVIEW_FONT_SIZE = 56f;

    class Tracked
    {
        public Label Label;
        public VisualElement Viewport;
        public readonly List<(Rect rect, EmoteSlot slot)> Hits = new List<(Rect, EmoteSlot)>();
    }

    static EmoteHover instance;

    readonly List<Tracked> tracked = new List<Tracked>();
    EmoteAtlas atlas;
    EmoteTooltip tooltip;
    // Emote in the tooltip preview right now, so it keeps animating.
    string shownId;
    bool loggedFirstHit;

    public static EmoteHover Create(EmoteAtlas atlas)
    {
        var go = new GameObject("TRLEmotesHover");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<EmoteHover>();
        instance.atlas = atlas;
        return instance;
    }

    public static void Track(Label label)
    {
        // A re-rendered message is tracked already; its hits refresh on the next layout.
        if (instance == null || instance.tracked.Exists(t => t.Label == label))
        {
            return;
        }
        var entry = new Tracked
        {
            Label = label,
            Viewport = label.GetFirstAncestorOfType<ScrollView>()?.contentViewport,
        };
        label.PostProcessTextVertices = glyphs => instance.CollectHits(entry, glyphs);
        instance.tracked.Add(entry);
    }

    void CollectHits(Tracked entry, TextElement.GlyphsEnumerable glyphs)
    {
        entry.Hits.Clear();
        float atlasW = atlas.Texture.width;
        float atlasH = atlas.Texture.height;

        foreach (TextElement.Glyph glyph in glyphs)
        {
            if (glyph.vertices.Length < 4)
            {
                continue;
            }
            Vector2 uvMin = glyph.vertices[0].uv;
            Vector2 uvMax = uvMin;
            Vector2 posMin = glyph.vertices[0].position;
            Vector2 posMax = posMin;
            for (int i = 1; i < 4; i++)
            {
                Vertex v = glyph.vertices[i];
                uvMin = Vector2.Min(uvMin, v.uv);
                uvMax = Vector2.Max(uvMax, v.uv);
                posMin = Vector2.Min(posMin, (Vector2)v.position);
                posMax = Vector2.Max(posMax, (Vector2)v.position);
            }

            var origin = new Vector2Int(Mathf.RoundToInt(uvMin.x * atlasW), Mathf.RoundToInt(uvMin.y * atlasH));
            if (!atlas.TryGetByOrigin(origin, out EmoteSlot slot))
            {
                continue;
            }
            if (Mathf.Abs(uvMin.x * atlasW - slot.Rect.xMin) > UV_TOLERANCE_TEXELS
                || Mathf.Abs(uvMin.y * atlasH - slot.Rect.yMin) > UV_TOLERANCE_TEXELS
                || Mathf.Abs(uvMax.x * atlasW - slot.Rect.xMax) > UV_TOLERANCE_TEXELS
                || Mathf.Abs(uvMax.y * atlasH - slot.Rect.yMax) > UV_TOLERANCE_TEXELS)
            {
                continue;
            }

            entry.Hits.Add((Rect.MinMaxRect(posMin.x, posMin.y, posMax.x, posMax.y), slot));
        }

        if (!loggedFirstHit && entry.Hits.Count > 0)
        {
            loggedFirstHit = true;
            Plugin.Log($"[Emotes] Hover: first emote rect {entry.Hits[0].rect} in label of size {entry.Label.layout.size}");
        }
    }

    void Update()
    {
        if (tracked.Count == 0)
        {
            return;
        }
        tracked.RemoveAll(t => t.Label.panel == null);

        if (UnityEngine.Cursor.lockState == CursorLockMode.Locked || Mouse.current == null)
        {
            Hide();
            return;
        }

        Vector2 screen = Mouse.current.position.ReadValue();
        screen.y = Screen.height - screen.y;

        for (int i = tracked.Count - 1; i >= 0; i--)
        {
            Tracked t = tracked[i];
            if (t.Hits.Count == 0 || t.Label.ClassListContains("blurred"))
            {
                continue;
            }
            Vector2 world = RuntimePanelUtils.ScreenToPanel(t.Label.panel, screen);
            if (!t.Label.worldBound.Contains(world))
            {
                continue;
            }
            if (t.Viewport != null && !t.Viewport.worldBound.Contains(world))
            {
                continue;
            }
            Vector2 local = t.Label.WorldToLocal(world);
            foreach ((Rect rect, EmoteSlot slot) in t.Hits)
            {
                if (rect.Contains(local))
                {
                    Show(t.Label, rect, slot);
                    return;
                }
            }
        }
        Hide();
    }

    void Show(Label label, Rect localRect, EmoteSlot slot)
    {
        VisualElement root = EmoteUi.DocumentRoot(label);
        if (tooltip == null || tooltip.parent != root)
        {
            tooltip?.RemoveFromHierarchy();
            tooltip = new EmoteTooltip(label);
            root.Add(tooltip);
        }
        // Large preview, shrunk for wide emotes so it stays about square.
        float aspect = slot.Rect.height > 0 ? (float)slot.Rect.width / slot.Rect.height : 1f;
        float previewSize = aspect > 1f ? PREVIEW_FONT_SIZE / aspect : PREVIEW_FONT_SIZE;
        tooltip.ShowAt(":" + slot.Name + ":", root.WorldToLocal(label.LocalToWorld(localRect)), atlas.Tag(slot), previewSize);
        shownId = slot.Id;
    }

    public static void CollectVisible(HashSet<string> ids)
    {
        if (instance?.shownId != null)
        {
            ids.Add(instance.shownId);
        }
    }

    void Hide()
    {
        shownId = null;
        tooltip?.Hide();
    }

    public void Destroy()
    {
        foreach (Tracked t in tracked)
        {
            t.Label.PostProcessTextVertices = null;
        }
        tracked.Clear();
        tooltip?.RemoveFromHierarchy();
        instance = null;
        Object.Destroy(gameObject);
    }
}
