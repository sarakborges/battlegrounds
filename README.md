# Battlegrounds

Local single-player clone of the Hearthstone Battlegrounds game mode, built with Godot 4 + C#.

## Stack

- Godot 4.7.2 .NET
- C# / .NET 8
- xUnit v3 for framework-free domain tests

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

- framework-free Core project;
- Godot project isolated at the presentation boundary;
- strong `CardId` domain type;
- immutable `CardDefinition`;
- injected seeded random source;
- deterministic RNG regression test;
- Core CI workflow.

Next architectural slice: match lifecycle + authoritative player state + explicit recruitment commands, before card/effect implementation expands.
