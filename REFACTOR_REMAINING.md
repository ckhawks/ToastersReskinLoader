# Refactor — Remaining Work

Tracks what's left from `REFACTOR_REVIEW.md` after the first cleanup pass. Items are grouped by
effort/risk. Each is a candidate, not a commitment.

## Done (open PRs as of this writing)

- **#18 — cleanup + verified bug fixes:** deleted `PartyHatSwapper`/`TestArena`/`TestStick`;
  fixed config cwd path, gated `PatchClientChat`, `ServerNameFetcher` callback race, FrameProfiler
  reentrancy (`__state`), stale `ModMenuEnhancer` caches.
- **#19 — profile schema collapse:** `Profile` is now the on-disk shape via a contract resolver;
  deleted `SerializableProfile` + the hand-written load/save bodies (−559 lines). Fixes the
  `reskinType` round-trip TODO. Backward-compat proven against the real Newtonsoft.
- **#20 — dropdown dedup:** `UITools.AddReskinDropdownRow` + `ReskinRegistry.UnchangedEntry`/
  `GetReskinChoices`; converted Sticks/Skaters/Arena/Goalies (−381 lines).

Each of #19/#20 has an in-game checklist in its PR body (neither is fully verifiable offline).

---

## Deliberately NOT doing (verified false / out of scope)

- `UISection.CreateSliderRow` "persists wrong profile" — **false**; it double-saves but writes the
  correct profile. Not a bug.
- `PresetApplier.RefreshAll` hard-codes swappers — the *fact* is true but it's intentional
  complementary coverage, not a bug. (Could still consolidate into one `RefreshEverything()` — see
  below — but it's a smell, not a defect.)
- Deleting `FullArenaSection.cs` — **keep** (owner's call), even though it's currently undispatched.
- csproj hardcoded Steam copy path — **keep** as-is (owner's call).
- `PlayersSection` / `TapesSection` dropdowns — already use a separate, factored registry/callback
  pattern; folding them into `AddReskinDropdownRow` would change behavior.

---

## Remaining — low-risk quality dedup (no behavior change)

- **`FormatBytes` ×3** with disagreeing formats: `FrameProfilerOverlay.cs:~1214`,
  `FrameProfilerPatches.cs:~307`, `ModMenuEnhancer.cs:~251`. → one shared helper.
- **`NS_TO_MS` ×3:** `FrameProfilerOverlay.cs`, `FrameProfilerBuiltinMarkers.cs` (×2). → one const.
- **`"_BaseMap"` literal ×18**, only `PuckSwapper` caches `Shader.PropertyToID`. → shared cached id
  in `SwapperUtils`; route `IceSwapper`/`StickSwapper`/`ArenaSwapper`/`HatSwapper` through it.
- **`Shader.Find("Universal Render Pipeline/Lit")` per-apply** in `StickSwapper`/`StickTapeSwapper`;
  `HatSwapper` caches its own. → one shared `SwapperUtils.UrpLitShader`.
- **"Reset to default" button (×7)** and **"rebuild section in place" (×6)** in UI sections, ignoring
  `UITools.StyleConfigButton`. → `UITools.CreateResetButton` + `RebuildSection`.
- **`QoLRunner.Instance?.Config?.X ?? default` (~39×):** → a typed `QoLRunner.Cfg` accessor returning
  a non-null config with defaults applied.

## Remaining — lowest-priority correctness

- **`FrameProfilerNetwork` ring buffers** read on main thread / written on the RPC thread with no
  locks; `ConsumeAndResetFrameTickCount` is a non-atomic read-then-zero. Tearable but "usually fine";
  fixing properly means defining the thread-handoff contract. Low payoff.
- **Material/texture leaks:** `ChangingRoomHelper`/`ChangingRoomPatcher` touch `renderer.material`
  (instantiates) in preview-refresh paths without destroying; `FrameProfilerOverlay.colorTexCache`
  (`:~1157`) and `TextureManager` failed-`LoadImage` path (`:~79-95`) also leak.
- **Standardize exception logging:** 131 of 275 `catch` blocks log only `ex.Message`; level policy is
  inconsistent. → log full `e`; expected-and-ignorable → `LogDebug`, else `LogError` with stack.

## Remaining — bigger architectural refactors (each its own PR)

- **God-class splits:**
  - `swappers/ModMenuEnhancer.cs` (~1750 lines): mod abstraction + disk/reflection probing + UI
    builder + filter/sort state + workshop-update orchestration.
  - `qol/BetterFriendsList.cs` (~1180): lifecycle + 5 patches + presence reader + UI + TCP/JSON ping
    client + main-thread dispatcher.
  - `qol/FrameProfilerOverlay.cs` (~1120): collector + stats + graph rasterizer + 4-mode IMGUI
    renderer + texture manager + CSV IO.
- **`PlayerPartSwapper` base** for the copy-pasted equipment swappers (`GoalieHelmetSwapper`,
  `SkaterHelmetSwapper`, `GoalieEquipmentSwapper`, partly `JerseySwapper`): shared snapshot cache,
  cache-then-apply/restore, validate-player guard, per-team-player loop. Also resolves the
  restore-on-load-failure inconsistency. ~250 lines.
- **Stringly-typed identifiers → enums/consts:** reskin type/slot strings, filter/sort modes,
  renderer/child names. Centralize the team/role token logic in `PresetFieldRegistry`/
  `ProfileTeamTools` (substring matching like `name.Contains("Blue")` can mis-bucket; add a
  `Validate()` that no two fields collide on `BaseKey`).
- **`UITheme`/`UIColors` static** for the dark palette inlined ~150× (`0.25,0.25,0.25`, etc.) plus
  layout constants (`DropdownWidth = 400`, `SliderWidth = 300`).
- **`ReskinMenu` section registry:** replace the parallel `string[] sections` + `sidebarLayout` +
  18-arm `switch` (all synced on magic strings) with one `Dictionary<string, Action<VisualElement>>`.
- **`SwapperManager.RefreshEverything()`** shared by `PresetApplier.RefreshAll` and the Reload button,
  so new presetable swappers aren't a knowledge-leak.
- **Global mutable state:** `ReskinProfileManager.currentProfile` reached into ~579×;
  `ReskinRegistry.reskinPacks` is a public non-`readonly` `List`. At minimum make collections
  `readonly` + expose `IReadOnlyList`.

## Naming / misc

- `UISection` is the concrete HUD/Display section but reads like a base class → rename
  `HudSection`/`DisplaySection`.
- `SkaterSection` class (singular) lives in `SkatersSection.cs` (plural).
- Carry-over names from another mod's lineage: `QuickChatPlusSettingsCloseButtonClickHandler`,
  `MainMenuOpenReskinManagerClickHandler` (`ReskinMenu.cs`).
- ~25 "moved to QoL profile" tombstone comments in `ReskinProfileManager.cs` → one migration note.
