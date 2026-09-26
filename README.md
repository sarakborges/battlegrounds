# Battlegrounds

A local single-player auto-battler engine built with Godot 4 + C#, designed from the start to be **fully mod-based**.

The engine is not a Warcraft-specific implementation. A selected mod package owns terminology, authored content, balance values, pool composition, localization and presentation; `Battlegrounds.Core` owns neutral game mechanics and deterministic simulation.

## Core rule: mechanics are neutral, theme belongs to mods

Examples:

| Core concept | A mod may display it as |
| --- | --- |
| `Unit` | Minion, Digimon, Fighter, Creature |
| `Resource` | Gold, Data, Credits, Energy |
| `Offer` | Tavern, Market, Portal, Draft |
| `Tier` | Tavern Tier, Level, Rank, Stage |
| `Reserve` | Hand, Bench, Roster |
| `Field` | Board, Arena, Team |
| `UnitType` | Beast, Demon, Vaccine, Machine |
| `EliminatedOpponentSnapshot` | Ghost, Echo, Kel'Thuzad-like dummy, etc. |

The Core must never encode fandom-specific display terminology into IDs, commands, rules or algorithms. There is intentionally no hardcoded `Standard` gameplay preset in Core.

## Native behaviors, mod-defined identities

Reusable mechanics are implemented once in Core under neutral native handler keys. Mods choose their own IDs and display names.

Current native handlers:

- `damageBarrier` — Divine Shield-like first positive damage prevention;
- `targetPriority` — Taunt-like target restriction;
- `reviveOnce` — Reborn-like one-time return at 1 Health;
- `lethalFirstDamagePerCombat` — Venomous-like first damaging hit destroys the target;
- `extraAttack` — Windfury-like extra consecutive strike.

## Shared triggers and effects

Battlecry-like, Deathrattle-like, summon, damage, destroy and buff mechanics belong to the shared game domain, not to a specific phase.

Current triggers:

- `onPlay`
- `onDeath`
- `onSummon`
- `onAttack`
- `onDamage`
- `onCombatStart`
- `onCombatEnd`
- `onTurnStart`
- `onTurnEnd`
- `afterFriendlyDeaths` — counted Avenge-like listener; requires positive `count`.

Current effects:

- `modifyStats`
- `dealDamage`
- `destroyUnit`
- `triggerEvent`
- `summonUnit`
- `addBehavior`
- `removeBehavior`
- `addResource`

`GameEffectRuntime` owns deterministic trigger/effect ordering. Preparation supplies a persistent authoritative state adapter; Combat supplies an isolated combat-local adapter.

Real simultaneous deaths are removed as a death wave before death-related effects resolve. Each death then resolves deterministically; deaths created during that resolution wait for the next wave. Authored `onDeath` resolves before `reviveOnce`, and a successful revive is treated as a normal summon and runs `onSummon`.

See `EFFECTS.md` for the detailed ordering contract.

## Match lifecycle

`MatchEngine` orchestrates the authoritative loop:

```text
Setup
  → Preparation
  → Combat
  → post-combat settlement
  → Preparation
  → ...
  → Finished
```

`PlayerState` owns generic `Health`; `rules/match.json` provides `startingHealth`. A player at zero Health is eliminated and stops entering Preparation.

Combat remains an isolated simulation. Settlement applies combat results back to persistent match state only after simulation completes.

The current native post-combat damage policy is `winnerTierPlusSurvivorTiers`: winner Tier plus the Tiers of surviving units. Draws deal zero player damage. Generated/token survivors not present in the starting combat snapshot use their combat survivor Tier, currently Tier 1 by default.

Combat `addResource` effects leave combat as result deltas and are applied after the next Preparation resource baseline and before `onTurnStart`.

## Odd-player combat and eliminated-opponent snapshots

`CombatPairing` remains explicit: matchmaking is not hidden inside `MatchEngine`.

When the number of active players is odd, exactly one pairing must use:

```csharp
CombatPairing.VersusEliminatedOpponent(playerId)
```

The opponent is the immutable `EliminatedOpponentSnapshot` from the **most recently eliminated player before that combat round started**. It preserves that player's Field and Tier.

The archived opponent:

- is not a live `PlayerState`;
- does not count toward active players;
- cannot receive persistent damage or resource changes;
- can still win combat and deal normal post-combat damage;
- is frozen for the whole round, so a newly eliminated player cannot replace it halfway through settlement;
- is replaced by the most recently eliminated player only for a later round.

An initially odd lobby has no eliminated-player snapshot yet and is therefore rejected rather than silently inventing a bye.

## Placement and elimination history

`MatchState` owns authoritative placement/history data.

`MatchElimination` records:

- elimination sequence;
- round;
- `PlayerId`;
- final placement;
- Health before combat;
- Health after settlement.

Players eliminated during the same combat round are ranked for displayed placement by their pre-combat Health, with `PlayerId` as a deterministic tie-breaker. Elimination sequence is stored separately, because it also determines which eliminated player becomes the next archived opponent snapshot.

When the match finishes, the remaining player receives placement 1. Consumers can query placement through `MatchState.TryGetPlacement(...)`.

## Mod validation is mandatory

`ModLoader.Load(...)` validates the complete mod before creating a `ModPackage`. Invalid mods are rejected as a whole.

`ModLoader.Validate(...)` returns a structured report suitable for UI, including file, JSON path, issue code, severity and message.

Validation covers required files/keys, unknown keys, JSON types/ranges, duplicate IDs/references, cross-file references, unsupported native handlers/triggers/effects/policies, taxonomy references and conditional required parameters.

Examples of required rules:

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

`Battlegrounds.Content` owns filesystem/JSON loading and validation. `Battlegrounds.Core` never reads files or JSON directly. `mods/example` is only a neutral schema/integration fixture.

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
  example/

tests/
  Battlegrounds.Core.Tests/
  Battlegrounds.Content.Tests/
```

Read `ARCHITECTURE.md` before adding features. Its ownership, dependency, mutation, determinism and mod-neutrality rules are mandatory.

## Current foundation

- authoritative `MatchState` lifecycle (`Setup → Preparation → Combat → Finished`), Health, elimination, placement and history;
- `MatchEngine` round orchestration, explicit pairings, post-combat settlement and odd-player eliminated-opponent combat;
- immutable eliminated-player combat snapshots using the latest prior elimination;
- mod-driven `startingHealth`, starting-side policy and post-combat damage policy;
- authoritative `PlayerState` with read-only `Reserve`, `Field` and `Offer` views;
- immutable `UnitDefinition` separated from mutable `UnitInstance`;
- deterministic catalogs for units, behaviors, types and tags;
- authoritative shared `UnitPool`;
- preparation commands for acquire, release, deploy, refresh, tier upgrade, freeze/unfreeze and end preparation;
- deterministic injected RNG;
- shared phase-neutral `GameEffectRuntime`;
- deterministic death waves, counted friendly-death listeners, Deathrattle-like effects, Reborn-like behavior and summons;
- immutable combat snapshots isolated from persistent Preparation state;
- deterministic attack order, targeting, simultaneous damage, death resolution and winner/draw resolution;
- whole-mod validation before loading;
- regression/invariant tests and CI.

## Local development

Open `src/Battlegrounds.Game/project.godot` with the .NET build of Godot 4.7.2.

Run tests with:

```bash
dotnet test tests/Battlegrounds.Core.Tests/Battlegrounds.Core.Tests.csproj
dotnet test tests/Battlegrounds.Content.Tests/Battlegrounds.Content.Tests.csproj
```

## Next architectural slice

Introduce neutral leader definitions and leader-owned runtime state, then layer mod-defined leader terminology, starting modifiers such as armor/Health adjustments, and powers on top of the existing shared effect runtime. Matchmaking policy/history (including repeat-opponent restrictions) remains a separate concern from combat pairing execution.
