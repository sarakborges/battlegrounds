import { createCombatScreen, resetCombatScreen } from './combat/combat.js';
import { createLeaderSelectionScreen } from './leader-selection/leader-selection.js';
import { createLoadingScreen } from './loading/loading.js';
import { createModSelectionScreen } from './mod-selection/mod-selection.js';
import { createPreparationScreen } from './preparation/preparation.js';

let previousStatus = null;

export async function createScreen(state) {
  if (previousStatus === 'combat' && state?.status !== 'combat') resetCombatScreen();
  previousStatus = state?.status ?? null;

  if (state?.status === 'mod-selection') return createModSelectionScreen(state);
  if (state?.status === 'leader-selection') return createLeaderSelectionScreen(state);
  if (state?.status === 'match' || state?.status === 'finished') return createPreparationScreen(state);
  if (state?.status === 'combat') return createCombatScreen(state);
  return createLoadingScreen(state);
}
