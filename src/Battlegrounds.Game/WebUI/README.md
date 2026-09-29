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

If `CefTexture` is unavailable, `WebUiHost.gd` emits `web_unavailable` and the existing Godot UI remains active. This makes the migration screen-by-screen and keeps the current presentation as a fallback while the browser UI is incomplete.

## Files

- `index.html` — browser entry point loaded through `res://`.
- `styles.css` — layout and baseline visual styles.
- `app.js` — renderer, event binding, IPC client, and mod-theme application.
- `WebUiHost.gd` — optional CEF host and Godot/JavaScript IPC relay.
- `WebUiHost.tscn` — full-screen host scene.
- `../Scripts/Main.WebUi.cs` — C# state/command bridge.

There is deliberately no frontend build step yet. The first migration slice stays as plain HTML/CSS/JavaScript so UI changes can be made directly and reloaded without introducing npm/Vite/React before the bridge and presentation contract are stable.

## Contract

Browser to Godot messages are JSON objects with a `type` field. Godot responds with envelopes shaped as:

```json
{
  "type": "state",
  "payload": {}
}
```

The browser never mutates authoritative match state. It sends user intent; C# translates that intent to the existing domain/application commands and then pushes a fresh presentation snapshot.

The resolved `ModThemeCatalog` is included in the state snapshot. `app.js` maps colors, spacing, radii, font sizes, metrics, component roles, and screen roles into CSS/custom properties. Asset-backed theme fields are the next presentation slice.
