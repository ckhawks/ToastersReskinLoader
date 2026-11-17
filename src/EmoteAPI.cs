using System;
using System.Collections.Generic;
using System.Linq;
using ToasterReskinLoader.emotes;

namespace ToasterReskinLoader;

/// <summary>
/// Public API for other mods to use the local player's puckstats emotes (e.g. binding them
/// to quick chat slots). Sending an emote is just sending its ":name:" as chat text; TRL on
/// every receiving client turns it into the image.
///
/// Only plain types cross the boundary, so it works over reflection without referencing
/// ToasterReskinLoader.dll:
///   Type api = null;
///   foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
///   {
///       api = asm.GetType("ToasterReskinLoader.EmoteAPI");
///       if (api != null) break;
///   }
///   if (api != null)
///   {
///       int version   = (int)api.GetField("VERSION").GetValue(null);
///       string[] ids  = (string[])api.GetMethod("GetMyEmoteIds").Invoke(null, null);
///       string name   = (string)api.GetMethod("GetMyEmoteName").Invoke(null, new object[] { ids[0] });
///       string image  = (string)api.GetMethod("GetEmoteImageTag").Invoke(null, new object[] { ids[0] });
///       api.GetEvent("MyEmotesChanged").AddEventHandler(null, (Action)OnMyEmotesChanged);
///   }
///
/// Ids are stable across renames on puckstats, so store ids and look the name up when sending.
/// </summary>
public static class EmoteAPI
{
    /// <summary>Bumped when members are added or change meaning.</summary>
    public const int VERSION = 1;

    /// <summary>Raised on the main thread when the local player's set may have changed.</summary>
    public static event Action MyEmotesChanged;

    // Last set fetched for the local player, ordered by name. Kept across reconnects so the
    // set doesn't blink empty while it is refetched.
    static List<(string id, string name)> mySet = new List<(string, string)>();

    /// <summary>True when TRL's emote feature is running (it can be disabled or fail to start).</summary>
    public static bool IsAvailable => EmoteFeed.Instance != null && EmoteRuntime.Atlas != null;

    /// <summary>Whether the user has emotes turned on in TRL's chat settings.</summary>
    public static bool IsRenderingEnabled => EmoteRuntime.Enabled;

    /// <summary>Ids of the emotes in the local player's set, ordered by name. Empty until fetched.</summary>
    public static string[] GetMyEmoteIds()
    {
        return mySet.Select(e => e.id).ToArray();
    }

    /// <summary>The name the local player's set uses for this emote, or null if it isn't in the set.</summary>
    public static string GetMyEmoteName(string id)
    {
        foreach (var emote in mySet)
        {
            if (emote.id == id)
            {
                return emote.name;
            }
        }
        return null;
    }

    /// <summary>
    /// Rich text that draws the emote in any UI Toolkit Label with rich text on, or null while it
    /// isn't loaded yet (MyEmotesChanged fires once it is).
    /// </summary>
    public static string GetEmoteImageTag(string id)
    {
        EmoteSlot slot = EmoteRuntime.Atlas?.Get(id);
        return slot == null ? null : EmoteRuntime.Atlas.Tag(slot);
    }

    internal static void OnSetsChanged(ICollection<string> steamIds)
    {
        EmoteFeed feed = EmoteFeed.Instance;
        if (feed == null)
        {
            return;
        }
        if (steamIds != null && (feed.LocalSteamId == null || !steamIds.Contains(feed.LocalSteamId)))
        {
            return;
        }
        if (feed.TryGetLocalSet(out ICollection<RemoteEmote> set))
        {
            mySet = set
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .Select(e => (e.Id, e.Name))
                .ToList();
        }
        try
        {
            MyEmotesChanged?.Invoke();
        }
        catch (Exception e)
        {
            Plugin.LogError($"[Emotes] A MyEmotesChanged handler failed: {e}");
        }
    }

    internal static void Reset()
    {
        mySet = new List<(string, string)>();
        try
        {
            MyEmotesChanged?.Invoke();
        }
        catch (Exception e)
        {
            Plugin.LogError($"[Emotes] A MyEmotesChanged handler failed: {e}");
        }
    }
}
