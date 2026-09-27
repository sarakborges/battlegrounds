# Battlegrounds

A local single-player auto-battler engine built with Godot 4 + C#, designed from the start to be **fully mod-based**.

The engine is not a Warcraft-specific implementation. A selected mod package owns terminology, authored content, balance values, pool composition, localization and presentation; `Battlegrounds.Core` owns neutral game mechanics and deterministic simulation.

## Core rule: mechanics are neutral, theme belongs to mods

Examples:

| Core concept | A mod may display it as |
| --- | --- |
| `Unit` | Minion, Digimon, Fighter, Creature |
| `Action` | Spell, Technique, Item, Tactic |
| `Leader` | Hero, Tamer, Trainer, Commander |
| `Power` | Hero Power, Ability, Skill, Technique |
| `Resource` | Gold, Data, Credits, Energy |
| `Offer` | Tavern, Market, Portal, Draft |
| `Tier` | Tavern Tier, Level, Rank, Stage |
| `Reserve` | Hand, Bench, Roster |
| `Field` | Board, Arena, Team |
| `UnitType` | Beast, Demon, Vaccine, Machine |
| `UnitCombine` | Triple, Golden, Fusion, Evolution |
| `EliminatedOpponentSnapshot` | Ghost, Echo, Kel'Thuzad-like dummy, etc. |

The Core must never encode fandom-specific display terminology into IDs, commands, rules or algorithms. There is intentionally no hardcoded `Standard` gameplay preset in Core.

## Mod-owned vocabulary and localization

Visible vocabulary, authored entity display text and UI templates are validated Content data rather than engine-owned English strings.

`mod.json` contains the package's fallback terminology for neutral concepts such as Unit, Action, Leader, Power, Health, Armor, Resource, Offer, Tier, Reserve, Field, Preparation, Combat and Round. `localization/presentation.json` selects the default locale, while `localization/<locale>.json` files provide localized `term.*`, `ui.*` and stable `entity.<kind>.<id>.*` overrides.

The default locale must be complete for required UI templates. Secondary locales may be partial and resolve missing keys through deterministic exact-locale → language-locale → default-locale fallback. Authored entity names additionally fall back to the validated `name` from their content file, so gameplay IDs and immutable definitions never become locale-dependent. `Battlegrounds.Game` chooses the requested/system locale and formats the validated presentation text; Application only passes the validated package through, and Core never sees locale data.

`mods/example` currently demonstrates `en` and `pt-BR` presentation data, including localized authored entity names and an optional description.

Presentation media follows the same ownership rule. An optional `assets/presentation.json` maps stable Leader, Unit and Action IDs to mod-relative portrait/art slots. `Battlegrounds.Content` validates entity references, slots, path containment, file existence and media extension before exposing an immutable `ModPresentationAssetCatalog`; `Battlegrounds.Game` owns runtime image decoding, texture caching and rendering. Missing asset entries are valid presentation fallbacks and never change gameplay identity or rules.

See `LOCALIZATION.md` for the text format/fallback contract and `PRESENTATION_ASSETS.md` for presentation-media metadata and path ownership.

## One authored entity per file

Mod content never uses giant catalog arrays such as `units.json`, `actions.json`, `leaders.json`, `powers.json`, `combines.json`, `types.json` or `behaviors.json`.

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
  actions/
    training.json
  combines/
    scout-upgrade.json
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

This rule applies to future ID-addressable content too: artifacts, quests, anomalies, or other authored entities should each have their own file rather than being accumulated into one array document.

Aggregate files are reserved for genuinely package-global configuration, such as `mod.json`, `rules/*.json`, `content/pool.json`, `localization/presentation.json` and `assets/presentation.json`.

## Leaders and powers

`Leader` is the neutral Core role for concepts such as a Battlegrounds Hero, Digimon Tamer, Pokémon Trainer, Commander, etc.

Leaders are authored under `content/leaders/<id>.json` and define stable identity, display name, `healthModifier`, starting `armor`, and an `initialPowerId` reference.

Powers are independent authored entities under `content/powers/<id>.json`. A leader does **not** own or embed a power definition. `LeaderDefinition.InitialPowerId` only selects the starting power; `LeaderState.CurrentPowerId` is mutable runtime state and may change during the match.

Powers reuse the shared trigger/effect system. Active powers opt into an `activation` block and execute `onActivate`; passive and active powers may also respond to lifecycle events such as `onMatchStart`, `onTurnStart`, `onTurnEnd`, `onCombatStart` and `onCombatEnd`.

`UsePowerCommand` activates the player's current power during Preparation. Usage is tracked per `PowerId`, so replacing a power and later returning to it does not erase its usage history. Per-turn counts reset when a new Preparation round begins.

Power effects run through the same `GameEffectRuntime` as Unit and Action effects. There is no power-specific effect language. Powers may use `selected` targeting for an explicit Field unit chosen by UI/AI during `onActivate`, and the neutral `setPower` effect can replace `LeaderState.CurrentPowerId` without mutating the immutable `LeaderDefinition`.

Leader setup is explicit and deterministic. `LeaderSelectionState` creates mod-driven offers from `LeaderSelectionRules`, stores selections by `PlayerId`, and produces validated `PlayerSetup` values only when every player has selected.

## Generic playables, Actions and pending choices

The Preparation offer can contain Units and Actions through one generic playable surface.

`ActionDefinition` is immutable authored content under `content/actions/<id>.json`. `ActionInstance` is consumable runtime state in the player's reserve. `PlayActionCommand` executes ordered effects through the same `GameEffectRuntime` used everywhere else.

Unit and Action generation can create immediate results or queue a pending choice. A player may have only one pending choice at a time; unrelated Preparation commands are rejected until it is resolved through the explicit Unit/Action choice command.

Presentation and AI consume the generic read-only `PlayableOffer` and `PlayableReserve` surfaces. They do not own a second copy of state.

See `PLAYABLES.md` and `GENERATION.md` for detailed semantics.

## Generic Unit combines

Unit combining is mod-defined rather than hardcoded as a themed Triple/Golden system.

Each recipe under `content/combines/<id>.json` identifies a source `UnitId`, exact required-copy count and result `UnitId`. `CombineUnitsCommand` contains the exact runtime `UnitInstanceId` values to consume, so Core never silently decides which owned copies disappear.

Components may come from Reserve and/or Field. Still-owned pooled inputs return their copies exactly once, the result is created as `Generated`, and the result's optional `onCombine` reward runs only after the atomic combine mutation completes.

See `COMBINES.md` for the ordering and pool-ownership contract.

## Native behaviors, mod-defined identities

Reusable mechanics are implemented once in Core under neutral native handler keys. Mods choose their own IDs and display names.

Current native handlers:

- `damageBarrier` — Divine Shield-like first positive damage prevention;
- `targetPriority` — Taunt-like target restriction;
- `reviveOnce` — Reborn-like one-time return at 1 Health;
- `lethalFirstDamagePerCombat` — Venomous-like first damaging hit destroys the target;
- `extraAttack` — Windfury-like extra consecutive strike.

## Shared triggers and effects

Battlecry-like, Deathrattle-like, summon, damage, destroy, buff, Action and Power mechanics belong to the shared game domain, not to a specific phase.

Current trigger families include Unit events such as `onPlay`, `onCombine`, `onSummon`, `onAttack`, `onDamage`, `onDeath` and counted `afterFriendlyDeaths`, plus shared lifecycle events such as `onMatchStart`, `onTurnStart`, `onTurnEnd`, `onCombatStart`, `onCombatEnd` and active-power `onActivate`.

Effects include stat modification, damage, destruction, explicit trigger activation, summon, behavior mutation, resource adjustment, power replacement, Unit/Action generation and choices, persistent Unit transform/copy, and named persistent Unit modifiers.

Numeric effect parameters may be dynamic expressions. Target selection and conditions are composable. Scoped event history supports counted conditions and activation limits without introducing a global event bus.

`GameEffectRuntime` owns deterministic trigger/effect ordering. Preparation supplies a persistent authoritative state adapter; Combat supplies an isolated combat-local state adapter. When a Combat trigger actually resolves effects, the runtime also exposes that fact to the Combat world so the immutable result timeline can preserve source attribution without introducing a second effect engine.

Real simultaneous deaths are removed as a death wave before death-related effects resolve. Each death then resolves deterministically; deaths created during that resolution wait for the next wave. Authored `onDeath` resolves before `reviveOnce`, and a successful revive is treated as a normal summon and runs `onSummon`.

See `EFFECTS.md` for effect semantics and `COMBAT_TIMELINE.md` for the immutable replay/presentation event contract.

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

Combat `addResource`, power changes and scoped effect-history deltas leave Combat as explicit result data and are settled by `MatchEngine` rather than mutating persistent state from inside the simulator.

`CombatResult.Timeline` is a separate immutable ordered observation of visible combat-local transitions. It includes trigger attribution, attack starts, summons with stable combat identity/position, stat changes, damage/destruction, deaths/revives, behavior transitions and Resource/Power changes. Simulation never reads this timeline back.

## Matchmaking policy and combat pairing history

`CombatPairing` remains explicit: matchmaking is not hidden inside `MatchEngine`.

`MatchState` owns authoritative `CombatPairingHistory`. Only pairings that were actually resolved by `MatchEngine` are recorded. Eliminated-opponent entries preserve the exact archived opponent source used at the beginning of that combat round.

`HistoryAwareCombatPairingPolicy` is a neutral baseline policy outside the engine. It prefers less-repeated live opponents, then the least-recent prior meeting, with injected deterministic RNG for exact ties. When an odd active-player count requires an eliminated-opponent pairing, it distributes those assignments using the same history-aware principle.

Callers still make the boundary explicit:

```csharp
var pairings = pairingPolicy.CreatePairings(match, randomSource);
var roundResult = matchEngine.ResolveCombatRound(match, pairings);
```

See `MATCHMAKING.md` for the contract.

## Odd-player combat and eliminated-opponent snapshots

When the number of active players is odd, exactly one pairing must use:

```csharp
CombatPairing.VersusEliminatedOpponent(playerId)
```

The opponent is the immutable `EliminatedOpponentSnapshot` from the **most recently eliminated player before that combat round started**. It preserves that player's Field, Tier, current Power and effect-history snapshot relevant to Combat.

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

## Deterministic AI

`Battlegrounds.AI` is a framework-free policy layer that depends only on `Battlegrounds.Core`.

The baseline `PreparationAiAgent` reads the same public state available to presentation code and expresses every decision through normal Core boundaries: Leader selection, pending choices, combines, deploy/release, Actions, Powers, acquisition, upgrades, refresh/freeze and end Preparation.

The baseline scoring is intentionally mechanical and theme-neutral. Tie-breaking uses injected `IRandomSource`, and a command-count safety budget prevents authored zero-cost loops from trapping AI control flow.

See `AI.md` for ownership and determinism rules.

## Single-player application/session

`Battlegrounds.Application` is a framework-free orchestration layer over validated Content, Core and AI.

`SinglePlayerSession` coordinates one human player plus AI opponents. AI Leader selection and Preparation use `PreparationAiAgent`; human input uses the same `IPreparationCommand` types; combat pairings come from `ICombatPairingPolicy`; and all authoritative mutation still enters through `MatchEngine`/Core.

`AdvanceAutomated()` finishes active AI Preparations, waits if the human is not ready, and otherwise resolves exactly one combat round before returning control at the next Preparation or Finished state. Immediately before resolution, the session freezes the paired starting Unit views; after Core returns, `LastCombat` exposes a `SessionCombatRecord` containing those immutable snapshots plus the authoritative `CombatRoundResult`. Presentation can therefore keep showing the completed combat after the Match has already advanced without owning or delaying simulation.

The session exposes the validated `ModPackage`, including its immutable presentation catalog, but does not select locales or interpret localized strings.

See `APPLICATION.md` for the orchestration and ownership contract.

## Godot presentation adapter

`Battlegrounds.Game` boots a validated mod and deterministic `SinglePlayerSession` and keeps Godot on the presentation side of the architecture boundary.

The playable Preparation surface now covers Leader selection, generic acquire/deploy/release, refresh, upgrade, freeze/unfreeze, Action/Power activation, pending Unit/Action choices, explicit Unit-combine component selection and ending Preparation.

Multi-step intent is stored only in `PresentationInteractionState`. Targeted Actions/Powers first submit without a target; when Core reports `InvalidActionTarget`/`InvalidPowerTarget`, Godot enters target-selection mode and resubmits the chosen `UnitInstanceId`. Combine selection stores exact highlighted component IDs and finishes with the existing `CombineUnitsCommand`.

Godot resolves the selected mod's `ModPresentationCatalog` using the exported locale or Godot's system locale. Preparation labels, summaries, concept vocabulary, authored Leader/Power/Unit/Action/combine names and combat playback text are rendered from mod-owned presentation data with Content-owned fallback instead of hardcoded engine English or locale-dependent gameplay identity.

Validated image metadata remains outside Core. `ModPresentationTextureStore` is the Godot-side runtime boundary for loading mod-relative images into cached `Texture2D` instances. Reusable `PresentationCardButton` controls now render Leader choices plus Unit/Action pending choices, Offer, Reserve and Field entries from the same stable IDs, composing optional portrait/art, localized name/description and localized mechanical stat lines. Missing media collapses to a text/stat layout; cards still submit the same existing slots/IDs/commands and never become gameplay authority.

Resolved human combat is displayed through a full-screen presentation-owned playback overlay. `CombatPlaybackState` starts from the immutable session snapshots and consumes `CombatResult.Timeline` in order, so combat-local summons, triggers, buffs/debuffs, effect damage/destruction, deaths/revives, behavior changes and Power/Resource transitions are rendered from immutable Core output rather than reconstructed in Godot. Playback may auto-step, advance manually or skip to settlement; none of those controls rerun combat or mutate Core state.

See `GAME.md` for the Godot ownership contract, `LOCALIZATION.md` for the presentation string contract and `PRESENTATION_ASSETS.md` for asset metadata/runtime ownership.

## Mod validation is mandatory

`ModLoader.Load(...)` validates the complete mod before creating a `ModPackage`. Invalid mods are rejected as a whole.

`ModLoader.Validate(...)` and `ModValidator.Validate(...)` return a structured report suitable for UI, including the actual file, JSON path, issue code, severity and message.

Validation covers required global files/content directories, required and unknown keys, JSON types/ranges, one-object-per-entity-file structure, entity ID/file-name agreement, duplicate IDs/references, cross-file references, unsupported native handlers/triggers/effects/policies, taxonomy references, leader starting values, Leader → initial-Power references, persistent-effect phase legality, Action/choice/generation rules, Unit-combine references/constraints, required presentation terminology, locale identifiers, default-locale completeness, localization string shape, stable authored-entity localization references, and presentation-asset entity/slot/path/type/existence constraints.

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
      setup.json
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
      actions/
        <action-id>.json
      combines/
        <combine-id>.json
      pool.json
    localization/
      presentation.json
      <locale>.json
    assets/
      presentation.json
      leaders/
      units/
      actions/
```

`Battlegrounds.Content` owns filesystem/JSON loading and validation. `Battlegrounds.Core` never reads files or JSON directly. `mods/example` is only a neutral schema/integration fixture.

## Stack

- Godot 4.7.2 .NET
- C# / .NET 8
- xUnit v3

## Structure

```text
src/
  Battlegrounds.Core/         # deterministic framework-free domain/simulation
  Battlegrounds.Content/      # mod filesystem + JSON loading/validation
  Battlegrounds.AI/           # deterministic policy over public Core boundaries
  Battlegrounds.Application/  # framework-free single-player use-case orchestration
  Battlegrounds.Game/         # Godot presentation/input/audio/rendering

mods/
  example/

tests/
  Battlegrounds.Core.Tests/
  Battlegrounds.Content.Tests/
  Battlegrounds.AI.Tests/
  Battlegrounds.Application.Tests/
```

Read `ARCHITECTURE.md` before adding features. Its ownership, dependency, mutation, determinism and mod-neutrality rules are mandatory.

## Current foundation

- authoritative `MatchState` lifecycle (`Setup → Preparation → Combat → Finished`), Health, elimination, placement and history;
- deterministic Leader offers/selection and explicit `PlayerSetup` creation;
- neutral `LeaderDefinition`, `LeaderCatalog`, `LeaderState`, Health modifiers and Armor;
- independent trigger-based `PowerDefinition`/`PowerCatalog`, active/passive lifecycle events and mutable current-power state;
- `MatchEngine` round orchestration, explicit pairings, post-combat settlement and odd-player eliminated-opponent combat;
- authoritative combat pairing history plus a history-aware pairing policy outside `MatchEngine`;
- immutable eliminated-player combat snapshots using the latest prior elimination;
- mod-driven starting Health, starting-side policy and post-combat damage policy;
- authoritative `PlayerState` with read-only Unit/Action offer and reserve views;
- immutable `UnitDefinition` / `ActionDefinition` separated from mutable runtime instances;
- deterministic catalogs for Units, Actions, Leaders, Powers, combines, behaviors, types and tags;
- one authored ID-addressable entity per JSON file;
- authoritative shared `UnitPool` with explicit ownership on transform/copy/combine paths;
- generic generation, pending Unit/Action choices and explicit resolution commands;
- generic Action acquisition/play through the shared effect runtime;
- explicit mod-defined Unit combines and `onCombine` reward lifecycle;
- persistent Unit transform/copy and named modifier effects limited to Preparation-capable contexts;
- dynamic effect values, expressive targets/conditions and scoped event history/activation limits;
- preparation commands for acquire, release, deploy, Action play, combine, refresh, tier upgrade, Power use, choice resolution, freeze/unfreeze and end Preparation;
- deterministic injected RNG;
- shared phase-neutral `GameEffectRuntime`;
- deterministic death waves, counted friendly-death listeners, Deathrattle-like effects, Reborn-like behavior and summons;
- immutable combat snapshots isolated from persistent Preparation state;
- deterministic attack order, targeting, simultaneous damage, death resolution and winner/draw resolution;
- immutable ordered `CombatResult.Timeline` for replay/presentation of combat-local triggers and state transitions;
- deterministic framework-free AI using the same public commands as presentation;
- framework-free single-player session orchestration over validated Content + Core + AI + matchmaking;
- immutable session combat observations that freeze starting boards before authoritative settlement advances the Match;
- validated mod-owned presentation terminology/locales with deterministic fallback and immutable `ModPresentationCatalog`;
- stable localized authored display keys for Leaders, Powers, Units, Actions, behaviors, types, tags and combines, with content-file name fallback and optional descriptions;
- validated ID-keyed Leader portrait and Unit/Action art metadata with an immutable `ModPresentationAssetCatalog` and optional per-entity fallback;
- Godot-owned runtime external-image decoding/texture caching through `ModPresentationTextureStore`, with no asset filesystem dependency in Core;
- reusable Godot Leader/Unit/Action presentation cards combining optional art, localized names/descriptions and mechanical stats while preserving existing command wiring;
- thin Godot presentation adapter over the Application boundary with a playable localized Preparation loop;
- explicit Godot multi-step interaction state for selected targets, pending choices and combine components;
- deterministic Godot combat playback over the Core event timeline with manual/automatic stepping, settlement skip and mod-owned presentation vocabulary/entity names;
- whole-mod validation before loading;
- regression/invariant tests and CI, including a Godot project build.

## Local development

Open `src/Battlegrounds.Game/project.godot` with the .NET build of Godot 4.7.2.

Run tests/build with:

```bash
dotnet test tests/Battlegrounds.Core.Tests/Battlegrounds.Core.Tests.csproj
dotnet test tests/Battlegrounds.Content.Tests/Battlegrounds.Content.Tests.csproj
dotnet test tests/Battlegrounds.AI.Tests/Battlegrounds.AI.Tests.csproj
dotnet test tests/Battlegrounds.Application.Tests/Battlegrounds.Application.Tests.csproj
dotnet build src/Battlegrounds.Game/Battlegrounds.Game.csproj
```

## Next architectural slice

Reuse the same mod-owned visual identity in deterministic combat playback. Render combat board Units from `CombatPlaybackState` with the validated Unit art/card presentation, and let timeline events drive presentation-only emphasis/animation cues for attacks, summons, damage, deaths, revives and trigger sources. Playback must remain a pure consumer of the already-resolved immutable timeline: no animation timing or visual state may influence simulation, settlement or authoritative Match state.
