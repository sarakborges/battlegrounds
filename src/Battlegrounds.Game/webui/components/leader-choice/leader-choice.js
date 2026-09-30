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
  applyAttributes(element, {
    'data-action': 'select-leader',
    'data-leader-id': leader.id ?? '',
    ...inspectionAttributes({
      kind: 'leader',
      id: leader.id,
      name: leader.name ?? '',
      description: leader.description ?? ''
    }),
    'data-inspect-placement': 'top'
  });

  const portrait = await createCharacterPortrait({
    name: leader.name ?? '',
    art,
    artAlt: leader.name ?? '',
    showName: true,
    themeRole: 'panel.heroPortrait',
    className: 'leader-choice__portrait'
  });
  appendChildren(element.querySelector('[data-slot="portrait"]'), [portrait]);

  const armor = element.querySelector('[data-field="armor"]');
  armor.textContent = leader.armor ?? '';
  armor.hidden = !(leader.armor > 0);
  return element;
}
