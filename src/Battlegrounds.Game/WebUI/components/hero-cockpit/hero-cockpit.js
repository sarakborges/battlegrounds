import { appendChildren, cloneTemplate, useStyle } from '../../core/template.js';
import { createButton } from '../button/button.js';
import { createCharacterPortrait } from '../character-portrait/character-portrait.js';

const templateUrl = new URL('./hero-cockpit.html', import.meta.url);
useStyle(new URL('../panel/panel.css', import.meta.url));
useStyle(new URL('./hero-cockpit.css', import.meta.url));

export async function createHeroCockpit({
  labels = {},
  human = {},
  heroName = '—',
  heroArt = null,
  blocked = false
} = {}) {
  const element = await cloneTemplate(templateUrl);
  element.querySelector('[data-field="health"]').textContent = human.health ?? '';
  element.querySelector('[data-field="armor"]').textContent = human.armor ?? '';
  element.querySelector('[data-field="resource-label"]').textContent = labels.resource ?? 'Resource';
  element.querySelector('[data-field="resource"]').textContent = human.resource ?? '';

  const portrait = await createCharacterPortrait({
    name: heroName,
    art: heroArt,
    artAlt: heroName,
    hint: 'Drop a tavern card to buy',
    themeRole: 'panel.heroPortrait',
    dropKind: 'hero',
    className: 'hero-cockpit__portrait'
  });

  const power = await createButton({
    label: '✦',
    action: 'use-power',
    disabled: blocked || !human.power,
    themeRole: 'button.tavernAction',
    className: 'hero-cockpit__power-button',
    attributes: { title: labels.usePower ?? 'Use power' }
  });

  appendChildren(element.querySelector('[data-slot="portrait"]'), [portrait]);
  appendChildren(element.querySelector('[data-slot="power"]'), [power]);
  return element;
}
