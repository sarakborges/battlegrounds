# Battlegrounds Architecture

Architecture exists to make invalid ownership, mutation, theme coupling, and nondeterminism difficult to introduce.

## Core rules

1. `Battlegrounds.Core` is framework-free and must not reference Godot, JSON, filesystem, or presentation APIs.
2. The engine is mod-first. Core owns mechanics; the active mod owns theme, terminology, content, assets, and configurable numbers.
3. Core public vocabulary must be fandom-neutral. Prefer mechanical roles such as `Unit`, `Resource`, `Offer`, `Tier`, `Reserve`, `Field`, and `Preparation`.
4. Theme words such as Gold, Tavern, Minion, Hero, or fandom-specific names must not become Core domain types, property names, commands, rule names, or IDs.
5. Authoritative mutable game state has one owner: the match aggregate and its owned aggregates.
6. UI and AI never mutate domain state directly. They issue explicit commands through the same domain boundary.
7. Unit definitions are immutable authored data; unit instances contain runtime state only.
8. Randomness is injected through an explicit deterministic RNG abstraction and seeded per match.
9. Lifecycle logic is modeled with explicit state/phase types, not loosely related booleans.
10. Domain collections expose read-only views. Mutation stays beside the invariant it protects.
11. Read models/snapshots may be shaped for UI or AI but are never authoritative mutation owners.
12. New abstractions must correspond to an observed invariant or real boundary; no speculative frameworks.
13. Trigger/effect semantics belong to the game domain, not to Preparation or Combat. Phases provide state adapters to one shared effect runtime.

## Mod boundary

Mods live under:

```text
mods/<mod-id>/
```

A mod package may provide:

- package metadata and schema version;
- display terminology;
- match/preparation/combat rules;
- unit definitions;
- pool composition;
- effects and tags;
- localization;
- art/audio and other presentation assets.

`Battlegrounds.Content` is the adapter that reads and validates untrusted mod files and maps them into validated Core models. Core never knows which directory, JSON document, fandom, localization, or asset produced those models.

There is no canonical `Standard` rules object in Core. Defaults that define gameplay belong to a mod package.

## Neutral identifiers

IDs identify stable mechanical/content entities, not themed labels. Examples:

- `PlayerId`
- `UnitId`
- `UnitInstanceId`

Display names are data. Renaming a displayed resource from "Gold" to "Data" must not require changing Core code or persisted mechanical IDs.

If cross-mod qualification becomes necessary, qualification belongs at the content/package boundary rather than by baking fandom names into Core types.

## Dependency direction

```text
Battlegrounds.Game (Godot presentation)
          |
          +-------------------+
          v                   v
Battlegrounds.Content      Battlegrounds.Core
(mod loading/validation)       ^
          |                     |
          +---------------------+
```

A future `Battlegrounds.Application` layer may orchestrate use cases between Game, Content, and Core. Dependencies still point inward toward stable mechanical concepts.

## Domain ownership

### Match

Owns authoritative lifecycle, round/revision, and player collection. Player-count constraints are injected as `MatchRules` from the active mod.

### Player

Owns player-scoped runtime facts: generic resource amount, tier, reserve, field, current offer, upgrade cost, and readiness. External consumers cannot mutate these collections directly.

### Unit catalog

Owns immutable `UnitDefinition` lookup by stable `UnitId`. Definitions are distinct from runtime `UnitInstance` state.

### Unit pool

Owns shared availability/copy counts and deterministic offer selection. Pool composition is supplied by mod data.

### Effects

`GameEffectRuntime` owns trigger dispatch, effect semantics, deterministic target resolution, explicit consequence ordering, and the death/trigger chain. It is phase-neutral.

Preparation and Combat must not implement their own copies of `dealDamage`, `destroyUnit`, `summonUnit`, `triggerEvent`, behavior mutation, or death-trigger semantics. They expose only the state operations required by the shared runtime.

A real death and an explicitly triggered `onDeath` are different operations: real death enters the death/revive lifecycle; `triggerEvent(onDeath)` only executes authored `onDeath` effects on the selected living unit.

### Preparation

Owns pre-combat actions, economy mechanics, and the persistent authoritative state adapter used by `GameEffectRuntime`. `PreparationRules` are supplied by mod data. Commands describe mechanical intent:

- `AcquireUnit`
- `ReleaseUnit`
- `DeployUnit`
- `RefreshOffer`
- `UpgradeTier`
- `FreezeOffer`
- `UnfreezeOffer`
- `EndPreparation`

### Combat

Owns combat-local simulation state, attack selection/rotation, simultaneous attack damage, and the isolated state adapter used by `GameEffectRuntime`. Given the same validated combat input and seed, it must produce the same ordered result. Combat-local effects must never mutate persistent Preparation state directly.

## Data-driven rule

Prefer data for repeated/configurable variants and code for algorithms/invariants.

Data should own, when representable:

- terminology and display names;
- numeric costs/rewards/capacities;
- player-count constraints;
- tier limits and offer sizes;
- unit stats/tags/types;
- pool copy counts;
- effect parameters;
- presentation metadata.

Core code should own:

- command validation;
- state transitions;
- ownership invariants;
- deterministic selection/order;
- damage/combat algorithms;
- trigger ordering and effect execution semantics.

Do not add a themed hardcoded default to Core merely because one mod currently needs it.

## Mutation model

All meaningful authoritative mutations enter through intent-revealing commands validated by the domain owner. UI and AI use the same command path.

Effect-driven mutations enter through the shared `GameEffectRuntime`; phase adapters expose narrow mutation capabilities but do not duplicate effect algorithms.

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
- order-sensitive candidates use deterministic tie-breakers;
- simulation time is logical, not frame time;
- replay/debug state records enough information to reproduce behavior.

## Effects

Effects are composable game-domain behaviors. Prefer narrow effects/triggers over deep unit-class inheritance.

Current trigger vocabulary includes:

- `onPlay`
- `onSummon`
- `onAttack`
- `onDamage`
- `onDeath`
- `onCombatStart`
- `onCombatEnd`
- `onTurnStart`
- `onTurnEnd`

Current generic effects include stat modification, damage, destruction, explicit trigger activation, summon, behavior add/remove, and resource adjustment.

Display names such as Battlecry or Deathrattle belong to mod presentation/content. The underlying `onPlay` / `onDeath` mechanics are not owned by a specific phase. Do not introduce a global event bus; dispatch belongs to `GameEffectRuntime` with defined ordering and failure semantics.

## Testing contract

Core tests must run without Godot or mod filesystem access. Content tests protect the mod boundary separately.

Priority coverage:

- allowed/invalid phase transitions;
- legal command validation;
- state ownership;
- deterministic ordering/RNG;
- reserve/field capacity rules;
- preparation economy rules;
- pool copy invariants;
- mod validation/mapping;
- cross-phase effect semantics;
- combat resolution;
- bug regressions.

## Performance contract

Correctness and clarity first; optimize from measurements.

For simulations and AI search:

- avoid rendering/content-loader dependencies in hot simulation paths;
- avoid repeated derived work when source revision is unchanged;
- use compact/data-oriented representations only after profiling justifies them;
- bound parallel AI work;
- reject stale AI results using match/state revisions.

## Review gate

Reject or refactor changes that introduce without a strong reason:

- fandom/theme terminology into Core mechanics;
- hardcoded gameplay presets that belong to mods;
- phase-specific copies of shared effect mechanics;
- multiple mutable owners for one fact;
- Godot/filesystem/JSON types inside Core;
- UI/AI direct mutation;
- static mutable state;
- ambient/global randomness;
- unstable ordering where ordering affects results;
- `Manager`/`Utils` dumping grounds;
- deep inheritance for unit behavior;
- hidden mutation/control flow;
- unbounded queues/tasks/caches;
- serialization models used directly as domain models;
- duplicated business rules.

When adding a component, answer: what invariant does it own, why does it exist, which direction do dependencies point, which parts are mod data, and what test protects it?
