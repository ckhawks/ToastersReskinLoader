using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ToasterReskinLoader.emotes;

// One emote as the puckstats game feed describes it (/api/emotes/sets, /api/emotes/server).
public class RemoteEmote
{
    public string Id;
    // The name the set uses for it.
    public string Name;
    // Path under https://puckstats.io; immutable, named by content hash.
    public string Sheet;
    public int FrameWidth;
    public int FrameHeight;
    public int Columns;
    public int Frames;
    public int[] DelaysMs;

    public bool IsAnimated => Frames > 1;

    public static RemoteEmote FromJson(JToken token)
    {
        var emote = new RemoteEmote
        {
            Id = (string)token["id"],
            Name = (string)token["name"],
            Sheet = (string)token["sheet"],
            FrameWidth = (int?)token["frameWidth"] ?? 0,
            FrameHeight = (int?)token["frameHeight"] ?? 0,
            Columns = (int?)token["columns"] ?? 1,
            Frames = (int?)token["frames"] ?? 1,
            DelaysMs = token["delaysMs"]?.ToObject<int[]>() ?? new int[0],
        };
        bool valid = !string.IsNullOrEmpty(emote.Id)
            && !string.IsNullOrEmpty(emote.Name)
            && emote.Sheet != null && emote.Sheet.StartsWith("/api/resource/emotes/")
            && emote.FrameWidth is > 0 and <= 64
            && emote.FrameHeight is > 0 and <= 64
            && emote.Columns > 0
            && emote.Frames > 0;
        return valid ? emote : null;
    }

    // Frame i of the sheet in Unity texture space (origin bottom-left; the PNG is top-down).
    public RectInt FrameRect(int frame, int sheetHeight)
    {
        int col = frame % Columns;
        int row = frame / Columns;
        return new RectInt(col * FrameWidth, sheetHeight - (row + 1) * FrameHeight, FrameWidth, FrameHeight);
    }
}
