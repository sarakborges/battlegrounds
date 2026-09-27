# AI command boundary

`Battlegrounds.AI` is a framework-free policy layer that depends only on `Battlegrounds.Core`.

The AI does not own or mutate authoritative match state. It reads the same public, read-only domain state available to presentation code and expresses every decision through the same public boundaries:

- Leader selection uses `LeaderSelectionState.Select`.
- Preparation uses `IPreparationCommand` values submitted through `MatchEngine.ExecutePreparation`.
- Pending Unit/Action choices are resolved through their normal commands.
- Unit combines use explicit `CombineUnitsCommand` component instance IDs.
- Actions and Powers use the same selected-target command fields as a human UI.

The baseline `PreparationAiAgent` is intentionally mechanical rather than fandom-aware. It can select a Leader, resolve pending choices, combine Units, deploy Units, replace weaker field Units, play Actions, activate Powers, acquire playables, upgrade tier, refresh/freeze and end Preparation.

Its scoring uses only neutral mechanical data such as tier, current Attack/Health, cost, Health modifiers and Armor. Mods remain responsible for names, themes, authored effects and balance.

## Preparation personalities

`PreparationAiPersonality` changes the order in which the baseline agent values otherwise legal economic actions. It is policy only: personalities do not mutate state directly, inspect hidden pool counts or bypass Core command validation.

The current profiles are:

- `Tempo` — prefers immediate board development and usable effects, then tier progression, with at most one baseline refresh;
- `Greedy` — prefers tier progression before spending on the current offer, then falls back to immediate development;
- `Roller` — values searching for a better offer and may spend up to three refreshes before committing to acquisitions or upgrades.

Mandatory state handling remains shared across personalities. Pending choices must still be resolved, legal combines are still taken, reserve Units are deployed when space exists, stronger reserve replacements may release weaker field Units, and owned Actions are played through the normal command boundary before the economic personality ordering is consulted.

This separation is intentional. Personality describes *how* an AI pilots a position; future archetype/tribe strategy can describe *what* composition or synergy it is pursuing. Two AIs pursuing the same archetype can therefore still make different economic decisions.

`SinglePlayerSession` assigns one personality to each AI at session creation using the same injected deterministic RNG used by the rest of the match. The assignment is exposed read-only for diagnostics through `AiPersonalities` and remains stable for that AI throughout the session.

## Determinism

Tie-breaking and personality assignment use injected `IRandomSource` values. The same state plus the same RNG sequence produces the same decisions and personality assignments.

`PlayPreparation` has a command-count safety budget. This is an AI control-flow guard against authored zero-cost generation loops; it is unrelated to combat attack resolution and does not add an attack limit to Combat. Personality-specific refresh preferences are additionally bounded, so a zero-cost refresh rule cannot create an unbounded personality loop before the general safety budget is reached.

## Ownership

Dependency direction:

```text
Battlegrounds.AI -> Battlegrounds.Core
```

The AI project must not depend on Godot, JSON, filesystem loading or `Battlegrounds.Content`. Callers construct an agent from already validated rules/catalogs.

A future stronger policy may replace the baseline heuristic, run simulations or search multiple candidate lines. Those implementations must still emit the same Core commands and must not gain privileged mutation access.
