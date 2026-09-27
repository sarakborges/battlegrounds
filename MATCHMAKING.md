# Matchmaking and combat pairing history

Matchmaking is a policy boundary. It is deliberately separate from combat execution.

`MatchEngine.ResolveCombatRound(...)` accepts explicit `CombatPairing` values and remains responsible for validating that every active player appears exactly once, resolving isolated combats, settling persistent consequences, recording eliminations, and advancing the match lifecycle.

A matchmaking policy may read authoritative match state and pairing history, but it does not execute combat and does not mutate players directly.

## Authoritative history

`MatchState.CombatPairingHistory` records the pairings that were actually resolved by `MatchEngine`.

Each `MatchCombatPairing` stores:

- `Round`;
- `LeftPlayerId`;
- `RightPlayerId` for a live opponent, otherwise `null`;
- `EliminatedOpponentSourcePlayerId` when the pairing used the archived eliminated-player snapshot.

The eliminated-opponent source is captured from the snapshot that existed at the start of that combat round. A player eliminated later in the same settlement cannot rewrite the recorded opponent retroactively.

Pairing history is read-only outside the match aggregate.

## History-aware native policy

`HistoryAwareCombatPairingPolicy` is a neutral baseline policy. It does not encode themed concepts or mutate match state.

For live opponents it prefers, in order:

1. fewer previous meetings between the two players;
2. the least-recent previous meeting;
3. injected deterministic RNG for an exact tie.

When the number of active players is odd, exactly one active player must face the latest eliminated-opponent snapshot. The policy assigns that slot by preferring:

1. fewer previous eliminated-opponent assignments;
2. the least-recent such assignment;
3. injected deterministic RNG for an exact tie.

The initial odd-player case remains invalid because no eliminated-opponent snapshot exists yet.

## Boundary

Typical use is explicit:

```csharp
var pairings = pairingPolicy.CreatePairings(match, randomSource);
var roundResult = matchEngine.ResolveCombatRound(match, pairings);
```

The policy creates intent. `MatchEngine` is still the authoritative executor and validator.

A future mod-driven matchmaking selector may choose among native policies, but the selected policy must continue to return explicit `CombatPairing` values rather than gaining mutation access to the match aggregate.
