# Battlegrounds Web UI

This directory is the HTML/CSS/JavaScript gameplay presentation layer.

The authoritative game remains in the existing C# projects. `Main.WebUi.cs` serializes presentation state from `SinglePlayerSession` and maps browser messages back to the same preparation commands used by the application boundary.

## Runtime

Gameplay presentation uses Godot CEF. The addon is intentionally not committed because its release archive is very large.

On Windows, install the pinned version from the repository root:

```powershell
./tools/install-godot-cef.ps1
```

The installer pins Godot CEF `v1.16.2` and verifies the published SHA-256 before copying the addon into `src/Battlegrounds.Game/addons/godot_cef`.

CEF is required for gameplay. There is no Godot Control fallback for the gameplay UI. If `CefTexture` cannot be created or the Web UI cannot load, the game fails fast instead of silently falling back to a second presentation implementation.

The Godot launcher remains active for mod discovery/selection until its own dedicated web migration slice.

## Source layout

The browser UI is deliberately componentized without a frontend framework or build step:

```text
WebUI/
  app.js                  # bootstrap only: state -> screen, theme, polling
  index.html              # browser entry point
  bridge/                 # Godot CEF IPC and action dispatch
  core/                   # template/style loading infrastructure
  theme/                  # resolved ModThemeCatalog -> CSS/component styles
  styles/                 # global baseline only
  design-system/          # generic reusable UI primitives
    badge/
    button/
    empty-state/
    horizontal-stack/
    modal-dialog/
    panel/
  components/             # gameplay-aware reusable composition
    card/
    card-area/
    character-portrait/
    combat-card/
    hero-cockpit/
    opponent-rail/
    player-chip/
    shopkeeper/
    tavern-controls/
    turn-rail/
    zone/
    ...
  screens/
    leader-selection/
      leader-selection.html
      leader-selection.css
      leader-selection.js
    preparation/
      preparation.html    # screen composition slots only
      preparation.css     # screen-level grid/stage only
      preparation.js      # state -> component orchestration
    combat/
      combat.html
      combat.css
      combat.js
```

HTML structure must live in `.html` templates. Design-system, component and screen JavaScript clones those templates, fills text/state, assigns semantic `data-*` properties and connects behavior. It must not build markup with template strings or `innerHTML`.

Every reusable UI module under `design-system/` or `components/` owns a directory named after the module and contains matching `.html`, `.css`, and `.js` files. The CI checks this convention and rejects HTML construction in reusable modules and screens.

Design-system modules are generic presentation primitives and must not know gameplay concepts. Components may compose design-system primitives and other components, but they do not know game rules. Screens compose gameplay components from presentation state.

A reusable module owns its own geometry and visual state. Screen CSS may position component roots as part of the screen composition, but it must not reach into reusable internals. For example, preparation card width/height belongs to `components/card/card.css`; the preparation screen is not allowed to size `.card`, `.action-button`, `.horizontal-stack`, `.player-chip`, or `.character-portrait` directly.

`app.js` does not know card/button/panel markup. Authoritative state and commands remain in C#.

## Contract

Browser to Godot messages are JSON objects with a `type` field. Godot responds with envelopes shaped as:

```json
{
  "type": "state",
  "payload": {}
}
```

The browser never mutates authoritative match state. It sends user intent; C# translates that intent to the existing domain/application commands and then pushes a fresh presentation snapshot.

The resolved `ModThemeCatalog` is included in the state snapshot. `theme/theme.js` maps colors, spacing, radii, font sizes, metrics, component roles and screen roles into CSS/custom properties. Asset-backed theme fields remain a separate presentation slice.
