# Effect and death semantics

Effects and triggers are game-domain mechanics. They are not owned by Preparation or Combat. Both phases execute authored mechanics through the shared `GameEffectRuntime` and provide only the state adapter appropriate to that phase.

## Persistent unit mutations

Four effects intentionally mutate authoritative Unit state and are therefore valid only in Preparation-only contexts:

- `transformUnit`: preserves the runtime instance identity and field position, replaces its authored Unit definition, resets definition-derived stats/behaviors, and clears prior named modifiers;
- `copyUnitToReserve`: creates a new `Generated` reserve Unit carrying the target's current definition, stats, runtime behaviors, and named modifiers;
- `applyUnitModifier`: applies a persistent Attack/Health contribution under a neutral `modifierKey`; applying the same key again replaces the previous contribution;
- `removeUnitModifier`: removes that key and reverses the stored contribution.

Example:

```json
{
  "kind": "applyUnitModifier",
  "target": { "scope": "selected" },
  "modifierKey": "training-aura",
  "attack": 2,
  "health": 3
}
```

A transformed Unit that originally came from the shared pool returns its original pooled definition immediately, then becomes `Generated`. This prevents its transformed definition from being returned to the pool and prevents a later release/death from returning the original copy twice. Copies created by `copyUnitToReserve` are also `Generated` and never consume or return pool copies.

The Content boundary rejects these four effects from Combat-capable authored triggers. Combat receives only an isolated snapshot and never receives the persistent-mutation adapter.

## Combine reward lifecycle

`onCombine` is a neutral Preparation-only hook for the result of an explicit `CombineUnitsCommand`. It does not mean `onPlay`, `onSummon`, or `onDeath`, and combining does not synthesize any of those events for consumed or resulting Units.

The combine operation first validates the complete explicit input set, removes all selected runtime instances, returns their still-owned pooled copies, creates the result as a `Generated` Unit, and places that result in Reserve. Only then does the result's optional `onCombine` reward execute.

Current `onCombine` support is intentionally narrow: it is for direct post-combine rewards that can execute without an interactive target, condition, counter, or activation-limit context. Result stats, behaviors, tags, types, and ordinary triggers belong directly on the independently-authored result `UnitDefinition`. Detailed combine ordering and pool semantics are specified in `COMBINES.md`.

Example:

```json
{
  "id": "scout-merged",
  "name": "Merged Scout",
  "tier": 1,
  "attack": 4,
  "health": 6,
  "triggers": [
    {
      "event": "onCombine",
      "effects": [
        { "kind": "addResource", "amount": 1 }
      ]
    }
  ]
}
```

Any random consequence produced by the reward still uses the normal injected deterministic Preparation RNG. The combine operation itself consumes no RNG because the caller supplies the exact runtime instance IDs to consume.

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

For Powers, `selected` targets are only valid inside `onActivate`, because lifecycle events do not involve a UI/AI target selection step.

Preparation lifecycle effects mutate authoritative preparation state through its effect-world adapter. Combat lifecycle effects execute against the isolated combat snapshot. Persistent consequences such as `setPower`, resource deltas, and persistent effect-history deltas are returned explicitly in `CombatResult` and settled by `MatchEngine`; Combat never mutates `PlayerState` directly.

When a power changes during an event, the event keeps the original source snapshot. The newly selected power becomes eligible starting with the next lifecycle event rather than being re-entered during the current one.

## Target selectors

Targeting is composed instead of encoded as one enum value per combination. A targeted effect may define:

- `scope`: `self`, `selected`, `friendly`, or `enemy`;
- `selection`: `all`, `random`, `lowestAttack`, `highestAttack`, `lowestHealth`, `highestHealth`, `leftmost`, `rightmost`, `adjacent`, `leftAdjacent`, or `rightAdjacent`;
- `relativeTo`: optional spatial anchor for adjacent selections, either `source` (default) or `selected`;
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

`selected` is context-owned rather than synonymous with UI selection. During an activatable Power's `onActivate`, the selected target is supplied by the command issued by UI or AI. During a Unit's `onPlay`, the selected target is supplied by `DeployUnitCommand` when that trigger requires one. During a Unit's `onAttack`, Combat supplies the already-locked attack target. During a Unit's `onDamage`, Combat supplies the living targetable Unit that caused that damage when one exists. `onAcquire` and `onRelease` have no interactive target-selection step, so they do not expose `selected`. Triggers without an available context target resolve `selected` as empty.

`self` and `selected` are already singular, so they cannot add another selection mode or a limit. Adjacent selections default to `relativeTo: "source"`, preserving the original friendly-only behavior around the source Unit's current field position. `relativeTo: "selected"` instead anchors `adjacent`, `leftAdjacent`, or `rightAdjacent` around the contextual selected Unit; it is valid only where such a context target exists and may use `scope: "enemy"` to select neighbors of an attack target. The anchor itself is not part of an adjacent result. Random selection samples without replacement and uses the injected deterministic RNG.

## Acquisition lifecycle

`onAcquire` is the neutral Unit trigger for a real successful Unit acquisition from the current Offer. It is not synthesized by Unit generation, pending-choice resolution, copying, combining, summoning, or deployment.

A successful Unit acquisition resolves in this order:

1. remove the authored Unit copy from the Offer;
2. create its pooled runtime instance and place that instance in Reserve;
3. spend the normal acquisition resource cost;
4. record the mechanical `unitAcquired` history event;
5. resolve that instance's authored `onAcquire` triggers through `GameEffectRuntime`.

Because the acquired instance is already in Reserve when the trigger resolves, `self` reads and persistent mutations apply to that exact authoritative runtime Unit. Resource effects also observe the post-payment state, so an authored reward may refund part or all of the acquisition cost. `onAcquire` is a Preparation-only context and may use Preparation-only generation, choice, Action-generation, and persistent-Unit-mutation effects. It does not expose `selected`, because acquisition itself has no interactive target-selection step.

## Release lifecycle

`onRelease` is the neutral Unit trigger for a real successful `ReleaseUnitCommand`. It is not synthesized by death, combine consumption, transformation, elimination cleanup, or any other pool-return path.

A successful Unit release resolves in this order:

1. remove the runtime Unit from the authoritative Field;
2. surrender its still-owned pooled copy, if any, and return that definition to the shared Unit pool;
3. grant the normal release resource value;
4. record the mechanical `unitReleased` history event;
5. resolve the released Unit's authored `onRelease` triggers through `GameEffectRuntime`.

The released Unit remains available as the effect source snapshot, so source-stat conditions and `sourceStat` value expressions read its state at release time. Because it has already left the Field, the source is added to the resolution context as non-selectable: targeted effects cannot select the released Unit itself, while friendly targets see only Units that still occupy the authoritative Field. Resource effects observe the post-release-value state.

Current support intentionally stays narrow: `onRelease` uses the generic effect surface already valid for this trigger and does not by itself enable Preparation-only generation, pending-choice, Action-generation, or persistent-unit-mutation schemas. It does not expose `selected`, because release has no interactive target-selection step.

## Attack target context

`onAttack` is the neutral trigger for effects that occur when a Unit attacks. Combat chooses and locks the ordinary attack target before resolving this trigger, then exposes that target through `scope: "selected"`.

Example:

```json
{
  "event": "onAttack",
  "effects": [
    {
      "kind": "dealDamage",
      "target": { "scope": "selected" },
      "amount": 2
    }
  ]
}
```

One attack attempt resolves in this order:

1. choose and lock a valid attack target using the normal target-priority and deterministic RNG rules;
2. emit the presentation attack-start observation for that attacker/target pair;
3. record the mechanical `unitAttacked` history event and resolve any consequences it creates;
4. if the attacker and locked target still exist, resolve the attacker's `onAttack` triggers with the locked target as `selected`;
5. if both still exist after those effects, resolve the ordinary simultaneous strike damage.

If the attacker disappears before the strike, the attack ends. If the locked target disappears before the strike, the attack attempt does not silently choose a replacement target. A later extra-attack attempt, when applicable, performs its own fresh target selection. Because target locking happens first, its RNG draw also occurs before any random draws performed by that attempt's `onAttack` effects.

This context is an engine mechanic, not a presentation keyword. A mod may label `onAttack` effects with any terminology it wants or expose no keyword at all.

## Damage source context

`onDamage` is the neutral trigger for effects that occur after a Unit actually takes positive damage. When the damage was caused by another Unit that is still alive and targetable when the trigger resolves, that source Unit is exposed through `scope: "selected"`. This applies uniformly to ordinary strike damage and authored `dealDamage` effects.

If the damage source is not a targetable living Unit at resolution time—for example, an `onDeath` source that has already left the combat field—`selected` is absent and selected-target effects resolve no targets. Damage provenance never makes a dead or synthetic effect source targetable. Damage prevented by a barrier does not produce `onDamage`, because no positive damage occurred.

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

A generic `value` condition compares any two value expressions, including history expressions:

```json
{
  "kind": "value",
  "left": { "kind": "eventCount", "event": "unitAcquired", "scope": "turn" },
  "comparison": "greaterThanOrEqual",
  "right": 3
}
```

Supported comparisons are `equal`, `notEqual`, `lessThan`, `lessThanOrEqual`, `greaterThan`, and `greaterThanOrEqual`.

## Dynamic effect values

Numeric effect parameters may be authored as ordinary integers or as value-expression objects. Integer literals remain the shorthand for fixed values:

```json
{ "kind": "dealDamage", "target": { "scope": "enemy", "selection": "random" }, "amount": 3 }
```

Dynamic expressions currently support:

- `sourceStat`: current source `attack` or `health`;
- `targetStat`: current selected effect target `attack` or `health`;
- `unitCount`: count from an `EffectUnitQuery` using the same `scope`, `excludeSource`, `typeId`, and `tagId` vocabulary as conditions;
- `eventCount`: count of a mechanical game event in `turn`, `combat`, or `match` scope, optionally filtered by `typeId` and/or `tagId`;
- `add`, `multiply`, `min`, and `max`: recursive composition over two or more value expressions.

Example: gain Attack equal to twice the number of friendly Organic units:

```json
{
  "kind": "modifyStats",
  "target": { "scope": "self" },
  "attack": {
    "kind": "multiply",
    "values": [
      2,
      {
        "kind": "unitCount",
        "query": { "scope": "friendly", "typeId": "organic" }
      }
    ]
  }
}
```

Example: deal damage equal to the source's current Attack:

```json
{
  "kind": "dealDamage",
  "target": { "scope": "enemy", "selection": "random" },
  "amount": { "kind": "sourceStat", "stat": "attack" }
}
```

Example: give each target Health equal to its own current Health:

```json
{
  "kind": "modifyStats",
  "target": { "scope": "friendly", "selection": "all" },
  "health": { "kind": "targetStat", "stat": "health" }
}
```

Dynamic values are evaluated when their effect is applied, so an earlier effect may change the value read by a later effect in the same trigger. `targetStat` is evaluated separately for every resolved target. It is invalid on effects without unit targets such as `addResource` or `summonUnit`.

A dynamic `dealDamage` amount or `summonUnit` count that resolves to zero or below is a no-op. A dynamic `addResource` result of zero is also a no-op. Expression arithmetic uses checked integer operations so overflow fails explicitly instead of wrapping silently. Value-expression nesting is validator-bounded.

## Stateful event history

The engine records mechanical events separately from trigger names. Current event keys are `unitAcquired`, `unitReleased`, `unitPlayed`, `unitSummoned`, `unitDied`, `unitAttacked`, `unitDamaged`, `powerActivated`, `offerRefreshed`, and `tierUpgraded`.

History has three scopes:

- `turn`: cleared when the player begins the next Preparation turn;
- `combat`: exists only inside one isolated combat simulation;
- `match`: persists for the player's whole match.

`turn` and `match` history enter combat through the immutable combat input snapshot. Combat maintains its own `combat` history and returns only persistent turn/match deltas for settlement. An eliminated-opponent snapshot never writes its generated history back to the eliminated player.

A counted-event trigger is authored as:

```json
{
  "event": "afterEventCount",
  "counter": {
    "event": "unitAcquired",
    "scope": "turn",
    "typeId": "organic"
  },
  "count": 3,
  "effects": [
    { "kind": "addResource", "amount": 1 }
  ]
}
```

It fires when the matching counter crosses each multiple of `count`: with `count: 3`, activations happen at 3, 6, 9, and so on. Unit and Power sources use the same mechanism. `typeId` and `tagId` filters refer to the unit associated with the recorded event when that event has a unit subject.

A trigger may also declare a generic activation limit:

```json
"activationLimit": { "scope": "turn", "count": 1 }
```

This is the neutral representation of rules such as "once per turn". Limits are keyed by the stable runtime source (unit instance or Power ID) plus trigger index and are recorded before effects execute, preventing recursive re-entry from bypassing the limit.

Recorded gameplay events are not aliases for authored triggers. For example, `triggerEvent` may explicitly execute an `onDeath` trigger without killing a unit; this does **not** increment `unitDied`. Only an actual death does. Likewise, `unitSummoned`, `unitDamaged`, and other counters are incremented by the corresponding real mechanical occurrence.

## Resolution phases

A logical effect action resolves in this order:

1. evaluate the trigger's activation limit and conditions against the current effect-world snapshot;
2. if eligible, record its scoped activation before applying effects;
3. resolve authored effects in authored order;
4. evaluate each effect's dynamic values against the current runtime state as that effect is applied;
5. record real mechanical history events and resolve any `afterEventCount` threshold crossings they cause;
6. resolve directly-created follow-up triggers such as `onDamage`, `onSummon`, or `triggerEvent`;
7. once that event phase is complete, identify all units that are dead;
8. remove the complete simultaneous-death batch before resolving any death-related trigger;
9. resolve each death deterministically;
10. during that death batch, newly lethal units remain in play until the current batch finishes;
11. after a unit's death-related triggers resolve, attempt its revive-once behavior;
12. a successful revive is a real summon, increments `unitSummoned`, and runs normal `onSummon` listeners;
13. after the original batch is complete, create the next death batch if new deaths are pending.

This preserves the important Battlegrounds/Hearthstone invariants that simultaneous dead units cannot be targeted by each other's death effects, Deathrattle-like effects resolve before Reborn-like revival, and consequences of one death can affect listeners that observe a later death in the same batch.

`onCombine` is entered only after the combine transaction described above has produced its Reserve result. Its reward effects then follow the ordinary effect-runtime consequence/death ordering where applicable.

## Counted friendly-death trigger

The neutral trigger used for Avenge-like mechanics remains available as a death-specific convenience:

```json
{
  "event": "afterFriendlyDeaths",
  "count": 3,
  "effects": [
    { "kind": "addResource", "amount": 1 }
  ]
}
```

`count` is mandatory and must be positive for both counted trigger kinds: `afterFriendlyDeaths` and `afterEventCount`. `afterEventCount` additionally requires `counter`.

The `afterFriendlyDeaths` counter advances once for each actual friendly death while the listener remains in play. When it reaches `count`, the authored effects resolve and the counter resets, allowing repeated activation after another `count` deaths.

The engine names are intentionally neutral. A mod may present these mechanics as `Avenge`, another keyword, or no visible keyword at all.

## Death-related listener ordering

Actual deaths and their death-related listeners use deterministic runtime ordering. A dead unit's `onDeath` effects and living friendly `afterFriendlyDeaths` listeners are placed into the same ordered resolution set for that death. Reborn-like revival is always attempted after those triggers.

The runtime does not use a global event bus. Event creation, listener discovery, history recording, effect application, death batches, and follow-up events are explicit and bounded by the internal recursion safety budget.
