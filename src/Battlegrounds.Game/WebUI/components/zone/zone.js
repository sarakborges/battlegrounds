import { appendChildren, cloneTemplate, useStyle } from '../../core/template.js';
import { createHorizontalStack } from '../../design-system/horizontal-stack/horizontal-stack.js';

const templateUrl = new URL('./zone.html', import.meta.url);
useStyle(new URL('../../design-system/panel/panel.css', import.meta.url));
useStyle(new URL('./zone.css', import.meta.url));

export async function createZone({ title = '', count = 0, children = [], variant = 'default', themeRole = 'panel' } = {}) {
  const element = await cloneTemplate(templateUrl);
  element.classList.add(`zone-${variant}`);
  element.dataset.variant = variant;
  element.dataset.themeRole = themeRole;
  element.querySelector('[data-field="title"]').textContent = title;
  element.querySelector('[data-field="count"]').textContent = count;
  appendChildren(element.querySelector('[data-slot="content"]'), [await createHorizontalStack({ children })]);
  return element;
}
