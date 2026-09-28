# Matchmaking and combat pairing history

Matchmaking is a policy boundary. It is deliberately separate from combat execution.

`MatchEngine.ResolveCombatRound(...)` accepts explicit `CombatPairing` values and remains responsible for validating that every active player appears exactly once, resolving isolated combats, settling persistent consequences, recording eliminations, and advancing the match lifecycle.

A matchmaking policy may read authoritative match state and pairing history, but it does not execute combat and does not mutate players directly.

## Authoritative history

`MatchState.CombatPairingHistory` records the pairings that were actually assigned by `MatchEngine`, including byes.

Each `MatchCombatPairing` stores:

- `Round`;
- `LeftPlayerId`;
- `RightPlayerId` for a live opponent, otherwise `null`;
- `EliminatedOpponentSourcePlayerId` when the pairing used the archived eliminated-player snapshot.

A pairing with both `RightPlayerId` and `EliminatedOpponentSourcePlayerId` equal to `null` is a bye. A bye records matchmaking history but does not create a combat settlement or execute combat effects.

The eliminated-opponent source is captured from the snapshot that existed at the start of that combat round. A player eliminated later in the same settlement cannot rewrite the recorded opponent retroactively.

Pairing history is read-only outside the match aggregate.

## History-aware native policy

`HistoryAwareCombatPairingPolicy` is a neutral baseline policy. It does not encode themed concepts or mutate match state.

For live opponents it prefers, in order:

1. fewer previous meetings between the two players;
2. the least-recent previous meeting;
3. injected deterministic RNG for an exact tie.

When the number of active players is odd and an eliminated-opponent snapshot exists, exactly one active player faces that archived snapshot. The policy assigns that slot by preferring:

1. fewer previous eliminated-opponent assignments;
2. the least-recent such assignment;
3. injected deterministic RNG for an exact tie.

When the number of active players is odd and no eliminated-opponent snapshot exists yet, exactly one active player receives a bye. Byes use the same fairness rule: fewer previous byes, then least-recent bye, then deterministic RNG for an exact tie.

This means an initial match may contain any participant count allowed by the mod's `MatchRules`; an odd first round no longer requires a fake archived opponent.

## Boundary

Typical use is explicit:

```csharp
var pairings = pairingPolicy.CreatePairings(match, randomSource);
var roundResult = matchEngine.ResolveCombatRound(match, pairings);
```

The policy creates intent. `MatchEngine` is still the authoritative executor and validator.

A future mod-driven matchmaking selector may choose among native policies, but the selected policy must continue to return explicit `CombatPairing` values rather than gaining mutation access to the match aggregate.
