import { appendChildren, cloneTemplate, useStyle } from '../../core/template.js';
import { createCombatUnitToken } from '../../components/combat-unit-token/combat-unit-token.js';
import { createEmptyState } from '../../design-system/empty-state/empty-state.js';
import { cosmeticsForState, entityArtUrl } from '../../theme/cosmetics.js';

const templateUrl = new URL('./combat.html', import.meta.url);
useStyle(new URL('../../design-system/panel/panel.css', import.meta.url));
useStyle(new URL('./combat.css', import.meta.url));

let lastStepKey = '';

async function populateSide(element, side, sideName, cosmetics) {
  const sideElement = element.querySelector(`[data-side="${sideName}"]`);
  sideElement.classList.toggle('human', !!side?.human);
  sideElement.querySelector(`[data-field="${sideName}-label"]`).textContent = side?.label ?? `P${side?.playerId ?? '?'}`;
  sideElement.querySelector(`[data-field="${sideName}-archived"]`).hidden = !side?.archived;

  const tokens = [];
  for (const unit of side?.units ?? []) {
    tokens.push(await createCombatUnitToken({
      unit,
      art: await entityArtUrl(cosmetics, 'unit', unit.unitId),
      attackDirection: sideName === 'left' ? 'up' : 'down'
    }));
  }
  if (!tokens.length) tokens.push(await createEmptyState({ label: 'Empty field' }));
  appendChildren(sideElement.querySelector(`[data-slot="${sideName}-board"]`), tokens);
}

export async function createCombatScreen(state) {
  const element = await cloneTemplate(templateUrl);
  const combat = state.combat;
  if (!combat) return element;

  const stepKey = `${combat.round}:${combat.eventSequence}:${combat.settlementVisible}`;
  element.querySelector('.combat-shell').classList.toggle('combat-step-changed', stepKey !== lastStepKey);
  lastStepKey = stepKey;

  element.querySelector('[data-field="mod-name"]').textContent = state.mod?.name ?? 'Battlegrounds';
  element.querySelector('[data-field="combat-label"]').textContent = state.labels?.combat ?? 'Combat';
  element.querySelector('[data-field="round-label"]').textContent = state.labels?.round ?? 'Round';
  element.querySelector('[data-field="round"]').textContent = combat.round;
  element.querySelector('[data-field="progress"]').textContent = combat.settlementVisible ? 'Settlement' : `${combat.eventSequence > 0 ? combat.eventSequence : 0} / ${combat.eventCount}`;

  const eventContainer = element.querySelector('[data-field="event-container"]');
  eventContainer.classList.toggle('settlement', !!combat.settlementVisible);
  element.querySelector('[data-field="event-kind"]').textContent = combat.settlementVisible ? 'result' : (combat.eventKind ?? 'ready');
  element.querySelector('[data-field="event-text"]').textContent = combat.eventText ?? '';

  const cosmetics = cosmeticsForState(state);
  await populateSide(element, combat.right, 'right', cosmetics);
  await populateSide(element, combat.left, 'left', cosmetics);
  return element;
}

export function resetCombatScreen() {
  lastStepKey = '';
}
