using System.IO;
using ToasterReskinLoader.core;

namespace ToasterReskinLoader.emotes;

public static class EmoteRuntime
{
    const string ATLAS_NAME = "ToasterEmotes";

    // POC stand-in for the server's "allow personal emotes" community setting.
    const string PERSONAL_DISABLED_MARKER = "personal-disabled";

    public static EmoteAtlas Atlas { get; private set; }
    // Read per message so settings changes apply to new messages straight away.
    public static bool Enabled => Settings.Current?.enableEmotes ?? true;

    public static EmoteFormatOptions FormatOptions => new EmoteFormatOptions
    {
        Render = Enabled,
        WhenHidden = ParseHiddenMode(Settings.Current?.emotesWhenHidden),
        Jumbo = Settings.Current?.emotesJumbo ?? true,
    };

    static HiddenEmoteMode ParseHiddenMode(string value)
    {
        switch (value)
        {
            case "remove":
                return HiddenEmoteMode.Remove;
            case "hideEmoteOnly":
                return HiddenEmoteMode.HideEmoteOnly;
            default:
                return HiddenEmoteMode.ShowName;
        }
    }
    public static bool PersonalAllowed { get; private set; } = true;
    static EmoteAnimator animator;
    static EmoteHover hover;
    static EmoteInput input;

    // Whether :name: may render or be inserted right now: server emotes always,
    // personal ones only where the server allows them.
    public static bool IsUsable(string name)
    {
        return Atlas != null
            && Atlas.Slots.TryGetValue(name, out EmoteSlot slot)
            && (!slot.Source.Personal || PersonalAllowed);
    }

    public static void Setup()
    {
        string pluginDir = Path.GetDirectoryName(typeof(EmoteRuntime).Assembly.Location);
        string emotesDir = Path.Combine(pluginDir, "emotes");
        PersonalAllowed = !File.Exists(Path.Combine(emotesDir, PERSONAL_DISABLED_MARKER));
        Plugin.Log($"[Emotes] Personal emotes {(PersonalAllowed ? "allowed" : "disabled")}.");
        var sources = LocalEmoteLoader.Load(emotesDir);
        if (sources.Count == 0)
        {
            Plugin.Log("[Emotes] No emotes loaded.");
            return;
        }

        Atlas = EmoteAtlas.Build(ATLAS_NAME, sources);
        SpriteAssetRegistry.Register(Atlas.SpriteAsset);
        animator = EmoteAnimator.Create(Atlas);
        hover = EmoteHover.Create(Atlas);
        input = EmoteInput.Create(Atlas);
    }

    public static void Teardown()
    {
        input?.Destroy();
        input = null;
        hover?.Destroy();
        hover = null;
        animator?.Destroy();
        animator = null;
        if (Atlas != null)
        {
            SpriteAssetRegistry.Unregister(Atlas.SpriteAsset);
            Atlas.Destroy();
            Atlas = null;
        }
    }
}
