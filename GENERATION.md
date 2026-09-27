# Generation and choice semantics

Generation is a mod-neutral engine mechanic. It creates generated unit instances without consuming or returning copies from the shared `UnitPool`.

## Generate directly to Reserve

A preparation-only trigger can generate one or more exact units into the owner's Reserve:

```json
{
  "kind": "generateUnitToReserve",
  "unitId": "guard",
  "count": 1
}
```

Generated instances use `UnitInstanceOrigin.Generated`. The effect fills only currently available Reserve capacity and otherwise stops; it never evicts existing units and never touches pool inventory.

Pending choices reserve future Reserve slots, so direct generation cannot consume capacity already promised to queued choices.

## Generate a unit choice

A mod can create a choice from the unit catalog:

```json
{
  "kind": "generateUnitChoice",
  "generationQuery": {
    "minimumTier": 1,
    "maximumTier": 3,
    "typeId": "organic",
    "excludeSource": true
  },
  "optionCount": 3
}
```

The query may filter by `minimumTier`, `maximumTier`, `typeId`, `tagId`, and `excludeSource`. All fields are optional. Options are unique by unit id and are sampled without replacement through the injected deterministic RNG. If fewer eligible definitions exist than `optionCount`, every eligible definition is offered. If no eligible definition exists, no choice is queued.

The Core intentionally has no `Discover` type or keyword. A mod may present this mechanic as Discover, Scan, Search, Decode, Recruit, or any other themed term.

## Authoritative pending choice state

Choices are stored on `PlayerState`. The public `PendingChoice` exposes the current `ChoiceId` and immutable unit-definition options for UI/AI read models.

A choice is resolved through the same command path used by every controller:

```csharp
new ResolveUnitChoiceCommand(playerId, choiceId, optionIndex)
```

While a player has a pending choice, normal preparation commands are rejected with `PendingChoiceMustBeResolved`. This prevents state changes between option generation and resolution and keeps human and AI behavior identical.

Multiple choice effects may be queued. Only the current choice is exposed and resolved; the next queued choice then becomes current. Each queued choice reserves one future Reserve slot, preventing chained choices from deadlocking because earlier resolutions consumed all capacity.

Resolving a choice creates a `Generated` unit instance and adds it to Reserve. It does not consume the shared unit pool.

## Phase boundary

`generateUnitToReserve` and `generateUnitChoice` are currently preparation-only effects. The content validator allows them only from preparation-exclusive triggers:

- unit triggers: `onPlay`, `onTurnStart`, `onTurnEnd`;
- power triggers: `onActivate`, `onMatchStart`, `onTurnStart`, `onTurnEnd`.

The shared effect runtime exposes generation through an optional preparation adapter instead of adding Reserve mutation to Combat. Combat therefore remains isolated and cannot mutate authoritative preparation collections.
