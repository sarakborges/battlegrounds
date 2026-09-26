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

There is intentionally no `Standard` gameplay preset in Core. Numeric rules and selectable native policies are supplied by the selected mod.

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
- `afterFriendlyDeaths` — counted listener used for Avenge-like mechanics; requires positive `count`.

Supported effect kinds currently are:

- `modifyStats`
- `dealDamage`
- `destroyUnit`
- `triggerEvent`
- `summonUnit`
- `addBehavior`
- `removeBehavior`
- `addResource`

The shared `GameEffectRuntime` owns event ordering and effect semantics. Consequences are explicit and deterministic rather than hidden behind a global event bus. Preparation supplies an authoritative persistent effect world; Combat supplies an isolated combat-local effect world.

Real simultaneous deaths are removed as a death wave before death-related effects resolve. Each death then resolves deterministically; newly-created deaths wait for the next wave. Authored `onDeath` resolves before `reviveOnce`, and a successful revive is treated as a normal summon and runs `onSummon`.

Runtime unit behaviors are mutable instance state: `addBehavior`/`removeBehavior` persist into later combat snapshots without mutating immutable unit definitions. Unit origin is explicit: `Pooled` units return their copy to the shared pool when permanently removed; `Generated` units do not create pool copies when released or destroyed.

See `EFFECTS.md` for the detailed trigger/death ordering contract.

## Match lifecycle and player health

`PlayerState` owns generic `Health`; the mod provides `startingHealth` in `rules/match.json`. A player at zero Health is eliminated and no longer enters Preparation.

`MatchEngine` orchestrates the authoritative round loop while reusing the same Preparation and Combat engines:

```text
Setup
  → Preparation
  → Combat
  → post-combat settlement
  → Preparation
  → ...
  → Finished
```

Combat remains an isolated simulation. Settlement applies its result back to the authoritative match only after the simulation finishes.

The current native post-combat damage policy is selected by the mod as `winnerTierPlusSurvivorTiers`: on a non-draw, damage is the winner's current Tier plus the tiers of surviving units. Generated survivors that were not present in the starting combat snapshot use their combat survivor tier; the current combat representation defaults such generated/token survivors to Tier 1. Draws deal zero player damage.

Combat-authored `addResource` changes are returned as deltas by combat and applied after the next Preparation resource baseline is initialized, before `onTurnStart` effects.

A combat round receives explicit `CombatPairing` values. Every active player must appear exactly once. Even-player rounds are supported now; odd-player Battlegrounds-style ghost opponents are intentionally deferred rather than modeled as an incorrect bye.

## Mod validation is mandatory

`ModLoader.Load(...)` validates the complete mod before materializing a `ModPackage`. Invalid mods are rejected as a whole.

`ModLoader.Validate(...)` returns a structured report suitable for the future UI. Validation covers required files/keys, unknown keys, JSON types and ranges, duplicate IDs/references, cross-file references, unsupported handlers/triggers/effects/policies, taxonomy references, and effect-specific required parameters.

Current required lifecycle rules include:

```json
// rules/match.json
{
  "minimumPlayers": 2,
  "maximumPlayers": 8,
  "startingHealth": 30
}
```

```json
// rules/combat.json
{
  "startingSidePolicy": "largerFieldThenRandom",
  "postCombatDamagePolicy": "winnerTierPlusSurvivorTiers"
}
```

## Mod layout

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

`Battlegrounds.Content` owns filesystem/JSON loading and validation. `Battlegrounds.Core` never reads files or JSON directly. The repository contains `mods/example` only as a schema/integration fixture; it is not a canonical gameplay ruleset.

## Stack

- Godot 4.7.2 .NET
- C# / .NET 8
- xUnit v3

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

- authoritative `MatchState` lifecycle (`Setup → Preparation → Combat → Finished`), round, revision, player health and elimination;
- `MatchEngine` orchestration for even-player combat rounds and post-combat settlement;
- mod-driven `startingHealth` and native post-combat damage policy selection;
- authoritative `PlayerState` with read-only `Reserve`, `Field`, and `Offer` views;
- immutable `UnitDefinition` separated from mutable `UnitInstance` runtime state;
- validated deterministic catalogs for units, behaviors, unit types, and tags;
- shared authoritative `UnitPool` with per-unit copy counts;
- explicit preparation commands for acquire, release, deploy, refresh, tier upgrade, freeze/unfreeze, and end preparation;
- deterministic injected RNG;
- fully data-driven match/preparation/combat rules;
- shared phase-neutral `GameEffectRuntime` for authored effects and trigger chains;
- deterministic death waves, counted friendly-death listeners, Deathrattle-like effects, Reborn-like behavior and summons;
- immutable combat snapshots isolated from persistent preparation state;
- deterministic combat start, attacker rotation, targeting, simultaneous damage, deaths and winner/draw resolution;
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

Add explicit odd-player/ghost combat assignments and match placement/history, then introduce leader definitions (including leader-specific starting modifiers such as armor) without moving those concepts into the neutral Core vocabulary.
