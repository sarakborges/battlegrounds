# Presentation assets and cues

Presentation assets and playback cues are mod-owned data. They must never become authoritative gameplay state, and `Battlegrounds.Core` must never read image/audio files, filesystem paths, animation names or presentation timing.

## Manifest

Unit and action card art is authored directly in each card JSON through an optional `art` path. `assets/presentation.json` is reserved for non-card presentation metadata such as leader portraits and playback cues and is optional.

```json
{
  "leaders": {
    "steady": {
      "portrait": "assets/leaders/steady.svg",
      "cues": {
        "ui.select": {
          "animation": "pulse",
          "durationSeconds": 0.16
        }
      }
    }
  },
  "units": {
    "scout": {
      "art": "assets/units/scout.svg",
      "cues": {
        "combat.attack": {
          "animation": "lunge",
          "durationSeconds": 0.2,
          "audio": "assets/audio/scout-attack.wav"
        },
        "combat.death": {
          "animation": "fade",
          "durationSeconds": 0.32
        }
      }
    }
  },
  "actions": {
    "training": {
      "art": "assets/actions/training.svg"
    }
  }
}
```

IDs are the same stable authored IDs used by mechanical content. Presentation metadata does not introduce a second identity system.

## Static asset slots

The current stable image slots are:

- `content/units/<unit-id>.json` -> `art`;
- `content/actions/<action-id>.json` -> `art`;
- `assets/presentation.json` -> `leaders.<leader-id>.portrait` for non-card portraits.

Image slots accept `.png`, `.jpg`, `.jpeg`, `.webp` and `.svg`.

## Cue roles

Each entity may define a `cues` object keyed by a stable presentation event role. Current roles are:

- Leader: `ui.select`;
- Unit UI: `ui.select`, `ui.acquire`, `ui.deploy`, `ui.release`;
- Action UI: `ui.select`, `ui.acquire`, `ui.play`;
- Unit combat: `combat.attack`, `combat.target`, `combat.summon`, `combat.stats`, `combat.damage`, `combat.destroy`, `combat.death`, `combat.revive`, `combat.trigger`, `combat.behavior`.

A cue must define at least one of `animation` or `audio`. `durationSeconds` is optional, applies only to animation, must be greater than zero and is capped at five seconds.

Current animation names are `none`, `pulse`, `shake`, `lunge`, `fade` and `pop`. They describe presentation intent rather than simulation semantics. `none` can explicitly suppress an engine fallback animation while still allowing audio.

Audio references currently accept `.wav`. Audio is optional; mods without authored sound remain fully valid.

## Theme-owned fallback motion

Entity cue metadata and theme motion metrics solve different layers of the presentation contract:

- `assets/presentation.json` chooses entity/role-specific animation intent, duration and audio;
- the resolved `presentation/theme.json` owns the neutral numeric motion profile used when cue data is partial or absent.

The theme controls combat playback cadence, UI-selection timing and the scale/rotation/opacity/duration parameters for `pulse`, `shake`, `lunge`, `fade` and `pop`. A selected mod may override those `motion.*` metrics without authoring cues for every entity. Entity-specific `durationSeconds` still wins over the theme's fallback duration for that cue.

These values only animate already-created presentation controls. They cannot change command timing, combat resolution, deterministic RNG or any authoritative state.

## Path contract

Asset references are untrusted mod input. `Battlegrounds.Content` validates them before exposing metadata:

- paths must be non-empty, relative, forward-slash paths under `assets/`;
- absolute paths, backslashes and `.`/`..` traversal segments are rejected;
- referenced authored entity IDs must exist;
- static slots and cue roles must be valid for their entity category;
- referenced files must exist;
- media extensions must match the declared use;
- cue objects reject unknown keys, invalid animation names and invalid durations.

The manifest may be partial. Missing art, audio or cue metadata is a presentation fallback, not a gameplay error.

## Immutable Content boundary

`ModPresentationAssetLoader` derives unit/action art directly from card definitions and maps validated image metadata into an immutable `ModPresentationAssetCatalog`.

`ModPresentationCueLoader` maps validated cue metadata into an immutable `ModPresentationCueCatalog`. Each cue contains only stable entity identity, a presentation role, optional animation intent/duration and an optional audio file reference. It contains no Godot node, `Tween`, `AudioStream`, texture or decoded bytes.

## Godot runtime ownership

`Battlegrounds.Game` owns decoding and playback:

- `ModPresentationTextureStore` loads/caches external images as `Texture2D`;
- `ModPresentationCuePlayer` maps entity/role lookups to presentation-only tweens and optional cached `.wav` playback;
- reusable cards emit `ui.select` cues when pressed;
- deterministic combat playback maps the already-resolved current timeline event to the appropriate Unit cue role;
- numeric fallback motion is read from the resolved engine-default-plus-mod theme instead of duplicated as C# literals.

Authored cue duration never delays, advances or gates the simulation. Combat playback continues on its own presentation clock/manual controls; clip completion is not observed by Core or Application. That presentation clock is itself theme-owned and remains outside simulation.

If a cue is absent, Godot uses its neutral semantic fallback animation with theme-owned numeric tuning. If authored audio cannot be played at runtime, the event still advances normally and the visual/text fallback remains usable.

## Ownership invariant

Neither `Battlegrounds.Core` nor `Battlegrounds.Application` receives filesystem paths, decoded media, Godot resources, animation completion callbacks or clip duration. Simulation and settlement are fully determined before presentation consumes any cue.
