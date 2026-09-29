import { createCombatScreen, resetCombatScreen } from './combat/combat.js';
import { createLeaderSelectionScreen } from './leader-selection/leader-selection.js';
import { createLoadingScreen } from './loading/loading.js';
import { createPreparationScreen } from './preparation/preparation.js';

let previousStatus = null;

export async function createScreen(state) {
  if (previousStatus === 'combat' && state?.status !== 'combat') resetCombatScreen();
  previousStatus = state?.status ?? null;

  if (state?.status === 'leader-selection') return createLeaderSelectionScreen(state);
  if (state?.status === 'match' || state?.status === 'finished') return createPreparationScreen(state);
  if (state?.status === 'combat') return createCombatScreen(state);
  return createLoadingScreen(state);
}
