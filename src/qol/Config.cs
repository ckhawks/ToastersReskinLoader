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
    public bool enableNumberedNames = false;

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