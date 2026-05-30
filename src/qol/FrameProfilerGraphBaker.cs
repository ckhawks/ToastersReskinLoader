using System;
using UnityEngine;

namespace ToasterReskinLoader.qol;

// A baked graph: instead of issuing N GUI.DrawTexture calls per frame to
// draw N bars, we paint the bars into a pixel buffer at low frequency
// (10Hz), upload to a Texture2D, then OnGUI just blits the whole texture
// with a single GUI.DrawTexture. Drops per-graph cost from ~150 IMGUI
// draw calls to 1, at the price of one SetPixels32+Apply per re-bake.
//
// Coordinate system: y = 0 is the BOTTOM of the graph (matches Unity
// Texture2D's bottom-left origin), so values that grow "upward" can be
// drawn intuitively without flipping.
public sealed class BakedGraph
{
    public Texture2D Tex;
    Color32[] pixels;
    public readonly int W, H;
    Color32 bg;

    public BakedGraph(int w, int h, Color bgColor)
    {
        W = w; H = h;
        bg = ToColor32(bgColor);
        pixels = new Color32[w * h];
        Tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Tex.filterMode = FilterMode.Point;
        Tex.wrapMode = TextureWrapMode.Clamp;
        Clear();
        Apply();
    }

    public void Clear()
    {
        // Fast clear via array fill.
        for (int i = 0; i < pixels.Length; i++) pixels[i] = bg;
    }

    // 1-px horizontal line at row y (y=0 = bottom of graph).
    public void HLine(int y, Color color)
    {
        if (y < 0 || y >= H) return;
        var c = ToColor32(color);
        int row = y * W;
        for (int x = 0; x < W; x++) pixels[row + x] = c;
    }

    // Filled rectangle from (x, yBottom) up to (x+w-1, yTop), y=0 = bottom.
    public void Rect(int x, int yBottom, int yTop, int w, Color color)
    {
        if (w <= 0) return;
        var c = ToColor32(color);
        int x0 = Math.Max(0, x);
        int x1 = Math.Min(W, x + w);
        int y0 = Math.Max(0, yBottom);
        int y1 = Math.Min(H - 1, yTop);
        for (int y = y0; y <= y1; y++)
        {
            int row = y * W;
            for (int xi = x0; xi < x1; xi++) pixels[row + xi] = c;
        }
    }

    // Single-pixel-thick (or h-tall) horizontal dot at column x, row y.
    public void Dot(int x, int y, int w, int h, Color color)
    {
        Rect(x, y, Math.Min(H - 1, y + h - 1), w, color);
    }

    public void Apply()
    {
        Tex.SetPixels32(pixels);
        Tex.Apply(false);
    }

    public void Dispose()
    {
        if (Tex != null) UnityEngine.Object.Destroy(Tex);
        Tex = null;
        pixels = null;
    }

    static Color32 ToColor32(Color c) => new Color32(
        (byte)Mathf.Clamp(c.r * 255f, 0, 255),
        (byte)Mathf.Clamp(c.g * 255f, 0, 255),
        (byte)Mathf.Clamp(c.b * 255f, 0, 255),
        (byte)Mathf.Clamp(c.a * 255f, 0, 255));
}
