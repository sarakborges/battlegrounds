# Presentation assets

Presentation assets are mod-owned data. They must never become authoritative gameplay state and `Battlegrounds.Core` must never read image/audio files or filesystem paths.

## Manifest

A mod may define `assets/presentation.json`. The file is optional; omitting it produces an empty presentation-asset catalog.

```json
{
  "leaders": {
    "steady": {
      "portrait": "assets/leaders/steady.svg"
    }
  },
  "units": {
    "scout": {
      "art": "assets/units/scout.svg"
    }
  },
  "actions": {
    "training": {
      "art": "assets/actions/training.svg"
    }
  }
}
```

The current stable slots are:

- `leaders.<leader-id>.portrait`: image;
- `units.<unit-id>.art`: image;
- `actions.<action-id>.art`: image.

IDs are the same stable authored IDs used by mechanical content. Assets do not introduce a second identity system.

## Path contract

Asset references are untrusted mod input. `Battlegrounds.Content` validates them before they are exposed:

- paths must be non-empty, relative, forward-slash paths under `assets/`;
- absolute paths, backslashes and `.`/`..` traversal segments are rejected;
- the referenced authored entity must exist;
- the slot must be valid for its entity category;
- the referenced file must exist;
- image slots currently accept `.png`, `.jpg`, `.jpeg`, `.webp` and `.svg`.

The manifest may be partial. A Leader, Unit or Action without a presentation asset remains valid and presentation must fall back to text/layout rather than changing gameplay.

## Runtime ownership

`ModPresentationAssetLoader` maps validated metadata into an immutable `ModPresentationAssetCatalog`. Entries contain only stable entity identity, presentation slot, asset type and mod-relative path.

`Battlegrounds.Game` owns runtime decoding and rendering. `ModPresentationTextureStore` resolves image references from the active mod directory, caches `Texture2D` instances and can apply them to Godot `TextureRect` or `Button` controls.

Neither `Battlegrounds.Core` nor `Battlegrounds.Application` receives Godot textures, decoded image bytes or filesystem concerns.

## Evolution

The catalog intentionally separates an asset reference's type from its slot. Future audio slots can use the same validated ID-keyed boundary while Godot remains responsible for decoding/playback. Animation metadata should describe presentation behavior without becoming simulation state or changing deterministic rules.
