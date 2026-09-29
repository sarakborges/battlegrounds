import { appendChildren, cloneTemplate, useStyle } from '../../core/template.js';
import { createButton } from '../../design-system/button/button.js';
import { createCharacterPortrait } from '../character-portrait/character-portrait.js';

const templateUrl = new URL('./hero-cockpit.html', import.meta.url);
useStyle(new URL('../../design-system/panel/panel.css', import.meta.url));
useStyle(new URL('./hero-cockpit.css', import.meta.url));

function applyPowerInspection(element, power) {
  if (!power?.id) return;
  element.dataset.inspectKind = 'power';
  element.dataset.inspectId = power.id;
  element.dataset.inspectName = power.name ?? power.id;
  if (power.description) element.dataset.inspectDescription = power.description;
  if (power.cost != null) element.dataset.inspectCost = power.cost;
}

export async function createHeroCockpit({
  labels = {},
  human = {},
  power = null,
  heroId = '',
  heroName = '—',
  heroDescription = '',
  heroArt = null,
  blocked = false
} = {}) {
  const element = await cloneTemplate(templateUrl);
  element.querySelector('[data-field="health"]').textContent = human.health ?? '';
  element.querySelector('[data-field="armor"]').textContent = human.armor ?? '';
  element.querySelector('[data-field="resource-label"]').textContent = labels.resource ?? 'Resource';
  element.querySelector('[data-field="resource"]').textContent = human.resource ?? '';

  const portraitAttributes = {};
  if (heroId) {
    portraitAttributes['data-inspect-kind'] = 'leader';
    portraitAttributes['data-inspect-id'] = heroId;
    portraitAttributes['data-inspect-name'] = heroName;
    if (heroDescription) portraitAttributes['data-inspect-description'] = heroDescription;
  }

  const portrait = await createCharacterPortrait({
    name: heroName,
    art: heroArt,
    artAlt: heroName,
    hint: 'Drop a tavern token to buy',
    themeRole: 'panel.heroPortrait',
    dropKind: 'player-hero',
    className: 'hero-cockpit__portrait',
    attributes: portraitAttributes
  });

  const powerButton = await createButton({
    label: '✦',
    action: 'use-power',
    disabled: blocked || !power?.id || power.activatable === false,
    themeRole: 'button.tavernAction',
    className: 'hero-cockpit__power-button',
    attributes: { title: labels.usePower ?? 'Use power' }
  });

  const powerSlot = element.querySelector('[data-slot="power"]');
  applyPowerInspection(powerSlot, power);
  appendChildren(element.querySelector('[data-slot="portrait"]'), [portrait]);
  appendChildren(powerSlot, [powerButton]);
  return element;
}
