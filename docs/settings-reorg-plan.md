# Settings reorg — move personal/perf settings out of the reskin profile

Status: **implemented (stages A–G)** — shadows, gloss, minimap, chat, team indicator moved to
the QoL profile with one-time migration; team colors reworked to per-team enables and surfaced
in the Players editor; menu regrouped (HUD + Display). Not yet runtime-tested in-game.

Goal: keep the reskin profile (and therefore presets) to the shareable *look*. Personal,
performance, and HUD settings move to the QoL profile (`config/…QoL.json`, not shared). Team
colors stay in the reskin profile but become part of the per-team setup.

## Decisions (confirmed)

| Section | Fields | Action |
|---|---|---|
| Shadows | crispyShadowsEnabled, shadowResolution, shadowDistance, shadowCascadeCount, shadowSoftShadows | **→ QoL** (performance) |
| Minimap | blue/redMinimapNumberColor, minimapPuckColor, minimapPlayerScale, minimapPuckScale, minimapRefreshRate, localPlayerMinimapIconEnabled, blue/redLocalPlayerMinimapIconColor | **→ QoL** (HUD) |
| Chat | chatHeight, chatBackground, quickChatX, quickChatY, chatRenderAllEmojis | **→ QoL** (HUD) |
| Gloss | glossRemoverEnabled, glossSmoothness, glossAffectSticks/Players/Pucks | **→ QoL** (performance) |
| Team indicator | teamIndicatorEnabled | **→ QoL** (personal toggle) |
| Team colors | blueTeamColor, redTeamColor, blueTeamName, redTeamName | **STAY** in reskin profile, move into the per-team editor |
| Team colors enable | teamColorsEnabled (single toggle) | **REWORK** → per-team enable (see below) |
| Puck FX | all | **STAY** (shareable) |
| Arena | all | **STAY for now** (don't consolidate the toggles into QoL yet) |
| Player appearance / sticks / tape / puck / skybox | — | **STAY** (the look) |

## Team colors rework

- Team color + name belong to a team's identity, alongside the jersey. Surface them in the **2x2
  Players editor at team level** (Role-agnostic — shown for the team regardless of skater/goalie).
- Replace the single `teamColorsEnabled` with **per-team** enables: `blueTeamColorEnabled`,
  `redTeamColorEnabled`. A team's custom color applies only when its own toggle is on.
- Consumers that branch on `teamColorsEnabled` (`TeamColorSwapper`, `ArenaSwapper.UpdateGoalFrameColors`,
  `TeamIndicatorSwapper`, `EffectiveTeamColor` in PlayersSection, etc.) switch to the per-team flag.
- These stay in the reskin profile + presets (they're part of the shareable look).

## Per-moved-field mechanics (→ QoL)

1. Remove field from `Profile`, `SerializableProfile`, `Load()`, `Save()`; drop its `[PresetField]`
   tag (this removes it from presets automatically). Old keys left in existing `ReskinProfile.json`
   are harmlessly ignored by Newtonsoft and dropped on next save.
2. Add to `QoLConfig` (Config.cs) + `QoLProfile` (with `[JsonProperty]`) + `ToConfig`/`FromConfig`.
3. Rewire consumers to read `QoLRunner.Instance.Config.X` instead of
   `ReskinProfileManager.currentProfile.X`, and save via the QoL runner instead of `SaveProfile()`.
4. Reset helpers (`ResetShadowsToDefault`, `ResetMinimapToDefault`, `ResetGlossRemoverToDefault`)
   move/retarget to QoL.

### Consumers to rewire
- Shadows: `CrispyShadowsSwapper`, `ShadowsSection`.
- Minimap: `MinimapSwapper`, minimap controls in `UISection`.
- Chat: chat layout consumers, `UISection.ApplyChatBackground`, chat controls.
- Gloss: `GlossSwapper`, `GlossSection`.
- Team indicator: `TeamIndicatorSwapper`, indicator toggle in `UISection`.

## Migration (one-time, safe)

Existing users have these values in `ReskinProfile.json`. On startup, before the old keys are
dropped:
1. Add a `displaySettingsMigrated` (bool/int version) flag to `QoLProfile`.
2. If not migrated and `ReskinProfile.json` exists, parse it as a raw `JObject`, read the old
   keys (shadow*, minimap*, chat*, gloss*, teamIndicatorEnabled), populate the QoL config, set the
   flag, save QoL. Team colors are NOT migrated (they stay in the reskin profile); only the single
   `teamColorsEnabled` is mapped to both per-team enables in the reskin profile's own load path.
3. Runs once; idempotent.

## Menu impact
- Shadows / Minimap / Chat / Gloss / Team-indicator controls move under **Quality of Life**
  (or a "Display" subsection there).
- **Team Colors** integrate into the Players editor (team level); the old "User Interface" section
  (currently the team-colors page) is retired or repurposed.

## Staging
- **A.** Shadows → QoL (establish the move + migration pattern end to end).
- **B.** Gloss → QoL.
- **C.** Minimap → QoL.
- **D.** Chat → QoL.
- **E.** Team indicator → QoL.
- **F.** Team colors: per-team enable rework + integrate into Players editor.
- **G.** Menu cleanup (retire/repurpose UISection, move controls under QoL).
