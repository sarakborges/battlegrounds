import { applyComponentStyles, applyTheme } from '../theme/theme.js';
import { fixtureTheme } from './fixture-theme.js';

export function renderAsync(factory, args = {}, { wide = false, theme = fixtureTheme } = {}) {
  const stage = document.createElement('div');
  stage.className = `story-stage${wide ? ' story-stage--wide' : ''}`;
  stage.setAttribute('aria-busy', 'true');

  Promise.resolve()
    .then(() => applyTheme(theme, 'preparation'))
    .then(() => factory(args))
    .then(async element => {
      stage.replaceChildren(element);
      await applyComponentStyles(theme, stage);
      stage.setAttribute('aria-busy', 'false');
    })
    .catch(error => {
      stage.dataset.storyError = 'true';
      stage.setAttribute('aria-busy', 'false');
      stage.textContent = `Story render failed: ${error?.message ?? error}`;
      console.error(error);
    });

  return stage;
}
