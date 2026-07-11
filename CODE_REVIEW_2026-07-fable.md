# ToasterReskinLoader — Code Review (2026-07)

A full-project review covering `src/` (core, api, swappers, ui, diagnostics, hud, display,
serverbrowser, social, presets, input). Findings are grouped by theme and severity so each can be
picked up as an independent session. Each item lists **where**, **the problem**, and **the intended
fix**. Nothing here has been changed — this is a punch-list.

Severity: **HIGH** = user-visible bug / crash / data loss risk · **MED** = latent bug, perf, or
significant smell · **LOW** = cleanup / consistency / dead code.

---

## 1. Correctness bugs (fix first)

### 1.1 HIGH — Color sliders write to disk on every drag tick
`src/ui/UITools.cs:441` — `CreateColorConfigurationRow`'s continuous `ChangeEvent<float>` callback
calls `onSave?.Invoke()` on *every* slider tick, in addition to the correct `PointerUpEvent` handler
at `:445`. Callers wire `onSave` to `ReskinProfileManager.SaveProfile()`, so dragging any color
slider (Skaters, Goalies, Arena, Skybox, PuckFX, Presets…) triggers a full JSON disk write per tick.
Same pattern in `CreateNumberOutlineConfigurationRow` (`:518`), and in `ArenaSection.cs:132-137,
212-217` (ice/glass smoothness) and `PlayersSection.cs:140-146` (team-name field saves + full
`TeamColorSwapper.RefreshAll()` per keystroke).
**Fix:** remove the per-tick `onSave` (PointerUp already persists); debounce text fields on
`FocusOutEvent`. The spectator-density slider in `ArenaSection` already shows the correct pattern.

### 1.2 HIGH — `JerseySwapper.SetJerseyForPlayer` null check is dead
`src/swappers/JerseySwapper.cs:111-120` — dereferences `player.Username.Value` and `player.Team`
before the `player == null` check at `:120`, so the guard never fires and the method NREs when called
from the ApplyCustomizations postfix with a null player.
**Fix:** move the null check to the top of the method.

### 1.3 HIGH — GlossSwapper never clears renderer property blocks on restore
`src/display/GlossSwapper.cs:191-201, 320-344` — `WritePropertyBlock` writes smoothness/roughness
overrides into each renderer's `MaterialPropertyBlock`, but `Restore`/`RestoreAll` only revert the
shared *material*, never clearing the per-renderer block. Disabling the gloss remover leaves every
touched renderer dulled until scene reload. Compounded by `:272-317` applying via **both** shared-
material float writes *and* a property block (the two overlap and the block omits
`_Metallic`/`_Clear_Coat_*`), and by mutating `sharedMaterials` (`:278-288`) which alters every object
sharing that material globally.
**Fix:** track touched renderers, clear their property block (`SetPropertyBlock(null)`) on restore,
and settle on a single application mechanism (property block is the non-destructive choice).

### 1.4 HIGH — Reload button's error handling can't catch reload failures
`src/ui/ReskinManagerMenu.cs:247-292` — the try/catch wraps only the *scheduling*; the actual
`reload()` runs later on the scheduler, so exceptions from `ReloadPacks`/`LoadProfile`/`SetAll` escape
uncaught and the button is stuck on "Reloading..." forever.
**Fix:** move the try/catch inside `reload()` and restore button text/state in the catch.

### 1.5 HIGH — Goalie leg-pad color sliders go stale
`src/ui/sections/GoaliesSection.cs:122-183` — the pad dropdowns never pass an `onChanged` callback, so
`SetColorSliderEnabled(colorSection, AnyPadUnchanged())` runs only at build time. Switching a pad
between "Unchanged" and a skin doesn't enable/disable the color sliders until the section rebuilds
(the helmet/mask dropdowns at `:281,:335` do it correctly).
**Fix:** pass `onChanged: _ => SetColorSliderEnabled(...)`, reordering so the color section exists first
(as `SkatersSection` does).

### 1.6 MED — Missing null guards inside Harmony postfixes (NRE escapes the patch)
- `src/swappers/SwapperManager.cs:166` — `__instance.Player.IsReplay.Value` with no `Player` null guard.
- `src/swappers/SwapperManager.cs:96-97` — `throw new ArgumentOutOfRangeException()` on an unknown
  `PlayerTeam` runs inside a postfix; a new enum value in a game update throws out of the patch.
  Log-and-return instead.
- `src/swappers/PuckFXSwapper.cs:412-437` — `ServerConfigurationPatch.Postfix` calls
  `UIManager.Instance.ToastManager.ShowToast(...)` with no null guard; NREs if UIManager isn't up.
- `src/ChangingRoomHelper.cs:531` — reflected `_field.GetValue(...)` cast without the `?.` used
  elsewhere in the same file; a renamed private field null-refs inside the postfix.
- `src/swappers/StickSwapper.cs:79` — `stick.Player.GetPlayerStickSkinID()` restore path unguarded.
**Fix:** early-return on null; log-and-continue instead of throwing inside any postfix.

### 1.7 MED — StickTape "Unchanged" doesn't restore overwritten textures
`src/swappers/StickTapeSwapper.cs:277-281` — switching a tape from Textured back to Unchanged/RGB
restores shader + color but never restores the original `_BaseMap`/`_Normal`/etc. textures that
`ApplyTapeTexture` wrote onto the material instance, so the custom texture persists.
**Fix:** snapshot textures (reuse `SwapperUtils.MaterialSnapshot`) and restore them.

### 1.8 MED — Thread-unsafe caches in the server browser
- `src/serverbrowser/SavedServerPasswords.cs:363-376` — `_serverNameCache` is a plain `Dictionary`
  written from `SetServerPreviewData` (documented as possibly running on vanilla's async ping thread)
  and read from `ServerSlotQueue.ResolveServerName`'s worker path → corruption/throw.
  **Fix:** `ConcurrentDictionary` or lock.
- `src/serverbrowser/ServerSlotQueue.cs:311-343` — the queue wedges in "CONNECTING" if a join raises
  neither connect nor rejection. **Fix:** timeout that clears `_joining` after N seconds.
- `src/serverbrowser/ServerPreviewCachePatches.cs:672-678` — `_lastFlush` DateTime read/written across
  threads unsynchronized. **Fix:** guard with the existing `_staleLock` / `Volatile`.

### 1.9 MED — Naive substring team/role bucketing can mis-tag fields
`src/presets/ProfileTeamTools.cs:59-67` and `src/presets/PresetFieldRegistry.cs:180-203` — `BaseKey`
blanket-`Replace("red","")`/`Replace("blue","")` and `Contains("red")` mangle any field whose name
contains those substrings; `Validate()` only checks missing swap partners, not `BaseKey` collisions
(the "Personal" tape collision is already flagged in `docs/presets-backlog.md`).
**Fix:** derive tokens from explicit boundaries/attribute metadata; add a `BaseKey` collision check to
`Validate()`.

### 1.10 MED — Steam ticket never retried; whole session fails silently
`src/api/AppearanceAPI.cs:228-239` — `RequestTicket` sets `ticketRequested = true` before the async
callback; if `SteamManager.IsInitialized` is false at Initialize time the ticket is never requested or
retried (only `Cleanup` resets the flag), so all POSTs/heartbeats silently fail for the session.
**Fix:** retry on a timer or when the first POST finds `cachedTicket == null`.

### 1.11 MED — Relative-path file writes
- `src/PatchClientChat.cs:54` — `/hierarchy` writes `hierarchy.txt` to the process CWD (the exact
  thing `ModSettings.GetConfigPath` documents avoiding), no try/catch.
- `src/diagnostics/DevConsole.cs:565-577` — `PersistRect` writes `Settings.Current` but never calls
  `Settings.Save()`.
**Fix:** resolve under `PathManager.GameRootFolder`, wrap in try/catch, and actually save.

### 1.12 MED — `Plugin.LogDebug` NRE risk during early init / dedicated server
`src/Plugin.cs:235` — dereferences `Plugin.modSettings.DebugLoggingModeEnabled` unconditionally. Today
`OnEnable` orders assignment before use, but any early caller (or the dedicated-server path where
modSettings is never loaded) throws.
**Fix:** `if (Plugin.modSettings?.DebugLoggingModeEnabled ?? false)`.

---

## 2. Resource / memory leaks

- **HIGH-ish — Instanced materials never destroyed.** `HatSwapper.cs:335-365` (`FixMaterials`
  instances every material; `Object.Destroy(hat)` frees the GameObject but not the materials) and
  `GenderSwapper.cs:272` leak a Material set per apply/removal cycle. `SkyboxSwapper.cs:27-35` is the
  one correct model (destroys its instance). **Fix:** destroy instanced materials on removal or
  cache/reuse them.
- **MED — Font atlases leak.** `src/ui/UnicodeFontFallback.cs:142-171` — `Disable()` removes fallbacks
  but never `Object.Destroy()`s the created `TMP_FontAsset`/atlas textures; toggling leaks atlases.
- **MED — TCPClient leaked on exception paths.** `social/BetterFriendsList.cs:1227-1270`
  (`ServerNameFetcher.PingServer`) and `social/probe/ProbePinger.cs:97-128` (`PingOne`) create a
  `TCPClient` without `using`/`try-finally`; `Disconnect()` only runs inside `if (IsConnected)`, so a
  throw before/at `Connect()` leaks the socket + `ManualResetEventSlim`. **Fix:** `try/finally` dispose.
- **MED — Unbounded growth.** `src/ui/VanillaUIRetheme.cs:45,88-96` — `hookedRoots` never prunes
  destroyed `UIDocument` roots, pinning dead visual trees. **Fix:** prune null/detached panels or use
  weak refs / unhook on `DetachFromPanelEvent`.
- **MED — `TextureManager.cs:79-87`** — comment says "non-readable for performance" but `Apply(true)`
  keeps the CPU copy. **Fix:** `texture.Apply(true, true)` to drop the readable copy.
- **LOW — `HatSwapper.cs:377-424`** — `originalHeadScales` keyed by Transform is never pruned;
  removing a hat never un-scales the head (big-head stays after removal — a UX bug too).
- **LOW — Pervasive `renderer.material` instancing** across swappers; acceptable for scene objects but
  worth one shared, commented helper acknowledging lifetime.

---

## 3. Duplication & missing shared abstractions

These overlap with `REFACTOR_REMAINING.md`; consolidated here with concrete call-outs.

- **MED — Equipment-swapper base class.** `GoalieEquipmentSwapper`, `GoalieHelmetSwapper`,
  `SkaterHelmetSwapper` (and partly `JerseySwapper`) are structurally identical: `(team,player,part)`
  snapshot cache, `ClearCache`, capture→apply-or-restore, per-team-player loops, and one-line
  `On*Changed` forwarders. `SwapperManager`'s forwarders add a third pure pass-through layer. **Fix:**
  extract `SnapshotTextureSwapper` (or `PlayerPartSwapper`); also resolves the restore-on-load-failure
  inconsistency (`GoalieHelmetSwapper.cs:93-98` doesn't restore, others do).
- **MED — Renderer-name classification duplicated & fragile.** `ChangingRoomPatcher` (`:172-266`) and
  `ChangingRoomHelper.ApplyHelmetColors` (`:198-261`) both re-walk `PlayerHead` children with
  `name.ToLower().Contains("helmet"/"goalie"/"cage"/"neck")`. A game rename breaks both. `ToLower()`
  allocates per renderer and is culture-sensitive. **Fix:** one shared classifier enum
  (GoalieShell/Cage/NeckGuard/SkaterHelmet) using `StringComparison.OrdinalIgnoreCase`.
- **MED — Section registry duplicated in three places.** `ReskinManagerMenu.cs` — a new section must be
  added to `builtinSections` (`:27`), `sidebarLayout` (`:83`), and a 25-case switch (`:406`), all
  string-matched by display name; a miss silently yields "section does not exist." **Fix:** one ordered
  table `(name, group, Action<VisualElement>)` driving all three.
- **MED — Blue/red branch duplication** in `GoaliesSection.cs:142-179, 252-441` (identical except the
  profile field + callback, with a tautological `team=="blue"?Blue:Red` inside a known branch). **Fix:**
  parametrize with getter/setter delegates — strongest case for migrating Skaters/Goalies/Tapes onto
  the `PresetFieldRegistry` renderer that `PlayersSection` already uses.
- **MED — `StylePopover` duplicated** near line-for-line: `VanillaUIRetheme.cs:165-219` vs
  `UITools.cs:134-188`. `SetColorSliderEnabled` copy-pasted in `SkatersSection` + `GoaliesSection`
  (both reach into `CreateColorConfigurationRow` internals by child index `[1]`). **Fix:** single shared
  implementations in `UITools`; have the color row return a handle exposing `SetEnabled`.
- **LOW — Smaller dups:** four identical `On<Team><Role>TapeChanged` handlers
  (`StickTapeSwapper.cs:392-430`); replay-local-player heuristic `OwnerClientId - 1337UL`
  (`SwapperManager.cs:62-63, 166-167`); `SetStickMeshTexture`/`SetStickTexture` ~80% identical
  (`StickSwapper.cs:17-100`); `Shader.Find("Universal Render Pipeline/Lit")` per-apply
  (`StickSwapper`/`StickTapeSwapper`) vs `HatSwapper`'s cached lookup; two `locker_room` blocks in
  `SwapperManager.OnSceneLoaded` (`:202,:230`); `FormatBytes`×3, `NS_TO_MS`×3, `"_BaseMap"`×18, palette
  colors ×~20 files (per `REFACTOR_REMAINING.md`). **Fix:** shared helpers / `UIPalette` static.

---

## 4. Performance

- **MED — Arena full-scene scans.** `ArenaSwapper.SetAll` triggers `FindObjectsByType` many times per
  call (GameObject ×3, MeshRenderer ×6+); `CrowdManagerRegisterCrowdPosition` postfix (`:248-256`) runs
  `FindObjectsByType<CrowdMember>` on *every* crowd registration → O(n²) on load. **Fix:** one scan +
  dispatch, cache a renderer-by-material index per scene, debounce the crowd update.
- **MED — ModMenuEnhancer synchronous disk work on the UI thread.** `GetSizeBytes` walks the whole mod
  tree and `IsOutdatedMod` reads the entire .dll into memory during row render; `IsResourcePack` does
  `File.Exists` per entry on *every* `ApplyFilters` (i.e. per search keystroke). **Fix:** cache
  resource-pack flag per path; defer size/dll scans off-frame.
- **LOW — Debug string allocation in hot paths.** Interpolated `LogDebug($"...")` strings are built even
  when debug logging is off (StickTapeSwapper builds ~25 per tape apply). **Fix:** guard behind
  `if (Plugin.DebugEnabled)` or make `LogDebug` lazy.
- **LOW — `DevConsole.RefreshLogList`** rebuilds up to 600 Labels per tailed line / keystroke;
  `PositionSelectFreeLook.cs:315` allocates a `GUIStyle` every OnGUI. **Fix:** incremental append; cache
  the style. `GetHeadgearRenderer` called 3× in `GoalieHelmetSwapper.cs:135-190` — scan once.

---

## 5. Harmony patch fragility

- **MED — String-literal gating.** `SwapperManager.cs:324` keys on exact `"LOOKING FOR A MATCH..."`;
  `PuckFXSwapper.cs:415` on `StartsWith("PHL Official")` — any wording/localization change silently
  disables the feature. **Fix:** looser match or phase enum.
- **MED — `FullArenaSwapper.cs:76`** — `Assembly.LoadFrom(sceneryChangerPath)` can load a *second copy*
  of SceneryChanger.dll, producing duplicate types. **Fix:** search
  `AppDomain.CurrentDomain.GetAssemblies()` first, LoadFrom only as fallback.
- **LOW — Broad `UIView.Show`/`Hide` hooks** in `TeamColorSwapper.cs:292-344`, `BetterFriendsList.cs:64`
  fire for every view transition to catch one view. Works but heavy; note it.
- **LOW — Chat postfix pile-up.** Four separate postfixes on `UIChat.AddChatMessage`
  (`HideInactiveChat`, `SelectableChat`, `NumberedNames`, `UiTextShadow`) each re-walk the row; only
  `NumberedNames` sets an explicit priority. **Fix:** consolidate behind one dispatching hook.

---

## 6. Error handling / logging

- **MED — Stack traces discarded.** ~131 of 275 catch blocks log only `ex.Message` (swappers, Plugin,
  ChangingRoomHelper, AppearanceAPI, PartyLineup). Field reports become undiagnosable.
  **Fix:** log full `e`; expected-and-ignorable → `LogDebug`, else `LogError` with stack. Standardize.
- **LOW — Guaranteed error spam.** `IceSwapper.cs:20,64` LogErrors when "Ice Bottom"/"Ice Top" aren't
  found — but `SetAll` runs every scene including locker room where they legitimately don't exist.
  **Fix:** downgrade to `LogDebug` or gate by scene.
- **LOW — `WorkshopUpdateChecker.cs:239-256`** — `TriggerDownload` overwrites the callback if the same
  item is re-requested; the first caller's button hangs on "Downloading...". **Fix:** reject/chain.
- **LOW — `ModSettings.cs:46,59`** — config *writes* aren't try/catch-wrapped (reads are).

---

## 7. Dead code / cleanup

- `src/ui/sections/FullArenaSection.cs` — **222 lines unreachable** (not in `builtinSections`,
  `sidebarLayout`, or the switch; has its own stale-status bugs). `REFACTOR_REMAINING.md` says keep it
  (owner's call) — if so, wire it in; otherwise delete.
- `ModMenuEnhancer.cs:1679-1717` `ShrinkStatistics` (no callers); `:585-598` `batchStatusLabel`
  (write-only).
- `ReskinProfileManager.cs:1081-1107` `SerializableColor` (superseded by `ColorJsonConverter`);
  `:361-366` commented-out block; ~25 "moved to QoL profile" tombstone comments; stale `currentProfile`
  TODO (`:19`).
- `FullArenaSwapper.cs:20-22` `requestUnloadMethod` (`#pragma`-suppressed, never assigned); `IceSwapper`
  `UpdateIceSmoothness` returns an unused bool.
- `AppearanceAPI.cs:291` `initialFetchDone` (write-only, `#pragma`-suppressed); `PartyLineup.cs:576`
  `GetPath` unused; `PuckPreview.TryCaptureAssets` (verify still wired).
- UI dead fields: `ReskinManagerMenu.cs:21` / `ReskinManagerMenuAccessButtons.cs:123` `uiMainMenu`;
  `ArenaSection.cs:10-12` reflection FieldInfo; `PucksSection.cs:208` unused param + stale
  `_puckDropdown`; `PuckFXSection.cs:280-284` empty `SyncGameSettingsUI()`; `ReskinManagerMenu.cs:144`
  dead `IsInMainMenu()` condition in an `else` that already excludes it.
- `MinimapSwapper.cs:132-156` puck-tint recursion is effectively dead (B1117 puck is painter-drawn).
- **Branding residue from another mod's lineage:** `DevConsole` prints "PoncePlayerInput"/"PlayerQoL",
  uses "PPKB_"/"[QoL]" prefixes; class names `QuickChatPlusSettingsCloseButtonClickHandler`,
  `MainMenuOpenReskinManagerClickHandler`. Rename to TRL.

---

## 8. Naming / organization

- `UISection` reads like a base class but is the concrete HUD/Display section → rename
  `HudSection`/`DisplaySection`.
- `SkaterSection` (singular) lives in `SkatersSection.cs` (plural).
- **God classes** (each its own PR): `ModMenuEnhancer.cs` (~1750), `BetterFriendsList.cs` (~1380 —
  lifecycle + 5 patches + presence reader + UI + hand-rolled JSON parser + TCP client + main-thread
  dispatcher; the `ExtractJsonString/Int` parser duplicates `System.Text.Json` used elsewhere),
  `FrameProfilerOverlay.cs` (~1120).
- **Global mutable state:** `ReskinProfileManager.currentProfile` reached into ~579×;
  `ReskinRegistry.reskinPacks` public non-readonly `List`. **Fix:** at minimum `readonly` +
  `IReadOnlyList`.
- **Reflection-drift risk:** `ReskinProfileManager.GetAllActiveReskinEntries` (`:382-434`) is a hand-
  maintained ~40-field list; a missed field means its texture isn't retained and silently evicts.
  Drive it by reflection over `ReskinEntry` fields.

---

## 9. UX / feature opportunities

- **Multiple named profiles.** The `currentProfile` TODO plus the whole Presets subsystem show users
  want more than one saved look — a named-profile switcher is the natural feature.
- **Consolidate the two overlapping editors.** Skaters/Goalies/Sticks/Tapes *and* the newer Players
  2×2 grid edit the same profile fields. Retiring the legacy sections onto the `PresetFieldRegistry`
  removes both duplication and user confusion.
- **Search/filter box** in the menu — with 25 sections and hundreds of rows, finding e.g. "number
  outline" requires knowing which section owns it.
- **Color pickers:** add a hex field + preset swatches (generalize `PlayerCustomizationSection`'s
  swatch UI) + per-row reset-to-default. RGB 0–255 sliders only today.
- **"Saved" feedback:** most controls apply instantly but nothing confirms it — a transient tick on
  section headers (reuse the Presets Toast) builds trust, especially for tape-mode dropdowns whose
  effect isn't visible without a preview.
- **Pack/preset thumbnails:** `PacksSection` lists reskin names with no preview even though
  `TextureManager.GetTexture` + the Presets `BuildRefPreview` thumbnail pattern exist — reuse for
  discoverability.
- **Escape handling:** in the Presets save view / rename, Escape closes the whole menu (Harmony prefix)
  and discards silently — consider Escape backing out to the list first; wire Enter to Save.
- **Offline indicator:** `BackendReachable` is tracked but never surfaced; when puckstats.io is down,
  hats/XP silently stop working.
- **Accurate party lineup:** `PartyLineup` randomizes members' cosmetics because real per-member data
  isn't fetched; `AppearanceAPI` already returns body/skin/hat — fetch it for accuracy.
- **Team + personal tape split** (`docs/presets-backlog.md`) — fully specced, not built; the 24 tape
  fields are team-wide-labeled but applied local-only. Ready to pick up.
- **Finish or gate BetterFriendsList server-name lookup** (`:18-28`) — the whole `ServerNameFetcher`
  machinery populates a detail line that never resolves.

---

*Generated from a four-part parallel review. The HIGH items in §1 and the leaks in §2 are the
highest-value first sessions; §3 and §8 track the larger refactors already partially scoped in
`REFACTOR_REMAINING.md`.*
