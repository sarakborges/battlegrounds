# Engine default presentation theme

The presentation adapter resolves one effective theme for runtime use:

```text
engine default theme -> selected mod overrides -> Godot presentation resources
```

The engine default is stored as data in `src/Battlegrounds.Content/Defaults/presentation/theme.json` and embedded in `Battlegrounds.Content`. It uses the same versioned theme vocabulary as mod-owned `presentation/theme.json` files.

## Why this exists

Presentation defaults should not be distributed across Godot scripts as fallback literals. The base theme owns the neutral visual baseline: typography sizes, colors, spacing, shape tokens, component styles, screen colors and all engine-consumed layout metrics.

Godot code consumes the resolved catalog. A missing required metric or semantic role in that resolved catalog is a contract error rather than an invitation to silently fall back to another hardcoded number.

## Layering rules

A mod theme is optional and may be partial. Maps such as colors, font sizes, spacing, radii and metrics use key-level override semantics.

Component styles merge property-by-property. Component states also merge property-by-property, so a mod can override only one hover property without losing the base pressed, disabled or focus state.

Screen styles merge property-by-property.

The engine default theme is deliberately asset-free. Fonts, image-backed components and screen background assets remain mod-owned and resolve relative to the selected mod directory.

## Ownership boundary

The base theme is an engine presentation default, not gameplay data. Mods retain final ownership of presentation by overriding any supported theme token, semantic component role, screen role or metric.

`Battlegrounds.Core` and `Battlegrounds.Application` remain unaware of theme data.
