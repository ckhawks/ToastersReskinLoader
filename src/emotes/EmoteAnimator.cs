using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ToasterReskinLoader.emotes;

// Animates by overwriting each animated emote's atlas slot on the GPU when its frame changes.
// Every chat label showing that emote samples the same slot, so cost scales with the number
// of distinct animated emotes, not with how many are on screen, and no text is re-laid out.
public class EmoteAnimator : MonoBehaviour
{
    // Browsers treat GIF delays under 20 ms as 100 ms; match that so broken GIFs don't spin.
    const int MIN_DELAY_MS = 20;
    const int FALLBACK_DELAY_MS = 100;

    class Track
    {
        public EmoteSlot Slot;
        public int[] FrameEndsMs;
        public int TotalMs;
        public int CurrentFrame;
    }

    readonly List<Track> tracks = new List<Track>();
    Texture2D atlasTexture;

    public static EmoteAnimator Create(EmoteAtlas atlas)
    {
        if (SystemInfo.copyTextureSupport == CopyTextureSupport.None)
        {
            Plugin.LogError("[Emotes] GPU does not support CopyTexture. Animated emotes will show their first frame only.");
            return null;
        }

        if (atlas.Texture.format != TextureFormat.RGBA32)
        {
            Plugin.LogError($"[Emotes] Atlas format is {atlas.Texture.format}, frames are RGBA32. Animated emotes will show their first frame only.");
            return null;
        }

        var go = new GameObject("ToasterEmotesAnimator");
        DontDestroyOnLoad(go);
        var animator = go.AddComponent<EmoteAnimator>();
        animator.atlasTexture = atlas.Texture;

        foreach (EmoteSlot slot in atlas.Slots.Values)
        {
            if (!slot.Source.IsAnimated)
            {
                continue;
            }
            var ends = new int[slot.Source.Frames.Count];
            int total = 0;
            for (int i = 0; i < ends.Length; i++)
            {
                int delay = slot.Source.FrameDelaysMs[i];
                total += delay < MIN_DELAY_MS ? FALLBACK_DELAY_MS : delay;
                ends[i] = total;
            }
            animator.tracks.Add(new Track { Slot = slot, FrameEndsMs = ends, TotalMs = total });
        }

        animator.enabled = animator.tracks.Count > 0;
        Plugin.Log($"[Emotes] Animating {animator.tracks.Count} emotes");
        return animator;
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

            RectInt rect = track.Slot.Rect;
            Graphics.CopyTexture(
                track.Slot.Source.Frames[frame], 0, 0, 0, 0, rect.width, rect.height,
                atlasTexture, 0, 0, rect.x, rect.y);
        }
    }

    public void Destroy()
    {
        Object.Destroy(gameObject);
    }
}
