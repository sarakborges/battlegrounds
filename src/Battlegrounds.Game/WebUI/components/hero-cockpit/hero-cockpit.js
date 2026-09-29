import { appendChildren, cloneTemplate, useStyle } from '../../core/template.js';
import { createCharacterPortrait } from '../character-portrait/character-portrait.js';
import { createHeroPowerButton } from '../hero-power-button/hero-power-button.js';

const templateUrl = new URL('./hero-cockpit.html', import.meta.url);
useStyle(new URL('../../design-system/panel/panel.css', import.meta.url));
useStyle(new URL('./hero-cockpit.css', import.meta.url));

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
    showName: false,
    themeRole: 'panel.heroPortrait',
    dropKind: 'player-hero',
    className: 'hero-cockpit__portrait',
    attributes: portraitAttributes
  });

  const powerControl = await createHeroPowerButton({
    heroPower: power,
    label: labels.usePower ?? 'Use power',
    blocked
  });

  appendChildren(element.querySelector('[data-slot="portrait"]'), [portrait]);
  appendChildren(element.querySelector('[data-slot="power"]'), [powerControl]);
  return element;
}
