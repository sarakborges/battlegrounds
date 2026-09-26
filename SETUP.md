# Match Setup

Match setup is intentionally separate from the authoritative match lifecycle.

`MatchState` starts only after every player has a validated `LeaderId`. The pre-match selection boundary is `LeaderSelectionState`.

## Mod rules

Leader offers are configured by `rules/setup.json`:

```json
{
  "leaderOfferSize": 2,
  "leaderOfferPolicy": "independentPerPlayer"
}
```

Supported policies:

- `independentPerPlayer` — each player receives a unique-within-that-offer sample from the full Leader catalog. The same Leader may appear in different players' offers and may therefore be selected by multiple players.
- `uniqueAcrossMatch` — all offer slots for the lobby are drawn without replacement. A Leader can appear in at most one player's offer.

Offer generation uses the injected `IRandomSource` and stable Leader catalog ordering, so the same player IDs, rules, catalog and RNG seed produce the same offers.

Players are ordered by `PlayerId` before offers are assigned. Caller iteration order must not change the result.

## Flow

A loaded mod can create setup state with:

```csharp
var selection = mod.CreateLeaderSelection(playerIds, randomSource);
```

Presentation/AI reads a player's immutable offer through:

```csharp
selection.GetOffer(playerId);
```

A final choice is submitted through:

```csharp
selection.Select(playerId, leaderId);
```

The Core rejects unknown players, a second final selection from the same player, and Leaders that were not in that player's offer.

After `selection.IsComplete` becomes true:

```csharp
var setups = selection.GetCompletedPlayerSetups();
var match = matchEngine.CreateMatch(setups);
```

The setup boundary never mutates `MatchState`, and `MatchEngine` does not secretly generate or reroll Leader offers.

## Validation

`rules/setup.json` is mandatory for a mod package. Whole-mod validation checks:

- required and unknown keys;
- positive `leaderOfferSize`;
- supported `leaderOfferPolicy`;
- enough Leaders for the configured policy.

For `independentPerPlayer`, the catalog must contain at least `leaderOfferSize` Leaders.

For `uniqueAcrossMatch`, the catalog must contain at least:

```text
maximumPlayers × leaderOfferSize
```

This capacity rule is checked before the mod loads so a lobby supported by `rules/match.json` cannot fail later merely because the Leader pool is too small.
