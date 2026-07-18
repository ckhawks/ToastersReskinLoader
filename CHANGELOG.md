# Changelog

## 2.4.0

### Added
- **Usage analytics + Privacy section** (new "Privacy" settings page, on by
  default). TRL can share anonymous usage stats with puckstats.io once per game
  launch: which reskins you have equipped (to rank skins by real usage on the
  public gallery) and which mod features you have enabled (internal only, to help
  prioritize what to improve). Two independent opt-out toggles live on the new
  Privacy page at the bottom of the settings sidebar; turning either off stops
  that data being sent and tells the server to forget what it already stored.
  Server passwords and other private data are never sent.
- **Global free-fly camera** with a rebindable toggle key. Detach the camera and
  fly around freely; rebind the toggle in Input & Camera.
- **Friends-in-game board** (Multiplayer, on by default). A board to the right of
  the main menu listing which of your friends are currently in Puck. Now has its
  own toggle in the Multiplayer settings section.
- **Fix Steam server presence** (Fixes, on by default). Puck broadcasts an empty
  server IP to Steam, which breaks the "Join Game" button and hides which server
  you're on from friends. This rewrites your Steam presence with the real address
  you connected to. Only affects your own presence, and friends only benefit if
  they also run this mod. Toggle in the Fixes settings section.

## 2.3.4

### Added
- **Shrink puck viewmodel** (Pucks, on by default). Pulls the visible puck body
  in by 1% so it stops clipping into the stick at contact. Cosmetic only — the
  collider lives on the puck root, so physics and hit detection are unaffected.
  Toggle in the Pucks settings section.

## 2.3.3

### Added
- **"Refresh Visible" server-browser button** (Server Browser, on by default).
  A button next to REFRESH that re-pings only the servers currently shown in the
  list (those passing your active filters) to update their player counts and
  names, skipping the master-server roundtrip a full REFRESH does. Pings fan out
  in parallel like fast scanning, and the button shows live progress. Toggle in
  the Server Browser settings section.
- **Clock milliseconds precision** (Scoreboard). The "Clock shows milliseconds"
  option now has a precision picker — tenths, hundredths, or full milliseconds —
  plus an "Only show milliseconds in the final 5 seconds" toggle so the clock
  reads as plain `MM:SS` until the finish. Off/full by default.
- **Puck FX trail opacity** (Puck FX). Re-exposed the trail Start/End Opacity
  sliders. The trail material now uses straight alpha blending so the gradient
  actually controls opacity; it defaults to opaque at the head fading out along
  the tail.
- **Per-object reflection removal** (Rendering → Glossiness). "Remove From
  Specific Objects" strips the rink reflection from just sticks, players, or
  pucks — a finer-grained alternative to the scene-wide slider. The chosen
  objects stop reflecting the rink entirely (fallback is black, not the blue
  sky). Off by default. Note any other surface not covered by a reflection probe
  may lose its reflection too.
- **Overtime period numbering** (Scoreboard, always on). Successive overtimes now
  read `OVERTIME`, `OVERTIME 2`, `OVERTIME 3`, … instead of a flat `OVERTIME`
  every period, matching broadcast labeling.
- **Full server name in the scoreboard** (TAB, always on). Long server names that
  the game truncates in the replicated netcode value are re-fetched via the same
  TCP preview request the server browser uses and re-asserted onto the scoreboard.
- **Developer diagnostics**: a "verbose debug logging" toggle, and a per-mod
  enable-timing log (`[EnableTiming]` lines) that times how long each mod's
  `Enable()` takes at startup (mods that load after this one).

### Fixed
- **Launch freeze / black screen.** Startup used to decode every active reskin
  texture synchronously on the main thread, freezing the render thread at launch.
  Texture warm-up is now chunked across frames with a loading overlay, then the
  (cached) apply pass runs — the game keeps rendering throughout.
- **Female jerseys, sticks, and pucks going black/invisible on Reload.** The
  Reload button destroys the texture cache and re-applies without respawning,
  leaving spawn-time materials pointing at destroyed textures. The female
  torso/groin copy is now re-synced after a jersey apply, and stick/puck textures
  are re-applied in the reload pass.
- **Chat-open key leaking into the message** on non-QWERTY layouts (Dvorak/AZERTY)
  and after Shift/Caps Lock — the swallow now matches on the OS-layout character
  and is case-insensitive.

### Internal
- Sidebar rows in the mod menu now run flush against the vertical scrollbar
  instead of leaving a gap.

## 2.3.2

### Added
- **Environment Reflections control** (Rendering → Glossiness). A "Reduce
  Environment Reflections" toggle plus a Reflection amount slider that scale the
  scene's reflection probe(s) down. The game maps a static reflection of the rink
  (ice, boards, lights) onto glossy surfaces — it doesn't move with the world, so
  it looks pasted-on and slides oddly across a spinning puck or a stick. Dialing
  it down drops that reflection while keeping the direct shine from the arena
  lights. Scene-wide (affects ice/boards too), off by default. Re-applies on scene
  load since the game resets probe intensity per scene.

## 2.3.1

Bug-fix and quality-of-life release, primarily catching the mod up to the
recent game builds (B1117 / B1153) that reworked the render pipeline, minimap,
and mod-menu toggles.

### Added
- **Color & Saturation correction** (Rendering). Layers a runtime color grade
  on top of the game's grading to counteract the washed-out/gray look B1153
  introduced (HDR buffer disabled, ambient dimmed). Saturation, Contrast,
  Brightness, and Warmth sliders plus Neutral/Vivid/Punchy/Warm presets, and an
  experimental "Re-enable HDR" root-cause toggle. Off by default.
- **Disable goal-scored screen flash** (Scoreboard). Optional toggle that hides
  the full-screen team-colored flash on a goal. The goal slow-motion is
  unaffected. Off by default.
- **Minimap stick icon scale** slider, matching the existing player/puck scales.
- **Minimap puck elevation overrides**: puck can grow with height instead of
  shrinking, and the height fade can be turned off.

### Fixed
- **Custom ice textures read backwards** after a game update flipped the ice
  mesh UV orientation — the mapping is now rotated 180° to restore it.
- **Minimap puck color** had no effect since B1117 (the dot is painter-drawn
  with no background image); it's now drawn with a custom painter that fills the
  configured color. Puck-scale override likewise now composes on top of the
  game's per-frame elevation scale instead of being clobbered.
- **Minimap team colors** restored for B1117's new `(Root, Body)` player map and
  applied to the new stick elements.
- **Team-select button names** no longer double-render on B1153 — custom team
  names now retarget the new `NameLabel` child instead of the button text.
- **Mod-menu toggle chips** adapt to the game now rendering its own
  ENABLED/DISABLED text: the chip sizes to the native toggle and only recolors
  for state, instead of drawing a duplicate label.
- **Server-browser favorite star** no longer swallows the whole row's clicks
  (the game's `.server Button` USS stretched it to 100%); it's pinned to its
  glyph so clicks fall through to the connect button.
- **Scoreboard milliseconds** now count each second down (`.999 → .000`) in step
  with the vanilla digits, including the final `0:00` second, instead of freezing.

### Internal
- Corrective safety net for the vanilla goal-announcement leak
  (`AnnouncementHideGuard`), always on.
