# Battlegrounds

A local single-player auto-battler engine built with Godot 4 + C#, designed from the start to be **fully mod-based**.

The engine is not a Warcraft-specific implementation. A selected mod package owns terminology, authored content, balance values, pool composition, localization and presentation; `Battlegrounds.Core` owns neutral game mechanics and deterministic simulation.

## Core rule: mechanics are neutral, theme belongs to mods

Examples:

| Core concept | A mod may display it as |
| --- | --- |
| `Unit` | Minion, Digimon, Fighter, Creature |
| `Leader` | Hero, Tamer, Trainer, Commander |
| `Power` | Hero Power, Ability, Skill, Technique |
| `Resource` | Gold, Data, Credits, Energy |
| `Offer` | Tavern, Market, Portal, Draft |
| `Tier` | Tavern Tier, Level, Rank, Stage |
| `Reserve` | Hand, Bench, Roster |
| `Field` | Board, Arena, Team |
| `UnitType` | Beast, Demon, Vaccine, Machine |
| `EliminatedOpponentSnapshot` | Ghost, Echo, Kel'Thuzad-like dummy, etc. |

The Core must never encode fandom-specific display terminology into IDs, commands, rules or algorithms. There is intentionally no hardcoded `Standard` gameplay preset in Core.

## One authored entity per file

Mod content never uses giant catalog arrays such as `units.json`, `leaders.json`, `powers.json`, `types.json` or `behaviors.json`.

Every authored entity with its own ID lives in its own file:

```text
content/
  leaders/
    steady.json
    vital.json
  powers/
    steady-pulse.json
    vital-shift.json
  units/
    scout.json
    guard.json
  types/
    organic.json
    construct.json
  tags/
    starter.json
  behaviors/
    protector.json
    ward.json
```

The file name is part of the validation contract: `content/units/guard.json` must contain `"id": "guard"`. A mismatch rejects the whole mod.

This rule applies to future ID-addressable content too: artifacts, spells, quests, anomalies, or other authored entities should each have their own file rather than being accumulated into one array document.

Aggregate files are reserved for genuinely package-global configuration, such as `mod.json`, `rules/*.json` and `content/pool.json`.

## Leaders and powers

`Leader` is the neutral Core role for concepts such as a Battlegrounds Hero, Digimon Tamer, Pokémon Trainer, Commander, etc.

Leaders are authored under `content/leaders/<id>.json` and define stable identity, display name, `healthModifier`, starting `armor`, and an `initialPowerId` reference.

Powers are independent authored entities under `content/powers/<id>.json`. A leader does **not** own or embed a power definition. `LeaderDefinition.InitialPowerId` only selects the starting power; `LeaderState.CurrentPowerId` is mutable runtime state and may change during the match.

A power currently defines:

- stable `PowerId` and display name;
- `cost` in the mod's generic Resource;
- `maxUsesPerTurn`;
- optional `maxUsesPerMatch`;
- ordered shared `effects`.

`UsePowerCommand` activates the player's current power during Preparation. Usage is tracked per `PowerId`, so replacing a power and later returning to it does not erase its usage history. Per-turn counts reset when a new Preparation round begins.

Power effects run through the same `GameEffectRuntime` as unit-triggered effects. There is no power-specific effect language. Powers may use `selected` targeting for an explicit Field unit chosen by UI/AI, and the neutral `setPower` effect can replace `LeaderState.CurrentPowerId` without mutating the immutable `LeaderDefinition`.

`PlayerState` owns a `LeaderState`. Armor is mutable runtime state and absorbs player damage before Health. A player is eliminated only when Health reaches zero.

Match creation from a real mod uses explicit `PlayerSetup(PlayerId, LeaderId)` values. The Core resolves the selected ID through the mod's validated `LeaderCatalog`; callers cannot invent Health or Armor values outside the authored leader definition.

## Native behaviors, mod-defined identities

Reusable mechanics are implemented once in Core under neutral native handler keys. Mods choose their own IDs and display names.

Current native handlers:

- `damageBarrier` — Divine Shield-like first positive damage prevention;
- `targetPriority` — Taunt-like target restriction;
- `reviveOnce` — Reborn-like one-time return at 1 Health;
- `lethalFirstDamagePerCombat` — Venomous-like first damaging hit destroys the target;
- `extraAttack` — Windfury-like extra consecutive strike.

## Shared triggers and effects

Battlecry-like, Deathrattle-like, summon, damage, destroy, buff and power mechanics belong to the shared game domain, not to a specific phase.

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
- `setPower`

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

`PlayerState` owns generic `Health`; `rules/match.json` provides `startingHealth`, then the selected Leader may apply a `healthModifier`. A player at zero Health is eliminated and stops entering Preparation.

Combat remains an isolated simulation. Settlement applies combat results back to persistent match state only after simulation completes.

The current native post-combat damage policy is `winnerTierPlusSurvivorTiers`: winner Tier plus the Tiers of surviving units. Draws deal zero player damage. Generated/token survivors not present in the starting combat snapshot use their combat survivor Tier, currently Tier 1 by default.

Player damage is applied to Leader Armor first and Health second. `CombatSettlement` reports incoming damage, Armor absorbed, Armor after and Health after so UI/replay consumers do not need to reconstruct the calculation.

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

`MatchElimination` records elimination sequence, round, `PlayerId`, final placement, Health before combat and Health after settlement.

Players eliminated during the same combat round are ranked for displayed placement by their pre-combat Health, with `PlayerId` as a deterministic tie-breaker. Elimination sequence is stored separately, because it also determines which eliminated player becomes the next archived opponent snapshot.

When the match finishes, the remaining player receives placement 1. Consumers can query placement through `MatchState.TryGetPlacement(...)`.

## Mod validation is mandatory

`ModLoader.Load(...)` validates the complete mod before creating a `ModPackage`. Invalid mods are rejected as a whole.

`ModLoader.Validate(...)` and `ModValidator.Validate(...)` return a structured report suitable for UI, including the actual file, JSON path, issue code, severity and message.

Validation covers required global files/content directories, required and unknown keys, JSON types/ranges, one-object-per-entity-file structure, entity ID/file-name agreement, duplicate IDs/references, cross-file references, unsupported native handlers/triggers/effects/policies, taxonomy references, leader starting values, leader → initial-power references, power → power references, and conditional effect/trigger parameters.

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
      leaders/
        <leader-id>.json
      powers/
        <power-id>.json
      behaviors/
        <behavior-id>.json
      types/
        <type-id>.json
      tags/
        <tag-id>.json
      units/
        <unit-id>.json
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
- neutral `LeaderDefinition`, `LeaderCatalog`, `LeaderState`, Health modifiers and Armor;
- independent `PowerDefinition`/`PowerCatalog`, leader initial-power references and mutable current-power state;
- active power cost/usage limits, selected targets and runtime `setPower` replacement;
- explicit player-to-leader setup through validated `LeaderId` values;
- `MatchEngine` round orchestration, explicit pairings, post-combat settlement and odd-player eliminated-opponent combat;
- immutable eliminated-player combat snapshots using the latest prior elimination;
- mod-driven starting Health, starting-side policy and post-combat damage policy;
- authoritative `PlayerState` with read-only `Reserve`, `Field` and `Offer` views;
- immutable `UnitDefinition` separated from mutable `UnitInstance`;
- deterministic catalogs for units, leaders, powers, behaviors, types and tags;
- one authored ID-addressable entity per JSON file;
- authoritative shared `UnitPool`;
- preparation commands for acquire, release, deploy, refresh, tier upgrade, power use, freeze/unfreeze and end preparation;
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

Expand power/leader lifecycle events and match setup rules (leader availability, offers and selection) while continuing to reuse the shared effect runtime. Matchmaking policy/history remains separate from combat pairing execution.
