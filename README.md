# Battlegrounds

Local single-player clone of the Hearthstone Battlegrounds game mode, built with Godot 4 + C#.

## Stack

- Godot 4.7.2 .NET
- C# / .NET 8
- xUnit v3 for framework-free domain tests

The repository pins the .NET SDK through `global.json` so local builds and CI use the same major SDK.

## Structure

```text
src/
  Battlegrounds.Core/   # deterministic, framework-free domain/simulation
  Battlegrounds.Game/   # Godot presentation/input/audio/rendering

tests/
  Battlegrounds.Core.Tests/
```

Read `ARCHITECTURE.md` before adding features. Its ownership, mutation, dependency and determinism rules are mandatory.

## Local development

Open `src/Battlegrounds.Game/project.godot` with the .NET build of Godot 4.7.2.

Run core tests with:

```bash
dotnet test tests/Battlegrounds.Core.Tests/Battlegrounds.Core.Tests.csproj
```

## Current foundation

- framework-free deterministic Core project;
- Godot isolated at the presentation boundary;
- strong `CardId`, `PlayerId`, and `MinionInstanceId` domain identities;
- immutable `CardDefinition` separated from mutable `MinionInstance` runtime state;
- explicit `MatchState` lifecycle (`Setup → Recruitment → Combat`), round, and revision;
- authoritative `PlayerState` with read-only public collections;
- explicit recruitment commands for buy, sell, play, refresh, upgrade, and end-turn intent;
- `RecruitmentEngine` as the single mutation boundary shared by future UI and AI;
- tavern offer policy isolated behind `ITavernOfferSource`;
- injected seeded random source;
- domain-aware command rejection without partial mutation;
- invariant and lifecycle regression tests;
- Core CI workflow.

## Next architectural slice

Introduce the authoritative card catalog/tavern pool and freeze semantics behind the existing tavern-offer boundary. After that, build combat as a separate deterministic state machine instead of mixing combat behavior into recruitment or presentation code.
