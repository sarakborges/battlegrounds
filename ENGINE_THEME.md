# Engine default presentation theme

The presentation adapter resolves one effective theme for runtime use:

```text
engine default theme -> selected mod overrides -> Godot presentation resources
```

The engine default is stored as data in `src/Battlegrounds.Content/Defaults/presentation/theme.json` and embedded in `Battlegrounds.Content`. It uses the same versioned theme vocabulary as mod-owned `presentation/theme.json` files.

## Why this exists

Presentation defaults should not be distributed across Godot scripts or scene files as fallback literals. The base theme owns the neutral presentation baseline: typography sizes, colors, spacing, shape tokens, component styles, screen colors, layout metrics and numeric motion tuning.

Godot code consumes the resolved catalog. A missing required metric or semantic role in that resolved catalog is a contract error rather than an invitation to silently fall back to another hardcoded number.

The gameplay and launcher `.tscn` files own hierarchy, anchors needed to express structural relationships, visibility, input wiring and semantic theme variations. They do not own playable visual dimensions, spacing, font sizes, colors or presentation timing.

Runtime-created presentation nodes follow the same rule. They are created structurally first and receive spacing, margins, motion and other visual values only from the resolved theme before they become visible.

If selected-mod presentation initialization fails, gameplay applies the embedded engine theme as a deferred fallback so the error surface does not depend on scene-authored visual defaults.

## Layering rules

A mod theme is optional and may be partial. Maps such as colors, font sizes, spacing, radii and metrics use key-level override semantics.

Component styles merge property-by-property. Component states also merge property-by-property, so a mod can override only one hover property without losing the base pressed, disabled or focus state.

Screen styles merge property-by-property.

The engine default theme is deliberately asset-free. Fonts, image-backed components and screen background assets remain mod-owned and resolve relative to the selected mod directory.

## Motion metrics

Motion tuning is presentation data. The resolved theme currently owns:

- combat playback step timing;
- default UI-selection cue timing;
- `pulse`, `shake`, `lunge`, `fade` and `pop` durations;
- their visual scale, rotation and opacity parameters.

The animation names themselves remain semantic presentation intents. Entity-specific `assets/presentation.json` cues can still override an animation, duration or suppress the fallback with `none`; the theme supplies the neutral numeric baseline used when authored cue data is partial or absent.

## Ownership boundary

The base theme is an engine presentation default, not gameplay data. Mods retain final ownership of presentation by overriding any supported theme token, semantic component role, screen role or metric.

Pointer hit tolerances, drag insertion hitboxes, drag threshold and cursor semantics are interaction behavior rather than visual skin values and remain adapter-owned. Visual drag feedback such as opacity, scale, rotation, drop-target color, border, radius, padding and shadow scale belongs to the resolved theme.

Simulation timing is also outside the theme. Theme-owned combat playback timing only controls how already-resolved immutable combat data is displayed; it cannot delay or affect Core combat resolution.

`Battlegrounds.Core` and `Battlegrounds.Application` remain unaware of theme data.
