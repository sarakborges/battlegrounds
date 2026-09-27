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
- `ParticipantCount`: one human plus AI opponents. It must satisfy the selected mod's player-count rules and is currently required to be even because round one has no eliminated-opponent snapshot.

Bootstrap uses `ModLoader.Load(...)` before session creation. Invalid mod data therefore never becomes a running session.

Player `0` is the local human and the remaining generated player IDs are AI-controlled for this local prototype. These IDs are presentation/bootstrap configuration, not theme concepts or gameplay rules.

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

The returned `CombatRoundResult` is currently rendered as a textual session log. Combat animation timing is presentation-owned, but simulation is already complete and immutable from the scene's perspective.

After a resolved non-terminal combat, the scene may ask the session to prepare AI players for the next round. It does not directly ready those players or choose their commands.

## Read-only rendering

The current main screen reads:

- match phase and round;
- each player's Leader, Health, Armor, Tier, readiness/elimination state;
- the human Resource, upgrade cost and freeze state;
- generic playable offer and reserve views;
- human Field state;
- pending-choice state;
- validated combine definitions;
- combat settlements returned from Application.

Rendering may create derived labels, ordering, buttons and highlights. Those values are view state only and are never written back into Core.

## CI contract

CI builds `Battlegrounds.Game` in addition to testing Core, Content, AI and Application. This catches C#/Godot API drift at the adapter boundary even though headless CI does not run the interactive scene.

## Next presentation boundary

The next Godot slice should add combat playback over immutable combat result data. `CombatResult.Attacks` already exposes an ordered `CombatAttack` sequence with attacker/target IDs, damage, barrier/lethal flags, health-after values, death and revive information.

Presentation should turn that sequence into a playback queue/view model and animate it without rerunning combat, changing authoritative state, or delaying Core settlement. Preparation for the next round may already exist authoritatively while the scene is still displaying the completed prior combat.
