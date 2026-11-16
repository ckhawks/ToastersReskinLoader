using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ToasterReskinLoader.emotes;

// Animates by copying each animated emote's current frame from its sprite sheet into its
// atlas cell on the GPU, only when the frame changes. Every chat label showing that emote
// samples the same cell, so cost scales with distinct animated emotes, not with how many are
// on screen, and no text is re-laid out.
public class EmoteAnimator : MonoBehaviour
{
    // Browsers treat GIF delays under 20 ms as 100 ms; match that so broken GIFs don't spin.
    const int MIN_DELAY_MS = 20;
    const int FALLBACK_DELAY_MS = 100;

    class Track
    {
        public Texture2D Sheet;
        public RectInt[] Frames;
        public int[] FrameEndsMs;
        public int TotalMs;
        public RectInt Cell;
        public int CurrentFrame;
    }

    readonly List<Track> tracks = new List<Track>();
    Texture2D atlasTexture;

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
        animator.enabled = false;
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
        tracks.Add(new Track { Sheet = sheet, Frames = frames, FrameEndsMs = ends, TotalMs = total, Cell = slot.Rect });
        enabled = true;
    }

    void Update()
    {
        // Shared clock keeps every copy of an emote, and every client, roughly in phase.
        long nowMs = (long)(Time.unscaledTimeAsDouble * 1000.0);

        for (int i = 0; i < tracks.Count; i++)
        {
            Track track = tracks[i];
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

    public void Destroy()
    {
        tracks.Clear();
        Object.Destroy(gameObject);
    }
}
