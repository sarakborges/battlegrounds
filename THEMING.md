# Mod-driven theming

Visual theme data belongs to the selected mod. `Battlegrounds.Core` and `Battlegrounds.Application` do not interpret colors, fonts, button imagery, borders or screen backgrounds.

A mod may provide an optional aggregate theme file at:

```text
presentation/theme.json
```

`Battlegrounds.Content` validates this file and all referenced assets. `Battlegrounds.Game` translates the validated, engine-neutral theme model into Godot `Theme`, font, texture and style resources at runtime.

The JSON contract intentionally contains no Godot class names or property names.

## Example structure

```json
{
  "version": 1,
  "colors": {
    "background": "#0E1117",
    "surface": "#171C25",
    "text": "#F4F7FB",
    "primary": "#E7A93D"
  },
  "typography": {
    "fonts": {
      "body": { "asset": "assets/fonts/body.ttf" },
      "display": { "asset": "assets/fonts/display.otf" }
    },
    "sizes": {
      "caption": 12,
      "body": 15,
      "button": 15,
      "heading": 19,
      "title": 30
    }
  },
  "spacing": {
    "sm": 8,
    "md": 12,
    "lg": 20
  },
  "shape": {
    "medium": 9,
    "large": 15
  },
  "components": {
    "button": {
      "font": "body",
      "fontSize": "button",
      "textColor": "text",
      "backgroundColor": "surface",
      "radius": "medium",
      "padding": {
        "horizontal": "md",
        "vertical": "sm"
      }
    },
    "button.primary": {
      "backgroundColor": "primary",
      "backgroundAsset": "assets/ui/button-primary.png",
      "slice": {
        "left": 14,
        "top": 14,
        "right": 14,
        "bottom": 14
      }
    }
  },
  "screens": {
    "preparation": {
      "backgroundColor": "background",
      "backgroundAsset": "assets/backgrounds/preparation.webp"
    }
  }
}
```

Every section other than `version` is optional. Missing theme files and missing optional sections fall back to the presentation adapter's normal defaults.

## Design-token layers

The intended hierarchy is:

```text
raw value -> semantic token -> component style
#E7A93D -> primary -> button.primary.backgroundColor
```

UI code should consume semantic component roles rather than fandom-specific colors or files. Mods can therefore produce radically different visual identities without changing game mechanics or scene logic.

## Colors

`colors` maps arbitrary stable token names to `#RRGGBB` or `#RRGGBBAA` literals.

Component and screen color properties accept either a declared color token or a literal color. Token references are preferred for values reused throughout a theme.

## Typography

`typography.fonts` maps semantic font roles to mod-relative assets. Supported font file extensions are:

- `.ttf`
- `.otf`
- `.woff`
- `.woff2`

The runtime understands `body` and `display` as useful conventional roles: semantic title/heading labels prefer `display`, while body/caption text prefers `body`. Component styles may reference any declared font role through `font`.

`typography.sizes` maps arbitrary size tokens to integer pixel sizes. The current presentation adapter conventionally consumes `caption`, `body`, `button`, `heading` and `title` when available.

A mod does not need to ship custom fonts. An empty `fonts` object keeps the platform/default font while still allowing mod-owned font sizes.

## Spacing and shape

`spacing` contains integer spacing tokens. Component `padding.horizontal` and `padding.vertical` reference these tokens.

`shape` contains integer corner-radius tokens. Component `radius` references one of these values.

These remain presentation values; they do not alter game rules or authoritative state.

## Component roles

The current adapter recognizes these semantic component roles:

- `button` — base button styling;
- `button.primary` — primary call-to-action styling, inheriting unspecified values from `button`;
- `card` — presentation cards, inheriting unspecified values from `button`;
- `input` — text input styling;
- `panel` — panel/container styling.

A component style may contain:

```text
font
fontSize
textColor
backgroundColor
borderColor
borderWidth
radius
padding
backgroundAsset
slice
opacity
states
```

`font`, `fontSize`, `radius` and padding values reference their corresponding token maps. Color properties reference color tokens or literals.

## Interaction states

`states` may override component properties for:

- `normal`
- `hover`
- `pressed`
- `disabled`
- `focus`

For variants such as `button.primary`, resolution is layered deterministically from the base component through the variant and then the requested state. A mod only needs to override what differs.

## Image-backed components and nine-slice

`backgroundAsset` may point to a mod-owned `.png`, `.jpg`, `.jpeg`, `.webp` or `.svg` image.

For scalable button/panel artwork, pair it with `slice`:

```json
{
  "backgroundAsset": "assets/ui/button-primary.png",
  "slice": {
    "left": 18,
    "top": 18,
    "right": 18,
    "bottom": 18
  }
}
```

The Godot adapter interprets this as a nine-slice texture so borders/corners are preserved when a control changes size.

When both `backgroundAsset` and `backgroundColor` are present, the color acts as a tint. This allows a mod to reuse one neutral image across multiple semantic states or variants.

## Screen roles

The contract currently reserves:

- `launcher`
- `preparation`
- `combat`

A screen style supports `backgroundColor` and `backgroundAsset`. Background assets are loaded from the selected mod at runtime.

## Asset safety

Theme asset paths:

- are relative to the mod root;
- may not escape the mod directory;
- must have a supported media extension;
- must exist when the mod is validated.

A theme with invalid token references, malformed values or invalid asset paths rejects the mod through the normal `ModValidationReport` boundary.

## Ownership boundary

```text
mod files
  -> Battlegrounds.Content validation + immutable theme catalog
  -> Battlegrounds.Game runtime translation
  -> Godot Theme / FontFile / Texture2D / StyleBox resources
```

Core mechanics never read theme data. Theme choices cannot affect deterministic simulation, gameplay identity, pool ownership or command legality.
