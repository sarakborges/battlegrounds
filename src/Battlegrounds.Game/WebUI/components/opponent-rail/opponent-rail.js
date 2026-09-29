import { appendChildren, cloneTemplate, useStyle } from '../../core/template.js';
import { createPlayerChip } from '../player-chip/player-chip.js';

const templateUrl = new URL('./opponent-rail.html', import.meta.url);
useStyle(new URL('../../design-system/panel/panel.css', import.meta.url));
useStyle(new URL('./opponent-rail.css', import.meta.url));

export async function createOpponentRail({
  modName = '',
  roundLabel = 'Round',
  round = '',
  players = []
} = {}) {
  const element = await cloneTemplate(templateUrl);
  element.querySelector('[data-field="mod-name"]').textContent = modName;
  element.querySelector('[data-field="round-label"]').textContent = roundLabel;
  element.querySelector('[data-field="round"]').textContent = round;

  const chips = [];
  for (const player of players) {
    chips.push(await createPlayerChip(player));
  }
  appendChildren(element.querySelector('[data-slot="players"]'), chips);
  return element;
}
