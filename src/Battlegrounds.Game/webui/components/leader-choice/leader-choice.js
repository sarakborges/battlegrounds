import { appendChildren, applyAttributes, cloneTemplate, useStyle } from '../../core/template.js';
import { inspectionAttributes } from '../../core/inspection.js';
import { createCharacterPortrait } from '../character-portrait/character-portrait.js';

const templateUrl = new URL('./leader-choice.html', import.meta.url);
useStyle(new URL('./leader-choice.css', import.meta.url));

export async function createLeaderChoice({
  leader = {},
  art = null
} = {}) {
  const element = await cloneTemplate(templateUrl);
  const power = leader.power ?? {};
  applyAttributes(element, {
    'data-action': 'select-leader',
    'data-leader-id': leader.id ?? '',
    ...inspectionAttributes({
      kind: 'power',
      id: power.id,
      name: power.name ?? '',
      description: power.description ?? '',
      cost: power.cost
    }),
    'data-inspect-placement': 'top'
  });

  const portrait = await createCharacterPortrait({
    name: leader.name ?? '',
    art,
    artAlt: leader.name ?? '',
    showName: true,
    themeRole: 'panel.leaderChoicePortrait',
    className: 'leader-choice__portrait'
  });
  appendChildren(element.querySelector('[data-slot="portrait"]'), [portrait]);

  const armor = element.querySelector('[data-field="armor"]');
  applyAttributes(armor, {
    'data-component': 'panel',
    'data-theme-role': 'panel.armorBadge'
  });
  armor.textContent = leader.armor ?? '';
  armor.hidden = !(leader.armor > 0);
  return element;
}
