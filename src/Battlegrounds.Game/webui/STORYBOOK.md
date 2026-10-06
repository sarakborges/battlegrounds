# Web UI Storybook

Storybook is the development/test harness for the vanilla HTML/CSS/ES-module UI. It is not part of the Godot CEF runtime build.

Stories mirror the Atomic Design architecture used by the runtime:

- `Atomic Design/Atoms/*` for generic primitives;
- `Atomic Design/Molecules/*` for small semantic components;
- `Atomic Design/Organisms/*` for feature-level compositions;
- `Atomic Design/Overview/*` for mixed galleries.

Templates and pages are normally tested through composed page/interaction scenarios rather than treated as standalone design-system components.

From `src/Battlegrounds.Game/webui`:

```bash
npm install
npm run storybook
```

Storybook runs at `http://localhost:6006` and loads the real factories/templates. The fixture theme is asset-free on purpose, so stories can run without the Godot asset bridge.

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

Add `*.stories.js` files under `webui/storybook/` and place the story title under the component's Atomic Design layer. Prefer rendering the real exported async factory through `.storybook/story-renderer.js` rather than duplicating component markup.

Use `play` assertions for contracts that matter to the runtime: accessible names, disabled state, `data-action`, inspection metadata and visible state markers.
