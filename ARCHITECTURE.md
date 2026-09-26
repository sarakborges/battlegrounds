# Battlegrounds Architecture

Architecture exists to make invalid ownership, mutation, theme coupling, and nondeterminism difficult to introduce.

## Core rules

1. `Battlegrounds.Core` is framework-free and must not reference Godot, JSON, filesystem, or presentation APIs.
2. The engine is mod-first. Core owns mechanics; the active mod owns theme, terminology, content, assets, and configurable numbers/policies.
3. Core public vocabulary must be fandom-neutral. Prefer mechanical roles such as `Unit`, `Leader`, `Power`, `Resource`, `Offer`, `Tier`, `Reserve`, `Field`, `Health`, and `Preparation`.
4. Theme words such as Gold, Tavern, Minion, Hero, or fandom-specific names must not become Core domain types, property names, commands, rule names, or IDs.
5. Authoritative mutable game state has one owner: the match aggregate and its owned aggregates.
6. UI and AI never mutate domain state directly. They issue explicit commands through the same domain boundary.
7. Unit, Leader, and Power definitions are immutable authored data; runtime instances/state contain mutable facts only.
8. Randomness is injected through an explicit deterministic RNG abstraction and seeded per match.
9. Lifecycle logic is modeled with explicit state/phase types, not loosely related booleans.
10. Domain collections expose read-only views. Mutation stays beside the invariant it protects.
11. Read models/snapshots may be shaped for UI or AI but are never authoritative mutation owners.
12. New abstractions must correspond to an observed invariant or real boundary; no speculative frameworks.
13. Trigger/effect semantics belong to the game domain, not to Preparation or Combat. Phases provide state adapters to one shared effect runtime.
14. Combat simulation is isolated. Persistent player health/resource/lifecycle changes happen only during explicit post-combat settlement.
15. Every authored content entity with a stable ID gets one source file. Do not create aggregate entity arrays such as `units.json`, `leaders.json`, `powers.json`, `types.json`, or `behaviors.json`.

## Mod boundary

Mods live under `mods/<mod-id>/` and may provide package metadata, terminology, rules, units, leaders, powers, pool composition, effects/taxonomy, localization, and presentation assets.

`Battlegrounds.Content` reads and validates untrusted mod files and maps them into validated Core models. Core never knows which directory, JSON document, fandom, localization, or asset produced those models.

There is no canonical `Standard` rules object in Core. Defaults that define gameplay belong to a mod package.

### One entity per file

ID-addressable authored content is stored by category and ID:

```text
content/
  units/<unit-id>.json
  leaders/<leader-id>.json
  powers/<power-id>.json
  behaviors/<behavior-id>.json
  types/<type-id>.json
  tags/<tag-id>.json
```

The entity ID must equal the file stem. `content/units/foo.json` must contain `"id": "foo"`; otherwise validation fails.

Future ID-addressable content follows the same convention. Spells, artifacts, quests, anomalies, or equivalent concepts get one file per entity rather than an array catalog.

Aggregate documents are allowed only for genuinely aggregate/package-wide facts such as `mod.json`, `rules/*.json`, or current pool composition in `content/pool.json`.

Validation errors for authored entities must identify the concrete source file and JSON path so presentation can expose useful diagnostics without re-running validation logic.

## Neutral identifiers

IDs identify stable mechanical/content entities, not themed labels. Examples: `PlayerId`, `UnitId`, `UnitInstanceId`, `LeaderId`, `PowerId`, `BehaviorId`, `UnitTypeId`, and `TagId`.

Display names are data. Renaming a displayed resource from "Gold" to "Data", a displayed Leader from "Hero" to "Tamer", or a displayed Power from "Hero Power" to "Skill" must not require changing Core code or persisted mechanical IDs.

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

`MatchState` owns authoritative lifecycle, round/revision, player collection, elimination state, placement/history, and terminal winner state.

`MatchRules` are injected from the active mod and currently include player-count constraints and generic starting Health.

`MatchEngine` owns cross-phase orchestration: starting a match, resolving player/Leader setup, delegating Preparation commands, resolving explicit combat pairings, applying post-combat settlement, carrying combat resource deltas into the next Preparation, and ending the match.

Pairing selection itself is not hidden in `MatchEngine`. A round receives explicit `CombatPairing` values so matchmaking policy can evolve independently.

### Player

Owns player-scoped runtime facts: generic Health, elimination, selected `LeaderState`, resource amount, tier, reserve, field, current offer, upgrade cost, and readiness. External consumers cannot mutate these directly.

### Leader

`LeaderDefinition` is immutable authored data resolved by stable `LeaderId`. `LeaderState` is player-owned runtime state.

Starting Health is calculated from mod-wide `MatchRules.StartingHealth` plus the selected Leader's `HealthModifier`; that result must remain positive. Starting Armor comes from the selected Leader definition and is consumed before Health when player damage is applied.

A Leader definition references only `InitialPowerId`. It does not embed or own a `PowerDefinition`. `LeaderState.CurrentPowerId` is authoritative mutable state and may change during the match without mutating the immutable Leader definition.

Power-use counters belong to `LeaderState` and are keyed by `PowerId`. Replacing a power and later returning to it must not erase usage history accidentally.

A real mod-backed match must receive explicit `PlayerSetup(PlayerId, LeaderId)` values. Callers choose IDs; they do not directly supply Leader stats.

### Power

`PowerDefinition` is an independent immutable authored entity resolved by `PowerId` through `PowerCatalog`.

A power owns activation data such as cost, per-turn/per-match usage limits, and ordered effect definitions. It does not own Leader state and it is not nested inside a Leader definition.

Active power use enters through `UsePowerCommand`. Power effects reuse `GameEffectRuntime`; there is no leader-only/power-only effect engine. Explicit UI/AI target selection is represented mechanically by the `selected` target scope rather than by direct state mutation.

`setPower` changes the player's current `PowerId` through the authoritative state adapter. The initial power reference remains unchanged.

### Unit catalog

Owns immutable `UnitDefinition` lookup by stable `UnitId`. Definitions are distinct from runtime `UnitInstance` state.

### Unit pool

Owns shared availability/copy counts and deterministic offer selection. Pool composition is supplied by mod data.

### Effects

`GameEffectRuntime` owns trigger dispatch, effect semantics, deterministic target resolution, explicit consequence ordering, counted listeners, and death-wave resolution. It is phase-neutral.

Preparation and Combat must not implement their own copies of `dealDamage`, `destroyUnit`, `summonUnit`, `triggerEvent`, behavior mutation, or death-trigger semantics. Power activation also reuses this runtime instead of introducing a parallel executor.

A real death and an explicitly triggered `onDeath` are different operations: real death enters the death/revive lifecycle; `triggerEvent(onDeath)` only executes authored `onDeath` effects on the selected unit.

### Preparation

Owns pre-combat actions, economy mechanics, current active-power invocation, and the persistent authoritative state adapter used by `GameEffectRuntime`. Eliminated players do not enter Preparation or receive offers/resources.

`PreparationRules` are supplied by mod data. Commands describe mechanical intent: acquire, release, deploy, refresh, tier upgrade, power use, freeze/unfreeze, and end Preparation.

### Combat

Owns combat-local simulation state, attack selection/rotation, simultaneous attack damage, and the isolated state adapter used by `GameEffectRuntime`. Given the same validated combat input and seed, it must produce the same ordered result.

Combat-local effects must never mutate persistent Preparation state directly. Persistent consequences leave combat as explicit `CombatResult` data such as winner/survivors/resource deltas.

### Post-combat settlement

Settlement belongs to match orchestration, not combat simulation. A native damage policy selected by mod data converts `CombatResult` plus authoritative player state into player damage.

The current policy `winnerTierPlusSurvivorTiers` uses winner Tier plus surviving unit Tiers; draw deals zero. Generated/token survivors not present in the initial combat snapshot currently default to Tier 1.

Incoming player damage is absorbed by Leader Armor first, then reduces Health. Settlement result data records the absorption and resulting values explicitly.

If settlement eliminates all but one player, the match enters `Finished`. Otherwise the next Preparation begins and any combat resource deltas are applied after the new round's baseline resource is initialized and before `onTurnStart` effects.

## Data-driven rule

Prefer data for repeated/configurable variants and code for algorithms/invariants.

Data should own, when representable:

- terminology and display names;
- unit, Leader, Power, behavior, type and tag definitions;
- Leader starting modifiers such as Health delta and Armor;
- Leader initial-power references;
- Power cost, usage limits and effects;
- starting Health and numeric costs/rewards/capacities;
- player-count constraints;
- selectable native policies such as starting-side and post-combat-damage policy;
- tier limits and offer sizes;
- unit stats/tags/types;
- pool copy counts;
- effect parameters;
- presentation metadata.

Core code should own command validation, state transitions, ownership invariants, deterministic selection/order, combat algorithms, settlement algorithms, trigger ordering, armor application, power-usage accounting, and effect execution semantics.

Do not add a themed hardcoded default to Core merely because one mod currently needs it.

## Mutation model

All meaningful authoritative mutations enter through intent-revealing domain boundaries. UI and AI use the same path.

Effect-driven mutations enter through `GameEffectRuntime`. `setPower` changes only `LeaderState.CurrentPowerId`; immutable authored Leader/Power definitions remain unchanged. Combat produces isolated results; `MatchEngine` applies their persistent settlement consequences.

Public setters on authoritative runtime state are forbidden unless a type is explicitly a DTO/read model.

## Determinism

Simulation correctness must not depend on wall-clock time, Godot frame timing, hash/dictionary iteration order, global random state, filesystem enumeration order, or machine-specific ordering.

Content files are sorted ordinally before materialization. Randomness is injected, order-sensitive candidates use deterministic tie-breakers, simulation time is logical, and replay/debug state must record enough information to reproduce behavior.

## Effects

Current trigger vocabulary includes `onPlay`, `onSummon`, `onAttack`, `onDamage`, `onDeath`, `onCombatStart`, `onCombatEnd`, `onTurnStart`, `onTurnEnd`, and counted `afterFriendlyDeaths`.

Current generic effects include stat modification, damage, destruction, explicit trigger activation, summon, behavior add/remove, resource adjustment, and current-power replacement.

Display names such as Battlecry, Deathrattle, Avenge, Reborn, or Hero Power belong to mod presentation/content. Do not introduce a global event bus; dispatch belongs to `GameEffectRuntime` with defined ordering and failure semantics.

## Testing contract

Core tests must run without Godot or mod filesystem access. Content tests protect the mod boundary separately.

Priority coverage:

- allowed/invalid phase transitions;
- legal command validation;
- state ownership and player elimination;
- Leader selection, Health modifiers and Armor;
- Power activation, usage limits, selected targets and runtime replacement;
- deterministic ordering/RNG;
- reserve/field capacity and preparation economy;
- pool copy invariants;
- per-entity-file mod validation/mapping;
- ID/file-name agreement;
- cross-file Leader/Power references;
- cross-phase effect semantics and death waves;
- combat resolution;
- post-combat damage and full round-loop settlement;
- bug regressions.

## Performance contract

Correctness and clarity first; optimize from measurements. Avoid rendering/content-loader dependencies in hot simulation paths, repeated derived work, unbounded parallel AI work, and stale AI results.

## Review gate

Reject or refactor changes that introduce without a strong reason:

- fandom/theme terminology into Core mechanics;
- hardcoded gameplay presets that belong to mods;
- aggregate JSON arrays for ID-addressable authored entities;
- entity IDs that disagree with their source file name;
- Leader definitions embedding mutable/current Power state or entire Power definitions;
- phase-specific copies of shared effect mechanics;
- leader/power-specific copies of shared effect mechanics;
- combat simulation mutating persistent match state directly;
- multiple mutable owners for one fact;
- Godot/filesystem/JSON types inside Core;
- UI/AI direct mutation;
- static mutable state or ambient/global randomness;
- unstable ordering where ordering affects results;
- `Manager`/`Utils` dumping grounds;
- deep inheritance for unit behavior;
- hidden mutation/control flow;
- unbounded queues/tasks/caches;
- serialization models used directly as domain models;
- duplicated business rules.

When adding a component, answer: what invariant does it own, why does it exist, which direction do dependencies point, which parts are mod data, and what test protects it?
