using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ToasterReskinLoader.emotes;

// POC source: <plugin dir>/emotes/<name>.png for static emotes, and
// <plugin dir>/emotes/<name>/<n>.png (+ optional delay.txt, in ms) for animated ones.
// Emotes under emotes/personal/ stand in for the player's personal set; everything else is the
// server set. Server emotes are returned first so they win name collisions in the atlas.
public static class LocalEmoteLoader
{
    public const string PERSONAL_DIR = "personal";
    const string SERVER_DIR = "server";

    const int DEFAULT_FRAME_DELAY_MS = 100;
    const int MAX_SIZE = 128;
    const int RENDER_SIZE = 64;
    const int MAX_FRAMES = 60;

    static readonly Regex validName = new Regex("^[A-Za-z0-9_]{2,32}$");

    public static List<EmoteSource> Load(string emotesDir)
    {
        var result = new List<EmoteSource>();
        if (!Directory.Exists(emotesDir))
        {
            Plugin.Log($"[Emotes] No emotes folder at {emotesDir}; emotes are off.");
            return result;
        }
        LoadDir(emotesDir, false, result);
        LoadDir(Path.Combine(emotesDir, SERVER_DIR), false, result);
        LoadDir(Path.Combine(emotesDir, PERSONAL_DIR), true, result);
        return result;
    }

    static void LoadDir(string dir, bool personal, List<EmoteSource> result)
    {
        if (!Directory.Exists(dir))
        {
            return;
        }

        foreach (string file in Directory.GetFiles(dir, "*.png"))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            if (!CheckName(name))
            {
                continue;
            }
            Texture2D frame = LoadFrame(file);
            if (frame == null)
            {
                continue;
            }
            var source = new EmoteSource { Name = name, Personal = personal };
            source.Frames.Add(frame);
            source.FrameDelaysMs.Add(0);
            result.Add(source);
        }

        foreach (string sub in Directory.GetDirectories(dir))
        {
            string name = Path.GetFileName(sub);
            if (name == PERSONAL_DIR || name == SERVER_DIR || !CheckName(name))
            {
                continue;
            }
            EmoteSource source = LoadAnimated(name, sub);
            if (source != null)
            {
                source.Personal = personal;
                result.Add(source);
            }
        }
    }

    static EmoteSource LoadAnimated(string name, string dir)
    {
        // delay.txt holds one value for every frame, or one value per line per frame.
        var delays = new List<int>();
        string delayFile = Path.Combine(dir, "delay.txt");
        if (File.Exists(delayFile))
        {
            foreach (string line in File.ReadAllLines(delayFile))
            {
                if (int.TryParse(line.Trim(), out int parsed) && parsed > 0)
                {
                    delays.Add(parsed);
                }
            }
        }

        var files = Directory.GetFiles(dir, "*.png")
            .Select(f => new { path = f, index = ParseIndex(Path.GetFileNameWithoutExtension(f)) })
            .Where(f => f.index >= 0)
            .OrderBy(f => f.index)
            .Take(MAX_FRAMES)
            .ToList();

        var source = new EmoteSource { Name = name };
        foreach (var f in files)
        {
            Texture2D frame = LoadFrame(f.path);
            if (frame == null)
            {
                continue;
            }
            if (source.Frames.Count > 0 && (frame.width != source.Width || frame.height != source.Height))
            {
                Plugin.LogError($"[Emotes] Emote {name}: frame {f.path} is {frame.width}x{frame.height}, expected {source.Width}x{source.Height}. Skipped.");
                UnityEngine.Object.Destroy(frame);
                continue;
            }
            int frameIndex = source.Frames.Count;
            source.Frames.Add(frame);
            source.FrameDelaysMs.Add(delays.Count == 0
                ? DEFAULT_FRAME_DELAY_MS
                : delays[Math.Min(frameIndex, delays.Count - 1)]);
        }

        if (source.Frames.Count == 0)
        {
            Plugin.LogError($"[Emotes] Emote {name}: no usable frames in {dir}");
            return null;
        }
        return source;
    }

    static int ParseIndex(string s)
    {
        return int.TryParse(s, out int i) ? i : -1;
    }

    static bool CheckName(string name)
    {
        if (validName.IsMatch(name))
        {
            return true;
        }
        Plugin.LogError($"[Emotes] Skipping emote '{name}': names must be 2-32 letters, digits or underscores.");
        return false;
    }

    // Always returns RGBA32 so every frame matches the atlas format for Graphics.CopyTexture,
    // downscaled so the longest side is at most RENDER_SIZE.
    static Texture2D LoadFrame(string path)
    {
        try
        {
            var decoded = new Texture2D(2, 2);
            if (!decoded.LoadImage(File.ReadAllBytes(path)))
            {
                Plugin.LogError($"[Emotes] Could not decode {path}");
                UnityEngine.Object.Destroy(decoded);
                return null;
            }
            if (decoded.width > MAX_SIZE || decoded.height > MAX_SIZE)
            {
                Plugin.LogError($"[Emotes] {path} is {decoded.width}x{decoded.height}; max is {MAX_SIZE}x{MAX_SIZE}. Skipped.");
                UnityEngine.Object.Destroy(decoded);
                return null;
            }
            Texture2D frame = Resize(decoded);
            UnityEngine.Object.Destroy(decoded);
            return frame;
        }
        catch (Exception e)
        {
            Plugin.LogError($"[Emotes] Failed to load {path}: {e.Message}");
            return null;
        }
    }

    static Texture2D Resize(Texture2D source)
    {
        float scale = Mathf.Min(1f, (float)RENDER_SIZE / Mathf.Max(source.width, source.height));
        int width = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
        int height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));

        RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        source.filterMode = FilterMode.Bilinear;
        Graphics.Blit(source, rt);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        var frame = new Texture2D(width, height, TextureFormat.RGBA32, false);
        frame.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        frame.Apply(false, false);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        return frame;
    }
}
