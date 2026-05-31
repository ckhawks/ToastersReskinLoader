# Reskin / Presets — Backlog

Deferred work, with enough detail to pick up cold.

## Team + personal tape (split tape like sticks)

**Status:** deferred (design agreed, not built).

### Goal
Make tape behave like sticks: a **team tape** that all players on a team/role show, and a
**personal tape** that only the local player shows — independent of each other.

### Current behavior (for context)
- Tape data is stored per `(team, role)` (e.g. `blueSkaterBladeTapeMode/Tape/Color`,
  `blueSkaterShaftTape...`), 6 fields per cell, 24 total — but it is **applied only to the
  local player** in game:
  - `SwapperManager.StickApplyCustomizationsPatch` calls `StickTapeSwapper.SetStickTapeForPlayer`
    only when `IsLocalPlayer || isReplayLocal`.
  - `StickTapeSwapper.On{Blue,Red}{Skater,Goalie}TapeChanged` only touch the local player's stick.
- `SetStickTapeForPlayer(stick)` is already generic (reads team/role off the player), so the
  painter supports any player; only the call sites are gated.
- Sticks, by contrast: team stick applies to all teammates on spawn; personal stick applies to
  the local player. See `SwapperManager.SetStickReskinForPlayer`.

### Agreed design
- The **existing 24 tape fields become the TEAM tape** (applied to all players on that
  team/role, like jerseys).
- Add a parallel **personal** tape set (24 new fields): `{team}{role}{Blade,Shaft}Tape`
  `Personal{Mode,,Color}` — i.e. personal mode + texture + color for blade and shaft, per cell.
- **Personal-unset semantic: vanilla, exactly like sticks.** If personal tape mode is
  "Unchanged", the local player's stick shows no tape customization — it does NOT inherit the
  team tape. (Confirmed choice.)
- Application, mirroring sticks:
  - Spawn hook applies tape to **all** players (team tape) — drop the `IsLocalPlayer` gate in
    `StickApplyCustomizationsPatch`.
  - Local player additionally gets **personal** tape (overrides team for their own stick).
  - Change handlers: a team-tape change updates all players on that team/role (loop like
    `OnBlueJerseyChanged`); a personal-tape change updates only the local player's stick.

### Implementation checklist
1. **Profile** (`ReskinProfileManager.Profile`): add 24 personal fields with `[PresetField]`
   tags (group "Tape", role auto-derives, team auto-derives). Suggested ids:
   `{blue,red}{Skater,Goalie}{Blade,Shaft}TapePersonalMode` (string),
   `...TapePersonal` (ReskinEntry, ReskinType `tape_{attacker,goalie}_{blade,shaft}`),
   `...TapePersonalColor` (Color).
   - NOTE: base-key copy in `ProfileTeamTools` must keep team/personal distinct — verify
     `BaseKey` keeps "Personal" so personal doesn't collide with team tape (it does today,
     since only team/role tokens are stripped).
2. **SerializableProfile** + **Load()** + **Save()**: 24 new entries each (manual persistence;
   the registry doesn't drive the main profile yet — see Phase 6 in presets-system-design.md).
   Additive only — existing `reskinprofile.json` stays valid.
3. **StickTapeSwapper**:
   - `GetTapeSettings(team, role, part)` → add an `isPersonal` selector (or a parallel
     `GetPersonalTapeSettings`).
   - `SetStickTapeForPlayer`: apply team tape; if the stick's player is local, apply personal
     tape on top (personal overriding, vanilla when unset).
   - Add `On{Blue,Red}{Skater,Goalie}TapeChanged` to loop all team/role players (team tape) and
     keep/extend a personal-tape change path for the local stick.
4. **SwapperManager.StickApplyCustomizationsPatch**: apply tape to all players (remove the
   local-only gate), so teammates get the team tape.
5. **UI (`PlayersSection`)**: team tape stays under "Stick & tape"; personal tape moves to "Your
   stick (personal)" next to the personal stick. The generic tape renderer already exists
   (`RenderTapeControl`); add a personal variant that targets the personal fields.
6. **Presets**: the 24 new fields flow into presets automatically via `[PresetField]`. Sanity
   check the save tree and team-swap still behave (personal tape is team-scoped + role-scoped).

### Why deferred
Large (24 persisted fields + swapper rework + UI), and lower impact than other pending work.
Tape currently reads as team-wide in the editor; revisit the label if this stays deferred long.
