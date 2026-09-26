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

The Core must never encode fandom-specific terminology into IDs, state, commands, rules, or algorithms. Internal IDs describe stable mechanical roles (`UnitId`, `PlayerId`, `UnitInstanceId`) rather than presentation names.

There is intentionally no `Standard` gameplay preset in Core. Numeric rules are supplied by the selected mod.

## Mod layout

User-facing content lives under `/mods`, with one directory per mod:

```text
mods/
  <mod-id>/
    mod.json
    rules/
      match.json
      preparation.json
    content/
      units.json
      pool.json
    assets/                 # future
    localization/           # future
```

`mod.json` owns package identity and display terminology. Rule files own numbers and capacities. Content files own unit definitions and pool composition. Filesystem/JSON loading lives in `Battlegrounds.Content`; `Battlegrounds.Core` never reads files or JSON directly.

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
- validated deterministic `UnitCatalog`;
- shared authoritative `UnitPool` with per-unit copy counts;
- explicit preparation commands for acquire, release, deploy, refresh, tier upgrade, freeze/unfreeze, and end preparation;
- `PreparationEngine` as the mutation boundary shared by future UI and AI;
- unlimited offer freeze/unfreeze toggling;
- deterministic injected RNG;
- data-driven `MatchRules` and `PreparationRules` loaded from the active mod;
- data-driven terminology, units, and pool configuration;
- mod schema validation outside Core;
- regression/invariant tests and CI.

## Local development

Open `src/Battlegrounds.Game/project.godot` with the .NET build of Godot 4.7.2.

Run tests with:

```bash
dotnet test tests/Battlegrounds.Core.Tests/Battlegrounds.Core.Tests.csproj
dotnet test tests/Battlegrounds.Content.Tests/Battlegrounds.Content.Tests.csproj
```

## Next architectural slice

Build combat as a deterministic state machine using the same neutral vocabulary. Combat algorithms belong to Core; combat labels, unit content, effects, numbers, visuals, and other theme-specific data belong to mods whenever they can be represented as data.
