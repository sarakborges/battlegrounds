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

## Viewport scaling

The browser UI uses the same `1280x720` logical viewport declared by `project.godot`. `core/viewport-scale.js` computes one uniform scale from the actual CEF viewport using `min(actualWidth / 1280, actualHeight / 720)` and applies it to the application shell. The scaler recomputes on both window and visual-viewport resize events.

All screens and reusable modules lay themselves out in logical pixels. Window resizing must not introduce independent viewport breakpoints, `vw`/`vh` geometry, or per-screen scaling rules. If the physical window has a different aspect ratio, the logical viewport remains centered and the unused area is letterboxed. This keeps component geometry, drag/drop hit targets, hover previews and screen composition stable at every window size.

## Source layout

The browser UI is deliberately componentized without a frontend framework or build step:

```text
WebUI/
  app.js                  # bootstrap only: state -> screen, theme, polling
  index.html              # browser entry point
  bridge/                 # Godot CEF IPC and action dispatch
  core/                   # template/style loading and viewport infrastructure
  interactions/           # shared pointer/hover/drag interaction behavior
  theme/                  # asset bridge + resolved ModThemeCatalog -> CSS variables
  styles/                 # global baseline only
  design-system/          # generic reusable UI primitives
    badge/
    button/
    empty-state/
    horizontal-stack/
    modal-dialog/
    panel/
  components/             # gameplay/application-aware reusable composition
    card/                  # full card surface used where the card itself is the persistent object
    card-preview/          # inspection-only card shown by hover inspector
    character-portrait/
    combat-unit-token/     # combat-only timeline marker/status composition around a playable token
    hero-cockpit/
    mod-option/
    opponent-rail/
    playable-token/        # persistent unit/action representation in preparation and combat
    player-chip/
    player-field/
    player-reserve/
    shopkeeper/
    tavern-controls/
    tavern-offer/
    turn-rail/
  screens/
    mod-selection/
    leader-selection/
    preparation/
    combat/
```

HTML structure must live in `.html` templates. Design-system, component and screen JavaScript clones those templates, fills text/state, assigns semantic `data-*` properties and connects behavior. It must not build markup with template strings or `innerHTML`.

Every reusable UI module under `design-system/` or `components/` owns a directory named after the module and contains matching `.html`, `.css`, and `.js` files. CI checks this convention and rejects HTML construction in reusable modules and screens.

Design-system modules are generic presentation primitives and must not know gameplay or application concepts. Components may compose design-system primitives and other components, but they do not own authoritative rules. Screens compose those components from presentation state.

Component names describe their responsibility. Prefer `tavern-offer`, `player-field`, `player-reserve`, `playable-token`, `combat-unit-token`, `card-preview`, and `mod-option` over generic containers whose behavior is selected by magic variants such as `area`, `zone`, or `section`.

A reusable module owns its own geometry and visual state. Screen CSS may position component roots as part of screen composition, but it must not reach into reusable component internals.

## Tokens and inspection

Gameplay uses `playable-token` as the persistent representation of a unit or action. Preparation uses it in the tavern offer, player field, reserve, choice and target surfaces. Combat composes the same token inside `combat-unit-token`, which owns transient timeline markers, status text and attacker/target/damage/summon/trigger animation. A full rectangular card is not used as the board piece.

Full card presentation belongs to `card-preview` and is created on demand by `interactions/hover-inspector.js`. Any inspectable surface opts in with semantic `data-inspect-*` metadata. The same inspector is used for playable tokens, combat units, leaders and hero powers so hover behavior stays consistent rather than being reimplemented per screen.

Descriptions are presentation content owned by the mod. C# resolves `entity.<kind>.<id>.description` through `ModPresentationText` and sends the resolved description in the Web UI snapshot. The browser must not invent gameplay descriptions.

Static entity art is also mod-owned. The Web UI consumes the existing validated `assets/presentation.json` catalog (`leaders.<id>.portrait`, `units.<id>.art`, `actions.<id>.art`). Missing authored art falls back visually without changing gameplay.

An element may remain inspectable while it is not actionable. `playable-token` therefore uses `aria-disabled` / `data-disabled` instead of the native `disabled` attribute; the action and drag dispatchers enforce that semantic disabled state while pointer/focus inspection remains available.

## CSS cascade and theme ownership

Theme resolution must not write presentation properties such as padding, background, border, radius, font size, or opacity directly as inline styles. `theme/theme.js` exposes resolved values as element-scoped `--theme-component-*` custom properties; the owning primitive or component decides how those tokens are consumed and which geometry remains fixed by the component contract.

Theme asset fields use the same rule. `theme/assets.js` requests mod-local assets through the C# bridge, and `backgroundAsset` resolves to `--theme-component-background-image`. Components decide how to place that image. PNG, JPEG, WebP and SVG image assets are supported by the Web UI bridge.

The cascade is intentional: primitive defaults are the baseline, composed components use selectors that describe their composition, and interaction-state selectors describe temporary state. `!important` is forbidden by CI. If a rule cannot win without it, fix ownership, selector intent, or stylesheet structure instead of increasing force.

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

The resolved `ModThemeCatalog` is included in state snapshots. `theme/theme.js` maps colors, spacing, radii, font sizes, metrics, component roles and supported screen roles into CSS custom properties. Asset-backed theme fields are resolved through `theme/assets.js`.
