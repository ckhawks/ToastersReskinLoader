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
    public bool enableTeamButtonPlayerCount = true;
    public bool enablePartyLineup = true;
    public bool enableSavedServerPasswords = true;
    public bool enableServerBrowserSortTweaks = true;
    public bool enableUnicodeFontFallback = true;
    // Fixes the vanilla bug where every player's country flag renders identical
    // (all flag meshes share one baked material asset). See FlagMaterialFix.
    public bool enableFlagMaterialFix = true;

    // Additions — opt-in QoL enhancements layered on top of vanilla
    public bool enableBetterFriendsList = true;
    public bool enableBeaconPing = true;
    // Default off. Persisted under a renamed JSON key (see QoLProfile) so that
    // users who already had the original default-on key saved start fresh at
    // off rather than inheriting their old "true".
    public bool enableServerPreviewCache = false;
    // Fast server browser scanning. Vanilla pings servers one at a time on a
    // single worker; with N servers and ~1s timeout per dead one, a refresh
    // stalls for tens of seconds. When on, we fan the pings out across
    // multiple workers using a semaphore so the wave finishes in roughly
    // (N / concurrency) × timeout — a 50-server refresh drops from ~50s to
    // ~3-4s. Off falls back to vanilla's sequential wave; cache seeding
    // still works either way. Default off, persisted under a renamed JSON
    // key (see QoLProfile) for the same migration reason as the cache toggle.
    public bool enableFastServerBrowserScanning = false;
    public int  serverBrowserPingConcurrency = 16;
    public int  serverBrowserPingConnectTimeoutMs = 1000;
    public int  serverBrowserPingResponseTimeoutMs = 1000;
    public bool enableVanillaUIRetheme = true;
    public bool enableEnhancedModMenu = true;
    public bool enableAutoConnectMatchmaking = false;

    // Per-server "trust this mod list" memory. Keyed by "ip:port"; value
    // is the sorted, comma-joined list of mod IDs the user previously
    // accepted via the "Don't show this popup again" toggle. When a
    // future MODS REQUIRED popup would fire for the same server AND
    // the required mod list still matches exactly, we skip the popup
    // and emulate the OK-click side effects so the reconnect flow
    // proceeds unattended. Any change to the mod set invalidates the
    // entry and the popup re-appears, forcing the user to re-consent.
    public Dictionary<string, string> trustedServerMods = new Dictionary<string, string>();

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

}