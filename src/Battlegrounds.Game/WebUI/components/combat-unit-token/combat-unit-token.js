import { appendChildren, cloneTemplate, useStyle } from '../../core/template.js';
import { createPlayableToken } from '../playable-token/playable-token.js';

const templateUrl = new URL('./combat-unit-token.html', import.meta.url);
useStyle(new URL('./combat-unit-token.css', import.meta.url));

function highlightRole(marker) {
  switch (marker) {
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

export async function createCombatUnitToken({
  unit = {},
  art = null,
  attackDirection = 'down'
} = {}) {
  const element = await cloneTemplate(templateUrl);
  const marker = unit.highlight ?? '';
  const status = unit.status ?? '';
  const highlight = highlightRole(marker);

  if (highlight) element.dataset.highlight = highlight;
  element.dataset.attackDirection = attackDirection;
  element.dataset.alive = String(unit.alive !== false);

  const token = await createPlayableToken({
    kind: 'unit',
    id: unit.unitId ?? '',
    name: unit.name ?? '',
    description: unit.description ?? '',
    art,
    artAlt: unit.name ?? '',
    tier: unit.tier,
    attack: unit.attack,
    health: unit.health,
    location: 'combat',
    attributes: {
      'data-unit-instance-id': unit.instanceId ?? ''
    }
  });

  const markerElement = element.querySelector('[data-field="marker"]');
  markerElement.textContent = marker;
  markerElement.hidden = !marker;

  const statusElement = element.querySelector('[data-field="status"]');
  statusElement.textContent = status;
  statusElement.hidden = !status;

  appendChildren(element.querySelector('[data-slot="token"]'), [token]);
  return element;
}
