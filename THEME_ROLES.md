# Semantic theme roles

`presentation/theme.json` component keys are engine-neutral semantic roles. The Web UI consumes those roles directly; mods do not theme DOM tags, Godot control types, or game-specific display names.

All roles are optional. Missing specific roles fall back to the engine default theme.

## Gameplay pieces

```text
unit-token
action-token
unit-card-preview
action-card-preview
leader-inspector
power-tooltip
player-chip
artMask.unit
artMask.hero
```

A token is the persistent offer/field/reserve representation. A preview or inspector is the transient hover/focus representation. These are deliberately separate roles because units, actions, leaders and powers do not share one visual anatomy.

`artMask.unit` and `artMask.hero` describe the aperture used to clip artwork beneath decorative frames. They let a mod provide non-rectangular token and portrait frames without leaking rectangular source art through transparent regions.

There is intentionally no generic `card`, `card.board`, `playable-token` or `card-preview` role. Those legacy abstractions are rejected by theme validation.

## Buttons

```text
button
button.primary
button.endPreparation
button.tierUpgrade
button.offerRefresh
button.offerFreeze
button.power
```

Specific preparation controls own their visual identity instead of inheriting application-button semantics accidentally. Display terminology such as “Recruit”, “Tavern”, “Hero Power” or equivalent belongs to mod localization, not these shared role names.

Each role may declare normal, hover, pressed, disabled and focus state overrides.

## Panels and scene pieces

```text
panel
panel.opponentRail
panel.preparationControls
panel.offer
panel.board
panel.boardFrame
panel.reserve
panel.leaderDock
panel.leaderPortrait
panel.opponent
panel.opponent.self
panel.opponent.eliminated
panel.opponentPortrait
panel.tierBadge
panel.attackBadge
panel.healthBadge
panel.armorBadge
panel.resourceBadge
panel.interaction
panel.preparationHost
panel.combat
```

`panel.boardFrame` is a presentation layer over the screen background and below gameplay pieces. It is intended for transparent board shells/chrome rather than scenery itself.

These roles let a mod visually distinguish scene pieces and HUD/status surfaces without referring to implementation classes or a particular game's display vocabulary.

Example:

```json
{
  "components": {
    "unit-token": {
      "backgroundAsset": "assets/ui/unit-token-frame.svg",
      "borderWidth": 0
    },
    "artMask.unit": {
      "backgroundAsset": "assets/ui/unit-art-mask.svg"
    },
    "panel.boardFrame": {
      "backgroundAsset": "assets/ui/board-frame.png",
      "borderWidth": 0
    },
    "panel.leaderPortrait": {
      "backgroundAsset": "assets/ui/leader-portrait-frame.svg",
      "borderWidth": 0
    },
    "artMask.hero": {
      "backgroundAsset": "assets/ui/hero-art-mask.svg"
    },
    "button.power": {
      "backgroundAsset": "assets/ui/power-frame.svg",
      "borderWidth": 0
    }
  }
}
```

The filenames in a mod may use any vocabulary the mod author wants. Only the shared role keys are engine contract.

## HUD labels

```text
label.leaderName
label.attack
label.health
label.armor
label.tier
label.resource
```

A label role accepts typography/text properties such as `font`, `fontSize`, `textColor` and `opacity`.

## Interaction roles

```text
drag.preview
dropTarget.valid
dropTarget.valid.active
dropTarget.invalid
dropTarget.invalid.active
```

These roles are presentation-only. They never alter authoritative gameplay state.
