# Semantic theme roles

`presentation/theme.json` component keys are engine-neutral semantic roles. The Web UI consumes those roles directly; mods do not theme DOM tags or Godot control types.

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
```

A token is the persistent board/shop/reserve representation. A preview or inspector is the transient hover/focus representation. These are deliberately separate roles because a minion, Tavern Spell/action, leader and hero power do not share one visual anatomy.

There is intentionally no generic `card`, `card.board`, `playable-token` or `card-preview` role. Those legacy abstractions are rejected by theme validation.

## Buttons

```text
button
button.primary
button.endRecruitment
button.tavernUpgrade
button.tavernRefresh
button.tavernFreeze
button.heroPower
```

Specific board controls own their visual identity instead of inheriting application-button semantics accidentally. Each role may declare normal, hover, pressed, disabled and focus state overrides.

## Panels and board pieces

```text
panel
panel.opponentRail
panel.tavernControls
panel.tavern
panel.board
panel.reserve
panel.heroDock
panel.heroPortrait
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
panel.shopkeeper
panel.combat
```

These roles let a mod visually distinguish scene pieces and HUD/status surfaces without referring to implementation classes.

Example:

```json
{
  "components": {
    "unit-token": {
      "backgroundAsset": "assets/ui/unit-token-frame.svg",
      "borderWidth": 0
    },
    "action-token": {
      "backgroundAsset": "assets/ui/action-token-frame.svg",
      "borderWidth": 0
    },
    "panel.heroPortrait": {
      "backgroundAsset": "assets/ui/hero-portrait-frame.svg",
      "borderWidth": 0
    },
    "button.heroPower": {
      "backgroundAsset": "assets/ui/hero-power-frame.svg",
      "borderWidth": 0
    }
  }
}
```

## HUD labels

```text
label.heroName
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
