# Combat event timeline

`CombatResult.Timeline` is an immutable, ordered observation of visible combat-local transitions produced while Core resolves one combat exactly once.

The timeline exists for presentation, replay tooling and diagnostics. It is result data only: simulation never reads it back, matchmaking never depends on it, and Godot must not use it to decide gameplay outcomes.

## Ordering

Every event has a monotonically increasing `Sequence` assigned at the authoritative mutation boundary inside Combat. Consumers should process the list in its existing order and must not reconstruct ordering from timestamps or IDs.

The timeline can include:

- `Trigger` — a Unit or Power trigger that resolves effects;
- `AttackStarted` — the selected attacker and target before strike damage is applied;
- `UnitSummoned` — combat-local identity, current snapshot, source and insertion position;
- `UnitStatsChanged` — attack/health before and after a trigger-driven stat mutation;
- `UnitDamaged` — amount plus health before/after, including damage outside ordinary attacks;
- `UnitDestroyed` — explicit destruction before the normal death wave removes the Unit;
- `UnitDied` — removal from the combat field with the position it occupied;
- `UnitRevived` — the revived snapshot and reinsertion position;
- `BehaviorChanged` — add/remove/consume transitions for native behaviors;
- `ResourceChanged` — combat-local resource deltas that later settle through `MatchEngine`;
- `PowerChanged` — current-Power transitions that later settle through `MatchEngine`.

`CombatAttack` remains available as a compact strike summary for rules/tests/analytics. It is no longer the complete presentation event stream.

## Combat-local Unit identity

Summoned Units may not exist in the persistent starting field. `CombatUnitSummonedTimelineEvent` therefore carries the generated `UnitInstanceId`, `UnitId`, definition-backed display identity, current stats/behaviors and exact insertion position.

Death does not erase that identity from the result stream. A later `UnitRevived` event reuses the same `UnitInstanceId`, so replay clients can remove and reinsert the same visual entity deterministically.

## Triggers

The shared `GameEffectRuntime` notifies its world whenever a trigger actually resolves one or more effects. Combat converts that observation into a `CombatTriggerTimelineEvent` identifying the owning player and either the source Unit instance or source Power.

This covers ordinary lifecycle triggers plus nested/runtime-driven paths such as `onDamage`, `onDeath`, `onSummon`, counted friendly deaths and event-count listeners without introducing a Combat-specific effect engine.

## Death waves

The existing death-wave semantics remain authoritative. Units that are dead are removed as a wave before death-related effects resolve. The timeline records those removals and then records the resulting trigger/effect events in the same deterministic order used by the runtime.

A revive is emitted only when `TryRevive(...)` succeeds and the Unit has actually been reinserted into combat state.

## Settlement boundary

Combat resource changes, Power changes and effect-history changes are still result data settled by `MatchEngine` after simulation. Timeline events merely expose the visible transition that occurred inside the isolated combat simulation.

Likewise, player damage/Armor/Health settlement remains represented by `CombatSettlement`; it is intentionally not simulated again as a combat-local Unit event.

## Godot playback

`SessionCombatRecord` freezes the paired starting boards before resolution and retains the authoritative `CombatRoundResult` afterward. `CombatPlaybackState` initializes from those snapshots, then applies `CombatResult.Timeline` one event at a time.

Godot may auto-step, manually step, skip to settlement, animate, highlight or play audio from these events. It must never rerun effects, recalculate targets, create authoritative Units or delay Core settlement.
