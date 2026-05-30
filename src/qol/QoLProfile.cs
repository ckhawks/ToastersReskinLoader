// QoLProfile — persistence shape for QoL toggles + filters. Written by
// QoLStorage to <gameRoot>/config/ToastersReskinLoaderQoL.json. Does NOT
// include per-server credentials (saved passwords, trusted mod sets);
// those live in ToastersReskinLoaderServerPrefs.json (see ServerPrefsProfile
// below) so reskin profiles can be shared without leaking them.

using Newtonsoft.Json;
using UnityEngine;

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
    [JsonProperty("minimapRotationMode")]
    public string MinimapRotationMode { get; set; } = "off";
    [JsonProperty("enablePlayerUsernameTeamColors")]
    public bool EnablePlayerUsernameTeamColors { get; set; } = false;
    [JsonProperty("enableBrowserFilterPersistence")]
    public bool EnableBrowserFilterPersistence { get; set; } = true;
    [JsonProperty("enableNumberedNames")]
    public bool EnableNumberedNames { get; set; } = false;
    [JsonProperty("enableTeamButtonPlayerCount")]
    public bool EnableTeamButtonPlayerCount { get; set; } = true;
    [JsonProperty("enablePartyLineup")]
    public bool EnablePartyLineup { get; set; } = true;
    [JsonProperty("enableSavedServerPasswords")]
    public bool EnableSavedServerPasswords { get; set; } = true;
    [JsonProperty("enableServerBrowserSortTweaks")]
    public bool EnableServerBrowserSortTweaks { get; set; } = true;
    [JsonProperty("enableUnicodeFontFallback")]
    public bool EnableUnicodeFontFallback { get; set; } = true;
    [JsonProperty("enableFlagMaterialFix")]
    public bool EnableFlagMaterialFix { get; set; } = true;

    // Additions
    [JsonProperty("enableBetterFriendsList")]
    public bool EnableBetterFriendsList { get; set; } = true;
    [JsonProperty("enableBeaconPing")]
    public bool EnableBeaconPing { get; set; } = true;
    // NOTE: JSON keys intentionally renamed (…V2) so existing users — who had
    // the original default-on keys ("enableServerPreviewCache" /
    // "enableFastServerBrowserScanning") saved as true — get the new
    // default-off behavior instead of inheriting their old value. Newtonsoft
    // ignores the now-orphaned old keys on load and drops them on next save.
    [JsonProperty("enableServerPreviewCacheV2")]
    public bool EnableServerPreviewCache { get; set; } = false;
    [JsonProperty("enableFastServerBrowserScanningV2")]
    public bool EnableFastServerBrowserScanning { get; set; } = false;
    [JsonProperty("enableVanillaUIRetheme")]
    public bool EnableVanillaUIRetheme { get; set; } = true;
    [JsonProperty("enableEnhancedModMenu")]
    public bool EnableEnhancedModMenu { get; set; } = true;
    [JsonProperty("enableAutoConnectMatchmaking")]
    public bool EnableAutoConnectMatchmaking { get; set; } = false;

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
    [JsonProperty("enableFrameProfiler")]
    public bool EnableFrameProfiler { get; set; } = false;
    [JsonProperty("enableFrameProfilerModInstrumentation")]
    public bool EnableFrameProfilerModInstrumentation { get; set; } = false;
    [JsonProperty("devConsoleX")]
    public float DevConsoleX { get; set; } = 40f;
    [JsonProperty("devConsoleY")]
    public float DevConsoleY { get; set; } = 40f;
    [JsonProperty("devConsoleW")]
    public float DevConsoleW { get; set; } = 900f;
    [JsonProperty("devConsoleH")]
    public float DevConsoleH { get; set; } = 460f;

    // Display settings (moved out of the reskin profile)
    [JsonProperty("crispyShadowsEnabled")]
    public bool CrispyShadowsEnabled { get; set; } = true;
    [JsonProperty("shadowResolution")]
    public int ShadowResolution { get; set; } = 8192;
    [JsonProperty("shadowDistance")]
    public float ShadowDistance { get; set; } = 50f;
    [JsonProperty("shadowCascadeCount")]
    public int ShadowCascadeCount { get; set; } = 4;
    [JsonProperty("shadowSoftShadows")]
    public bool ShadowSoftShadows { get; set; } = true;
    [JsonProperty("glossRemoverEnabled")]
    public bool GlossRemoverEnabled { get; set; } = false;
    [JsonProperty("glossSmoothness")]
    public float GlossSmoothness { get; set; } = 0.5f;
    [JsonProperty("glossAffectSticks")]
    public bool GlossAffectSticks { get; set; } = true;
    [JsonProperty("glossAffectPlayers")]
    public bool GlossAffectPlayers { get; set; } = true;
    [JsonProperty("glossAffectPucks")]
    public bool GlossAffectPucks { get; set; } = true;
    // Minimap
    [JsonProperty("blueMinimapNumberColor")]
    public SerializableColor BlueMinimapNumberColor { get; set; } = new SerializableColor(Color.white);
    [JsonProperty("redMinimapNumberColor")]
    public SerializableColor RedMinimapNumberColor { get; set; } = new SerializableColor(Color.white);
    [JsonProperty("minimapPuckColor")]
    public SerializableColor MinimapPuckColor { get; set; } = new SerializableColor(new Color(0f, 0f, 0f, 1f));
    [JsonProperty("minimapPlayerScale")]
    public float MinimapPlayerScale { get; set; } = 1f;
    [JsonProperty("minimapPuckScale")]
    public float MinimapPuckScale { get; set; } = 1f;
    [JsonProperty("minimapRefreshRate")]
    public int MinimapRefreshRate { get; set; } = 60;
    [JsonProperty("localPlayerMinimapIconEnabled")]
    public bool LocalPlayerMinimapIconEnabled { get; set; } = false;
    [JsonProperty("blueLocalPlayerMinimapIconColor")]
    public SerializableColor BlueLocalPlayerMinimapIconColor { get; set; } = new SerializableColor(new Color(0f, 1f, 0f, 1f));
    [JsonProperty("redLocalPlayerMinimapIconColor")]
    public SerializableColor RedLocalPlayerMinimapIconColor { get; set; } = new SerializableColor(new Color(0f, 1f, 0f, 1f));
    [JsonProperty("teamIndicatorEnabled")]
    public bool TeamIndicatorEnabled { get; set; } = false;
    // Chat
    [JsonProperty("chatHeight")]
    public float ChatHeight { get; set; } = 300f;
    [JsonProperty("chatBackground")]
    public bool ChatBackground { get; set; } = false;
    [JsonProperty("quickChatX")]
    public float QuickChatX { get; set; } = 0f;
    [JsonProperty("quickChatY")]
    public float QuickChatY { get; set; } = 50f;
    [JsonProperty("chatRenderAllEmojis")]
    public bool ChatRenderAllEmojis { get; set; } = true;
    [JsonProperty("displaySettingsMigrated")]
    public bool DisplaySettingsMigrated { get; set; } = false;

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
            minimapRotationMode = MinimapRotationMode,
            enablePlayerUsernameTeamColors = EnablePlayerUsernameTeamColors,
            enableBrowserFilterPersistence = EnableBrowserFilterPersistence,
            enableNumberedNames = EnableNumberedNames,
            enableTeamButtonPlayerCount = EnableTeamButtonPlayerCount,
            enablePartyLineup = EnablePartyLineup,
            enableSavedServerPasswords = EnableSavedServerPasswords,
            enableServerBrowserSortTweaks = EnableServerBrowserSortTweaks,
            enableUnicodeFontFallback = EnableUnicodeFontFallback,
            enableFlagMaterialFix = EnableFlagMaterialFix,
            enableBetterFriendsList = EnableBetterFriendsList,
            enableBeaconPing = EnableBeaconPing,
            enableServerPreviewCache = EnableServerPreviewCache,
            enableFastServerBrowserScanning = EnableFastServerBrowserScanning,
            enableVanillaUIRetheme = EnableVanillaUIRetheme,
            enableEnhancedModMenu = EnableEnhancedModMenu,
            enableAutoConnectMatchmaking = EnableAutoConnectMatchmaking,
            browserSearch = BrowserSearch,
            browserMaxPing = BrowserMaxPing,
            browserShowFull = BrowserShowFull,
            browserShowEmpty = BrowserShowEmpty,
            browserShowLocked = BrowserShowLocked,
            browserShowModded = BrowserShowModded,
            browserShowUnreachable = BrowserShowUnreachable,
            enableDebugLogging = EnableDebugLogging,
            enableDevConsole = EnableDevConsole,
            enableFrameProfiler = EnableFrameProfiler,
            enableFrameProfilerModInstrumentation = EnableFrameProfilerModInstrumentation,
            devConsoleX = DevConsoleX,
            devConsoleY = DevConsoleY,
            devConsoleW = DevConsoleW,
            devConsoleH = DevConsoleH,
            crispyShadowsEnabled = CrispyShadowsEnabled,
            shadowResolution = ShadowResolution,
            shadowDistance = ShadowDistance,
            shadowCascadeCount = ShadowCascadeCount,
            shadowSoftShadows = ShadowSoftShadows,
            glossRemoverEnabled = GlossRemoverEnabled,
            glossSmoothness = GlossSmoothness,
            glossAffectSticks = GlossAffectSticks,
            glossAffectPlayers = GlossAffectPlayers,
            glossAffectPucks = GlossAffectPucks,
            blueMinimapNumberColor = BlueMinimapNumberColor != null ? (Color)BlueMinimapNumberColor : Color.white,
            redMinimapNumberColor = RedMinimapNumberColor != null ? (Color)RedMinimapNumberColor : Color.white,
            minimapPuckColor = MinimapPuckColor != null ? (Color)MinimapPuckColor : new Color(0f, 0f, 0f, 1f),
            minimapPlayerScale = MinimapPlayerScale,
            minimapPuckScale = MinimapPuckScale,
            minimapRefreshRate = MinimapRefreshRate,
            localPlayerMinimapIconEnabled = LocalPlayerMinimapIconEnabled,
            blueLocalPlayerMinimapIconColor = BlueLocalPlayerMinimapIconColor != null ? (Color)BlueLocalPlayerMinimapIconColor : new Color(0f, 1f, 0f, 1f),
            redLocalPlayerMinimapIconColor = RedLocalPlayerMinimapIconColor != null ? (Color)RedLocalPlayerMinimapIconColor : new Color(0f, 1f, 0f, 1f),
            teamIndicatorEnabled = TeamIndicatorEnabled,
            chatHeight = ChatHeight,
            chatBackground = ChatBackground,
            quickChatX = QuickChatX,
            quickChatY = QuickChatY,
            chatRenderAllEmojis = ChatRenderAllEmojis,
            displaySettingsMigrated = DisplaySettingsMigrated,
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
        MinimapRotationMode = c.minimapRotationMode ?? "off";
        EnablePlayerUsernameTeamColors = c.enablePlayerUsernameTeamColors;
        EnableBrowserFilterPersistence = c.enableBrowserFilterPersistence;
        EnableNumberedNames = c.enableNumberedNames;
        EnableTeamButtonPlayerCount = c.enableTeamButtonPlayerCount;
        EnablePartyLineup = c.enablePartyLineup;
        EnableSavedServerPasswords = c.enableSavedServerPasswords;
        EnableServerBrowserSortTweaks = c.enableServerBrowserSortTweaks;
        EnableUnicodeFontFallback = c.enableUnicodeFontFallback;
        EnableFlagMaterialFix = c.enableFlagMaterialFix;
        EnableBetterFriendsList = c.enableBetterFriendsList;
        EnableBeaconPing = c.enableBeaconPing;
        EnableServerPreviewCache = c.enableServerPreviewCache;
        EnableFastServerBrowserScanning = c.enableFastServerBrowserScanning;
        EnableVanillaUIRetheme = c.enableVanillaUIRetheme;
        EnableEnhancedModMenu = c.enableEnhancedModMenu;
        EnableAutoConnectMatchmaking = c.enableAutoConnectMatchmaking;
        BrowserSearch = c.browserSearch;
        BrowserMaxPing = c.browserMaxPing;
        BrowserShowFull = c.browserShowFull;
        BrowserShowEmpty = c.browserShowEmpty;
        BrowserShowLocked = c.browserShowLocked;
        BrowserShowModded = c.browserShowModded;
        BrowserShowUnreachable = c.browserShowUnreachable;
        EnableDebugLogging = c.enableDebugLogging;
        EnableDevConsole = c.enableDevConsole;
        EnableFrameProfiler = c.enableFrameProfiler;
        EnableFrameProfilerModInstrumentation = c.enableFrameProfilerModInstrumentation;
        DevConsoleX = c.devConsoleX;
        DevConsoleY = c.devConsoleY;
        DevConsoleW = c.devConsoleW;
        DevConsoleH = c.devConsoleH;
        CrispyShadowsEnabled = c.crispyShadowsEnabled;
        ShadowResolution = c.shadowResolution;
        ShadowDistance = c.shadowDistance;
        ShadowCascadeCount = c.shadowCascadeCount;
        ShadowSoftShadows = c.shadowSoftShadows;
        GlossRemoverEnabled = c.glossRemoverEnabled;
        GlossSmoothness = c.glossSmoothness;
        GlossAffectSticks = c.glossAffectSticks;
        GlossAffectPlayers = c.glossAffectPlayers;
        GlossAffectPucks = c.glossAffectPucks;
        BlueMinimapNumberColor = new SerializableColor(c.blueMinimapNumberColor);
        RedMinimapNumberColor = new SerializableColor(c.redMinimapNumberColor);
        MinimapPuckColor = new SerializableColor(c.minimapPuckColor);
        MinimapPlayerScale = c.minimapPlayerScale;
        MinimapPuckScale = c.minimapPuckScale;
        MinimapRefreshRate = c.minimapRefreshRate;
        LocalPlayerMinimapIconEnabled = c.localPlayerMinimapIconEnabled;
        BlueLocalPlayerMinimapIconColor = new SerializableColor(c.blueLocalPlayerMinimapIconColor);
        RedLocalPlayerMinimapIconColor = new SerializableColor(c.redLocalPlayerMinimapIconColor);
        TeamIndicatorEnabled = c.teamIndicatorEnabled;
        ChatHeight = c.chatHeight;
        ChatBackground = c.chatBackground;
        QuickChatX = c.quickChatX;
        QuickChatY = c.quickChatY;
        ChatRenderAllEmojis = c.chatRenderAllEmojis;
        DisplaySettingsMigrated = c.displaySettingsMigrated;
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
}
