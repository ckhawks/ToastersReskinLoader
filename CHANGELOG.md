# Changelog

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
