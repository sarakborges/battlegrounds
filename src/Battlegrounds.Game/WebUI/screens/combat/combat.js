import { appendChildren, cloneTemplate, useStyle } from '../../core/template.js';
import { createCombatCard } from '../../components/combat-card/combat-card.js';
import { createEmpty } from '../../components/empty/empty.js';

const templateUrl = new URL('./combat.html', import.meta.url);
useStyle(new URL('../../components/panel/panel.css', import.meta.url));
useStyle(new URL('./combat.css', import.meta.url));

let lastStepKey = '';

function highlightClass(highlight) {
  switch (highlight) {
    case '→': return 'attacker';
    case '◎': return 'target';
    case '+': return 'summoned';
    case 'Δ': return 'stats';
    case '−': return 'damaged';
    case '×': return 'destroyed';
    case '↻': return 'revived';
    case '◆': return 'triggered';
    case '◇': return 'behavior';
    default: return '';
  }
}

async function populateSide(element, side, sideName) {
  const sideElement = element.querySelector(`[data-side="${sideName}"]`);
  sideElement.classList.toggle('human', !!side?.human);
  sideElement.querySelector(`[data-field="${sideName}-label"]`).textContent = side?.label ?? `P${side?.playerId ?? '?'}`;
  sideElement.querySelector(`[data-field="${sideName}-archived"]`).hidden = !side?.archived;

  const units = [];
  for (const unit of side?.units ?? []) {
    units.push(await createCombatCard({ name: unit.name, attack: unit.attack, health: unit.health, tier: unit.tier, status: unit.status, marker: unit.highlight, highlight: highlightClass(unit.highlight), instanceId: unit.instanceId }));
  }
  if (!units.length) units.push(await createEmpty({ label: 'Empty field' }));
  appendChildren(sideElement.querySelector(`[data-slot="${sideName}-board"]`), units);
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

  await populateSide(element, combat.right, 'right');
  await populateSide(element, combat.left, 'left');
  return element;
}

export function resetCombatScreen() {
  lastStepKey = '';
}
