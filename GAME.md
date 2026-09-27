# Godot presentation adapter

`Battlegrounds.Game` is the Godot-facing presentation/input layer. It may bootstrap a local session, render read-only state and translate user interaction into Application/Core commands. It does not own gameplay rules or authoritative mutation.

Dependency flow:

```text
Godot scene/input
      |
      v
Battlegrounds.Game
      |
      v
Battlegrounds.Application
      |
      +--> Content
      +--> AI
      +--> Core
```

## Main scene bootstrap

`Main.cs` provides the current playable vertical slice. Its exported bootstrap values are:

- `ModPath`: validated mod directory, defaulting to the repository `mods/example` fixture while developing locally;
- `Seed`: deterministic single-player session seed;
- `ParticipantCount`: one human plus AI opponents. It must satisfy the selected mod's player-count rules and is currently required to be even because round one has no eliminated-opponent snapshot;
- `Locale`: optional presentation locale. Empty means use Godot's system locale and then the mod catalog's fallback rules;
- `CombatPlaybackStepSeconds`: presentation-only delay between automatic playback steps.

Bootstrap uses `ModLoader.Load(...)` before session creation. Invalid gameplay or presentation/localization data therefore never becomes a running session.

Player `0` is the local human and the remaining generated player IDs are AI-controlled for this local prototype. These IDs are presentation/bootstrap configuration, not theme concepts or gameplay rules.

## Mod-owned presentation text

Godot does not own the visible vocabulary for neutral engine concepts. After loading the mod it resolves `ModPackage.Presentation` for the requested locale and renders that `ModPresentationText`.

The current UI uses mod-owned terminology/templates for concepts and chrome such as Leader, Unit, Action, Power, Health, Armor, Resource, Offer, Tier, Reserve, Field, Preparation, Combat, Round, acquire/release verbs, summaries, interaction prompts and combat playback controls.

When `Locale` is empty, `TranslationServer.GetLocale()` provides the requested locale. Content applies exact-locale, language-locale and default-locale fallback; Godot does not implement a second fallback algorithm.

The scene may substitute runtime values into validated named templates, but localized strings never control simulation. Core commands, IDs, enums, targeting and validation remain independent of display text.

See `LOCALIZATION.md` for the package format and fallback contract.

## Input translation

The scene does not edit `MatchState` or `PlayerState`. Completed interactions create ordinary Core commands and send them through `SinglePlayerSession.ExecuteHumanPreparation(...)`.

The current presentation surface covers:

- human Leader selection from the authoritative `LeaderSelectionState` offer;
- generic playable acquisition through `AcquirePlayableCommand`;
- Unit deployment and release;
- offer refresh;
- tier upgrade;
- freeze/unfreeze;
- untargeted and selected-target Power activation;
- untargeted and selected-target Action play;
- pending Unit/Action choice resolution;
- explicit Unit-combine recipe and component selection;
- ending Preparation.

Rejected commands are displayed as presentation feedback. The UI does not reproduce affordability, capacity, phase, target or readiness validation.

## Multi-step presentation interaction state

`PresentationInteractionState` owns temporary UI intent only. It may remember:

- that an Action is waiting for a selected target and which reserve slot initiated it;
- that a Power is waiting for a selected target;
- that the user is choosing a combine recipe;
- which exact `UnitInstanceId` values are highlighted as combine components.

It never mutates domain objects and never resolves an effect itself.

### Selected targets

Godot deliberately does not parse effect selectors to decide whether an Action or Power needs a target. It first submits the ordinary command without a target.

If Core returns `InvalidActionTarget` or `InvalidPowerTarget`, presentation enters target-selection mode. Candidate Field Units are rendered as buttons and the selected `UnitInstanceId` is resubmitted through the same command type. Core remains the source of truth for whether that target is legal.

This keeps type/tag selector semantics, phase legality and effect validation out of the scene.

### Pending choices

`PlayerState.PendingChoice` is authoritative read-only state. While a pending choice exists, unrelated Preparation controls are disabled.

Godot renders the authored options and resolves the selected index only through:

- `ResolveUnitChoiceCommand`;
- `ResolveActionChoiceCommand`.

The scene never removes the pending choice or inserts the generated playable itself.

### Unit combines

Available recipe presentation is derived from the validated mod combine catalog plus the human player's read-only Reserve/Field state.

After choosing a recipe, the user explicitly toggles exact owned Unit instances. Presentation stores only those selected IDs. Confirmation submits one `CombineUnitsCommand` containing the selected `UnitInstanceId` values.

Core still validates the recipe, copy count and instances, consumes components, returns pool ownership and creates the result. Godot never performs those mutations.

## Automated advancement

After successful input, the scene asks `SinglePlayerSession.AdvanceAutomated()` to run AI Preparation and, when all active players are ready, resolve one combat round through the Application boundary.

Combat settlement completes before presentation playback starts. The Application layer publishes the latest immutable `SessionCombatRecord`, which contains frozen starting Unit views plus the authoritative `CombatRoundResult`. The Match may therefore already be in the next Preparation or Finished state while Godot is still displaying the prior combat.

After a resolved non-terminal combat, the scene may ask the session to prepare AI players for the next round. It does not directly ready those players or choose their commands.

## Deterministic combat playback

`CombatPlaybackState` is a presentation-only mutable view model built from one immutable `SessionCombatRecord` plus the resolved mod presentation text.

Playback starts from the frozen Unit snapshots and then consumes `CombatResult.Timeline` strictly in sequence order. Each step applies one already-resolved Core event to local visual state. The current event surface covers:

- trigger attribution for Units and Powers;
- attack start with attacker/target highlighting;
- combat-local summons with stable identity and exact board insertion position;
- trigger-driven stat changes;
- damage and destruction outside ordinary attacks;
- Unit death/removal and revive/reinsertion;
- native behavior add/remove/consume transitions;
- combat Resource deltas;
- Power replacement.

After the final timeline event, playback shows the already-computed `CombatSettlement`, including winner/draw and player damage/Armor absorption. The overlay can auto-step, advance manually or skip directly to settlement. None of those controls call combat simulation again.

A full-screen presentation overlay blocks the underlying Preparation controls while playback is visible. Closing playback simply returns to rendering the authoritative session state that already exists underneath.

`CombatAttack` remains a compact Core strike summary, but Godot no longer depends on it as the complete animation stream. See `COMBAT_TIMELINE.md` for the result contract.

## Read-only rendering

The current main screen reads:

- validated mod presentation/localization data;
- match phase and round;
- each player's Leader, Health, Armor, Tier, readiness/elimination state;
- the human Resource, upgrade cost and freeze state;
- generic playable offer and reserve views;
- human Field state;
- pending-choice state;
- validated combine definitions;
- immutable session combat observations, event timelines and settlements.

Rendering may create derived labels, ordering, buttons, highlights, formatted strings and playback cursors. Those values are view state only and are never written back into Core.

## CI contract

CI builds `Battlegrounds.Game` in addition to testing Core, Content, AI and Application. This catches C#/Godot API drift at the adapter boundary even though headless CI does not run the interactive scene.

## Next presentation boundary

Generic UI vocabulary/templates are now mod-owned, but authored entity names remain single display strings inside gameplay content files.

The next presentation/content slice should add stable localization keys for authored Leaders, Powers, Units, Actions, behaviors, types, tags and combines, plus future descriptions where appropriate. IDs and gameplay definitions must remain locale-independent. Binary art/audio should remain a later presentation concern.
