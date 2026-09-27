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
  --players 4 \
  --seed 12345
```

The current round-one combat contract requires an even initial player count because no eliminated-opponent snapshot exists before the first combat. If `--players` is omitted, the simulator chooses the supported even count closest to four.

Useful options:

```text
--mod <path>          mod directory; default mods/example
--matches <n>         number of matches; default 100
--players <n>         players per match
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
  --seed 7000 \
  --json artifacts/simulation.json \
  --csv artifacts/simulation.csv
```

## Current report

The console report aggregates:

- match count and player count;
- average match length in rounds;
- total AI Preparation command count;
- games, wins, win rate, average placement and average Preparation command count grouped by Leader;
- the same aggregates grouped by `PreparationAiPersonality`;
- the same aggregates grouped by `PreparationAiStrategy`.

The JSON report additionally includes every match and every participant result with seed, Leader, personality, strategy, placement, final Tier, final Health and total Preparation command count. The CSV export emits one row per participant with the same core fields.

The initial telemetry deliberately measures results and total AI activity without introducing privileged gameplay access. Per-command-type telemetry such as buy/release/refresh/upgrade counts can be added as an observation of commands already emitted by `PreparationAiAgent`; it must not alter decision policy or Core state ownership.

## Determinism

For a fixed repository/mod version, simulator options and base seed, the run is deterministic. Each match gets a separate `SeededRandomSource` seeded with `baseSeed + matchIndex`. Leader offers/selections, personality and strategy assignment, Preparation initiative, AI tie-breaking, offer draws, matchmaking and combat all consume that match-local deterministic stream.

Different simulator match seeds are independent match runs; one failed or changed match does not advance RNG state for later matches.

## CI

CI runs a short smoke simulation against `mods/example` after Core, Content, AI and Application tests. This verifies that the command-line project builds and that a complete AI-only match can advance through Preparation, Combat, elimination and final placement without Godot.
