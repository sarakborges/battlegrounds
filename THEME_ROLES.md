# Semantic theme roles

`presentation/theme.json` component keys are engine-neutral semantic roles. The Godot adapter maps the stable roles below to its concrete control variations.

All roles are optional. Missing semantic panel roles inherit the mod's generic `panel` style. Missing semantic HUD label roles keep the theme's normal heading typography and text color.

## Panels

```text
panel.heroPortrait
panel.opponent
panel.opponent.self
panel.opponent.eliminated
panel.opponentPortrait
panel.tierBadge
panel.healthBadge
panel.armorBadge
panel.resourceBadge
panel.interaction
panel.shopkeeper
panel.combat
```

These let a mod visually distinguish HUD/status surfaces without referring to Godot type names such as `PanelContainer`, `OpponentEntrySelf` or `HealthBadge`.

Example:

```json
{
  "components": {
    "panel": {
      "backgroundColor": "surface",
      "borderColor": "border",
      "borderWidth": 1,
      "radius": "large"
    },
    "panel.healthBadge": {
      "backgroundColor": "healthSurface",
      "borderColor": "healthAccent"
    },
    "panel.opponent.self": {
      "borderColor": "primary",
      "borderWidth": 2
    }
  }
}
```

## HUD labels

```text
label.heroName
label.health
label.armor
label.tier
label.resource
```

A label role accepts the same typography/text properties available to component styles, such as `font`, `fontSize`, `textColor` and `opacity`. If omitted, the adapter seeds the role from the theme's semantic heading typography.

Example:

```json
{
  "components": {
    "label.health": {
      "textColor": "healthAccent",
      "fontSize": "heading"
    },
    "label.resource": {
      "textColor": "resourceAccent"
    }
  }
}
```

These roles are presentation-only. They do not alter Health, Armor, Tier, Resource, leader identity or any authoritative state.
