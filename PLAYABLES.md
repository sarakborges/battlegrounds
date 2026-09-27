# Playables and Actions

`Playable` is the neutral presentation/query surface for things that can appear in an Offer and occupy Reserve capacity. It is not a second authoritative state tree.

## Current playable kinds

- `Unit`: an authored `UnitDefinition` with a mutable `UnitInstance`. Playing it deploys it to the Field and runs normal unit triggers.
- `Action`: an authored `ActionDefinition` with an `ActionInstance`. Playing it executes its ordered effects through `GameEffectRuntime` and consumes the instance.

A mod may display an Action as a spell, item, tactic, program, consumable, card, or any other theme-specific term.

## Content layout

Every action is independent authored content:

```text
content/actions/<action-id>.json
```

Example:

```json
{
  "id": "training",
  "name": "Training",
  "tier": 1,
  "cost": 2,
  "effects": [
    {
      "kind": "modifyStats",
      "target": { "scope": "selected" },
      "attack": 1,
      "health": 1
    }
  ]
}
```

The file stem must equal `id`.

## Offer composition

`rules/preparation.json` keeps the total offer size in `offerSizesByTier` and may reserve a number of those slots for Actions with `actionOfferSizesByTier`. The remaining slots are Units.

Action offers are sampled deterministically without replacement from definitions at or below the player's current Tier. Actions do not consume UnitPool copies.

## Reserve ownership

`PlayerState` remains the single authoritative owner. Unit and Action runtime instances are stored separately so their invariants remain explicit, while `PlayableReserve` exposes a neutral ordered view for UI and AI.

Reserve capacity is shared across all playable kinds and queued choices. A pending choice reserves one future Reserve slot so chained generation cannot create an unresolvable state.

## Commands

UI and AI use the same explicit preparation commands:

- `AcquirePlayableCommand` buys the selected current Offer entry;
- legacy `AcquireUnitCommand` remains as a Unit compatibility boundary;
- `DeployUnitCommand` plays a Unit;
- `PlayActionCommand` consumes an Action and executes its effects;
- `ResolveUnitChoiceCommand` and `ResolveActionChoiceCommand` resolve queued choices.

Actions that require `selected` targets validate the target before the Action is consumed.

## Effects and generation

Actions reuse the same effect definitions and `GameEffectRuntime` used by Units and Powers. There is no spell-only executor.

Preparation generation supports:

- `generateUnitToReserve`;
- `generateUnitChoice`;
- `generateActionToReserve`;
- `generateActionChoice`.

Generated content does not alter UnitPool availability. Generation and pending choices are Preparation-only because Combat operates on isolated snapshots and cannot mutate authoritative Reserve state.

## Compatibility

`PlayerState.Offer` and `PlayerState.Reserve` remain Unit-only compatibility views for existing Core mechanics. New presentation/AI code should prefer `PlayableOffer` and `PlayableReserve` whenever it needs to reason about every playable kind.
