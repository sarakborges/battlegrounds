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

`Main.cs` currently provides the first playable vertical slice. Its exported bootstrap values are:

- `ModPath`: validated mod directory, defaulting to the repository `mods/example` fixture while developing locally;
- `Seed`: deterministic single-player session seed;
- `ParticipantCount`: one human plus AI opponents. It must satisfy the selected mod's player-count rules and is currently required to be even because round one has no eliminated-opponent snapshot.

Bootstrap uses `ModLoader.Load(...)` before session creation. Invalid mod data therefore never becomes a running session.

Player `0` is the local human and the remaining generated player IDs are AI-controlled for this local prototype. These IDs are presentation/bootstrap configuration, not theme concepts or gameplay rules.

## Input translation

The scene does not edit `MatchState` or `PlayerState`. Buttons create ordinary Core commands and send them through `SinglePlayerSession.ExecuteHumanPreparation(...)`.

The first presentation surface covers:

- human Leader selection from the authoritative `LeaderSelectionState` offer;
- generic playable acquisition through `AcquirePlayableCommand`;
- Unit deployment and release;
- offer refresh;
- tier upgrade;
- freeze/unfreeze;
- untargeted Power activation;
- untargeted Action play;
- ending Preparation.

Rejected commands are displayed as presentation feedback. The UI does not reproduce affordability, capacity, phase, target or readiness validation.

Targeted Actions/Powers, pending-choice interaction and explicit combine selection are intentionally not reimplemented as ad-hoc UI rules. They need a presentation interaction-state slice that still derives legal intent from the public domain state and submits the existing commands.

## Automated advancement

After input, the scene asks `SinglePlayerSession.AdvanceAutomated()` to run AI Preparation and, when all active players are ready, resolve one combat round through the Application boundary.

The returned `CombatRoundResult` is currently rendered as a textual session log. Combat animation timing is presentation-owned, but simulation is already complete and immutable from the scene's perspective.

After a resolved non-terminal combat, the scene may ask the session to prepare AI players for the next round. It does not directly ready those players or choose their commands.

## Read-only rendering

The current main screen reads:

- match phase and round;
- each player's Leader, Health, Armor, Tier, readiness/elimination state;
- the human Resource, upgrade cost and freeze state;
- generic playable offer and reserve views;
- human Field state;
- combat settlements returned from Application.

Rendering may create derived labels, ordering and highlights. Those values are view state only and are never written back into Core.

## CI contract

CI builds `Battlegrounds.Game` in addition to testing Core, Content, AI and Application. This catches C#/Godot API drift at the adapter boundary even though headless CI does not run the interactive scene.

## Next presentation boundary

The next Godot slice should add explicit interaction state for mechanics that require multi-step human intent: selected Unit targets, pending Unit/Action choices and combine component selection. That state belongs to presentation and must end by submitting the existing Core commands rather than introducing scene-owned gameplay resolution.
