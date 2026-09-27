# Single-player application/session boundary

`Battlegrounds.Application` is a framework-free orchestration layer for the local single-player flow.

It depends on validated Content data, Core gameplay boundaries and the framework-free AI policy. It does not own a second copy of gameplay state and it does not mutate `PlayerState` or `MatchState` directly.

Dependency direction:

```text
Battlegrounds.Game
      |
      v
Battlegrounds.Application
   /        |        \
  v         v         v
Content     AI       Core
  \         |         ^
   +--------+---------+
```

`Battlegrounds.Content` still owns filesystem/JSON loading and validation. `Battlegrounds.Core` still owns authoritative simulation and mutation. `Battlegrounds.AI` still emits ordinary Core commands. Godot remains a presentation/input adapter.

## SinglePlayerSession

`SinglePlayerSession` coordinates one human `PlayerId` plus one or more AI-controlled opponents.

Creation accepts an already validated `ModPackage`, explicit participant IDs, deterministic RNG or a seed, and an optional `ICombatPairingPolicy`.

The session:

- creates the mod-defined `LeaderSelectionState`;
- lets `PreparationAiAgent` select AI Leaders through the same selection boundary used by callers;
- leaves the human Leader choice explicit;
- creates and begins `MatchState` only after Leader selection is complete;
- accepts human Preparation commands only for the configured human `PlayerId`;
- runs each active AI player's Preparation through `PreparationAiAgent.PlayPreparation(...)`;
- asks `ICombatPairingPolicy` for explicit `CombatPairing` values;
- captures a read-only observation of the paired starting fields immediately before combat resolution;
- resolves combat only through `MatchEngine.ResolveCombatRound(...)`;
- stops automated advancement after one resolved combat round, returning control at the next Preparation or Finished state.

The session never edits Health, Resource, offers, reserves, fields, Leaders, placements, history or phase directly.

## Human input boundary

Presentation constructs ordinary `IPreparationCommand` values using the human `PlayerId` and submits them through:

```csharp
var result = session.ExecuteHumanPreparation(command);
```

A command for an AI-controlled player is rejected by the application boundary before it reaches Core. This prevents presentation code from accidentally driving both sides while still preserving the same Core command types for human and AI actors.

## Automated advancement

`AdvanceAutomated()` has deliberately narrow semantics:

1. if the match is in Preparation, finish Preparation for every active AI player that is not already ready;
2. if the match is still waiting for the human player, stop;
3. if the match reached Combat, create explicit pairings and resolve exactly one combat round;
4. stop at the next Preparation or Finished state.

This makes UI timing a presentation concern. Godot may animate, delay or step through returned combat results without changing simulation ownership.

## Immutable combat observation

When a combat round is about to resolve, the session freezes the paired starting Unit views into `SessionCombatUnitSnapshot` values before calling Core. After Core returns, the session publishes a `SessionCombatRecord` through `LastCombat`.

A record contains:

- a monotonically increasing session-local sequence number;
- the combat round number;
- the explicit `CombatPairing` values used;
- immutable starting Unit identity/stats for live and eliminated-opponent participants;
- the authoritative `CombatRoundResult` returned by `MatchEngine`, including each settlement's immutable `CombatResult.Timeline`.

The starting snapshots plus the ordered Core timeline give presentation enough data to render combat-local summons, trigger/effect transitions, deaths/revives and settlement after the authoritative Match has already advanced.

This is a client/replay observation, not a second gameplay model. The snapshots and timeline are never read back into Core, never drive settlement and never replace `MatchState`. They exist so presentation can keep showing an already-resolved combat after Core has moved to the next lifecycle state.

`LastCombat` is intentionally only the most recent observation. Long-term replay/history persistence is a separate concern from the live single-player session boundary.

See `COMBAT_TIMELINE.md` for the Core result-event contract.

## Determinism

The same injected `IRandomSource` is shared by Leader offers, AI tie-breaking, offer generation, combat and matchmaking. The seed overload creates one `SeededRandomSource` for the entire session.

Given the same validated mod, participant IDs, human commands and seed, application orchestration follows the same deterministic Core/AI sequence.

## Current odd-player constraint

The current Core combat contract requires an eliminated-opponent snapshot when the active player count is odd. No such snapshot exists before the first combat round, so a session currently requires an even initial participant count.

This is an explicit application precondition, not a hidden bye rule. A future matchmaking design may change that contract, but the session must continue to produce explicit pairings rather than bypass `MatchEngine` validation.

## Presentation ownership

Godot should consume the session and read-only domain state rather than recreating rules in scene scripts.

Presentation may own:

- scene transitions;
- input mapping;
- animations and combat playback timing;
- audio;
- localization rendering;
- selection/highlight state;
- view models derived from authoritative read-only state;
- playback cursors over immutable `SessionCombatRecord` / `CombatResult` data.

Presentation must not own gameplay mutation, AI decisions, combat pairing rules or mod validation.
