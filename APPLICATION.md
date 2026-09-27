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
- assigns each AI one deterministic `PreparationAiPersonality` for the session;
- lets `PreparationAiAgent` select AI Leaders through the same selection boundary used by callers;
- leaves the human Leader choice explicit;
- creates and begins `MatchState` only after Leader selection is complete;
- rolls a fresh random Preparation initiative order for every round;
- accepts human Preparation commands only while the human owns initiative;
- runs an AI player's complete Preparation turn only while that AI owns initiative, using its assigned personality;
- lets each initiative owner execute any number of legal Preparation commands before `EndPreparationCommand` yields to the next player;
- asks `ICombatPairingPolicy` for explicit `CombatPairing` values;
- captures a read-only observation of the paired starting fields immediately before combat resolution;
- resolves combat only through `MatchEngine.ResolveCombatRound(...)`;
- stops automated advancement after one resolved combat round, returning control at the next Preparation or Finished state.

The session never edits Health, Resource, offers, reserves, fields, Leaders, placements, history or phase directly.

`AiPersonalities` exposes the assigned AI personalities through a read-only application-facing dictionary for diagnostics/presentation. The personality is stable for the session; it changes decision policy, not ownership or legality.

## Preparation initiative and the shared pool

Preparation is deliberately sequential in the local single-player orchestration. At the start of each Preparation round, `SinglePlayerSession` shuffles all active players using the session's injected `IRandomSource`. The resulting order is valid only for that round; the next round rolls again independently, with no fairness correction or rotation requirement.

Only the current initiative owner may execute Preparation commands. A player may perform as many legal actions as its economy and mechanics allow, including repeated acquire/release/refresh sequences, and yields initiative only by successfully ending Preparation. There is no one-human-action/one-AI-action coupling, no action quota and no Preparation timer in the current local flow.

This sequencing intentionally makes the shared Unit pool externally stable while the human is thinking: AI players do not execute commands in real time during the human turn. The human's own commands may still change the pool—for example refreshes exchange offered Units and released pooled Units return copies—but no opponent mutates the pool concurrently.

The authoritative pool remains hidden state. Presentation and AI may observe their ordinary public gameplay state, especially their own current Offer, but the human is not given exact remaining-copy counts or direct pool inspection. Apparent scarcity may only be inferred from visible game information and future offers.

Offer generation at the beginning of a round remains part of Core's Preparation setup. Once those offers exist, initiative controls all player-issued Preparation commands for the round.

The initiative boundary is independent of AI decision policy. `Tempo`, `Greedy` and `Roller` personalities may choose different priorities and therefore different command sequences—even when pursuing similar archetypes—without changing the rule that a single initiative owner acts at a time.

## Validated mod presentation pass-through

`SinglePlayerSession.Mod` exposes the same validated `ModPackage` supplied at creation time. That package now includes the immutable `ModPresentationCatalog` loaded by Content.

Application does not select locales, format templates, translate terminology or inspect localized text. Different presentation clients can resolve the same package for different locales while session orchestration remains framework- and language-independent.

Localized strings never influence AI, matchmaking, commands, targeting, random choices or lifecycle transitions.

See `LOCALIZATION.md` for the presentation package contract.

## Human input boundary

Presentation constructs ordinary `IPreparationCommand` values using the human `PlayerId` and submits them through:

```csharp
var result = session.ExecuteHumanPreparation(command);
```

A command for an AI-controlled player is rejected by the application boundary before it reaches Core. A human command submitted while another player owns Preparation initiative is also rejected by the session boundary. Core remains phase/rule authority; initiative is a single-player orchestration policy layered above it.

## Automated advancement

`AdvanceAutomated()` has deliberately narrow semantics:

1. if the match is in Preparation, run consecutive AI initiative owners one complete Preparation turn at a time with each AI's assigned personality;
2. stop immediately when initiative reaches the human player;
3. if every active player has ended Preparation and the match reached Combat, create explicit pairings and resolve exactly one combat round;
4. roll the next round's initiative if combat returns the Match to Preparation, then stop at that Preparation or Finished state.

This makes UI timing a presentation concern. Godot may animate, delay or step through returned combat results without changing simulation ownership. Calling `AdvanceAutomated()` after an ordinary successful human command is harmless: while the human still owns initiative, no AI command runs. Once the human ends Preparation, the call may continue through any later AI initiative turns and then resolve combat.

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

The same injected `IRandomSource` is shared by Leader offers, AI personality assignment, Preparation initiative, AI tie-breaking, offer generation, combat and matchmaking. The seed overload creates one `SeededRandomSource` for the entire session.

Given the same validated mod, participant IDs, human commands and seed, application orchestration follows the same deterministic Core/AI sequence, including the same AI personality assignments. Locale selection does not participate in this deterministic gameplay stream.

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
- locale selection and localization rendering;
- selection/highlight state;
- view models derived from authoritative read-only state;
- playback cursors over immutable `SessionCombatRecord` / `CombatResult` data.

Presentation must not own gameplay mutation, AI decisions, combat pairing rules or mod validation.
