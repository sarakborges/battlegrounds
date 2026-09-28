# Mod-driven theming

Visual theme data belongs to the selected mod. `Battlegrounds.Core` and `Battlegrounds.Application` do not interpret colors, fonts, button imagery, borders, screen backgrounds or presentation layout metrics.

A mod may provide an optional aggregate theme file at:

```text
presentation/theme.json
```

`Battlegrounds.Content` validates this file and all referenced assets. `Battlegrounds.Game` translates the validated, engine-neutral theme model into Godot `Theme`, font, texture, style and layout resources at runtime.

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
      "display": { "asset": "assets/fonts/title.ttf" }
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
  "metrics": {
    "row.offer.gap": 10,
    "row.offer.preferredCardWidth": 124,
    "row.offer.minimumCardWidth": 88,
    "row.offer.preferredCardHeight": 142,
    "row.offer.padding": 4,
    "card.shop.minimumWidth": 118,
    "card.shop.artHeight": 76,
    "card.contentGap": 2,
    "hud.heroPortrait.size": 88,
    "hud.opponent.portraitSize": 44,
    "layout.combat.marginHorizontal": 32,
    "layout.combat.marginVertical": 24,
    "drag.preview.scale": 1.045,
    "drag.preview.rotationDegrees": -1.5
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
raw value -> semantic token -> component/layout role
#E7A93D -> primary -> button.primary.backgroundColor
12 -> md -> card.padding.horizontal
124 -> row.offer.preferredCardWidth
```

UI code should consume semantic roles rather than fandom-specific colors, files or dimensions. Mods can therefore produce radically different visual identities without changing game mechanics or scene logic.

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

## Layout metrics

`metrics` is an optional map of finite numeric presentation values. It exists for semantic geometry that should be owned by the mod but is not naturally a reusable spacing token.

The preparation adapter currently recognizes these row-role families:

```text
row.leader.*
row.offer.*
row.field.*
row.reserve.*
```

Each family may define:

```text
gap
preferredCardWidth
minimumCardWidth
preferredCardHeight
padding
```

For example:

```json
{
  "metrics": {
    "row.field.gap": 16,
    "row.field.preferredCardWidth": 148,
    "row.field.minimumCardWidth": 98,
    "row.field.preferredCardHeight": 204,
    "row.field.padding": 10
  }
}
```

Card internals use semantic footprint roles based on where the card is shown:

```text
card.default.*
card.leader.*
card.shop.*
card.board.*
card.reserve.*
card.choice.*
```

Each role may define `minimumWidth` and `artHeight`. `card.contentGap` controls the vertical separation between the art/title/stats content inside every presentation card. These values complement the responsive row metrics: row geometry controls available layout space, while card footprint geometry controls the card's internal visual proportions.

The semantic HUD uses these metric keys:

```text
hud.heroDock.minimumHeight
hud.heroPortrait.size
hud.opponent.entryHeight
hud.opponent.marginHorizontal
hud.opponent.marginVertical
hud.opponent.gap
hud.opponent.rankWidth
hud.opponent.portraitSize
hud.opponent.statsWidth
hud.opponent.identityGap
hud.opponent.statsGap
```

They control HUD geometry only. Leader identity, Health, Armor, Tier, Resource and opponent state still come exclusively from authoritative match state and presentation text/assets.

Combat playback uses these semantic layout metrics:

```text
layout.combat.marginHorizontal
layout.combat.marginVertical
layout.combat.contentGap
layout.combat.boardsGap
layout.combat.unitGap
layout.combat.controlsGap
```

They control only the presentation-owned combat overlay: outer content margins, vertical content separation, spacing between the two combat boards, spacing between units, and spacing between playback controls. Missing values keep the adapter defaults (`32`, `24`, `12`, `24`, `6`, `8` respectively). Combat still consumes immutable resolved timeline data; these metrics cannot alter combat resolution, event ordering or authoritative state.

Preparation drag feedback also exposes two semantic metrics:

```text
drag.preview.scale
drag.preview.rotationDegrees
```

They control only the lifted visual preview while dragging a card. The card's source opacity remains a component property on `components.drag.preview.opacity`.

Missing metrics keep the adapter defaults, so existing mods do not need to declare layout values. The adapter clamps consumed metrics to safe presentation ranges; malformed or non-finite metric values reject the mod during validation.

The `card` component's existing `padding` tokens are also consumed by the card's actual content container, rather than only by its background style. This keeps component styling and content geometry under the same mod-owned contract.

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

A screen style supports `backgroundColor` and `backgroundAsset`. Background assets are loaded from the selected mod at runtime. The launcher previews valid mod themes; preparation and combat switch between their semantic screen roles during play.

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
  -> Godot Theme / FontFile / Texture2D / StyleBox / semantic layout values
```

Core mechanics never read theme data. Theme choices cannot affect deterministic simulation, gameplay identity, pool ownership or command legality.
