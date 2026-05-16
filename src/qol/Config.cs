// Trimmed QoLConfig — kept just the fields used by the surviving
// QoL features (goalie wide-view camera, arena visual disable, dev console,
// debug logging). The bigger PoncePlayerInput config surface (keybinds,
// position overrides, chat/tag, mute/social, sounds, etc.) was removed when
// the scope was scaled back.

using System;
using System.Collections.Generic;

namespace ToasterReskinLoader.qol;

[Serializable]
public class QoLConfig
{
    // Arena visuals
    public bool disableArenaVisuals = false;
    public bool disableArenaProps = false;
    public bool disableArenaLights = false;
    public bool disableArenaSkybox = false;
    public bool disableArenaParticles = false;
    public float arenaAudioVolume = 0.9f;

    // Base-game UX patches (default on)
    public bool enableEscCloseMenus = true;
    // Allow chat in any in-game phase (LockerRoom, Warmup, Playing) when
    // connected to a server. The chat key is also blocked when typing in
    // any text input, so this is safe at any phase.
    public bool enableChatAnyInGamePhase = true;
    // Allow the scoreboard hold-to-view in any in-game phase (TeamSelect,
    // PositionSelect, Spectate, etc.) — vanilla only shows it during Play.
    public bool enableScoreboardAnyInGamePhase = true;
    public bool enableChatDragSelect = true;
    public bool enableInlineServerBrowserFilters = true;
    public bool enableHideInactiveChat = false;
    public bool enableSpectatorMinimap = true;
    public bool enableBrowserFilterPersistence = true;
    public bool enableNumberedNames = true;
    public bool enableTeamButtonPlayerCount = true;
    public bool enablePartyLineup = true;
    public bool enableSavedServerPasswords = false;
    // Per-store toggles for the four server-browser-side memory stores.
    // Each is independently enable-able from the QoL UI's "Server
    // Browser" section.
    //   * enableServerFavorites  → ★ button + favorites-to-top sort
    //   * enableServerBlocks     → right-click block + hide blocked rows
    //   * enableTrustedModLists  → auto-confirm MODS REQUIRED popup
    public bool enableServerFavorites  = false;
    public bool enableServerBlocks     = false;
    public bool enableTrustedModLists  = true;
    // OS-font fallback registration for both TMP and UI Toolkit text
    // stacks. The b323 LiberationSans bundled with Puck only ships basic
    // Latin glyphs, so things like ▶/▼/★/☆ render as blank boxes
    // until we attach a system font (Segoe UI Symbol, etc.) as fallback.
    public bool enableUnicodeFontFallback = true;

    // Additions — opt-in QoL enhancements layered on top of vanilla
    public bool enableBetterFriendsList = true;
    public bool enableBeaconPing = true;
    public bool enableServerPreviewCache = true;
    public bool enableVanillaUIRetheme = true;
    // Auto-retry into a full server: on ServerFull rejection, poll the
    // target every 5s and rejoin the moment a slot opens. Reuses the
    // vanilla UIMatchmaking panel for status display.
    public bool enableServerSlotQueue = true;
    // Title-screen Quick Join button: refresh the server list and join
    // the best populated server matching the user's saved browser
    // filters. Lightly biased toward TR-tagged servers.
    public bool enableMainMenuQuickJoin = true;
    // Title-screen Server Browser button (off by default — vanilla
    // already exposes one inside the Play sub-menu, this is a shortcut
    // for users who'd rather skip it).
    public bool enableMainMenuServerBrowser = false;
    // Game-UI text shadow — single toggle that adds a CSS-like
    // text-shadow to the in-game score / period / clock labels AND to
    // every chat message label.
    public bool enableUiTextShadow = true;
    // In-game clock polish.
    //   * enableScoreboardMilliseconds → swap MM:SS for MM:SS.mmm on
    //     the clock, interpolated locally between server ticks.
    //   * enableScoreboardClockColor → white→red color lerp over the
    //     last 30s + alpha pulse over the last 5s of a period.
    public bool enableScoreboardMilliseconds = true;
    public bool enableScoreboardClockColor   = true;
    // Chat visual options, each independent so a user can mix-and-match.
    //   * enableChatNoFade → expired messages stay at full opacity
    //     instead of fading to the .blurred USS state.
    //   * enableChatTransparentContainer → chat container background
    //     is forced to fully transparent (overrides vanilla's dark
    //     panel USS).
    public bool enableChatNoFade               = true;
    public bool enableChatTransparentContainer = true;

    // Per-server "trust this mod list" memory. Keyed by "ip:port"; value
    // is the sorted, comma-joined list of mod IDs the user previously
    // accepted via the "Don't show this popup again" toggle. When a
    // future MODS REQUIRED popup would fire for the same server AND
    // the required mod list still matches exactly, we skip the popup
    // and emulate the OK-click side effects so the reconnect flow
    // proceeds unattended. Any change to the mod set invalidates the
    // entry and the popup re-appears, forcing the user to re-consent.
    public Dictionary<string, string> trustedServerMods = new Dictionary<string, string>();

    // Favorite servers, keyed by "ip:port". Value is the last-seen
    // friendly name (cached at favorite time so the QoL management UI
    // can show "ponseguck.net #1" instead of a bare ip:port even when
    // the server isn't currently in the browser list). Favorites always
    // sort to the top of the server browser regardless of column.
    public Dictionary<string, string> favoriteServers = new Dictionary<string, string>();

    // Blocked servers, same shape as favoriteServers. Rows that match
    // an entry get style.display = None in the server browser. Blocking
    // a server also removes it from favorites (mutually exclusive).
    public Dictionary<string, string> blockedServers = new Dictionary<string, string>();

    // ip:port -> last-known-good password. Populated when the user opts
    // in via the "Remember password" checkbox on the password popup.
    public Dictionary<string, string> savedServerPasswords = new Dictionary<string, string>();

    // Server browser filter state — defaults match the base game's
    // hard-coded values in UIServerBrowser.Awake so first-load behavior
    // is unchanged. Persisted across sessions.
    public string browserSearch = "";
    public int    browserMaxPing = 100;
    public bool   browserShowFull = true;
    public bool   browserShowEmpty = true;
    public bool   browserShowLocked = true;
    public bool   browserShowModded = true;
    public bool   browserShowUnreachable = false;

    // Debug + dev console
    public bool enableDebugLogging = false;
    public bool enableDevConsole = false;
    // Persisted dev console window position/size
    public float devConsoleX = 40f;
    public float devConsoleY = 40f;
    public float devConsoleW = 900f;
    public float devConsoleH = 460f;

}