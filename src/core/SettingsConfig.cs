// Trimmed SettingsConfig — kept just the fields used by the surviving
// QoL features (goalie wide-view camera, arena visual disable, dev console,
// debug logging). The bigger PoncePlayerInput config surface (keybinds,
// position overrides, chat/tag, mute/social, sounds, etc.) was removed when
// the scope was scaled back.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

using ToasterReskinLoader.input;

using ToasterReskinLoader.hud;

namespace ToasterReskinLoader.core;

[Serializable]
public class SettingsConfig
{
    // Arena visuals
    public bool disableArenaVisuals = false;
    public bool disableArenaProps = false;
    public bool disableArenaLights = false;
    public bool disableArenaSkybox = false;
    public bool disableArenaParticles = false;
    public float arenaAudioVolume = 0.9f;

    // Base-game UX patches (default on)
    public bool enableChatDragSelect = true;
    public bool enableHideInactiveChat = false;
    public bool enableSpectatorMinimap = true;
    // Minimap rotation mode. Mutually exclusive — only one applies at a time.
    // Values: "off" (vanilla), "rotate90" (fixed 90° turn), "followPlayer"
    // (continuously yaw the minimap so the local player's facing is "up").
    public string minimapRotationMode = "off";
    // Re-color floating world-space player username labels by team. Uses
    // TeamColorSwapper.GetOverrideColor first, falling back to the
    // profile's default blue/red.
    public bool enablePlayerUsernameTeamColors = false;
    public bool enableBrowserFilterPersistence = true;
    public bool enableNumberedNames = false;
    public bool enablePartyLineup = true;
    public bool enableSavedServerPasswords = true;
    public bool enableServerBrowserSortTweaks = true;
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
    // Renamed from enableBeaconPing when Edgegap Beacons became Probes. Existing
    // users' old key is ignored on load, so this starts fresh at its default (on).
    public bool enableProbePing = true;
    // Default off. Persisted under a renamed JSON key (see SettingsProfile) so that
    // users who already had the original default-on key saved start fresh at
    // off rather than inheriting their old "true".
    [JsonProperty("enableServerPreviewCacheV2")]
    public bool enableServerPreviewCache = false;
    // Fast server browser scanning. Vanilla pings servers one at a time on a
    // single worker; with N servers and ~1s timeout per dead one, a refresh
    // stalls for tens of seconds. When on, we fan the pings out across
    // multiple workers using a semaphore so the wave finishes in roughly
    // (N / concurrency) × timeout — a 50-server refresh drops from ~50s to
    // ~3-4s. Off falls back to vanilla's sequential wave; cache seeding
    // still works either way. Default off, persisted under a renamed JSON
    // key (see SettingsProfile) for the same migration reason as the cache toggle.
    [JsonProperty("enableFastServerBrowserScanningV2")]
    public bool enableFastServerBrowserScanning = false;
    // In-memory only — never persisted by the old SettingsProfile, so keep it that way.
    [JsonIgnore] public int  serverBrowserPingConcurrency = 16;
    [JsonIgnore] public int  serverBrowserPingConnectTimeoutMs = 1000;
    [JsonIgnore] public int  serverBrowserPingResponseTimeoutMs = 1000;
    public bool enableVanillaUIRetheme = true;
    // Auto-retry into a full server: on ServerFull rejection, poll the
    // target every 5s and rejoin the moment a slot opens. Reuses the
    // vanilla UIMatchmaking panel for status display.
    public bool enableServerSlotQueue = true;
    // Title-screen Quick Join button: refresh the server list and join
    // the best populated server matching the user's saved browser
    // filters. Lightly biased toward TR-tagged servers. Default off for
    // now — auto-connecting straight off the title screen is a big action
    // to take unprompted.
    [JsonProperty("enableMainMenuQuickJoinV2")]
    public bool enableMainMenuQuickJoin = false;
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
    //     Default off — the rolling sub-second digits are distracting
    //     for most players.
    //   * enableScoreboardClockColor → color ramp over the final 30s:
    //     amber→red lerp 30s→10s, solid red the last 10s, red flashing in
    //     the final 5s. Only animates during the Warmup / Play phases (see
    //     ScoreboardPolish).
    //   * scoreboardMillisecondsDigits → how many sub-second place values
    //     to show (1 = tenths, 2 = hundredths, 3 = milliseconds). Clamped
    //     1..3. Default 3 (full milliseconds).
    //   * enableScoreboardMillisecondsLast5Only → only append the sub-second
    //     digits during the final 5 seconds; above 5s the clock reads as the
    //     plain vanilla MM:SS. Default off (show them the whole time).
    [JsonProperty("enableScoreboardMillisecondsV2")]
    public bool enableScoreboardMilliseconds = false;
    public int  scoreboardMillisecondsDigits = 3;
    public bool enableScoreboardMillisecondsLast5Only = false;
    public bool enableScoreboardClockColor   = true;
    // Suppress the full-screen team-colored flash the game shows when a goal
    // is scored (UIOverlayManagerController.Event_Everyone_OnGoalScored →
    // FlashScreen). Only the screen flash — the goal slow-motion is a separate
    // server-side effect and is left untouched. Default off (vanilla flash on).
    public bool disableGoalScoredFlash = false;
    // Chat visual option: expired messages stay at full opacity instead
    // of fading to the .blurred USS state. Default off — the vanilla fade
    // keeps stale chatter from piling up on screen.
    [JsonProperty("enableChatNoFadeV2")]
    public bool enableChatNoFade = false;
    public bool enableEnhancedModMenu = true;
    public bool enableAutoConnectMatchmaking = false;
    // Fly the position-select bench camera around like a spectator while you've
    // joined a team but haven't claimed a position yet. Right-click toggles
    // free-look. Purely client-local (see PositionSelectFreeLook). Default on;
    // it only does anything once you right-click during position select.
    public bool enablePositionSelectFreeLook = true;

    // Per-server "trust this mod list" memory. Keyed by "ip:port"; value
    // is the sorted, comma-joined list of mod IDs the user previously
    // accepted via the "Don't show this popup again" toggle. When a
    // future MODS REQUIRED popup would fire for the same server AND
    // the required mod list still matches exactly, we skip the popup
    // and emulate the OK-click side effects so the reconnect flow
    // proceeds unattended. Any change to the mod set invalidates the
    // entry and the popup re-appears, forcing the user to re-consent.
    [JsonIgnore] public Dictionary<string, string> trustedServerMods = new Dictionary<string, string>();

    // Favorite servers, keyed by "ip:port". Value is the last-seen
    // friendly name (cached at favorite time so the QoL management UI
    // can show "ponseguck.net #1" instead of a bare ip:port even when
    // the server isn't currently in the browser list). Favorites always
    // sort to the top of the server browser regardless of column.
    [JsonIgnore] public Dictionary<string, string> favoriteServers = new Dictionary<string, string>();

    // Blocked servers, same shape as favoriteServers. Rows that match
    // an entry get style.display = None in the server browser. Blocking
    // a server also removes it from favorites (mutually exclusive).
    [JsonIgnore] public Dictionary<string, string> blockedServers = new Dictionary<string, string>();

    // ip:port -> last-known-good password. Populated when the user opts
    // in via the "Remember password" checkbox on the password popup.
    [JsonIgnore] public Dictionary<string, string> savedServerPasswords = new Dictionary<string, string>();

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
    // Frame timing / stutter profiler (overlay + Harmony instrumentation).
    // Off by default — only useful for diagnosing perf issues.
    public bool enableFrameProfiler = false;
    // Heavyweight option: when the profiler is enabled, also Harmony-patch
    // every Update/LateUpdate/FixedUpdate/OnGUI method in every other
    // loaded mod assembly. Gives per-mod cost rows in the Top Calls table
    // but adds 100s of patches at load time.
    public bool enableFrameProfilerModInstrumentation = false;
    // Persisted dev console window position/size
    public float devConsoleX = 40f;
    public float devConsoleY = 40f;
    public float devConsoleW = 900f;
    public float devConsoleH = 460f;

    // ── Display settings (moved out of the reskin profile — personal/perf, not shared) ──

    // Gloss remover.
    public bool  glossRemoverEnabled = true;
    public float glossSmoothness = 0.0f;
    public bool  glossAffectSticks = true;
    public bool  glossAffectPlayers = true;
    public bool  glossAffectPucks = true;
    // Global environment-reflection scale (RenderSettings.reflectionIntensity). Scales
    // the reflection-probe contribution across the whole scene, dialing back the static
    // rink cubemap that mirrors onto glossy surfaces while leaving direct light
    // highlights intact. On URP Lit in a built game this is the only reliable runtime
    // lever — the per-material _ENVIRONMENTREFLECTIONS_OFF keyword is stripped. Scene-
    // wide (affects ice/boards too), so it's opt-in. 1 = untouched, 0 = no reflections.
    public bool  reflectionReduceEnabled = false;
    public float reflectionIntensity = 0.0f;

    // Color grade — counteracts the washed-out/gray look the game took on when
    // its render pipeline was retuned (HDR buffer disabled, ambient dimmed). A
    // runtime Volume layered on top of the game's grading (see ColorGrade).
    // Saturation/Contrast/Warmth are -100..100; Exposure is EV (-2..2).
    public bool  colorGradeEnabled = false;
    public float colorGradeSaturation = 0f;
    public float colorGradeContrast = 0f;
    public float colorGradeExposure = 0f;
    public float colorGradeWarmth = 0f;
    // Root-cause fix: flip the pipeline HDR flag back on. Experimental — costs a
    // render-buffer reallocation, so it's a separate opt-in from the sliders.
    public bool  colorGradeReenableHDR = false;

    // Minimap (HUD).
    public Color blueMinimapNumberColor = Color.white;
    public Color redMinimapNumberColor = Color.white;
    public Color minimapPuckColor = new Color(0f, 0f, 0f, 1f);
    public float minimapPlayerScale = 1f;
    public float minimapPuckScale = 1f;
    public float minimapStickScale = 1f;
    public int   minimapRefreshRate = 120;
    // Puck elevation indicator overrides (vanilla: puck shrinks + fades as it rises).
    public bool  minimapPuckElevationReverse = false;      // true = grow instead of shrink
    public bool  minimapPuckElevationTransparency = true;  // false = no fade with height
    public bool  localPlayerMinimapIconEnabled = false;
    public Color blueLocalPlayerMinimapIconColor = new Color(0f, 1f, 0f, 1f);
    public Color redLocalPlayerMinimapIconColor = new Color(0f, 1f, 0f, 1f);


    // Chat (HUD).
    public float chatHeight = 300f;
    public bool  chatBackground = false;
    public float quickChatX = 0f;
    public float quickChatY = 50f;
    public bool  chatRenderAllEmojis = true;

    // One-time marker: display settings have been seeded from a pre-existing reskin profile.
    public bool displaySettingsMigrated = false;
}