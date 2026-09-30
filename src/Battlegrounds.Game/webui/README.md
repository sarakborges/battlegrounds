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

All screens and reusable modules lay themselves out in logical pixels. Window resizing must not introduce independent viewport breakpoints, `vw`/`vh` geometry, or per-screen scaling rules. If the physical window has a different aspect ratio, the logical viewport remains centered and the unused area is letterboxed. This keeps component geometry, drag/drop hit targets, hover inspection and screen composition stable at every window size.

## Source layout

The browser UI is deliberately componentized without a frontend framework or build step:

```text
WebUI/
  app.js
  index.html
  bridge/                 # Godot CEF IPC and action dispatch
  core/                   # templates, inspection metadata and viewport infrastructure
  interactions/           # shared hover/drag interaction behavior
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
    action-card-preview/
    action-token/
    character-portrait/
    combat-player-hero/
    combat-unit-token/
    end-recruitment-control/
    hero-cockpit/
    hero-power-button/
    leader-choice/
    leader-inspector/
    mod-option/
    opponent-rail/
    player-chip/
    player-field/
    player-reserve/
    power-tooltip/
    resource-counter/
    shopkeeper/
    tavern-controls/
    tavern-offer/
    unit-card-preview/
    unit-token/
  screens/
    mod-selection/
    leader-selection/
    preparation/
    combat/
```

HTML structure must live in `.html` templates. Design-system, component and screen JavaScript clones those templates, fills text/state, assigns semantic `data-*` properties and connects behavior. It must not build markup with template strings or `innerHTML`.

Every reusable UI module under `design-system/` or `components/` owns a directory named after the module and contains matching `.html`, `.css`, and `.js` files. CI checks this convention and rejects HTML construction in reusable modules and screens.

Design-system modules are generic presentation primitives and must not know gameplay or application concepts. Components may compose design-system primitives and other components, but they do not own authoritative rules. Screens compose those components from presentation state.

Component names describe responsibility rather than selecting gameplay meaning through magic variants. A unit token, tavern action, leader choice, hero power, resource counter and end-recruitment control are different components even when they share lower-level primitives.

A reusable component owns its internal geometry and visual state. The screen owns where the component lives in the scene. Components must not use screen-specific offsets to compensate for other components. Generic card components are intentionally absent: persistent entities and inspection views have semantic components instead.

## Board scenes

Preparation and combat are logical `1280x720` board scenes rather than vertical page layouts or dashboard grids. Screens own semantic anchors; reusable components own only their local anatomy.

Preparation declares anchors for the tavern cluster, offer, player field, hero HUD, reserve, resource counter and end-recruitment control. The opponent rail overlays the left board edge instead of consuming a page column. Tavern controls form one visual cluster around the shopkeeper. The hero, hero power, resource counter and end-recruitment control are independent HUD pieces anchored to the board.

Combat uses the same board/cosmetic scene instead of opening a separate dashboard. The human combat side is normalized to the lower half of the board, the opponent to the upper half, each side has an anchored hero presentation, and combat units remain physical tokens in two board rows. Timeline text is transient feedback over the board rather than a permanent panel separating the armies. Archived/ghost opponents may omit live player metadata without invalidating playback.

Scene positions are expressed in logical pixels and may be exposed as supported theme metrics under semantic scene roles such as `layout.preparationScene.*` and `layout.combatScene.*`. Do not reintroduce grid rows, viewport-relative layout, or component-owned screen offsets whose purpose is to compensate for another component.

Leader selection also uses a semantic `leader-choice` component instead of styling internals of a generic card from screen CSS. Screen CSS stays at the composition boundary.

## Tokens and inspection

Persistent gameplay pieces use semantic representations:

- `unit-token` is the compact board/tavern representation of a unit. Art dominates the silhouette; tier and combat stats are attached to the frame.
- `action-token` is the compact representation of a tavern action/spell. It has its own art/cost/tier anatomy and does not pretend to be a minion.
- `combat-unit-token` composes `unit-token` and owns only transient combat playback state such as attacker/target/damage/summon markers and animation.

A full card is never the persistent board piece.

`interactions/hover-inspector.js` owns the common pointer/focus lifecycle but dispatches to entity-specific inspection views. Units use `unit-card-preview`, tavern actions use `action-card-preview`, leaders use `leader-inspector`, and hero powers use `power-tooltip`. Inspectable surfaces opt in through semantic `data-inspect-*` metadata.

Inspection placement may also be semantic. Board-edge portraits open inward, lower HUD pieces prefer opening upward, upper combat heroes prefer opening downward, and otherwise the inspector chooses the side with available space. This prevents inspection content from arbitrarily crossing the center of the board.

Descriptions are presentation content owned by the mod. C# resolves localized entity descriptions and sends them in presentation snapshots; browser code must not invent gameplay descriptions.

Entity art is mod-owned and authored with the entity/content definition. C# exposes only validated authored asset paths to the browser. Missing art falls back visually without changing gameplay.

An element may remain inspectable while it is not actionable. Semantic tokens therefore use `aria-disabled` / `data-disabled` instead of the native `disabled` attribute; action and drag dispatchers enforce disabled behavior while pointer/focus inspection remains available.

## CSS cascade and theme ownership

Theme resolution must not write presentation properties such as padding, background, border, radius, font size, or opacity directly as ordinary inline CSS. `theme/theme.js` exposes resolved values as element-scoped `--theme-component-*` custom properties; the owning primitive or component decides how those tokens are consumed and which geometry belongs to the component contract.

Component role inheritance includes state dictionaries. Base and specific roles are merged per state so a specialized role can override one hover property without discarding the remaining base hover contract. The runtime exposes hover, pressed, focus and disabled state variables to CSS.

Theme asset fields use the same rule. `theme/assets.js` requests mod-local assets through the C# bridge. Image assets and authored fonts are supported. Screen background assets and component background assets resolve through CSS variables; component CSS determines placement. Sliced assets are consumed by primitives that explicitly support slicing.

Authored frame assets must respect component anatomy. Overlay frames keep their content windows transparent so they do not cover entity art. Physical board controls such as hero power, Tavern controls, resource counter and end recruitment own independent theme roles instead of inheriting unrelated generic button imagery.

Theme typography may provide authored font assets. The runtime installs those fonts with generated `@font-face` rules and exposes them as `--theme-font-*` variables. Components can select semantic font roles through the theme instead of depending on a fixed browser font.

The cascade is intentional: primitive defaults are the baseline, composed components use selectors that describe their composition, and interaction-state selectors describe temporary state. `!important` is forbidden by CI. If a rule cannot win without it, fix ownership, selector intent, or stylesheet structure instead of increasing force. Component CSS must not wipe authored assets with shorthand declarations such as `background:` when the component is expected to preserve a theme-provided background image.

`app.js` does not know component markup. It maps application status to a screen, applies the supported screen theme and waits for async theme assets before committing the screen render. Authoritative state and commands remain in C#.

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

The resolved `ModThemeCatalog` is included in state snapshots. `theme/theme.js` maps colors, typography, spacing, radii, metrics, component roles, interaction states and screen roles into CSS custom properties. Asset-backed fields are resolved through `theme/assets.js`.
