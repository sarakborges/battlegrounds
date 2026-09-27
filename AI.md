# AI command boundary

`Battlegrounds.AI` is a framework-free policy layer that depends only on `Battlegrounds.Core`.

The AI does not own or mutate authoritative match state. It reads the same public, read-only domain state available to presentation code and expresses every decision through the same public boundaries:

- Leader selection uses `LeaderSelectionState.Select`.
- Preparation uses `IPreparationCommand` values submitted through `MatchEngine.ExecutePreparation`.
- Pending Unit/Action choices are resolved through their normal commands.
- Unit combines use explicit `CombineUnitsCommand` component instance IDs.
- Actions and Powers use the same selected-target command fields as a human UI.

The baseline `PreparationAiAgent` is intentionally mechanical rather than fandom-aware. It can select a Leader, resolve pending choices, combine Units, deploy Units, replace weaker field Units, play Actions, activate Powers, acquire playables, upgrade tier, refresh once, freeze a useful remaining offer, and end Preparation.

Its scoring uses only neutral mechanical data such as tier, current Attack/Health, cost, Health modifiers, and Armor. Mods remain responsible for names, themes, authored effects, and balance.

## Determinism

Tie-breaking uses an injected `IRandomSource`. The same state plus the same RNG sequence produces the same decisions.

`PlayPreparation` has a command-count safety budget. This is an AI control-flow guard against authored zero-cost generation loops; it is unrelated to combat attack resolution and does not add an attack limit to Combat.

## Ownership

Dependency direction:

```text
Battlegrounds.AI -> Battlegrounds.Core
```

The AI project must not depend on Godot, JSON, filesystem loading, or `Battlegrounds.Content`. Callers construct an agent from already validated rules/catalogs.

A future stronger policy may replace the baseline heuristic, run simulations, or search multiple candidate lines. Those implementations must still emit the same Core commands and must not gain privileged mutation access.
