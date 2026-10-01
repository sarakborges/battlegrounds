import { appendChildren, applyAttributes, cloneTemplate, useStyle } from '../../core/template.js';
import { createCharacterPortrait } from '../character-portrait/character-portrait.js';

const templateUrl = new URL('./preparation-host.html', import.meta.url);
useStyle(new URL('../../design-system/panel/panel.css', import.meta.url));
useStyle(new URL('./preparation-host.css', import.meta.url));

export async function createPreparationHost({ name = 'Host', art = null } = {}) {
  const element = await cloneTemplate(templateUrl);
  applyAttributes(element, {
    'data-drop-kind': 'preparation-host'
  });
  element.classList.add('drag-target');

  const portrait = await createCharacterPortrait({
    name,
    art,
    artAlt: name,
    showName: false,
    themeRole: 'panel.preparationHost',
    className: 'preparation-host__portrait',
    attributes: {
      title: name,
      'aria-label': name,
      'data-drop-kind': 'preparation-host'
    }
  });
  appendChildren(element.querySelector('[data-slot="portrait"]'), [portrait]);
  return element;
}
