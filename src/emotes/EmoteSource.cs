using System.Collections.Generic;
using UnityEngine;

namespace ToasterReskinLoader.emotes;

public class EmoteSource
{
    public string Name;
    // From the player's personal set rather than the server's.
    public bool Personal;
    public List<Texture2D> Frames = new List<Texture2D>();
    public List<int> FrameDelaysMs = new List<int>();

    public bool IsAnimated => Frames.Count > 1;
    public int Width => Frames[0].width;
    public int Height => Frames[0].height;
}
