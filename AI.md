# AI command boundary

`Battlegrounds.AI` is a framework-free policy layer that depends only on `Battlegrounds.Core`.

The AI does not own or mutate authoritative match state. It reads the same public, read-only domain state available to presentation code and expresses every decision through the same public boundaries:

- Leader selection uses `LeaderSelectionState.Select`.
- Preparation uses `IPreparationCommand` values submitted through `MatchEngine.ExecutePreparation`.
- Pending Unit/Action choices are resolved through their normal commands.
- Unit combines use explicit `CombineUnitsCommand` component instance IDs.
- Actions and Powers use the same selected-target command fields as a human UI.

The baseline `PreparationAiAgent` is intentionally mechanical rather than fandom-aware. It can select a Leader, resolve pending choices, combine Units, deploy Units, replace weaker field Units, play Actions, activate Powers, acquire playables, upgrade tier, refresh/freeze and end Preparation.

Its baseline scoring uses only neutral mechanical data such as tier, current Attack/Health, cost, Health modifiers and Armor. Mods remain responsible for names, themes, authored effects and balance.

## Preparation personalities

`PreparationAiPersonality` changes the order in which the baseline agent values otherwise legal economic actions. It is policy only: personalities do not mutate state directly, inspect hidden pool counts or bypass Core command validation.

The current profiles are:

- `Tempo` — prefers immediate board development and usable effects, then tier progression, with at most one baseline refresh;
- `Greedy` — prefers tier progression before spending on the current offer, then falls back to immediate development;
- `Roller` — values searching for a better offer and may spend up to three refreshes before committing to acquisitions or upgrades.

Mandatory state handling remains shared across personalities. Pending choices must still be resolved, legal combines are still taken, reserve Units are deployed when space exists, stronger reserve replacements may release weaker field Units, and owned Actions are played through the normal command boundary before the economic personality ordering is consulted.

## Preparation strategies

`PreparationAiStrategy` is a separate layer from personality. Personality answers *how* an AI pilots its economy; strategy answers *what kind of Units* it currently values.

A strategy may prefer one or more neutral `UnitTypeId` and/or `TagId` values. Matching Units receive an additive score bonus on top of their normal mechanical value. Strategy currently influences:

- generated Unit choices;
- which reserve Unit is deployed first;
- whether a reserve Unit is valuable enough to replace a field Unit;
- which affordable Unit is acquired from the current Offer.

Target selection for damaging/buff effects is intentionally not strategy-biased yet, and Action scoring remains mechanical. This keeps the first strategy slice focused on composition building rather than making every tactical choice archetype-aware.

`PreparationAiStrategy.Balanced` adds no composition preference. `PreferType(...)` and `PreferTag(...)` create theme-neutral focused strategies without hardcoding any tribe/fandom IDs in the AI project.

This separation means two AIs can pursue the same type strategy with different personalities: a `Greedy` type-focused AI may tier aggressively, while a `Tempo` AI pursuing the same type may buy immediate board strength first.

## Session assignment

`SinglePlayerSession` assigns one personality and one strategy independently to each AI at session creation using the same injected deterministic RNG used by the rest of the match.

The available strategy set is derived from validated mod content: `Balanced` plus one `PreferType(...)` strategy for every Unit type present in the mod's Unit catalog. The assignments are exposed read-only through `AiPersonalities` and `AiStrategies` for diagnostics and remain stable for that AI throughout the session.

Strategies are intentionally not authored as AI-specific JSON yet. Once the content-rich playable mod demonstrates real multi-type archetypes or tag-driven builds, the same strategy object can be composed from authored content without changing Core command legality or mutation ownership.

## Command telemetry

`PlayPreparation` returns `PreparationAiResult`, including a typed `PreparationAiCommandCounts` observation. It records successful acquire, release, deploy, Action play, combine, refresh, upgrade, Power use, pending-choice resolution, freeze/unfreeze and end-Preparation commands.

A command enters telemetry only after `MatchEngine.ExecutePreparation(...)` succeeds. Invalid commands still throw through the existing AI guard and are not counted. `CommandsExecuted` remains available as the turn total and is equal to `CommandCounts.Total` for a successful completed result.

Telemetry is deliberately one-way observation. The AI policy never reads these counters, they consume no RNG, and Core does not know they exist. This allows headless simulation and diagnostics to measure behavior without giving the AI privileged information or changing the deterministic gameplay sequence.

## Determinism

Tie-breaking, personality assignment and strategy assignment use injected `IRandomSource` values. The same state plus the same RNG sequence produces the same decisions and assignments.

`PlayPreparation` has a command-count safety budget. This is an AI control-flow guard against authored zero-cost generation loops; it is unrelated to combat attack resolution and does not add an attack limit to Combat. Personality-specific refresh preferences are additionally bounded, so a zero-cost refresh rule cannot create an unbounded personality loop before the general safety budget is reached.

Command telemetry does not consume RNG or influence scoring, so enabling downstream reporting does not change AI decisions.

## Ownership

Dependency direction:

```text
Battlegrounds.AI -> Battlegrounds.Core
```

The AI project must not depend on Godot, JSON, filesystem loading or `Battlegrounds.Content`. Callers construct an agent from already validated rules/catalogs.

A future stronger policy may replace the baseline heuristic, run simulations or search multiple candidate lines. Those implementations must still emit the same Core commands and must not gain privileged mutation access.
