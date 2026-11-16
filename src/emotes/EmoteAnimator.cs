using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ToasterReskinLoader.emotes;

// Animates by copying each animated emote's current frame from its sprite sheet into its
// atlas cell on the GPU, only when the frame changes. Every label showing that emote samples
// the same cell, so cost scales with distinct animated emotes, not with how many are on
// screen, and no text is re-laid out.
//
// Only emotes visible right now are advanced (a chat line still showing, the open picker or
// autocomplete, the hover preview). Visibility is polled a few times a second, not per frame.
public class EmoteAnimator : MonoBehaviour
{
    // Browsers treat GIF delays under 20 ms as 100 ms; match that so broken GIFs don't spin.
    const int MIN_DELAY_MS = 20;
    const int FALLBACK_DELAY_MS = 100;
    const float VISIBILITY_INTERVAL_S = 0.25f;

    class Track
    {
        public Texture2D Sheet;
        public RectInt[] Frames;
        public int[] FrameEndsMs;
        public int TotalMs;
        public RectInt Cell;
        public int CurrentFrame;
    }

    readonly Dictionary<string, Track> tracks = new Dictionary<string, Track>();
    readonly List<Track> active = new List<Track>();
    readonly HashSet<string> visibleIds = new HashSet<string>();
    Texture2D atlasTexture;
    float nextVisibilityCheck;

    // Fills the set with ids of emotes on screen right now.
    public Action<HashSet<string>> CollectVisible;

    public static EmoteAnimator Create(EmoteAtlas atlas)
    {
        if (SystemInfo.copyTextureSupport == CopyTextureSupport.None)
        {
            Plugin.LogError("[Emotes] GPU does not support CopyTexture. Emotes will not render.");
            return null;
        }
        var go = new GameObject("TRLEmotesAnimator");
        DontDestroyOnLoad(go);
        var animator = go.AddComponent<EmoteAnimator>();
        animator.atlasTexture = atlas.Texture;
        return animator;
    }

    public void Add(RemoteEmote emote, Texture2D sheet, EmoteSlot slot)
    {
        if (!emote.IsAnimated)
        {
            return;
        }
        var frames = new RectInt[emote.Frames];
        var ends = new int[emote.Frames];
        int total = 0;
        for (int i = 0; i < emote.Frames; i++)
        {
            frames[i] = emote.FrameRect(i, sheet.height);
            int delay = i < emote.DelaysMs.Length ? emote.DelaysMs[i] : FALLBACK_DELAY_MS;
            total += delay < MIN_DELAY_MS ? FALLBACK_DELAY_MS : delay;
            ends[i] = total;
        }
        tracks[emote.Id] = new Track { Sheet = sheet, Frames = frames, FrameEndsMs = ends, TotalMs = total, Cell = slot.Rect };
        // Newly loaded emotes are usually about to be shown; pick them up straight away.
        nextVisibilityCheck = 0f;
    }

    public void Remove(string id)
    {
        if (tracks.Remove(id))
        {
            nextVisibilityCheck = 0f;
        }
    }

    void Update()
    {
        if (tracks.Count == 0)
        {
            return;
        }
        if (Time.unscaledTime >= nextVisibilityCheck)
        {
            nextVisibilityCheck = Time.unscaledTime + VISIBILITY_INTERVAL_S;
            RefreshActive();
        }

        // Shared clock keeps every copy of an emote, and every client, roughly in phase.
        long nowMs = (long)(Time.unscaledTimeAsDouble * 1000.0);
        for (int i = 0; i < active.Count; i++)
        {
            Track track = active[i];
            int t = (int)(nowMs % track.TotalMs);
            int frame = 0;
            while (t >= track.FrameEndsMs[frame])
            {
                frame++;
            }
            if (frame == track.CurrentFrame)
            {
                continue;
            }
            track.CurrentFrame = frame;

            RectInt source = track.Frames[frame];
            Graphics.CopyTexture(
                track.Sheet, 0, 0, source.x, source.y, source.width, source.height,
                atlasTexture, 0, 0, track.Cell.x, track.Cell.y);
        }
    }

    void RefreshActive()
    {
        visibleIds.Clear();
        try
        {
            CollectVisible?.Invoke(visibleIds);
        }
        catch (Exception e)
        {
            Plugin.LogError($"[Emotes] Visibility check failed: {e.Message}");
        }
        active.Clear();
        foreach (string id in visibleIds)
        {
            if (tracks.TryGetValue(id, out Track track))
            {
                active.Add(track);
            }
        }
    }

    public void Destroy()
    {
        tracks.Clear();
        active.Clear();
        UnityEngine.Object.Destroy(gameObject);
    }
}
