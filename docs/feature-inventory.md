# Feature Inventory

A complete map of every feature in the mod: what it is, how it's configured, how it's
wired into the lifecycle, what it depends on, and where it should live after the planned
re-foldering. This is the working reference for the `qol/` split, the config collapse, and
the feature-registry work (see the companion refactor docs).

> Snapshot taken at v2.2.1. ~70 user-facing features behind ~90 config fields, plus ~15
> reskin-profile-driven visual swappers. The mod has **7 logical domains** but only **3
> source folders** — that mismatch is the main source of the sprawl.

## Status — refactor completed (this pass)

> The body of this doc below is the **original pre-refactor analysis** (kept for the feature
> catalog, config-shape map, and dependency notes). The re-foldering it proposed is now **done**,
> with some naming deltas. Current reality:

**Done and committed** (compiles clean; playtested OK):
- **Tier 1/2 renames** — `SkaterSection`→`SkatersSection`, `Runner.cs`/`Config.cs` file names,
  `UISection`→`HudSection`, `ArenaVisuals`→`ArenaVisualsToggle`, `_cmd`→`_config`, lineage handler.
- **`ReskinMenu` → `ReskinManagerMenu`** (+ access buttons).
- **`qol/` namespace dissolved** into per-domain namespaces (flat — no `features/` umbrella):
  `core`, `serverbrowser`, `social` (+`social/beacon`), `diagnostics` (+`diagnostics/profiler`),
  `display`, `hud`, `input`, plus relocations into `ui`. The `qol` namespace no longer exists.
- **`QoL*` → `Settings*`**: `QoLRunner`→`SettingsRunner`, `QoLConfig`→`SettingsConfig`,
  `QoLProfile`→`SettingsProfile`, `QoLStorage`→`SettingsStorage`. (The `…QoL.json` filename,
  `[QoL]` log tags, and `ToasterPlayerQoL` GameObject name are intentionally unchanged — they
  track the on-disk key we're keeping.)
- **Settings UI regrouped by topic.** The old single "Quality of Life" page (and the catch-all
  `HudSection`/`GeneralSection`) are gone, replaced by a **Tweaks** sidebar group:
  - **HUD**: Minimap · Chat · Scoreboard · Player HUD
  - **Game**: Rendering (Shadows+Glossiness merged) · Interface · Fixes · Input & Camera
  - **Online**: Server Browser · Multiplayer
  - **Developer**
  Settings were re-homed by what they actually are (e.g. jersey-number-in-name → Chat; flag fix +
  Unicode glyphs → Fixes). Added inline descriptions to non-obvious settings and clarified labels.

**Naming deltas from the original proposal below:** namespaces are flat top-level domains (not
nested under `features/`); the config/runtime classes are `Settings*` (not `QoL*`); the UI bucket
is "Tweaks" with topic pages (not a single section).

**Final source layout:**
```
src/
  core/         SettingsRunner, SettingsConfig/Profile/Storage, DisplaySettingsMigration, Plugin, ...
  appearance is NOT yet split out — reskin core still at src/ root + swappers/ + presets/ + api/
  swappers/     reskin swappers (display swappers moved to display/)
  display/      shadows, gloss, minimap, team-indicator, arena-visuals, minimap-rotation
  serverbrowser/  + social/ (+beacon) + diagnostics/ (+profiler) + hud/ + input/
  ui/ (+ ui/sections/)   menu shell + the Tweaks topic-section files
  presets/  api/
```

**Still outstanding (next):**
- **`SettingsRunner` dissolve + config collapse + feature registry** — see the dedicated section
  near the end of this doc. This is the next architectural tier.
- The `appearance/` consolidation (reskin core is still at `src/` root, not moved into `appearance/`).
- The broader `REFACTOR_REMAINING.md` backlog (god-class splits, line-level dedup, etc.).

---

## Legends

**Config shape** — how a feature is configured (an on/off audit only captures the first):

| Code | Shape | Notes |
|------|-------|-------|
| `OO`  | Pure on/off — one bool | The only shape a plain `Enable()/Disable()` fully describes |
| `OO+` | On/off **plus parameters / sub-toggles** | Needs a settings *group*, not a toggle |
| `ENUM`| Multi-state string/enum | A bool can't represent it |
| `STATE`| Parametric/persisted state, no real "feature" toggle | Window rects, filter values, chat layout |
| `DICT`| Keyed collection (per-server, etc.) | Often stored in a different file than its toggle |
| `ALWAYS`| No toggle — always on | Doesn't belong in a feature-flag registry |
| `PROFILE`| Driven by the shareable reskin `Profile`, not QoL config | The core appearance system |

**Lifecycle shape** — how a feature is activated:

| Code | Shape |
|------|-------|
| `A` | static `Enable()/Disable()` from `Plugin.OnEnable/OnDisable` |
| `B` | static `Initialize()/Teardown()` from `QoLRunner.Awake/OnDestroy` |
| `C` | `MonoBehaviour` attached to a GameObject |
| `D` | Harmony-patch-only — activated by `PatchAll()`, gated by a config check inside the patch |
| `TICK` | driven per-frame from `QoLRunner.Update()` |
| `ONCE` | one-time startup utility (migration, etc.) |

**Config store** — `Profile` (shareable reskin profile) · `QoL` (`…QoL.json`, the
QoLConfig/QoLProfile mirror) · `Prefs` (`…ServerPrefs.json`, per-server credentials) ·
`Mod` (`ToasterReskinLoader.json` ModSettings) · `Preview` (`server_previews.json`).

---

## Domain 1 — Appearance / Reskin (the core mod)

Driven by the global `ReskinProfileManager.currentProfile`. Invoked via
`SwapperManager.SetAll()` (scene load / Reload button / preset apply), targeted
`On*Changed()` methods, and Harmony spawn hooks (`PlayerBody.ApplyCustomizations`,
`Stick.ApplyCustomizations`). All read the registry + profile; each caches a vanilla
snapshot for restore.

| Feature / Swapper | Changes | Config | Shape | Lifecycle | Target folder |
|---|---|---|---|---|---|
| StickSwapper | Stick blade texture | Profile | PROFILE | spawn hook | `appearance/swappers/` |
| StickTapeSwapper | Stick tape color/texture | Profile | PROFILE | spawn hook | `appearance/swappers/` |
| IceSwapper | Rink ice texture + smoothness | Profile | PROFILE | SetAll | `appearance/swappers/` |
| JerseySwapper | Jersey torso/groin texture | Profile | PROFILE | spawn hook | `appearance/swappers/` |
| PlayerTextSwapper | Jersey lettering + number outline | Profile | PROFILE | spawn hook | `appearance/swappers/` |
| GoalieEquipmentSwapper | Leg pad texture (team-scoped) | Profile | PROFILE | spawn hook | `appearance/swappers/` |
| GoalieHelmetSwapper | Goalie helmet/mask/cage | Profile | PROFILE | spawn hook | `appearance/swappers/` |
| SkaterHelmetSwapper | Skater helmet texture/color | Profile | PROFILE | spawn hook | `appearance/swappers/` |
| HatSwapper | Hat/headgear overlay | Profile + AppearanceAPI | PROFILE | spawn hook | `appearance/swappers/` |
| GenderSwapper | Body mesh (M/F) | AppearanceAPI (server-driven) | PROFILE | spawn hook | `appearance/swappers/` |
| PuckSwapper | Puck texture (+ randomizer list) | Profile | PROFILE | SetAll | `appearance/swappers/` |
| PuckFXSwapper | Puck outline/trail/elevation/silhouette | Profile | PROFILE | SetAll | `appearance/swappers/` |
| ArenaSwapper | Arena element visibility | Profile | PROFILE | SetAll | `appearance/swappers/` |
| FullArenaSwapper | Full arena asset-bundle replacement | Profile | PROFILE | SetAll | `appearance/swappers/` |
| SkyboxSwapper | Skybox atmosphere/exposure/sun/tint | Profile | PROFILE | SetAll | `appearance/swappers/` |
| TeamColorSwapper | Team colors (UI dots, scoreboard, goal mesh) | Profile | PROFILE | SetAll | `appearance/swappers/` |
| ToothbrushFilter | Hides toothbrush mustache | hardcoded | ALWAYS | reset-on-scene | `appearance/swappers/` |
| **Supporting infra** | | | | | |
| ReskinProfileManager | Holds `currentProfile`; load/save/apply | — | — | startup | `appearance/` |
| ReskinRegistry | Loads + indexes reskin packs | — | — | startup | `appearance/` |
| SwapperManager | Orchestrates SetAll + On*Changed + scene hooks | — | — | startup | `appearance/` |
| SwapperUtils | Shared shader/material helpers | — | — | — | `appearance/` |
| AppearanceAPI | Syncs hat/gender/skin/hair with puckstats backend | — | OO+ (Mod: ShowHats/SkinTones/BigHeads) | A | `appearance/` |
| ToasterReskinLoaderAPI | Public read API for other mods (team colors/names) | — | — | — | `appearance/` |
| ChangingRoomHelper / ChangingRoomPatcher | Locker-room preview application | — | — | startup + patches | `appearance/` |
| PuckPreview | Puck preview rendering | — | — | — | `appearance/` |
| PartyLineup | Locker-room cosmetic lineup | QoL: enablePartyLineup | OO | A | `appearance/` |
| **Presets** (Preset, PresetApplier, PresetFieldRegistry, PresetStore, ProfileTeamTools) | Save/merge/apply partial appearance presets | own files | — | — | `appearance/presets/` |

> AppearanceAPI does network I/O (Steam ticket → puckstats.io GET/POST, debounced 2s) and
> is the only outward data flow in this domain.

---

## Domain 2 — Display settings (the ambiguous seam)

These are **swapper code** but read **QoLConfig**, not the reskin profile. They were
migrated out of the reskin profile (`DisplaySettingsMigration`, plus ~25 "moved to QoL
profile" tombstones in `ReskinProfileManager`) on the rationale that they're personal/perf,
not shareable. They currently sit orphaned between Domain 1 and Domain 5.

**Decision needed:** treat as "swappers that read QoLConfig" (keep in swappers, document)
or promote to a real Display domain (move here). The half-migrated state is a live source
of confusion.

| Feature | Changes | Config | Shape | Lifecycle | Target folder |
|---|---|---|---|---|---|
| CrispyShadowsSwapper | Shadow distance/resolution/cascades/soft | QoL: crispyShadowsEnabled +4 params | OO+ | QoL callback | `display/` |
| GlossSwapper | Material smoothness (sticks/players/pucks) | QoL: glossRemoverEnabled +smoothness +3 affects | OO+ | QoL callback | `display/` |
| MinimapSwapper | Minimap colors/scale/refresh/local-icon | QoL: 3 colors + 2 scales + refreshRate + localIcon×3 | OO+ | QoL callback | `display/` |
| StaminaSwapper | Stamina-bar throttle (slaved to minimap refresh) | QoL: minimapRefreshRate | OO+ | QoL callback | `display/` |
| TeamIndicatorSwapper | On-ice team-color bar | QoL: teamIndicatorEnabled | OO | QoL callback | `display/` |
| PatchMinimapRotation | Minimap rotation mode | QoL: minimapRotationMode | **ENUM** (off/rotate90/followPlayer) | D + TICK | `display/` |
| ArenaVisuals | Disable arena props/lights/skybox/particles + audio vol | QoL: 5 bools + arenaAudioVolume | OO+ | E (util, called from UI) | `display/` |

---

## Domain 3 — Server browser + matchmaking (the densest tangle)

`ServerBrowserSort` is the hub: favorites, blocks, and saved-password badges all read its
dicts live. Toggles live in `QoL`; the keyed collections live in `Prefs` (separate file so
reskin profiles don't leak credentials). SlotQueue and QuickJoin both hijack the same
`MatchmakingPanelOverlay` and defer to each other.

| Feature | Description | Config | Shape | Lifecycle | Target folder |
|---|---|---|---|---|---|
| ServerBrowserSort | Sort by players%, badges, hub for favorites/blocks | QoL: enableServerBrowserSortTweaks | OO (hub) | B + D | `serverbrowser/` |
| Favorites | ★ button + favorites-first tier | QoL: enableServerFavorites; **Prefs: favoriteServers** | OO + DICT | (in Sort) | `serverbrowser/` |
| Blocks | Right-click hide (mutually excl. w/ favorites) | QoL: enableServerBlocks; **Prefs: blockedServers** | OO + DICT | (in Sort) | `serverbrowser/` |
| SavedServerPasswords | Auto-fill on rejection + "remember" checkbox | QoL: enableSavedServerPasswords; **Prefs: savedServerPasswords** | OO + DICT | B + D | `serverbrowser/` |
| TrustedModLists | Auto-confirm MODS REQUIRED popup | QoL: enableTrustedModLists; **Prefs: trustedServerMods** | OO + DICT | D | `serverbrowser/` |
| InlineServerBrowserFilters | Reparent filter controls into a strip | QoL: enableInlineServerBrowserFilters | OO | D | `serverbrowser/` |
| BrowserFilterPersistence | Persist filter values across sessions | QoL: enableBrowserFilterPersistence + 7 filter fields | OO + STATE | D | `serverbrowser/` |
| ServerPreviewCache (+Patches) | Seed rows from disk cache before live pings | QoL: enableServerPreviewCacheV2; **Preview store** | OO | A/D | `serverbrowser/` |
| FastServerBrowserScanning | Parallel ping fan-out | QoL: enableFastServerBrowserScanningV2 +concurrency +2 timeouts | OO+ | D | `serverbrowser/` |
| ServerSlotQueue | Poll a full server, auto-join on slot | QoL: enableServerSlotQueue | OO | B + own Task | `serverbrowser/` |
| AutoConnectMatchmaking | Auto-fire connect when match is READY | QoL: enableAutoConnectMatchmaking | OO | A | `serverbrowser/` |
| MainMenuButtons — QuickJoin | Title-screen best-server auto-join | QoL: enableMainMenuQuickJoinV2 | OO | B | `serverbrowser/` |
| MainMenuButtons — Browser shortcut | Title-screen open-browser button | QoL: enableMainMenuServerBrowser | OO | B | `serverbrowser/` |
| MatchmakingPanelOverlay | Shared panel shim (QuickJoin + SlotQueue) | — | ALWAYS (support) | reflection | `serverbrowser/` |

> Network I/O: fast-scan fans out pings via a semaphore; SlotQueue runs its own TCP poll
> (~750ms); preview cache piggybacks on the vanilla ping path. All isolated from Domain 1.

---

## Domain 4 — Social

| Feature | Description | Config | Shape | Lifecycle | Network | Target folder |
|---|---|---|---|---|---|---|
| BetterFriendsList | Friends panel w/ live server locations | QoL: enableBetterFriendsList | OO | A | TCP/JSON ping client | `social/` |
| BeaconPing (+Pinger/Cache/MainThread/PanelController/PingPanel) | Beacon RTT panel on Play menu | QoL: enableBeaconPing | OO | A | TCP sweep, **fully standalone** | `social/beacon/` |
| PartyLineup | (cross-listed in Domain 1) | QoL: enablePartyLineup | OO | A | — | `appearance/` |

> **BeaconPing is fully self-contained** — separate endpoints, own cache, touches nothing
> else. Cleanest extract-to-own-plugin candidate after FrameProfiler.

---

## Domain 5 — Chat / Scoreboard / HUD polish

Mostly small independent Harmony-only patches. Two ride the `QoLRunner.Update()` tick.

| Feature | Description | Config | Shape | Lifecycle | Target folder |
|---|---|---|---|---|---|
| EscClosesMenus | ESC closes secondary menus | QoL: enableEscCloseMenus | OO | D + TICK | `hud/` |
| ChatAnyPhase | Allow chat in any in-game phase | QoL: enableChatAnyInGamePhase | OO | D | `hud/chat/` |
| ScoreboardAnyPhase | Hold-to-view scoreboard any phase | QoL: enableScoreboardAnyInGamePhase | OO | D | `hud/` |
| SelectableChat | Drag-select chat text | QoL: enableChatDragSelect | OO | D | `hud/chat/` |
| HideInactiveChat | Hide chat when inactive | QoL: enableHideInactiveChat | OO | D | `hud/chat/` |
| ChatNoFade | Expired messages stay full-opacity | QoL: enableChatNoFade | OO | D | `hud/chat/` |
| ScoreboardPolish | Clock ms interpolation + end-period color ramp | QoL: enableScoreboardMilliseconds + enableScoreboardClockColor | OO+ | D + TICK | `hud/` |
| NumberedNames | Number chat/usernames | QoL: enableNumberedNames | OO | D | `hud/` |
| PatchPlayerUsernameColors | Team-color floating usernames | QoL: enablePlayerUsernameTeamColors | OO | D + TICK | `hud/` |
| SpectatorMinimap | Minimap while spectating | QoL: enableSpectatorMinimap | OO | D | `hud/` |
| UiTextShadow | Text-shadow on score/clock/chat labels | QoL: enableUiTextShadow | OO | B | `hud/` |
| TeamButtonPlayerCount | Show player count on team buttons | QoL: enableTeamButtonPlayerCount | OO | D | `hud/` |
| FlagMaterialFix | Fix shared-material country flags | QoL: enableFlagMaterialFix | OO (default on) | D | `hud/` |

---

## Domain 6 — Input / Camera

| Feature | Description | Config | Shape | Lifecycle | Target folder |
|---|---|---|---|---|---|
| PositionSelectFreeLook | Spectator free-look during position select | QoL: enablePositionSelectFreeLook | OO | C (MonoBehaviour) | `input/` |
| DisableControllerInput | Disable gamepads at Input-System level | QoL: disableControllerInput | OO | A | `input/` |

---

## Domain 7 — Diagnostics / Dev

| Feature | Description | Config | Shape | Lifecycle | Network | Target folder |
|---|---|---|---|---|---|---|
| FrameProfiler (+Overlay/Patches/Network/Mods/GeoIP/GraphBaker/BuiltinMarkers — 8 files, ~4k lines) | Frame-timing overlay + instrumentation | QoL: enableFrameProfiler + enableFrameProfilerModInstrumentation | OO+ | A + TICK | GeoIP HTTP | `diagnostics/profiler/` — **best extract-to-own-plugin candidate** |
| DevConsole | In-game dev console window | QoL: enableDevConsole + devConsole X/Y/W/H | OO + STATE | C (MonoBehaviour) | — | `diagnostics/` |
| WorkshopUpdateChecker | Check workshop items for updates | (always inits) | OO/ALWAYS | B-ish (Plugin) | Steam UGC API | `diagnostics/` |
| DebugBuildLabel | Show build label | none | ALWAYS | D | — | `diagnostics/` |
| DebugLogging | Verbose logging | Mod: DebugLoggingModeEnabled / QoL: enableDebugLogging | OO | — | — | `diagnostics/` |

---

## Domain 8 — UI / Menu infrastructure (renders the others)

| Component | Description | Target folder |
|---|---|---|
| ReskinMenu | Main menu shell — `string[] sections` + `sidebarLayout[]` + 18-arm switch (sync'd on magic strings) | `ui/` |
| ReskinMenuAccessButtons | Buttons that open the menu | `ui/` |
| UITools / UISection | Shared row builders + the concrete HUD section (misnamed — reads like a base class) | `ui/` |
| 19 `ui/sections/*` | One section per feature group; each hand-wires its own controls to config | `ui/sections/` |
| VanillaUIRetheme | Inline CSS overrides on vanilla UI | QoL: enableVanillaUIRetheme · `A` | `ui/` |
| UnicodeFontFallback | OS-font fallback for TMP + UI Toolkit | QoL: enableUnicodeFontFallback · `A` (no Disable) | `ui/` |
| MissingModsPopupSuppression | Suppress missing-mods popup | `D` | `ui/` |

---

## Cross-cutting findings (these drive the refactor priorities)

1. **The config mirror is the biggest hidden tax.** `QoLConfig` (fields) and `QoLProfile`
   (`[JsonProperty]` props) are twins joined by hand-written `ToConfig()`/`FromConfig()`.
   Every one of ~90 settings is written **4×** (field, property, two mapping lines) — 5× with
   the UI section. Adding one slider touches 5 files. Collapsing the two into one class (the
   same move PR #19 made for `Profile`/`SerializableProfile`, −559 lines) is likely the
   highest-leverage refactor left.

2. **A feature registry must model parameters, not just on/off.** Of the shapes above, only
   the pure-`OO` features are fully described by `Enable()/Disable()`. The `OO+`, `ENUM`,
   `STATE`, and `DICT` features need the registry to carry a typed config schema so it can
   drive lifecycle *and* render the settings UI — killing the parallel-array problem in
   Plugin.OnEnable, the QoLConfig↔QoLProfile mirror, and the per-section hand-wiring at once.

3. **Lifecycle is scattered across three places** (`Plugin.OnEnable`, `QoLRunner.Awake`,
   in-patch gates) in five shapes. `OnEnable` and `OnDisable` keep hand-maintained, already-
   divergent enable/disable lists (e.g. UnicodeFontFallback and ModMenuEnhancer enable but
   never cleanly disable). A registry + uniform `IFeature` lifecycle fixes the drift.

4. **Two domains are clean extractions** if scope-trimming is ever wanted: **FrameProfiler**
   (~4k lines, fully standalone) and **BeaconPing** (fully standalone). Neither shares state
   with the reskin core.

5. **The Display-settings seam (Domain 2) needs an explicit owner.** It's the one place where
   swapper code and QoL config cross; the half-finished migration left it ambiguous.

## Proposed target structure

```
src/
  appearance/        # Domain 1 — the actual reskin mod
    swappers/
    presets/
  display/           # Domain 2 — QoLConfig-driven visual settings (decide ownership)
  features/
    serverbrowser/   # Domain 3
    social/          # Domain 4
      beacon/
    hud/             # Domain 5
      chat/
    input/           # Domain 6
    diagnostics/     # Domain 7
      profiler/
  ui/                # Domain 8 — menu shell + sections
    sections/
  core/              # Plugin, ModSettings, PathManager, config, QoLRunner, storage, migrations
```

---

## Renaming recommendations

Grouped by risk. All verified against the code at v2.2.1.

### Tier 1 — Just wrong, zero behavioral risk (internal-only)

Find-and-replace safe. Removes names that are actively misleading or lie about what the
symbol is.

| Current | Suggested | Why |
|---|---|---|
| `SkatersSection.cs` → `class SkaterSection` | `class SkatersSection` | File is plural, class is singular. Every other section matches its file. Typo-level mismatch. |
| `Runner.cs` (holds `class QoLRunner`) | rename **file** → `QoLRunner.cs` | File/class mismatch. `Runner.cs` is unsearchable — nobody greps "Runner" to find the QoL bootstrapper. |
| `Config.cs` (holds `class QoLConfig`) | rename **file** → `QoLConfig.cs` | Generic file name for a specific class; collides mentally with `ModSettings`, `QoLProfile`, etc. |
| `QoLRunner._cmd` field | `_config` / `_cfg` | Leftover from the PoncePlayerInput port — it holds a `QoLConfig`, nothing command-related. Public property is already `Config`; the backing field disagreeing is just noise. |
| `QuickChatPlusSettingsCloseButtonClickHandler` (local fn, `ReskinMenu.cs:355`) | `CloseButtonClickHandler` | Lineage debris — this closes *our* reskin menu, nothing to do with QuickChatPlus. (The "QuickChatPlus pattern" *comments* in `ReskinMenuAccessButtons.cs` can stay as historical notes; the symbol name shouldn't lie.) Local function — scope is one method. |

### Tier 2 — Clarity renames (worth doing, low risk)

| Current | Suggested | Why |
|---|---|---|
| `UISection` / `UISection.cs` | `HudSection` (or `DisplaySection`) | **Most actively misleading name in the repo.** Reads like the base class for all 19 sections, but it's the *concrete* HUD/Display section. New contributors will assume the others inherit from it. |
| `ToasterReskinLoaderAPI` vs `api/AppearanceAPI` | `ReskinLoaderAPI` (public read surface) + `AppearanceSync` (internal backend sync) | Two "API" classes doing very different jobs — one is a public read API for other mods, the other is the puckstats HTTP sync. `AppearanceAPI` oversells an internal sync component as a public API. |
| `ArenaVisuals` | `ArenaVisualsToggle` / `ArenaVisibility` | You also have `ArenaSwapper`, `FullArenaSwapper`, `ArenaSection`. `ArenaVisuals` is the odd one out — it *disables* props/lights (a QoL display feature), not a reskin swapper. A name signalling "toggle/visibility" separates it. |

### Tier 3 — Identity-level (flag, but mostly DON'T do)

"Reskin" is now a misnomer — reskinning is ~1 of 7 domains. `ReskinMenu` is really the whole
mod's settings menu; `ReskinProfileManager` only manages appearance. But:

- **DO NOT rename** `MOD_GUID`, the config file names (`ToasterReskinLoader.json`,
  `…QoL.json`, `…ServerPrefs.json`), or the `reskinprofiles/` directory. These are
  user-facing persistence keys — renaming them silently wipes everyone's saved settings and
  workshop pack associations.
- `namespace ToasterReskinLoader.qol` (lowercase segment) violates PascalCase convention, but
  fixing it touches every file's `using`. Only worth doing *during* the folder reorg, not as
  standalone churn.
- `ReskinMenu` → `ToasterMenu` / `SettingsMenu` is defensible (class rename only, no
  persistence impact) but ripples through many files. Defer until the menu's
  `string[] sections` + 18-arm switch gets refactored anyway — rename in the same pass.

### Recommended now

Do **Tier 1** + **`UISection` → `HudSection`**. Hold the rest until they ride along with the
refactors that touch those files anyway — standalone rename commits create churn and merge
conflicts for the other open PRs without buying much.

---

## Planned refactor — dissolve `SettingsRunner` (formerly `QoLRunner`)

> Status: noted, not started. `QoLRunner` was renamed to `SettingsRunner` as an interim
> step; this is the structural follow-up.

`SettingsRunner` is a single `DontDestroyOnLoad` MonoBehaviour that conflates four jobs.
Only one of them actually requires a MonoBehaviour:

| Job | Needs MonoBehaviour? | Target home |
|---|---|---|
| Hold the in-memory config | No | a plain `static Settings` holder (`Settings.Current`) |
| Bootstrap features at startup (`Awake` init batch) | No | the **feature registry** (`IFeature` lifecycle) |
| Callback surface (`SaveAndRefresh`, `SendChatMessage`, DevConsole hooks) | No | the owning feature classes |
| Per-frame `Update()` (ScoreboardPolish clock interpolation; ESC polling) | **Yes — but thin** | a tiny tick pump / per-feature MonoBehaviour |

It became a god object by absorbing the three things that didn't need it. Even the one that
does is small: **ScoreboardPolish** is the only genuine per-frame need (smooth millisecond clock
interpolation); the **ESC-to-close-menus** half is currently polling `Keyboard.current` but the
pause-menu case is already a Harmony postfix — the rest can be the same kind of hook, removing
the poll.

**Target shape:**
- `core/Settings.cs` — static config owner + Load/Save (the ~45 `SettingsRunner.Instance.Config`
  call sites become `Settings.Current`).
- **Feature registry** — replaces the `Awake()` init batch *and* the `Enable/Disable` blocks in
  `Plugin.OnEnable` (see findings #2/#3 above).
- ScoreboardPolish owns its own small MonoBehaviour (or a registry-started coroutine); DevConsole
  / PositionSelectFreeLook already are MonoBehaviours and just attach to their own GameObject.
- ESC handling → Harmony hook; delete the polling loop. Callbacks → move to owners.
- Net: `SettingsRunner` disappears.

**Pairs with** the config collapse (merge `SettingsConfig` + `SettingsProfile`, killing the
4×-per-field `ToConfig`/`FromConfig` mirror) — do them together, since both touch the same
~45 config call sites. This is the deliberate "registry + config" pass, not a quick edit.

**Leave alone:** the `…QoL.json` persistence filename, `[QoL]` log tags, and the
`ToasterPlayerQoL` GameObject name — all track the on-disk key we're keeping (renaming the file
needs a migration).

