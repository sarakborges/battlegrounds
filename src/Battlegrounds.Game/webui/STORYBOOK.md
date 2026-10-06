# Web UI Storybook

Storybook is a development/test harness for the vanilla HTML/CSS/ES-module UI. It is not part of the Godot CEF runtime build and does not replace the existing `.html + .css + .js` component contract.

From `src/Battlegrounds.Game/webui`:

```bash
npm install
npm run storybook
```

Storybook runs at `http://localhost:6006` and loads the real component factories/templates. The fixture theme is asset-free on purpose, so stories can run without the Godot asset bridge.

## Tests

Install Chromium once for Vitest browser mode:

```bash
npm run storybook:install-browser
```

Then run all Storybook stories as browser component tests:

```bash
npm run test-storybook
```

The Storybook sidebar also exposes the test widget from `@storybook/addon-vitest`, including interaction and accessibility results.

## Adding stories

Add `*.stories.js` files under `webui/storybook/`. Prefer rendering the real exported async factory through `.storybook/story-renderer.js` rather than duplicating component markup in a story.

Use `play` assertions for behavior and semantic contracts that matter to the runtime: accessible names, disabled state, `data-action`, inspection metadata and visible state markers.
