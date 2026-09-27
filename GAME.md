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

The current UI uses mod-owned terminology/templates for concepts and chrome such as Leader, Unit, Action, Power, Health, Armor, Resource, Offer, Tier, Reserve, Field, Preparation, Combat, Round, acquire/release verbs, summaries, interaction prompts, reusable card stat lines and combat playback controls.

When `Locale` is empty, `TranslationServer.GetLocale()` provides the requested locale. Content applies exact-locale, language-locale and default-locale fallback; Godot does not implement a second fallback algorithm.

The scene may substitute runtime values into validated named templates, but localized strings never control simulation. Core commands, IDs, enums, targeting and validation remain independent of display text.

See `LOCALIZATION.md` for the package format and fallback contract.

## Mod-owned presentation assets

Validated image references remain presentation data. `Battlegrounds.Content` validates `assets/presentation.json`; `Battlegrounds.Game` resolves those references through `ModPresentationTextureStore` and owns image decoding plus `Texture2D` caching.

The reusable `PresentationCardButton` composes:

- optional Leader portrait or Unit/Action art;
- localized entity name;
- presentation context such as acquire/deploy/play/release or entity kind;
- localized mechanical stat text;
- optional localized entity description.

Missing art is not an error after validation when no asset reference was authored. The media region simply collapses and the same text/stat card remains usable. A failed runtime decode also degrades to the same text layout rather than changing gameplay state.

`PresentationCardButton` is view-only. It stores no authoritative slot, Unit instance, affordability, targeting or selection legality. The owning render method still captures the existing mechanical ID/slot and wires the same command handler as before.

See `PRESENTATION_ASSETS.md` for the asset path/type contract.

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

Leader choices, pending Unit/Action choices, Offer, Reserve and Field entries use the reusable presentation card while preserving the same command wiring.

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

Godot renders the authored options as Unit/Action cards and resolves the selected index only through:

- `ResolveUnitChoiceCommand`;
- `ResolveActionChoiceCommand`.

The scene never removes the pending choice or inserts the generated playable itself.

### Unit combines

Available recipe presentation is derived from the validated mod combine catalog plus the human player's read-only Reserve/Field state.

After choosing a recipe, the user explicitly toggles exact owned Unit instances. Presentation stores only those selected IDs. Confirmation submits one `CombineUnitsCommand` containing the selected `UnitInstanceId` values.

Reserve/Field component cards may show presentation-only selected styling, but Core still validates the recipe, copy count and instances, consumes components, returns pool ownership and creates the result. Godot never performs those mutations.

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

Combat board rows now use the same `PresentationCardButton` and validated Unit art as Preparation. Godot builds a presentation-only `UnitInstanceId → UnitId` lookup from frozen starting snapshots plus immutable summon/revive events so runtime instances retain stable authored visual identity without adding media concerns to Core.

The current timeline event selects a visual cue only. Attackers/targets, Unit trigger sources, summons, stat changes, damage, destruction, revives and behavior changes receive short scale/fade tweens on their rendered cards. A death event renders a transient fading Unit card at the event's recorded board position even though `CombatPlaybackState` has already removed that Unit from its projected board. These cues never affect timeline order or state mutation; rebuilding the same playback without tweens yields the same board projection and settlement.

After the final timeline event, playback shows the already-computed `CombatSettlement`, including winner/draw and player damage/Armor absorption. The overlay can auto-step, advance manually or skip directly to settlement. None of those controls call combat simulation again.

A full-screen presentation overlay blocks the underlying Preparation controls while playback is visible. Closing playback simply returns to rendering the authoritative session state that already exists underneath.

`CombatAttack` remains a compact Core strike summary, but Godot no longer depends on it as the complete animation stream. See `COMBAT_TIMELINE.md` for the result contract.

## Read-only rendering

The current main screen reads:

- validated mod presentation/localization data;
- validated mod-owned portrait/art references;
- match phase and round;
- each player's Leader, Health, Armor, Tier, readiness/elimination state;
- the human Resource, upgrade cost and freeze state;
- generic playable offer and reserve views;
- human Field state;
- pending-choice state;
- validated combine definitions;
- immutable session combat observations, event timelines and settlements.

Rendering may create derived labels, cards, textures, ordering, buttons, highlights, formatted strings, playback cursors and short-lived tweens. Those values are view state only and are never written back into Core.

## CI contract

CI builds `Battlegrounds.Game` in addition to testing Core, Content, AI and Application. This catches C#/Godot API drift at the adapter boundary even though headless CI does not run the interactive scene.

## Next presentation boundary

Extend mod-owned presentation metadata beyond static images. Add validated audio references and authored animation/cue metadata keyed by stable entity IDs and presentation event roles; `Battlegrounds.Content` should validate paths/types/schema, while Godot owns clip loading, playback and timeline-to-cue mapping. Audio duration, animation completion and missing optional media must never gate or alter simulation, settlement or authoritative Match state.