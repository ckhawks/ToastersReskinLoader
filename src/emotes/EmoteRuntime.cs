using ToasterReskinLoader.core;

namespace ToasterReskinLoader.emotes;

// Chat emotes from puckstats: see EmoteFeed for where sets come from and how :name:
// resolves per sender.
public static class EmoteRuntime
{
    public static EmoteAtlas Atlas { get; private set; }

    // Read per message so settings changes apply to new messages straight away.
    public static bool Enabled => Settings.Current?.enableEmotes ?? true;

    public static EmoteFormatOptions FormatOptions => new EmoteFormatOptions
    {
        Render = Enabled,
        WhenHidden = ParseHiddenMode(Settings.Current?.emotesWhenHidden),
        Jumbo = Settings.Current?.emotesJumbo ?? true,
    };

    static EmoteAnimator animator;
    static EmoteFeed feed;
    static EmoteHover hover;
    static EmoteInput input;

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

    public static void Setup()
    {
        Atlas = EmoteAtlas.Create();
        SpriteAssetRegistry.Register(Atlas.SpriteAsset);
        animator = EmoteAnimator.Create(Atlas);
        if (animator == null)
        {
            // Without CopyTexture nothing can be put in the atlas.
            Teardown();
            return;
        }
        feed = EmoteFeed.Create(Atlas, animator);
        EmoteChat.Setup();
        hover = EmoteHover.Create(Atlas);
        input = EmoteInput.Create(Atlas);
    }

    public static void Teardown()
    {
        input?.Destroy();
        input = null;
        hover?.Destroy();
        hover = null;
        EmoteChat.Teardown();
        feed?.Destroy();
        feed = null;
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
