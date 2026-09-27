# Headless AI simulation

`tools/Battlegrounds.Simulator` runs complete deterministic AI-vs-AI matches without Godot. It is intended for repeated balance and policy experiments while playable mod content evolves.

The simulator loads the same validated `ModPackage` as the game, constructs a fresh Core `MatchEngine` for every simulated match, lets every participant select a Leader through `PreparationAiAgent`, assigns the same Preparation personality/strategy families used by local single-player orchestration, rolls a fresh random initiative order every Preparation round, resolves combat through `HistoryAwareCombatPairingPolicy` and `MatchEngine`, and records the authoritative final placements.

It does not inspect hidden pool counts to make AI decisions, bypass command legality, mutate `PlayerState` directly, or depend on Godot. The tool is an orchestration/analysis consumer of Content + AI + Core.

## Usage

From the repository root:

```bash
dotnet run --project tools/Battlegrounds.Simulator/Battlegrounds.Simulator.csproj -- \
  --mod mods/example \
  --matches 1000 \
  --players 2 \
  --seed 12345
```

The current round-one combat contract requires an even initial player count because no eliminated-opponent snapshot exists before the first combat. If `--players` is omitted, the simulator chooses the smallest supported even count. For a production/content-rich mod, pass the intended lobby size explicitly.

`mods/example` is a compact schema/integration fixture rather than a balance-sized pool. Larger AI-only lobbies can exhaust its eligible copies while filling offers, so examples and CI intentionally use two players. A real playable mod should size its pool for its supported lobby and offer curve.

Useful options:

```text
--mod <path>          mod directory; default mods/example
--matches <n>         number of matches; default 100
--players <n>         players per match; default smallest supported even count
--seed <n>            base seed; match i uses baseSeed + i
--max-commands <n>    AI Preparation safety budget; default 128
--max-rounds <n>      per-match round safety budget; default 200
--json <path>         write the full report as JSON
--csv <path>          write one row per simulated player as CSV
```

Example with artifacts:

```bash
dotnet run --project tools/Battlegrounds.Simulator/Battlegrounds.Simulator.csproj -- \
  --mod mods/example \
  --matches 5000 \
  --players 2 \
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
- average acquire, refresh and upgrade counts in the compact console tables for quick economic comparison.

`PreparationAiAgent` returns a typed `PreparationAiCommandCounts` observation for every completed AI Preparation turn. A command is counted only after `MatchEngine.ExecutePreparation(...)` accepts it, so failed attempts cannot inflate telemetry. The counters are observations only: they are never read by AI decision policy and never participate in authoritative gameplay mutation.

The JSON report includes every match and every participant result with seed, Leader, personality, strategy, placement, final Tier, final Health, total Preparation command count and the full typed command breakdown. Group summaries also include per-category command averages, and the top-level report includes the total command mix across the run.

The CSV export emits one row per participant with columns for total Preparation commands plus every command category. This makes it practical to analyze questions such as refresh rate by personality, upgrade frequency by strategy, combine usage, Power usage, or unexpectedly passive builds without reconstructing actions from final boards.

## Determinism

For a fixed repository/mod version, simulator options and base seed, the run is deterministic. Each match gets a separate `SeededRandomSource` seeded with `baseSeed + matchIndex`. Leader offers/selections, personality and strategy assignment, Preparation initiative, AI tie-breaking, offer draws, matchmaking and combat all consume that match-local deterministic stream.

Telemetry observes successful commands without consuming RNG, so adding or exporting counters does not change the deterministic gameplay sequence.

Different simulator match seeds are independent match runs; one failed or changed match does not advance RNG state for later matches.

## CI

CI runs a short two-player smoke simulation against `mods/example` after Core, Content, AI and Application tests. This verifies that the command-line project builds and that a complete AI-only match can advance through Preparation, Combat, elimination and final placement without Godot.
