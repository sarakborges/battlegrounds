import { appendChildren, applyAttributes, cloneTemplate, useStyle } from '../../core/template.js';
import { inspectionAttributes } from '../../core/inspection.js';
import { createCharacterPortrait } from '../character-portrait/character-portrait.js';

const templateUrl = new URL('./combat-player-hero.html', import.meta.url);
useStyle(new URL('./combat-player-hero.css', import.meta.url));

export async function createCombatPlayerHero({
  side = {},
  art = null,
  placement = 'bottom'
} = {}) {
  const element = await cloneTemplate(templateUrl);
  element.dataset.placement = placement;
  element.querySelector('[data-field="label"]').textContent = side.label ?? '';
  element.querySelector('[data-field="health"]').textContent = side.health ?? '—';

  const armor = element.querySelector('[data-field="armor"]');
  armor.textContent = side.armor ?? '';
  armor.hidden = !side.armor;

  const attributes = inspectionAttributes({
    kind: 'leader',
    id: side.leaderId,
    name: side.leader ?? side.label ?? '',
    description: side.leaderDescription ?? ''
  });
  attributes['data-inspect-placement'] = placement === 'bottom' ? 'top' : 'bottom';

  const portrait = await createCharacterPortrait({
    name: side.leader ?? side.label ?? '',
    art,
    artAlt: side.leader ?? '',
    showName: false,
    themeRole: 'panel.heroPortrait',
    className: 'combat-player-hero__portrait-piece',
    attributes
  });

  applyAttributes(element, {
    'data-player-id': side.playerId ?? '',
    'data-human': side.human ? 'true' : 'false'
  });
  appendChildren(element.querySelector('[data-slot="portrait"]'), [portrait]);
  return element;
}
