# Headless AI simulation

`tools/Battlegrounds.Simulator` runs complete deterministic AI-vs-AI matches without Godot. It is intended for repeated balance and policy experiments while playable mod content evolves.

The simulator loads the same validated `ModPackage` as the game, constructs a fresh Core `MatchEngine` for every simulated match, lets every participant select a Leader through `PreparationAiAgent`, assigns the same Preparation personality/strategy families used by local single-player orchestration, rolls a fresh random initiative order every Preparation round, resolves combat through `HistoryAwareCombatPairingPolicy` and `MatchEngine`, and records the authoritative final placements.

It does not inspect hidden pool counts to make AI decisions, bypass command legality, mutate `PlayerState` directly, or depend on Godot. The tool is an orchestration/analysis consumer of Content + AI + Core.

## Usage

From the repository root:

```bash
dotnet run --project tools/Battlegrounds.Simulator/Battlegrounds.Simulator.csproj -- \
  --mod mods/warbands \
  --matches 1000 \
  --players 4 \
  --seed 12345
```

The current round-one combat contract requires an even initial player count because no eliminated-opponent snapshot exists before the first combat. If `--players` is omitted, the simulator chooses the smallest supported even count. For a production/content-rich mod, pass the intended lobby size explicitly.

`mods/example` is a compact schema/integration fixture rather than a balance-sized pool. Larger AI-only lobbies can exhaust its eligible copies while filling offers, so the lightweight fixture smoke uses two players. `mods/warbands` is the current content-rich playable mod and is the preferred target for repeated balance simulation.

Useful options:

```text
--mod <path>          mod directory; default mods/example
--matches <n>         number of matches; default 100
--players <n>         players per match; default smallest supported even count
--seed <n>            base seed; match i uses baseSeed + i
--max-commands <n>    AI Preparation safety budget; default 128
--max-rounds <n>      per-match round safety budget; default 200
--json <path>         write the full report as JSON
--csv <path>          write player CSV plus entity CSV sidecars
```

Example with artifacts:

```bash
dotnet run --project tools/Battlegrounds.Simulator/Battlegrounds.Simulator.csproj -- \
  --mod mods/warbands \
  --matches 5000 \
  --players 4 \
  --seed 7000 \
  --json artifacts/simulation.json \
  --csv artifacts/simulation.csv
```

## Current report

The console report aggregates:

- match count and player count;
- average match length in rounds;
- total AI Preparation command count;
- the global command mix for acquire, release, deploy, Action play, combine, refresh, upgrade, Power use, pending-choice resolution, freeze/unfreeze and end Preparation;
- games, wins, win rate, average placement and average Preparation command count grouped by Leader;
- the same aggregates grouped by `PreparationAiPersonality`;
- the same aggregates grouped by `PreparationAiStrategy`;
- average acquire, refresh and upgrade counts in the compact console tables for quick economic comparison;
- Unit offer appearances, acquisitions, buy rate, releases, deploys, final-board player/copy counts, final-board win rate and average placement when present;
- Action offer appearances, acquisitions, acquire rate, plays and plays per acquisition;
- combine executions per authored recipe;
- Unit/Action offer and acquisition volume plus final-board Unit copies by Tier.

`PreparationAiAgent` returns a typed `PreparationAiCommandCounts` observation for every completed AI Preparation turn. A command is counted only after `MatchEngine.ExecutePreparation(...)` accepts it, so failed attempts cannot inflate telemetry. The counters are observations only: they are never read by AI decision policy and never participate in authoritative gameplay mutation.

Entity telemetry uses the optional `IPreparationAiCommandObserver` boundary. The observer receives the public `PlayerState` immediately before an AI command and again only after Core accepts it. The simulator uses the pre-command snapshot to identify the public Unit/Action/combine involved, then commits the metric only from the accepted-command callback. The observer never sees Unit-pool remaining-copy counts and does not consume RNG.

### Offer appearance semantics

An entity "appears" when it is exposed to the AI in an Offer snapshot. The simulator records the current Offer once at the start of that player's Preparation turn and records another snapshot after every successful refresh. Buying one slot does not make the untouched slots count as new appearances.

A frozen Offer that remains visible into a later Preparation round counts as another exposure in that later turn. This makes `acquires / offer appearances` an exposure-based buy rate rather than a count of unique pool draws.

### Final-board semantics

Final-board telemetry reads each player's persistent `Field` after the match finishes. It records both total copies and whether a player has at least one copy of a Unit. Average placement and final-board win rate are calculated per player presence, so multiple copies on one player's board do not multiply that player's placement contribution.

## JSON and CSV output

The JSON report includes every match and every participant result with seed, Leader, personality, strategy, placement, final Tier, final Health, total Preparation command count, command breakdown, per-player entity counters and final-board Unit counts. The top-level `EntityTelemetry` section contains aggregated Unit, Action, combine and Tier summaries.

`--csv artifacts/simulation.csv` keeps the original one-row-per-player output and additionally writes deterministic sidecars next to it:

```text
artifacts/simulation.units.csv
artifacts/simulation.actions.csv
artifacts/simulation.combines.csv
artifacts/simulation.tiers.csv
```

This keeps variable content metrics out of the fixed player schema while making them directly usable in spreadsheets or balance scripts.

## Determinism

For a fixed repository/mod version, simulator options and base seed, the run is deterministic. Each match gets a separate `SeededRandomSource` seeded with `baseSeed + matchIndex`. Leader offers/selections, personality and strategy assignment, Preparation initiative, AI tie-breaking, offer draws, matchmaking and combat all consume that match-local deterministic stream.

Command and entity telemetry observe public state and accepted commands without consuming RNG, so enabling or exporting telemetry does not change the deterministic gameplay sequence.

Different simulator match seeds are independent match runs; one failed or changed match does not advance RNG state for later matches.

## CI

CI runs a short two-player smoke simulation against `mods/example` after Core, Content, AI and Application tests, plus a smoke of the current Warbands playable mod. This verifies that the command-line project builds and that complete AI-only matches can advance through Preparation, Combat, elimination, final placement and balance telemetry without Godot.
