# Field ordering

Field order is authoritative persistent gameplay state. It affects Combat because the combat snapshot preserves the Preparation Field sequence and attack rotation walks that sequence.

## Preparation command

Controllers change order only through:

```csharp
new ReorderFieldCommand(playerId, orderedUnitInstanceIds)
```

`orderedUnitInstanceIds` is the complete desired Field order, expressed with runtime `UnitInstanceId` values. Core accepts the command only when it contains every Unit currently on that player's Field exactly once.

The command rejects:

- missing Field Units;
- duplicate runtime Unit ids;
- Units that are not currently on that player's Field;
- a list with a different size from the current Field;
- ordinary Preparation boundary violations such as wrong phase, eliminated/ready player, or unresolved pending choice.

A successful reorder preserves Unit identity, stats, runtime behaviors, modifiers, ownership, pool ownership and Field membership. It changes only sequence. It costs no Resource, consumes no RNG, and does not synthesize `onPlay`, `onSummon`, release, acquire or other gameplay events.

## Ownership

`PlayerState` owns the mutable Field list. UI and AI never reorder that list directly. `PreparationEngine` validates `ReorderFieldCommand`, then applies the validated sequence through the aggregate's internal mutation boundary.

Combat remains isolated: it receives the already-ordered persistent Field as an immutable combat input snapshot and does not write combat-local ordering changes back into Preparation.

## Human presentation

The current Godot Field row shows small left/right ordering controls around normal Field cards. A click derives a complete order from the current authoritative `PlayerState.Field` and submits `ReorderFieldCommand` through `SinglePlayerSession.ExecuteHumanPreparation(...)`.

Ordering controls are unavailable while a pending choice or another multi-step interaction is active, after the player is ready, and outside Preparation. The Unit card itself retains its existing release interaction.

A future drag-and-drop presentation may replace the arrow affordance without changing the Core command contract.

## AI

The baseline `PreparationAiAgent` performs one final ordering pass after its composition/economic actions and before freezing/ending Preparation. It sorts Units by the same theme-neutral mechanical/strategy score it already uses for composition, then uses runtime Unit id as a stable tie-breaker.

The baseline heuristic is intentionally simple. More tactical policies may later value death order, adjacency, attack sequencing or archetype-specific positioning differently, but they must continue to express the final sequence through the same `ReorderFieldCommand` boundary.

AI reorder telemetry is counted as a successful Preparation command only after Core accepts it.
