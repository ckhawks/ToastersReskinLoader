# Agent-Verifiability Audit — Toaster's Reskin Loader

**Subject:** ~37,000 LOC C# BepInEx/Harmony client-side mod for the Unity game *Puck*. ~150 source files, single project, targeting `net4.8`. **No test project, no CI, no `.github/` — zero automated verification exists today.**

The dominant architectural pattern is *runtime monkey-patching of a closed-source game*: 39 `[HarmonyPatch]` classes, 49 files using reflection (`AccessTools`/`GetField`/`Traverse`) to reach private game internals, ~311 `public static` mutable members, and only 7 `enum` declarations across the whole codebase. This shape drives every score below.

> This is an "agent-verifiability" assessment: how well the architecture supports safe, automated verification of code changes — independent of who or what wrote them. Ratings are Low / Medium / High.

---

## 1. Locality of Effect — **Low**

Blast radius is large and hard to bound because the system is a web of `public static` manager classes that call each other directly. `SwapperManager.SetAll()` (`src/swappers/SwapperManager.cs:278`) fans out to ~25 static calls across `IceSwapper`, `ArenaSwapper`, `SkyboxSwapper`, `PuckFXSwapper`, etc., each of which reads global `ReskinProfileManager.currentProfile` and mutates live Unity materials via `GameObject.Find(...)`. There is no "unit of change" with a provably bounded scope: touching a field on `SettingsConfig` can ripple into `Plugin.OnEnable`'s 40-line init sequence (`src/Plugin.cs:106-141`), a UI section, a Harmony patch, and a swapper simultaneously.

**Example:** `Plugin.OnEnable()` hard-codes a strict ordering dependency (comments at `Plugin.cs:67-88` warn that `DisplaySettingsMigration.Run()` *must* precede `LoadProfile`, which *must* precede `PuckFXMigrator`). This implicit ordering is enforced only by comments — reordering two lines silently corrupts saved profiles.

---

## 2. Separation of Pure Logic from Effects — **Low**

There is almost no boundary. Effects (Unity material mutation, `GameObject.Find`, file I/O, singletons) are interleaved directly into every logic path. `IceSwapper.SetIceTexture()` (`src/swappers/IceSwapper.cs:10`) reads global profile state, does a scene lookup, caches an original texture in a static field, and writes to a live material — all in one method with no seam to test the decision logic independently. Realistically **well under 5%** of core logic is testable without mocking Unity, the filesystem, or the game's singletons.

The rare pure pieces prove the point by contrast: `PresetFieldRegistry.SwapBlueRed()` / `ResolveTeam()` (`src/presets/PresetFieldRegistry.cs:173-203`) are genuinely pure string transforms — deterministic, no effects — and are the only obviously unit-testable logic found. `ColorJsonConverter` and parts of the preset registry are similar islands.

**Example:** `SwapperManager.SetStickReskinForPlayer` (`src/swappers/SwapperManager.cs:52`) embeds real business logic (team/role/replay-offset resolution — note the `OwnerClientId - 1337UL` replay heuristic at line 63) directly against live `Player` objects, so the interesting branching can't be exercised without a running game.

---

## 3. State Model Explicitness — **Low**

State is almost entirely implicit and ad hoc. `SettingsConfig` (`src/core/SettingsConfig.cs`) is a flat bag of ~90 loosely-related primitive fields (`bool enableEscCloseMenus`, `string minimapRotationMode = "off"`, dictionaries, floats) with no grouping types, no invariants, and stringly-typed modes. `minimapRotationMode` is a `string` with three magic values documented only in a comment (`SettingsConfig.cs:42-45`) — an ideal case for a discriminated enum, left as free text. Reskin selection is dispatched via nested `string type`/`string slot` switch statements (`ReskinProfileManager.cs:24-120`) rather than typed keys; a typo in `"stick_attacker"` fails silently.

There is no state machine anywhere for the load/scene/appearance lifecycle; the valid states must be reconstructed by reading `Plugin.OnEnable`, `SwapperManager.OnSceneLoaded`, and scattered `Clear*Cache()` calls. Invalid states are freely representable — the type system prevents essentially nothing.

**Example:** the whole reskin routing in `SetSelectedReskinInCurrentProfile` uses `if (type == "jersey_torso") { switch (slot) { case "blue_skater": ... } }` — an unenumerable, convention-only state space.

---

## 4. Determinism & Reproducibility — **Low**

The logic is fundamentally coupled to non-deterministic, real-infrastructure inputs and **could not run in a replay harness without a live game**. Sources of non-determinism are pervasive and *not* isolated behind a boundary: `GameObject.Find("Ice Bottom")` depends on live scene graph state; `MonoBehaviourSingleton<UIManager>.Instance` and `PlayerManager.Instance` depend on game bootstrap timing; frame-tick sampling (`PlayerInputUpdatePatch` counts frames, `src/swappers/SwapperManager.cs:136`); coroutines with `yield return null` frame delays (`ReapplyLocalAppearanceAfterDelay`, `src/swappers/SwapperManager.cs:238`); plus network/Steam calls in `AppearanceAPI`, `BeaconPing`, and the server browser. `DateTime`/`Random`/`Time.*` appear 17 times, unencapsulated.

**Example:** `TickDriver.Update()` (`src/core/TickDriver.cs:38`) branches on `Keyboard.current.escapeKey.wasPressedThisFrame`, live menu display state, and per-frame `ScoreboardPolish.Tick()` interpolation — behavior is inseparable from wall-clock frame timing.

---

## 5. Testability Surface — **Low**

There is no test suite at all (the one filename matching `*spec*`, `SpectatorMinimap.cs`, is a HUD feature, not a test). Given the effect-saturation above, the coverable-by-property-test fraction is tiny — limited to the pure registry/converter helpers. Natural invariants exist but are **enforced nowhere at compile time**: e.g. `PresetFieldRegistry.Validate()` (`src/presets/PresetFieldRegistry.cs:222`) checks reskin-ref/team-partner consistency, but only at *runtime* and only by emitting `Plugin.LogWarning` — a broken annotation logs a warning and keeps running rather than failing a build. The prevailing error strategy is 327 `catch` blocks, many swallowing (`catch { }`), which actively converts bugs into silent no-ops (`Plugin.cs:177-179`, `TickDriver.cs:34`).

**Example:** `PresetFieldRegistry` is the codebase's one good testability story — a reflection-driven registry with an explicit self-check — yet even it degrades to a log line instead of an assertion or typed guarantee.

---

## 6. Change Safety — **Low**

An agent editing with imperfect system understanding is very likely to silently break something outside any coverage, because (a) there is no coverage, and (b) the compiler catches almost nothing meaningful. Correctness depends on: reflection strings resolving against game internals (`GetField("matchingPhaseLabel", ...)`, `src/swappers/SwapperManager.cs:319`) that fail only at runtime on a specific game version; Harmony patch signatures matching game methods that aren't in this repo; magic string modes; and the fragile init ordering in `Plugin.OnEnable`. None of these are type-level guarantees. The 327 catch blocks mean even runtime failures frequently don't surface — a wrong change can "pass" simply by throwing into a swallowed handler.

**Example:** renaming or retyping a `Profile` field is a compile-time-clean change that breaks JSON profile round-tripping, preset save/apply (reflection over field names, `src/presets/PresetFieldRegistry.cs:153`), and the blue↔red swap (`SwapBlueRed` name mangling) — all discoverable only by running the game and inspecting saved files.

---

## 7. Human-Judgment Surface — **Low separation**

This is UI/game-feel-heavy (retextures, HUD polish, minimap colors, clock color ramps, chat fade), so subjective judgment is unavoidable — but it is **not sandboxed** from underlying logic. Objectively-checkable concerns (does a texture load, does a color parse) are interleaved with subjective ones (does the clock's amber→red ramp *feel* right, `SettingsConfig.cs:121-124`) in the same swapper/UI methods that also perform live material mutation. Tweaking a visual constant means editing a file that also drives real Unity state, so an aesthetic change can throw and be swallowed, or mutate a shared material other features depend on. There is no design-token layer, no snapshot boundary, no separation of "presentation values" from "presentation effects."

**Example:** `GlossSwapper`, `CrispyShadowsSwapper`, and `MinimapSwapper` mix render-pipeline mutation with per-user cosmetic parameters in the same static methods — you cannot adjust the subjective layer without touching the effect layer.

---

## Summary of Ratings

| Dimension | Rating |
| --- | --- |
| 1. Locality of Effect | Low |
| 2. Separation of Pure Logic from Effects | Low |
| 3. State Model Explicitness | Low |
| 4. Determinism & Reproducibility | Low |
| 5. Testability Surface | Low |
| 6. Change Safety | Low |
| 7. Human-Judgment Surface (separation) | Low |

---

## Top 3 Structural Risks

1. **Reflection/Harmony coupling to an out-of-repo, versioned game (invisible to the compiler).** 39 patch classes and 49 reflection-using files bind to private `Puck` internals via string names and method signatures that this repo cannot see. An agent has no compile-time or test-time signal that a patch target or `GetField("...")` still exists — breakage is silent until a user runs a specific game build.

2. **Pervasive global mutable state with implicit ordering.** ~311 static members, a single global `ReskinProfileManager.currentProfile`, and a hand-ordered 40-line `OnEnable` init whose correctness lives in comments (`Plugin.cs:67-88`). Any change can ripple through unrelated features, and the 327 (often-swallowing) catch blocks hide the resulting failures.

3. **Stringly-typed, unenumerable state model.** Modes, reskin types, and slots are magic strings dispatched through nested switches (`ReskinProfileManager.cs`, `SettingsConfig.minimapRotationMode`). Invalid states are representable, typos fail silently, and the full valid-state space can't be enumerated without reading scattered code — so an agent can't reason about completeness of a change.

## Top 3 Highest-Leverage Refactors (ordered by effort-to-impact)

1. **Add a build + smoke-test CI and a minimal test project first (lowest effort, highest immediate leverage).** There is *zero* automated gate today. A GitHub Actions workflow that just compiles against `libs/` plus an xUnit project targeting the already-pure islands (`PresetFieldRegistry` string logic, `ColorJsonConverter`, profile JSON round-trip, `SwapBlueRed`) would immediately catch a whole class of rename/serialization regressions and give agents a signal to run.

2. **Replace magic strings with enums/discriminated unions for reskin type/slot and setting modes (moderate effort, broad correctness impact).** Turning `minimapRotationMode` and the `type`/`slot` dispatch into enums makes invalid states unrepresentable, lets the compiler enforce switch exhaustiveness, and shrinks the human-judgment/logic confusion — directly improving dimensions 3, 5, and 6.

3. **Extract pure decision cores from effectful swappers behind a thin seam (higher effort, structural payoff).** For the swappers, split "which texture/color should apply given profile + player state" (pure, testable) from "find the GameObject and mutate the material" (effect). Even doing this for the high-traffic paths (`SetStickReskinForPlayer`, `SetAll` selection logic) would move a meaningful fraction of logic into property-testable territory and shrink blast radius, addressing dimensions 1, 2, and 4 at once.

---

*Read-only audit — no production code was changed. Line references are to the state of `src/` at the time of writing.*
