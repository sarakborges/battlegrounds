import { createCombatPage, resetCombatPage } from './combat/combat.js';
import { createLeaderSelectionPage } from './leader-selection/leader-selection.js';
import { createLoadingPage } from './loading/loading.js';
import { createModSelectionPage } from './mod-selection/mod-selection.js';
import { createPreparationPage } from './preparation/preparation.js';

let previousStatus = null;

export async function createPage(state) {
  if (previousStatus === 'combat' && state?.status !== 'combat') resetCombatPage();
  previousStatus = state?.status ?? null;

  if (state?.status === 'mod-selection') return createModSelectionPage(state);
  if (state?.status === 'leader-selection') return createLeaderSelectionPage(state);
  if (state?.status === 'match' || state?.status === 'finished') return createPreparationPage(state);
  if (state?.status === 'combat') return createCombatPage(state);
  return createLoadingPage(state);
}
