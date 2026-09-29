# Battlegrounds Web UI

This directory is the incremental HTML/CSS/JavaScript replacement for the gameplay presentation layer.

The authoritative game remains in the existing C# projects. `Main.WebUi.cs` serializes presentation state from the current `SinglePlayerSession` and maps browser messages back to the same preparation commands already used by the Godot UI.

## Runtime

The web surface uses Godot CEF. The addon is intentionally not committed because its release archive is very large.

On Windows, install the pinned version from the repository root:

```powershell
./tools/install-godot-cef.ps1
```

The installer currently pins Godot CEF `v1.16.2` and verifies the published SHA-256 before copying the addon into `src/Battlegrounds.Game/addons/godot_cef`.

If `CefTexture` is unavailable, `WebUiHost.gd` emits `web_unavailable` and the existing Godot UI remains active.

## Source layout

The browser UI is deliberately componentized without a frontend framework or build step:

```text
WebUI/
  app.js                  # bootstrap only: state -> screen, theme, polling
  index.html              # browser entry point
  bridge/                 # Godot CEF IPC and action dispatch
  core/                   # template/style loading primitives
  theme/                  # resolved ModThemeCatalog -> CSS/component styles
  styles/                 # global baseline only
  components/
    button/
      button.html         # markup
      button.css          # component presentation
      button.js           # behavior/data binding
    card/
      card.html
      card.css
      card.js
    ...
  screens/
    leader-selection/
      leader-selection.html
      leader-selection.css
      leader-selection.js
    preparation/
      preparation.html
      preparation.css
      preparation.js
    combat/
      combat.html
      combat.css
      combat.js
```

HTML structure must live in `.html` templates. Component and screen JavaScript clones those templates, fills text/state, assigns semantic `data-*` properties and connects behavior. It must not build markup with template strings or `innerHTML`.

Every reusable UI component owns a directory named after the component and contains the matching `.html`, `.css`, and `.js` files. The CI checks this convention and rejects HTML construction in component/screen JavaScript.

`app.js` does not know card/button/panel markup. Screens compose components; components do not know game rules. Authoritative state and commands remain in C#.

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
