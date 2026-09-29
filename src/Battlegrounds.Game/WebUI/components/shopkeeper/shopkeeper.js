import { appendChildren, cloneTemplate, useStyle } from '../../core/template.js';
import { createCharacterPortrait } from '../character-portrait/character-portrait.js';

const templateUrl = new URL('./shopkeeper.html', import.meta.url);
useStyle(new URL('./shopkeeper.css', import.meta.url));

export async function createShopkeeper({ name = 'Shopkeeper', art = null } = {}) {
  const element = await cloneTemplate(templateUrl);
  const portrait = await createCharacterPortrait({
    name,
    art,
    artAlt: name,
    hint: 'Drop a board unit to sell',
    themeRole: 'panel.shopkeeper',
    dropKind: 'tavern-shopkeeper',
    className: 'shopkeeper__portrait'
  });
  appendChildren(element.querySelector('[data-slot="portrait"]'), [portrait]);
  return element;
}
