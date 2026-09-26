# Battlegrounds

A local single-player auto-battler engine built with Godot 4 + C#, designed from the start to be **fully mod-based**.

The engine is not a Warcraft-specific rules implementation. A match is driven by one selected mod package, and different mods can provide different fandoms, terminology, units, balance values, pool sizes, assets, and presentation.

## Core rule: mechanics are neutral, theme belongs to mods

`Battlegrounds.Core` uses neutral mechanical names only. Display terminology is data owned by the active mod.

Examples:

| Core concept | A mod may display it as |
| --- | --- |
| `Unit` | Minion, Digimon, Fighter, Creature, etc. |
| `Resource` | Gold, Data, Credits, Energy, etc. |
| `Offer` | Tavern, Market, Portal, Draft, etc. |
| `Tier` | Tavern Tier, Level, Rank, Stage, etc. |
| `Reserve` | Hand, Bench, Roster, etc. |
| `Field` | Board, Arena, Team, etc. |
| `UnitType` | Beast, Demon, Vaccine, Machine, etc. |
| `Tag` | any mod-defined selector or metadata |

The Core must never encode fandom-specific terminology into IDs, state, commands, rules, or algorithms. Internal IDs describe stable mechanical roles (`UnitId`, `PlayerId`, `UnitInstanceId`, `BehaviorId`, `UnitTypeId`, `TagId`) rather than presentation names.

There is intentionally no `Standard` gameplay preset in Core. Numeric rules are supplied by the selected mod.

## Native behaviors, mod-defined identities

Reusable mechanics are implemented once in Core under neutral native handler keys. Mods choose their own behavior IDs and display names and map them to those handlers.

The current native handlers are:

- `damageBarrier` — blocks the first positive damage event for that life;
- `targetPriority` — restricts target selection while an eligible priority target exists;
- `reviveOnce` — returns after a real death with 1 Health once, without the revive behavior;
- `lethalFirstDamagePerCombat` — the first unit actually damaged by this unit is destroyed, then the behavior is consumed for that life;
- `extraAttack` — performs one additional consecutive strike during that unit's attack activation.

## Triggers and authored effects are game mechanics, not phase mechanics

Battlecry-like, Deathrattle-like, summon, damage, destroy, buffs and similar mechanics do **not** belong to Preparation or Combat. They belong to the shared game effect runtime. Preparation and Combat only provide different state adapters to that runtime.

For example, `onDeath` can be caused by combat damage, a Preparation effect that destroys a friendly unit, or an authored effect that explicitly triggers another unit's `onDeath`. The trigger/effect semantics stay the same.

Supported trigger keys currently are:

- `onPlay`
- `onDeath`
- `onSummon`
- `onAttack`
- `onDamage`
- `onCombatStart`
- `onCombatEnd`
- `onTurnStart`
- `onTurnEnd`

Supported effect kinds currently are:

- `modifyStats`
- `dealDamage`
- `destroyUnit`
- `triggerEvent`
- `summonUnit`
- `addBehavior`
- `removeBehavior`
- `addResource`

`triggerEvent` can explicitly activate a supported trigger on selected units without pretending the underlying event happened naturally. For example, a mod may activate a friendly unit's `onDeath` without destroying it. `destroyUnit` performs an actual destruction, which then enters the normal death lifecycle and resolves `onDeath`.

Targeted effects use neutral scopes (`self`, `randomFriendly`, `randomEnemy`, `allFriendly`, `allEnemy`) and can filter by mod-defined `typeId` and/or `tagId`.

The shared `GameEffectRuntime` owns event ordering and effect semantics. Consequences are enqueued explicitly (`onPlay → damage/destroy → onDamage/onDeath → summon → onSummon`) rather than hidden behind a global event bus. Random target resolution uses injected deterministic RNG.

Preparation supplies an authoritative persistent effect world. Combat supplies an isolated combat-local effect world. The same authored effect therefore behaves consistently without Combat mutating persistent Preparation state.

Played units execute `onPlay` and then participate in `onSummon`; generated units execute `onSummon` but not `onPlay`. `onSummon` is observable by living friendly field units. Real deaths remove the unit from the field before `onDeath` resolves so death effects can use the vacated slot. Manually triggering `onDeath` does not destroy the unit or invoke the real death/revive lifecycle.

Runtime unit behaviors are mutable instance state: `addBehavior`/`removeBehavior` persist into later combat snapshots without mutating immutable unit definitions.

Unit origin is explicit. `Pooled` units return their copy to the shared pool when permanently removed; `Generated` units do not create pool copies when released or destroyed.

## Mod validation is mandatory

`ModLoader.Load(...)` validates the complete mod before materializing a `ModPackage`. Invalid mods are rejected as a whole.

`ModLoader.Validate(...)` returns a structured report suitable for the future UI. Validation covers:

- required files and keys;
- unknown keys;
- JSON types and value ranges;
- duplicate IDs and references;
- cross-file references;
- unsupported native handlers/triggers/effects;
- type/tag/unit/behavior references;
- effect-specific required parameters.

Conditional parameters are validated explicitly. For example, a trigger without `effects`, `dealDamage` without `amount`, `destroyUnit` without `target`, or `triggerEvent` without `event` produces `MISSING_REQUIRED_PARAMETER` at the exact JSON path.

## Mod layout

User-facing content lives under `/mods`, with one directory per mod:

```text
mods/
  <mod-id>/
    mod.json
    rules/
      match.json
      preparation.json
      combat.json
    content/
      behaviors.json
      types.json
      tags.json
      units.json
      pool.json
    assets/                 # future
    localization/           # future
```

`mod.json` owns package identity and display terminology. Rule files own numbers and policies. Content files own behavior identities, unit taxonomy, authored triggers/effects, unit definitions, and pool composition. Filesystem/JSON loading lives in `Battlegrounds.Content`; `Battlegrounds.Core` never reads files or JSON directly.

The repository contains `mods/example` only as a schema/integration fixture. It is not a canonical gameplay ruleset.

## Stack

- Godot 4.7.2 .NET
- C# / .NET 8
- xUnit v3

The repository pins the .NET SDK through `global.json` so local builds and CI use the same major SDK.

## Structure

```text
src/
  Battlegrounds.Core/       # deterministic framework-free domain/simulation
  Battlegrounds.Content/    # mod filesystem + JSON loading/validation
  Battlegrounds.Game/       # Godot presentation/input/audio/rendering

mods/
  example/                  # neutral example mod / schema fixture

tests/
  Battlegrounds.Core.Tests/
  Battlegrounds.Content.Tests/
```

Read `ARCHITECTURE.md` before adding features. Its ownership, dependency, mutation, determinism, and mod-neutrality rules are mandatory.

## Current foundation

- authoritative `MatchState` lifecycle (`Setup → Preparation → Combat`), round, and revision;
- authoritative `PlayerState` with read-only `Reserve`, `Field`, and `Offer` views;
- immutable `UnitDefinition` separated from mutable `UnitInstance` runtime state;
- explicit `Pooled` versus `Generated` unit origin;
- validated deterministic catalogs for units, behaviors, unit types, and tags;
- shared authoritative `UnitPool` with per-unit copy counts;
- explicit preparation commands for acquire, release, deploy, refresh, tier upgrade, freeze/unfreeze, and end preparation;
- `PreparationEngine` as the mutation boundary shared by future UI and AI;
- deterministic injected RNG;
- fully data-driven match/preparation/combat rules;
- data-driven terminology, behaviors, taxonomy, units, triggers/effects, and pool configuration;
- shared phase-neutral `GameEffectRuntime` for authored effects and trigger chains;
- Preparation execution for `onPlay`, `onSummon`, `onDamage`, `onDeath`, `onTurnStart`, and `onTurnEnd`;
- Preparation support for destroying friendly units and explicitly triggering their authored events;
- immutable combat snapshots isolated from persistent preparation state;
- Combat execution for `onCombatStart`, `onAttack`, `onDamage`, `onDeath`, `onSummon`, and `onCombatEnd` through the same effect runtime;
- deterministic combat starting-side selection, attacker rotation, target selection, simultaneous damage, deaths, and winner/draw resolution;
- native neutral implementations for damage barrier, target priority, revive-once, first-damage lethal, and extra attack;
- whole-mod validation report before loading;
- regression/invariant tests and CI.

## Local development

Open `src/Battlegrounds.Game/project.godot` with the .NET build of Godot 4.7.2.

Run tests with:

```bash
dotnet test tests/Battlegrounds.Core.Tests/Battlegrounds.Core.Tests.csproj
dotnet test tests/Battlegrounds.Content.Tests/Battlegrounds.Content.Tests.csproj
```

## Next architectural slice

Add player health and post-combat damage, then connect combat results back into the authoritative match lifecycle so a complete mod-driven `Preparation → Combat → Preparation` loop can run. After that, expand trigger conditions/counters such as Avenge-style mechanics on top of the shared effect runtime rather than adding phase-specific effect engines.
