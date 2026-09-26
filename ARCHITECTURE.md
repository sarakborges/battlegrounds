# Battlegrounds Architecture

This repository follows the engineering rules defined in the project's Code Best Practices guide. Architecture exists to make invalid ownership and mutation paths difficult to introduce.

## Core rules

1. `Battlegrounds.Core` is framework-free. It must not reference Godot APIs.
2. Authoritative game state has one owner: the match aggregate.
3. UI and AI never mutate domain state directly. They issue explicit commands.
4. Card definitions are immutable authored data; card/minion instances contain runtime state only.
5. Randomness is injected through an explicit deterministic RNG abstraction and seeded per match.
6. Lifecycle logic is modeled with explicit state/phase types, not loosely related booleans.
7. Domain collections expose read-only views. Mutation stays beside the invariant it protects.
8. Serialization, Godot resources, filesystem access, rendering, audio, input and diagnostics live outside the core.
9. Read models/snapshots may be shaped for UI or AI but are never authoritative mutation owners.
10. New abstractions must correspond to an observed invariant or real boundary; no speculative frameworks.

## Dependency direction

```text
Battlegrounds.Game (Godot UI / presentation)
          |
          v
Battlegrounds.Application (orchestration / use cases)
          |
          v
Battlegrounds.Core (domain / simulation)
```

`Battlegrounds.Core` depends only on the .NET base class library.

Infrastructure adapters may depend inward on Core/Application. Core never depends outward on them.

## Domain ownership

### Match
Owns the authoritative lifecycle and player collection for one local game.

### Player
Owns health, gold, tavern tier, hand, board and player-scoped runtime facts. External consumers cannot mutate those collections directly.

### Tavern
Owns shop-generation and refresh rules. It receives explicit randomness instead of using ambient/global random state.

### Combat
Owns combat resolution. Given the same combat input and seed, it must produce the same ordered result.

### Catalog
Owns immutable hero/card/minion definitions and ID lookups. Definitions are distinct from runtime instances.

## Mutation model

All meaningful mutations enter through intent-revealing commands, for example:

- `BuyMinion`
- `SellMinion`
- `PlayMinion`
- `MoveMinion`
- `RefreshTavern`
- `UpgradeTavern`
- `EndRecruitment`

Commands are validated by the domain owner. UI and AI use the same command path.

Public setters on authoritative runtime state are forbidden unless a type is explicitly a DTO/read model.

## Determinism

Simulation correctness must not depend on:

- wall-clock time;
- Godot frame timing;
- hash/dictionary iteration order;
- global random state;
- machine-specific ordering.

Rules:

- random source is injected;
- match seed is explicit;
- order-sensitive candidates are ordered with deterministic tie-breakers;
- simulation time is logical (turn/phase/sequence), not frame time;
- replay/debug state records enough information to reproduce behavior.

## Effects

Effects are composable domain behaviors. Prefer narrow effects/triggers over deep card-class inheritance.

Initial trigger vocabulary may include:

- `OnPlay`
- `OnSell`
- `OnSummon`
- `OnAttack`
- `OnDamage`
- `OnDeath`
- `OnDeathrattle`
- `OnCombatStart`
- `OnCombatEnd`
- `OnTurnStart`
- `OnTurnEnd`

Do not introduce a global event bus. Dispatch belongs to an explicit owner with defined ordering and failure semantics.

## Testing contract

Core tests must run without starting Godot.

Tests protect invariants rather than private implementation details. Priority coverage:

- allowed/invalid phase transitions;
- legal command validation;
- state ownership;
- deterministic ordering;
- deterministic RNG/replay;
- board/hand capacity rules;
- tavern economy rules;
- combat resolution;
- bug regressions.

## Performance contract

Correctness and clarity first; optimize from measurements.

For simulations and AI search specifically:

- avoid rendering dependencies;
- avoid repeated derived work when source revision is unchanged;
- use compact/data-oriented representations only after profiling justifies them;
- bound parallel AI work;
- reject stale AI results using match/state revisions.

## Review gate

A change should be rejected or refactored when it introduces any of the following without a strong reason:

- multiple mutable owners for one fact;
- Godot types inside Core;
- UI/AI direct mutation;
- static mutable state;
- ambient/global randomness;
- unordered behavior where ordering affects results;
- `Manager`/`Utils` dumping grounds;
- deep inheritance for card behavior;
- hidden mutation or hidden control flow;
- unbounded queues/tasks/caches;
- serialization models used as the domain model;
- business rules duplicated across consumers.

When adding a component, answer: what invariant does it own, why does it exist, which direction do dependencies point, and what test protects it?
