import { applyAttributes, cloneTemplate, useStyle } from '../../core/template.js';

const templateUrl = new URL('./preparation-host.html', import.meta.url);
useStyle(new URL('../../design-system/panel/panel.css', import.meta.url));
useStyle(new URL('./preparation-host.css', import.meta.url));

export async function createPreparationHost({ name = 'Host', art = null } = {}) {
  const element = await cloneTemplate(templateUrl);
  applyAttributes(element, {
    title: name,
    'aria-label': name,
    'data-drop-kind': 'preparation-host'
  });
  element.classList.add('drag-target');

  const image = element.querySelector('[data-field="art"]');
  const fallback = element.querySelector('[data-field="fallback"]');
  if (art) {
    image.src = art;
    image.alt = name;
    image.hidden = false;
    fallback.hidden = true;
    image.addEventListener('error', () => {
      image.hidden = true;
      fallback.hidden = false;
    }, { once: true });
  }

  return element;
}
