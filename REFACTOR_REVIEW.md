# Refactor & Code-Smell Review — Toaster's Reskin Loader

Whole-codebase maintainability review of `src/` (~34.6k LOC of C#, Unity/BepInEx mod).
Scope: refactoring opportunities and code smells — **not** a feature/correctness audit, though a
handful of genuine bugs surfaced and are flagged 🐞.

Findings are concrete (`file:line`) and grouped by theme. A prioritized action checklist is at the
end.

---

## The big picture

The codebase has good bones — `SwapperUtils`, `UITools`, the attribute-driven `PresetFieldRegistry`,
the FrameProfiler file split, and `PuckPreview`'s material tracking all show solid factoring
instinct. The recurring problem is **consistency and adoption**: shared helpers exist, but a large
fraction of the code predates them or ignores them, so the same patterns were copy-pasted instead.
The result is heavy duplication plus a handful of god classes.

---

## 1. Cross-cutting smells

These repeat across the whole codebase — fixing each once pays off everywhere.

### 1.1 Stack traces discarded at scale — MED
131 of 275 `catch` blocks log only `ex.Message`. Field failures in swappers / IO become
undiagnosable. Format is inconsistent (a few append `\n{ex.StackTrace}`, most don't), and the log
*level* is arbitrary — the same "swap failed" is `LogError` in `swappers/IceSwapper.cs:54` but
`LogDebug` in the `TeamColorSwapper` patches.
**Fix:** log the full `e`; standardize a level policy (expected-and-ignorable → `LogDebug`, else
`LogError` with stack).

### 1.2 Dark-theme palette inlined ~150× — MED
The same five colors are raw `new Color(...)` literals everywhere: `0.25,0.25,0.25` (~29×),
`0.15…` (~9×), `0.4…` (~15×), `0.7…` (~16×), `0.14…` (~3×) in the UI, plus more in swappers.
Re-theming is currently a find-replace across 25 files.
**Fix:** a `UITheme`/`UIColors` static (`PanelBg`, `ButtonBg`, `Border`, `MutedText`, `FieldFill`,
`Selected`) plus layout constants (`DropdownWidth = 400`, `SliderWidth = 300`).

### 1.3 Stringly-typed identifiers everywhere — HIGH
Reskin type/slot strings (`"stick_attacker"`, `"jersey_torso"`, `"blue_personal"`, …), filter/sort
modes, Steam rich-presence keys, and renderer/child names (`"helmet"`, `"cage"`, `"(Clone)"`,
`"torso"`) are duplicated literals across 5+ files. A typo fails **silently**.
- Worst case: `presets/PresetFieldRegistry.cs:173-218` and `presets/ProfileTeamTools.cs:52-60`
  derive team/role/swap-partner by substring matching (`name.Contains("Blue")`,
  `.Replace("Blue","Red")`). A field like `bluebell` mis-buckets; `BaseKey` collisions silently drop
  fields via `.First()` (`ProfileTeamTools.cs:34-37`). The token list is duplicated in both files and
  can drift.
**Fix:** `const`/`enum` for types/slots/modes; centralize the team/role token logic; add a
`Validate()` check that no two fields collide on `BaseKey`.

### 1.4 Global mutable state — HIGH (architectural)
`ReskinProfileManager.currentProfile` is reached into directly **392 times** with no encapsulation;
`ReskinRegistry.reskinPacks` is a public non-`readonly` `List` any caller can reassign. This is the
central coupling point of the entire mod.
**Fix:** at minimum make collections `readonly` + expose `IReadOnlyList`; longer term, route mutation
through methods that own the side-effect dispatch.

---

## 2. God classes

| File | Lines | Tangled responsibilities |
|---|---|---|
| `swappers/ModMenuEnhancer.cs` | 1942 | mod abstraction + disk/reflection probing + UI builder + filter/sort state + workshop-update orchestration |
| `ReskinProfileManager.cs` | 1665 | quadruple-mirrored schema + persistence + mutation dispatch + reset commands + randomizer list |
| `qol/BetterFriendsList.cs` | 1374 | lifecycle + 5 Harmony patches + presence reader + UI builder + hand-rolled TCP/JSON ping client + main-thread dispatcher |
| `qol/FrameProfilerOverlay.cs` | 1228 | data collector + stats engine + graph rasterizer + 4-mode IMGUI renderer + texture manager + CSV IO (~70 fields) |
| `api/AppearanceAPI.cs` | 1002 | static caches/counters + coroutine web client + ticketing |

Worst long methods: `ModMenuEnhancer.ApplyEnhancements` (~440 lines, `:971-1410`),
`ReskinProfileManager.LoadProfile` (~335, `:231-565`), `ModMenuEnhancer.EnsureControlsInjected`
(~270, `:360-633`), `FrameProfilerOverlay.DrawOverview` (~156, `:812-968`),
`BetterFriendsList`/`FriendsListHelper.UpdateSingleFriend` (~137, `:557-694`),
`ReskinProfileManager.SetSelectedReskinInCurrentProfile` (~163 nested `if/switch`, `:21-184`).

### ⭐ The #1 maintenance hazard: the quadruple-mirrored profile schema
`ReskinProfileManager`'s ~130 settings are declared **four times** — `Profile` fields (`:1042-1325`),
`SerializableProfile` properties (`:1346-1637`), and again in the `LoadProfile` (`:231-565`) and
`SaveProfile` (`:607-777`) bodies. Every new setting must be added in 4 places with **no compiler
enforcement**; the `TODO` at `:896` already documents a field silently failing to round-trip.
**Fix:** drive serialization off the `[PresetField]` metadata that already exists, or make `Profile`
*be* the serialized shape with a custom `JsonConverter` for `ReskinEntry → ReskinReference`. Removes
~600 lines and the hazard.

---

## 3. Duplication hot-spots (highest-leverage dedup)

### 3.1 UI reskin-dropdown rows — ~26 verbatim copies — HIGH
6 section files repeat a ~18-line `RegisterCallback<ChangeEvent<ReskinEntry>>` block that differs only
in a label + two slot strings + a `PreviewContext` pair (`ui/sections/SticksSection.cs:34-213` has 4
in one file; also `SkatersSection`, `GoaliesSection`, `ArenaSection`, `PlayersSection`,
`TapesSection`).
**Fix:** `UITools.AddReskinDropdownRow(container, label, choices, current, fallback, type, slot, team, role)`.
Removes ~350-400 lines and 18 leftover `// User picked ID=` debug logs. Pair with a
`ReskinRegistry.UnchangedEntry(type)` factory (the `"Unchanged"` sentinel is hand-built 16×).

### 3.2 Player-equipment swappers share a copy-pasted skeleton — HIGH
`GoalieHelmetSwapper`, `SkaterHelmetSwapper`, `GoalieEquipmentSwapper` (and partly `JerseySwapper`)
repeat: per-(team,player[,part]) snapshot cache, `ApplyX` (cache-if-absent → apply/restore), the
validate-player guard, and the `UpdateTeam → GetPlayersByTeam → role-filter → SetForPlayer` loop —
line-for-line equivalent.
**Fix:** a `PlayerPartSwapper` base exposing `EnsureCachedAndApply(...)` + `ForEachTeamPlayer(...)`.
Collapses ~250-300 lines into ~80 + thin subclasses, and resolves an inconsistency where
`GoalieEquipmentSwapper` restores-on-load-failure while the others just `return`.

### 3.3 Nine identical FrameProfiler patch classes — MED 🐞
`qol/FrameProfilerPatches.cs:315-471` — `Patch_GameManagerTick`, `Patch_SteamCallbackLoop`,
`Patch_PhysicsUpdate`, … are byte-for-byte identical except the `TrackedSystem` enum arg. The shared
static `Stopwatch`/`memBefore` are **not reentrancy-safe** (e.g. `EventManager.TriggerEvent` can
nest). The correct `out PerCallState __state` pattern already exists in `FrameProfilerMods.cs:319`.
**Fix:** collapse to one shared prefix/postfix keyed by `__originalMethod` using `__state`.

### 3.4 Other repeated blocks
- "Reset to default" button (7×) and "rebuild section in place" idiom (6×) in UI sections, ignoring
  `UITools.StyleConfigButton`. → `UITools.CreateResetButton` + `RebuildSection`.
- `FormatBytes` exists 3× with disagreeing formats (`FrameProfilerOverlay.cs:1214/1221`,
  `FrameProfilerPatches.cs:307`); `NS_TO_MS` declared 3×; axis "snap to nice band" logic 3×.
- Texture-slot handling forked into 4+ swapper implementations despite `SwapperUtils` being "the
  single source of truth" — `PuckSwapper`, `IceSwapper`, `StickSwapper`, `ArenaSwapper`,
  `HatSwapper` each reimplement `_BaseMap`/`_MainTex`/`_BaseColor` with their own caching. `"_BaseMap"`
  appears 18×, only `PuckSwapper` caches a `Shader.PropertyToID`.
- `Shader.Find("Universal Render Pipeline/Lit")` called per-apply in `StickSwapper`/`StickTapeSwapper`;
  `HatSwapper`/`PartyHatSwapper` each cache their own URP-Lit shader. → one shared
  `SwapperUtils.UrpLitShader`.
- `BetterFriendsList`: status-label UI builder ×3 (`:757-830`, `:919-997`, `:696-755`), async
  server-name refresh block ×2, presence-gather block ×2.
- `QoLRunner.Instance?.Config?.X ?? default` chain repeated ~59×; `Enabled =>` config-guard idiom
  copy-pasted across ~10 QoL files. → a typed `QoLRunner.Cfg` accessor returning a non-null config
  with defaults applied.

---

## 4. Dead / debug code shipping in the release DLL

- **`swappers/PartyHatSwapper.cs`** (319 lines) — zero references; fully superseded by `HatSwapper`
  (hat ID 1 = "Party Hat"). Delete.
- **`TestArena.cs`** (266 lines, ~200 commented) — referenced nowhere. **`TestStick.cs`** (128 lines)
  — entirely commented out. Delete both.
- **`PatchClientChat.cs`** 🐞 — header says "Debug-only," but it's registered via `PatchAll()` and
  runs in every build: logs on **every chat message** (`:26`) and exposes a `/hierarchy` command that
  dumps the whole scene graph to `hierarchy.txt` in the cwd (`:29-33`). Gate behind the existing
  `ModSettings.DebugLoggingModeEnabled`, or remove.
- **`ui/sections/FullArenaSection.cs`** (222 lines) — never dispatched by `ReskinMenu`; dead or an
  unwired feature (the live one is `ArenaSection`).
- Smaller dead bits: `UISection.CreateTextInput` (`:398-432`), `UITools.StyleDropdownField`
  (`:250-256`), `FrameProfilerOverlay.DrawRttRefLines` (`:970-1000`) + `DrawRefLine`,
  `PartyLineup.GetPath` (`:589-598`), `AppearanceAPI.initialFetchDone` (`#pragma`-suppressed dead
  field).

---

## 5. Correctness smells worth a look 🐞

- **`ServerNameFetcher._callback` is a single static field** (`BetterFriendsList.cs:1162`) overwritten
  per call and nulled after invoke — overlapping fetches drop or double-invoke callbacks. Capture as a
  local in the task instead.
- **`FrameProfilerNetwork` ring buffers** mutated from the Netcode RPC thread, read on the main thread,
  with no locks/`volatile` (`FrameProfilerNetwork.cs:142-176` vs `FrameProfilerOverlay.cs:246-249`).
  Usually fine in practice but undocumented and tearable; `ConsumeAndResetFrameTickCount` is a
  non-atomic read-then-zero.
- **Material instance leaks:** `ChangingRoomHelper`/`ChangingRoomPatcher` access `renderer.material`
  (which *instantiates* a material) in preview-refresh paths without destroying the instances.
  `FrameProfilerOverlay.colorTexCache` (`:1157`) and `TextureManager`'s failed-`LoadImage` path
  (`:79-95`) also leak.
- **`ModSettings.GetConfigPath`** uses `Path.GetFullPath(".")` (cwd) while everything else uses
  `PathManager.GameRootFolder` — config can land in the wrong directory under Steam/launchers
  (`ModSettings.cs:63`).
- **Stale caches:** `ModMenuEnhancer.sizeCache`/`outdatedDllCache` are never invalidated, even after the
  menu's own `DownloadAndRefresh` updates a mod (`:109`, `:231`, `:1814`).
- **`UISection.CreateSliderRow`** calls `ReskinProfileManager.SaveProfile()` even though its callers
  write the *QoL* config (`:339-341`) — likely persists the wrong profile.
- **`PresetApplier.RefreshAll`** hard-codes which swappers `SetAll()` misses (`:90-105`) — a
  knowledge-leak; new presetable swappers must be remembered here. Consolidate into one
  `SwapperManager.RefreshEverything()` shared with the Reload button.
- **csproj** unconditionally copies the built DLL to `C:\Program Files (x86)\Steam\...` (`:43`) —
  breaks the build for the other contributors named in the README. Guard with `Condition="Exists(...)"`
  or a property/env override.
- **Beacon pinger:** `_lastSweepUtc` is a tearable cross-thread `DateTime` (`BeaconPinger.cs:32-58`),
  and `SweepCore` blocks pool threads on `Task.WhenAll(...).Wait()` (`:56-91`) — the serverbrowser code
  deliberately uses `LongRunning` dedicated threads to avoid exactly this; align them.

---

## 6. Naming & misc

- `UISection` reads as a base class (in `sections/`, named like a base) but is the concrete **HUD/
  Display** section. Rename `HudSection`/`DisplaySection`.
- `SkaterSection` class (singular) lives in `SkatersSection.cs` (plural) — file/class mismatch.
- Carry-over names from another mod's lineage: `QuickChatPlusSettingsCloseButtonClickHandler`
  (`ReskinMenu.cs:355`), `MainMenuOpenReskinManagerClickHandler`.
- `ReskinMenu` keeps a `string[] sections` (`:26`), a parallel `sidebarLayout` (`:54-71`), and an
  18-arm `switch` (`:375-438`) all manually synced on the same magic strings — adding a section means
  editing 3+ places. → one `Dictionary<string, Action<VisualElement>>` registry.
- Mixed field-naming conventions (`originalTexture` vs `_originalTexture`, `BUNDLE_NAME` vs
  `ScanDebounceSeconds`) and namespace styles (file-scoped vs block) across swappers.
- ~25 "moved to QoL profile" tombstone comments in `ReskinProfileManager.cs` — consolidate into one
  migration note.

---

## Prioritized action checklist

### Quick wins — low risk, high signal
- [ ] Delete dead code: `PartyHatSwapper.cs`, `TestArena.cs`, `TestStick.cs` (~700 lines).
- [ ] Gate or remove `PatchClientChat` debug command + per-message logging.
- [ ] Make the csproj DLL-copy target conditional.

### Structural — biggest payoff
- [ ] Collapse the quadruple-mirrored profile schema in `ReskinProfileManager` (~600 lines + the #1 hazard).
- [ ] `UITools.AddReskinDropdownRow` + `ReskinRegistry.UnchangedEntry` factory (~400 lines).
- [ ] `PlayerPartSwapper` base for the equipment swappers (~250 lines).
- [ ] `UITheme` palette/layout constants; route swappers through `SwapperUtils` consistently.

### Follow-ups
- [ ] Collapse the 9 Stopwatch patches to the `__state` pattern (also fixes reentrancy).
- [ ] Introduce reskin type/slot + filter/sort enums; centralize team/role token logic.
- [ ] Split `ModMenuEnhancer` / `BetterFriendsList` / `FrameProfilerOverlay` along the seams above.
- [ ] Fix the material/texture leaks, the `ServerNameFetcher._callback` race, and `ModSettings` cwd path.
- [ ] Standardize exception logging (full `e`, consistent level).
