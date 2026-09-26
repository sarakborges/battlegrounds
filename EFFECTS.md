# Effect and death semantics

Effects and triggers are game-domain mechanics. They are not owned by Preparation or Combat. Both phases execute authored mechanics through the shared `GameEffectRuntime` and provide only the state adapter appropriate to that phase.

## Power lifecycle

Powers are independent content entities under `content/powers/<id>.json`. A leader stores only its initial power id; the player's `LeaderState` owns the current power id and may change it during the match.

A power is authored with the same trigger/effect vocabulary used elsewhere:

```json
{
  "id": "steady-pulse",
  "name": "Steady Pulse",
  "activation": {
    "cost": 1,
    "maxUsesPerTurn": 1
  },
  "triggers": [
    {
      "event": "onActivate",
      "effects": [
        {
          "kind": "modifyStats",
          "target": { "scope": "selected" },
          "attack": 1,
          "health": 1
        }
      ]
    },
    {
      "event": "onCombatStart",
      "effects": [
        { "kind": "addResource", "amount": 1 }
      ]
    }
  ]
}
```

`activation` is optional. A passive-only power omits it and cannot be invoked with `UsePowerCommand`. When `activation` exists, exactly one `onActivate` trigger is required. The supported power lifecycle events are `onMatchStart`, `onTurnStart`, `onTurnEnd`, `onCombatStart`, and `onCombatEnd`, in addition to `onActivate` for active use.

`selected` targets are only valid inside `onActivate`, because lifecycle events do not involve a UI/AI target selection step.

Preparation lifecycle effects mutate authoritative preparation state through its effect-world adapter. Combat lifecycle effects execute against the isolated combat snapshot. Persistent consequences such as `setPower` or resource deltas are returned explicitly in `CombatResult` and settled by `MatchEngine`; Combat never mutates `PlayerState` directly.

When a power changes during an event, the event keeps the original source snapshot. The newly selected power becomes eligible starting with the next lifecycle event rather than being re-entered during the current one.

## Target selectors

Targeting is composed instead of encoded as one enum value per combination. A targeted effect may define:

- `scope`: `self`, `selected`, `friendly`, or `enemy`;
- `selection`: `all`, `random`, `lowestAttack`, `highestAttack`, `lowestHealth`, `highestHealth`, `leftmost`, `rightmost`, `adjacent`, `leftAdjacent`, or `rightAdjacent`;
- `excludeSource`: valid for `friendly` selectors;
- `limit`: positive maximum number of selected units;
- `typeId` and `tagId`: optional mod-defined filters.

For example:

```json
{
  "kind": "modifyStats",
  "target": {
    "scope": "friendly",
    "selection": "lowestAttack",
    "excludeSource": true,
    "limit": 1,
    "typeId": "organic"
  },
  "attack": 2,
  "health": 2
}
```

`self` and `selected` are already singular, so they cannot add another selection mode or a limit. Adjacent selections are only meaningful for friendly units because adjacency is resolved from the source unit's current field position. Random selection samples without replacement and uses the injected deterministic RNG.

## Trigger conditions

Conditions belong to a trigger and gate its entire ordered effect list. Every condition must pass before any effect from that trigger resolves.

A count condition queries units without consuming RNG:

```json
{
  "event": "onPlay",
  "conditions": [
    {
      "kind": "unitCount",
      "query": {
        "scope": "friendly",
        "excludeSource": true,
        "typeId": "organic"
      },
      "comparison": "greaterThanOrEqual",
      "value": 2
    }
  ],
  "effects": [
    { "kind": "addResource", "amount": 1 }
  ]
}
```

A source-stat condition reads the source's current runtime stats, including buffs already applied in that phase:

```json
{
  "kind": "sourceStat",
  "stat": "attack",
  "comparison": "greaterThan",
  "value": 5
}
```

Supported comparisons are `equal`, `notEqual`, `lessThan`, `lessThanOrEqual`, `greaterThan`, and `greaterThanOrEqual`.

## Resolution phases

A logical effect action resolves in this order:

1. evaluate the trigger's conditions against the current effect-world snapshot;
2. if all conditions pass, resolve authored effects in authored order;
3. resolve directly-created follow-up events such as `onDamage`, `onSummon`, or `triggerEvent`;
4. once that event phase is complete, identify all units that are dead;
5. remove the complete simultaneous-death batch before resolving any death-related trigger;
6. resolve each death deterministically;
7. during that death batch, newly lethal units remain in play until the current batch finishes;
8. after a unit's death-related triggers resolve, attempt its revive-once behavior;
9. a successful revive is a summon and runs normal `onSummon` listeners;
10. after the original batch is complete, create the next death batch if new deaths are pending.

This preserves the important Battlegrounds/Hearthstone invariants that simultaneous dead units cannot be targeted by each other's death effects, Deathrattle-like effects resolve before Reborn-like revival, and consequences of one death can affect listeners that observe a later death in the same batch.

## Counted friendly-death trigger

The neutral trigger used for Avenge-like mechanics is:

```json
{
  "event": "afterFriendlyDeaths",
  "count": 3,
  "effects": [
    { "kind": "addResource", "amount": 1 }
  ]
}
```

`count` is mandatory and must be positive for `afterFriendlyDeaths`. It is invalid on other trigger kinds.

The counter advances once for each actual friendly death while the listener remains in play. When it reaches `count`, the authored effects resolve and the counter resets, allowing repeated activation after another `count` deaths.

The engine name is intentionally neutral. A mod may present this mechanic as `Avenge`, another keyword, or no visible keyword at all.

## Death-related listener ordering

Actual deaths and their death-related listeners use deterministic runtime ordering. A dead unit's `onDeath` effects and living friendly `afterFriendlyDeaths` listeners are placed into the same ordered resolution set for that death. Reborn-like revival is always attempted after those triggers.

The runtime does not use a global event bus. Event creation, listener discovery, effect application, death batches, and follow-up events are explicit and bounded by the internal recursion safety budget.
