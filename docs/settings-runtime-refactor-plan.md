# Plan — dissolve `SettingsRunner`, collapse config, add feature registry

Status: **Phase 1 done & committed** (`Settings` holder; config removed from the runner).
Registry phase **dropped**. Remaining: Phase 2 (config collapse) + Phase 3 (dissolve runner).
Companion to the "Planned refactor" section in `feature-inventory.md`.

## Goal

`SettingsRunner` (the renamed `QoLRunner`) is a god-object MonoBehaviour conflating four
jobs; only the per-frame tick truly needs a MonoBehaviour. Dissolve it into the right homes,
collapse the twin config classes, and replace the hand-wired `Plugin.OnEnable` feature blocks
with a registry.

### Current state (measured)
- `SettingsRunner.Instance` referenced **110×** across **46 files**; **77** are
  `SettingsRunner.Instance?.Config` reads.
- `SettingsConfig` (in-memory fields) + `SettingsProfile` (`[JsonProperty]` on-disk shape) are
  twins joined by `ToConfig()`/`FromConfig()` — **161** hand-written mapping lines. Every setting
  is typed 4×.
- `Plugin.OnEnable` has **8** `if (Config?.enableX ?? default) Feature.Enable()` blocks;
  `OnDisable` has a hand-maintained subset (already divergent).
- `SettingsRunner.Awake` bootstraps a batch: `DevConsole.AttachTo`,
  `PositionSelectFreeLook.AttachTo` (MonoBehaviours), and `.Initialize()` on
  `SavedServerPasswords`, `ServerSlotQueue`, `MainMenuButtons`, `ServerBrowserSort`,
  `UiTextShadow`.
- `SettingsRunner.Update` does exactly two things: `ScoreboardPolish.Tick()` and an ESC poll →
  `EscClosesMenus.TryCloseTopmostSecondaryMenu()`.
- Callbacks on the runner: `SaveAndRefresh()` (= `SettingsStorage.Save`), `ReloadFromProfile()`,
  `SendChatMessage()`, and `SaveConfigsAndRefresh()`/`DoReload()` (DevConsole calls these).

> Note: `SaveAndRefresh()` only *saves* — the "refresh"/apply is done by each UI callback
> itself (e.g. `ChatSection.ApplyChatHeight`). So `Settings.Save()` is a faithful replacement.

---

## Phased plan (each phase compiles green; its own commit[s])

### Phase 1 — Static `Settings` holder (mechanical, low risk)

Extract config ownership out of the MonoBehaviour.

- **New** `core/Settings.cs`: `static class Settings { static SettingsConfig Current; Load(); Save(); Reload(); }`
  wrapping `SettingsStorage`.
- Replace `SettingsRunner.Instance?.Config` → `Settings.Current` (77 sites), and
  `SettingsRunner.Instance?.SaveAndRefresh()` → `Settings.Save()`.
- `SettingsRunner` keeps `Awake`/`Update` for now but reads `Settings.Current` instead of owning
  `_config`. DevConsole's `SaveConfigsAndRefresh`/`DoReload` repoint to `Settings`.
- **Risk:** low — near-pure find/replace; compiler verifies. **Verify:** build + open menu, toggle
  a setting, confirm it persists to `…QoL.json`.

### Phase 2 — Collapse `SettingsConfig` + `SettingsProfile` (medium risk, isolated to `core/`)

Make `SettingsConfig` the serializable shape; delete `SettingsProfile` + the 161-line mirror.
Same move PR #19 made for `Profile`/`SerializableProfile`.

- Put Newtonsoft `[JsonProperty("…")]` on `SettingsConfig` fields. **Critical:** preserve the exact
  on-disk keys, including the deliberately-renamed migration keys (`enableServerPreviewCacheV2`,
  `enableFastServerBrowserScanningV2`, `enableMainMenuQuickJoinV2`, `enableScoreboardMillisecondsV2`,
  `enableChatNoFadeV2`) — these MUST stay or existing saves silently change defaults.
- Handle `Color` ↔ JSON: add a `JsonConverter` (or contract resolver) so `Color` fields serialize
  like the old `SerializableColor` did. Verify round-trip byte-compatibility against a real
  `…QoL.json`.
- `SettingsStorage` serializes/deserializes `SettingsConfig` directly. `ServerPrefsProfile`
  (passwords/favorites/blocks/trusted, separate file) is **unchanged**.
- **Risk:** medium — JSON back-compat. Mitigation: write a tiny round-trip test against a saved
  file before/after; diff the output. **Verify:** load an existing profile, confirm no keys lost
  and values identical.

### ~~Phase 3 — Feature registry~~ — DROPPED

Decided against. A registry only pays off when it drives the UI (ruled out — we hand-built the
topic pages) or at large scale. Only ~14 features have explicit `Enable/Disable/Initialize`
lifecycle (the rest are Harmony-patch-only and self-gate on their config field); a registry for 14
is more indirection than the plain wiring it replaces. The one real benefit — symmetric
enable/disable — is just "make `OnDisable` mirror `OnEnable`," done by hand during Phase 3 below.

### Phase 3 (was 4) — Tick driver + dissolve the runner (low/medium risk)

Now the runner has nothing left but the tick.

- **New** small `core/TickDriver.cs` MonoBehaviour whose `Update()` calls the per-frame work:
  `ScoreboardPolish.Tick()` and the ESC poll. (Or: register tickers via the registry —
  `IFeature.Tick()` optional — but a 2-item driver is simpler; pick during impl.)
- Move `SendChatMessage` to a small chat helper (it just calls `ChatManager`); repoint DevConsole.
- `DevConsole` / `PositionSelectFreeLook` attach to their own GameObject (or the TickDriver's).
- **Delete `SettingsRunner`.** `Bootstrap()`/`Teardown()` callers in `Plugin` switch to
  `Settings.Load()` + `FeatureRegistry.EnableAll()` + `TickDriver` creation.
- **Risk:** low/medium — make sure the ESC poll and `ScoreboardPolish` still fire every frame and
  that teardown (`OnDisable`) still cleans up. **Verify:** clock ms interpolation animates; ESC
  closes secondary menus; disabling the mod tears down cleanly.

---

## Open decisions (need your call before/at impl)

1. **Names:** `Settings` / `Settings.Current` for the static holder? `TickDriver` for the
   MonoBehaviour? `FeatureRegistry`?
2. **Registry scope:** include the Harmony-patch-only features as no-op entries (uniform list), or
   keep the registry to the `A`/`B`-lifecycle features only and leave patch-gated ones out?
3. **Tick model:** dedicated 2-line `TickDriver`, or generalize to `IFeature.Tick()`?
4. **Phase 2 timing:** do the config collapse now (medium JSON risk) or defer it and ship 1/3/4
   first? It's independent.

## Sequencing / risk summary

| Phase | What | Risk | Independent? |
|---|---|---|---|
| 1 | static `Settings` holder | low (mechanical) | enables 3/4 |
| 2 | collapse config twins | medium (JSON compat) | yes — can defer |
| 3 | feature registry | medium (ordering) | needs 1 |
| 4 | tick driver + delete runner | low/med | needs 1, 3 |

Recommended order: **1 → 3 → 4 → 2** (do the runner dissolve first; slot the config collapse in
whenever, since it's isolated and the riskiest). Or **1 → 2 → 3 → 4** if you'd rather clear the
config mirror early. Each phase is independently shippable and playtestable.
