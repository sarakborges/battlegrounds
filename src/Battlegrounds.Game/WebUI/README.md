# Battlegrounds Web UI

This directory is the HTML/CSS/JavaScript presentation layer for mod selection and gameplay.

Authoritative state remains in C#. The browser receives presentation snapshots and emits user intent; it does not discover mods, validate packages, create sessions, or mutate match state directly.

## Runtime

Presentation uses Godot CEF. The addon is intentionally not committed because its release archive is very large.

On Windows, install the pinned version from the repository root:

```powershell
./tools/install-godot-cef.ps1
```

The installer pins Godot CEF `v1.16.2` and verifies the published SHA-256 before copying the addon into `src/Battlegrounds.Game/addons/godot_cef`.

CEF is required. There is no parallel Godot Control implementation of the application UI.

The application starts in `ModSelection.tscn`. `ModSelectionController` owns filesystem discovery and validation through `ModDiscovery`, exposes only serializable package summaries to the browser, and accepts selection by a previously discovered directory name. A valid selection instantiates the gameplay scene and passes the selected mod path into `Main`.

Gameplay then uses `Main.WebUi.cs` to serialize presentation state from `SinglePlayerSession` and map browser messages back to the application/domain commands.

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
  components/             # gameplay/application-aware reusable composition
    card/
    character-portrait/
    combat-card/
    hero-cockpit/
    mod-option/
    opponent-rail/
    player-chip/
    player-field/
    player-reserve/
    shopkeeper/
    tavern-controls/
    tavern-offer/
    turn-rail/
  screens/
    mod-selection/
      mod-selection.html
      mod-selection.css
      mod-selection.js
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

Every reusable UI module under `design-system/` or `components/` owns a directory named after the module and contains matching `.html`, `.css`, and `.js` files. CI checks this convention and rejects HTML construction in reusable modules and screens.

Design-system modules are generic presentation primitives and must not know gameplay or application concepts. Components may compose design-system primitives and other components, but they do not own authoritative rules. Screens compose those components from presentation state.

Component names describe their responsibility. Prefer `tavern-offer`, `player-field`, `player-reserve`, and `mod-option` over generic containers whose behavior is selected by magic variants such as `area`, `zone`, or `section`.

A reusable module owns its own geometry and visual state. Screen CSS may position component roots as part of screen composition, but it must not reach into reusable component internals. For example, preparation card width/height belongs to `components/card/card.css`; the preparation screen is not allowed to size `.card`, `.action-button`, `.horizontal-stack`, `.player-chip`, or `.character-portrait` directly.

`app.js` does not know component markup. It maps the application status to a screen and applies the supported theme screen role. Authoritative state and commands remain in C#.

## Contract

Browser-to-Godot messages are JSON objects with a `type` field. Godot responds with envelopes shaped as:

```json
{
  "type": "state",
  "payload": {}
}
```

For mod selection, the browser can request a refresh or submit the `directoryName` of a package from the current discovery snapshot. C# resolves that value against its own discovered entries and refuses unknown or invalid packages. The browser never supplies an arbitrary filesystem path.

For gameplay, the browser sends user intent; C# translates that intent to the existing domain/application commands and then pushes a fresh presentation snapshot.

The resolved `ModThemeCatalog` is included in state snapshots. `theme/theme.js` maps colors, spacing, radii, font sizes, metrics, component roles and supported screen roles into CSS/custom properties.
