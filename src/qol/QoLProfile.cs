// QoLProfile — persistence shape for QoL toggles + filters. Written to
// reskinprofiles/QoL.json by QoLStorage. Does NOT include per-server
// credentials (saved passwords, trusted mod sets); those live in
// reskinprofiles/ServerPrefs.json so reskin profiles can be shared
// without leaking them.

using Newtonsoft.Json;

namespace ToasterReskinLoader.qol;

public class QoLProfile
{
    // Arena visuals
    [JsonProperty("disableArenaVisuals")]
    public bool DisableArenaVisuals { get; set; } = false;
    [JsonProperty("disableArenaProps")]
    public bool DisableArenaProps { get; set; } = false;
    [JsonProperty("disableArenaLights")]
    public bool DisableArenaLights { get; set; } = false;
    [JsonProperty("disableArenaSkybox")]
    public bool DisableArenaSkybox { get; set; } = false;
    [JsonProperty("disableArenaParticles")]
    public bool DisableArenaParticles { get; set; } = false;
    [JsonProperty("arenaAudioVolume")]
    public float ArenaAudioVolume { get; set; } = 0.9f;

    // Base-game UX patches (default on)
    [JsonProperty("enableEscCloseMenus")]
    public bool EnableEscCloseMenus { get; set; } = true;
    [JsonProperty("enableChatAnyInGamePhase")]
    public bool EnableChatAnyInGamePhase { get; set; } = true;
    [JsonProperty("enableScoreboardAnyInGamePhase")]
    public bool EnableScoreboardAnyInGamePhase { get; set; } = true;
    [JsonProperty("enableChatDragSelect")]
    public bool EnableChatDragSelect { get; set; } = true;
    [JsonProperty("enableInlineServerBrowserFilters")]
    public bool EnableInlineServerBrowserFilters { get; set; } = true;
    [JsonProperty("enableHideInactiveChat")]
    public bool EnableHideInactiveChat { get; set; } = false;
    [JsonProperty("enableSpectatorMinimap")]
    public bool EnableSpectatorMinimap { get; set; } = true;
    [JsonProperty("enableBrowserFilterPersistence")]
    public bool EnableBrowserFilterPersistence { get; set; } = true;
    [JsonProperty("enableNumberedNames")]
    public bool EnableNumberedNames { get; set; } = true;
    [JsonProperty("enableTeamButtonPlayerCount")]
    public bool EnableTeamButtonPlayerCount { get; set; } = true;
    [JsonProperty("enablePartyLineup")]
    public bool EnablePartyLineup { get; set; } = true;
    [JsonProperty("enableSavedServerPasswords")]
    public bool EnableSavedServerPasswords { get; set; } = false;
    [JsonProperty("enableServerFavorites")]
    public bool EnableServerFavorites { get; set; } = false;
    [JsonProperty("enableServerBlocks")]
    public bool EnableServerBlocks { get; set; } = false;
    [JsonProperty("enableTrustedModLists")]
    public bool EnableTrustedModLists { get; set; } = true;
    [JsonProperty("enableUnicodeFontFallback")]
    public bool EnableUnicodeFontFallback { get; set; } = true;

    // Additions
    [JsonProperty("enableBetterFriendsList")]
    public bool EnableBetterFriendsList { get; set; } = true;
    [JsonProperty("enableBeaconPing")]
    public bool EnableBeaconPing { get; set; } = true;
    [JsonProperty("enableServerPreviewCache")]
    public bool EnableServerPreviewCache { get; set; } = true;
    [JsonProperty("enableVanillaUIRetheme")]
    public bool EnableVanillaUIRetheme { get; set; } = true;
    [JsonProperty("enableServerSlotQueue")]
    public bool EnableServerSlotQueue { get; set; } = true;
    [JsonProperty("enableMainMenuQuickJoin")]
    public bool EnableMainMenuQuickJoin { get; set; } = true;
    [JsonProperty("enableMainMenuServerBrowser")]
    public bool EnableMainMenuServerBrowser { get; set; } = false;
    [JsonProperty("enableUiTextShadow")]
    public bool EnableUiTextShadow { get; set; } = true;
    [JsonProperty("enableScoreboardMilliseconds")]
    public bool EnableScoreboardMilliseconds { get; set; } = true;
    [JsonProperty("enableScoreboardClockColor")]
    public bool EnableScoreboardClockColor { get; set; } = true;
    [JsonProperty("enableChatNoFade")]
    public bool EnableChatNoFade { get; set; } = true;
    [JsonProperty("enableChatTransparentContainer")]
    public bool EnableChatTransparentContainer { get; set; } = true;

    // Persisted server browser filter values
    [JsonProperty("browserSearch")]
    public string BrowserSearch { get; set; } = "";
    [JsonProperty("browserMaxPing")]
    public int BrowserMaxPing { get; set; } = 100;
    [JsonProperty("browserShowFull")]
    public bool BrowserShowFull { get; set; } = true;
    [JsonProperty("browserShowEmpty")]
    public bool BrowserShowEmpty { get; set; } = true;
    [JsonProperty("browserShowLocked")]
    public bool BrowserShowLocked { get; set; } = true;
    [JsonProperty("browserShowModded")]
    public bool BrowserShowModded { get; set; } = true;
    [JsonProperty("browserShowUnreachable")]
    public bool BrowserShowUnreachable { get; set; } = false;

    // Debug + dev console
    [JsonProperty("enableDebugLogging")]
    public bool EnableDebugLogging { get; set; } = false;
    [JsonProperty("enableDevConsole")]
    public bool EnableDevConsole { get; set; } = false;
    [JsonProperty("devConsoleX")]
    public float DevConsoleX { get; set; } = 40f;
    [JsonProperty("devConsoleY")]
    public float DevConsoleY { get; set; } = 40f;
    [JsonProperty("devConsoleW")]
    public float DevConsoleW { get; set; } = 900f;
    [JsonProperty("devConsoleH")]
    public float DevConsoleH { get; set; } = 460f;

    public QoLConfig ToConfig()
    {
        return new QoLConfig
        {
            disableArenaVisuals = DisableArenaVisuals,
            disableArenaProps = DisableArenaProps,
            disableArenaLights = DisableArenaLights,
            disableArenaSkybox = DisableArenaSkybox,
            disableArenaParticles = DisableArenaParticles,
            arenaAudioVolume = ArenaAudioVolume,
            enableEscCloseMenus = EnableEscCloseMenus,
            enableChatAnyInGamePhase = EnableChatAnyInGamePhase,
            enableScoreboardAnyInGamePhase = EnableScoreboardAnyInGamePhase,
            enableChatDragSelect = EnableChatDragSelect,
            enableInlineServerBrowserFilters = EnableInlineServerBrowserFilters,
            enableHideInactiveChat = EnableHideInactiveChat,
            enableSpectatorMinimap = EnableSpectatorMinimap,
            enableBrowserFilterPersistence = EnableBrowserFilterPersistence,
            enableNumberedNames = EnableNumberedNames,
            enableTeamButtonPlayerCount = EnableTeamButtonPlayerCount,
            enablePartyLineup = EnablePartyLineup,
            enableSavedServerPasswords = EnableSavedServerPasswords,
            enableServerFavorites  = EnableServerFavorites,
            enableServerBlocks     = EnableServerBlocks,
            enableTrustedModLists  = EnableTrustedModLists,
            enableUnicodeFontFallback = EnableUnicodeFontFallback,
            enableBetterFriendsList = EnableBetterFriendsList,
            enableBeaconPing = EnableBeaconPing,
            enableServerPreviewCache = EnableServerPreviewCache,
            enableVanillaUIRetheme = EnableVanillaUIRetheme,
            enableServerSlotQueue = EnableServerSlotQueue,
            enableMainMenuQuickJoin = EnableMainMenuQuickJoin,
            enableMainMenuServerBrowser = EnableMainMenuServerBrowser,
            enableUiTextShadow = EnableUiTextShadow,
            enableScoreboardMilliseconds = EnableScoreboardMilliseconds,
            enableScoreboardClockColor   = EnableScoreboardClockColor,
            enableChatNoFade               = EnableChatNoFade,
            enableChatTransparentContainer = EnableChatTransparentContainer,
            browserSearch = BrowserSearch,
            browserMaxPing = BrowserMaxPing,
            browserShowFull = BrowserShowFull,
            browserShowEmpty = BrowserShowEmpty,
            browserShowLocked = BrowserShowLocked,
            browserShowModded = BrowserShowModded,
            browserShowUnreachable = BrowserShowUnreachable,
            enableDebugLogging = EnableDebugLogging,
            enableDevConsole = EnableDevConsole,
            devConsoleX = DevConsoleX,
            devConsoleY = DevConsoleY,
            devConsoleW = DevConsoleW,
            devConsoleH = DevConsoleH,
        };
    }

    public void FromConfig(QoLConfig c)
    {
        if (c == null) return;
        DisableArenaVisuals = c.disableArenaVisuals;
        DisableArenaProps = c.disableArenaProps;
        DisableArenaLights = c.disableArenaLights;
        DisableArenaSkybox = c.disableArenaSkybox;
        DisableArenaParticles = c.disableArenaParticles;
        ArenaAudioVolume = c.arenaAudioVolume;
        EnableEscCloseMenus = c.enableEscCloseMenus;
        EnableChatAnyInGamePhase = c.enableChatAnyInGamePhase;
        EnableScoreboardAnyInGamePhase = c.enableScoreboardAnyInGamePhase;
        EnableChatDragSelect = c.enableChatDragSelect;
        EnableInlineServerBrowserFilters = c.enableInlineServerBrowserFilters;
        EnableHideInactiveChat = c.enableHideInactiveChat;
        EnableSpectatorMinimap = c.enableSpectatorMinimap;
        EnableBrowserFilterPersistence = c.enableBrowserFilterPersistence;
        EnableNumberedNames = c.enableNumberedNames;
        EnableTeamButtonPlayerCount = c.enableTeamButtonPlayerCount;
        EnablePartyLineup = c.enablePartyLineup;
        EnableSavedServerPasswords = c.enableSavedServerPasswords;
        EnableServerFavorites  = c.enableServerFavorites;
        EnableServerBlocks     = c.enableServerBlocks;
        EnableTrustedModLists  = c.enableTrustedModLists;
        EnableUnicodeFontFallback = c.enableUnicodeFontFallback;
        EnableBetterFriendsList = c.enableBetterFriendsList;
        EnableBeaconPing = c.enableBeaconPing;
        EnableServerPreviewCache = c.enableServerPreviewCache;
        EnableVanillaUIRetheme = c.enableVanillaUIRetheme;
        EnableServerSlotQueue = c.enableServerSlotQueue;
        EnableMainMenuQuickJoin = c.enableMainMenuQuickJoin;
        EnableMainMenuServerBrowser = c.enableMainMenuServerBrowser;
        EnableUiTextShadow = c.enableUiTextShadow;
        EnableScoreboardMilliseconds = c.enableScoreboardMilliseconds;
        EnableScoreboardClockColor   = c.enableScoreboardClockColor;
        EnableChatNoFade               = c.enableChatNoFade;
        EnableChatTransparentContainer = c.enableChatTransparentContainer;
        BrowserSearch = c.browserSearch;
        BrowserMaxPing = c.browserMaxPing;
        BrowserShowFull = c.browserShowFull;
        BrowserShowEmpty = c.browserShowEmpty;
        BrowserShowLocked = c.browserShowLocked;
        BrowserShowModded = c.browserShowModded;
        BrowserShowUnreachable = c.browserShowUnreachable;
        EnableDebugLogging = c.enableDebugLogging;
        EnableDevConsole = c.enableDevConsole;
        DevConsoleX = c.devConsoleX;
        DevConsoleY = c.devConsoleY;
        DevConsoleW = c.devConsoleW;
        DevConsoleH = c.devConsoleH;
    }
}

// Persistence shape for per-server credentials. Written to a separate
// file so reskin profiles stay clean of any personal data.
public class ServerPrefsProfile
{
    [JsonProperty("savedServerPasswords")]
    public System.Collections.Generic.Dictionary<string, string> SavedServerPasswords { get; set; }
        = new System.Collections.Generic.Dictionary<string, string>();

    [JsonProperty("trustedServerMods")]
    public System.Collections.Generic.Dictionary<string, string> TrustedServerMods { get; set; }
        = new System.Collections.Generic.Dictionary<string, string>();

    // ip:port -> last-seen friendly server name. Existence in this dict
    // = favorited; the cached name is just to keep the QoL management UI
    // readable when the favorited server isn't currently in the browser
    // listing.
    [JsonProperty("favoriteServers")]
    public System.Collections.Generic.Dictionary<string, string> FavoriteServers { get; set; }
        = new System.Collections.Generic.Dictionary<string, string>();

    // Same shape as FavoriteServers, but rows that match are hidden from
    // the server browser entirely.
    [JsonProperty("blockedServers")]
    public System.Collections.Generic.Dictionary<string, string> BlockedServers { get; set; }
        = new System.Collections.Generic.Dictionary<string, string>();
}
