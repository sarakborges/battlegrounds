# Effect and death semantics

Effects and triggers are game-domain mechanics. They are not owned by Preparation or Combat. Both phases execute authored mechanics through the shared `GameEffectRuntime` and provide only the state adapter appropriate to that phase.

## Resolution phases

A logical effect action resolves in this order:

1. resolve the authored trigger and its effects in authored order;
2. resolve directly-created follow-up events such as `onDamage`, `onSummon`, or `triggerEvent`;
3. once that event phase is complete, identify all units that are dead;
4. remove the complete simultaneous-death batch before resolving any death-related trigger;
5. resolve each death deterministically;
6. during that death batch, newly lethal units remain in play until the current batch finishes;
7. after a unit's death-related triggers resolve, attempt its revive-once behavior;
8. a successful revive is a summon and runs normal `onSummon` listeners;
9. after the original batch is complete, create the next death batch if new deaths are pending.

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
