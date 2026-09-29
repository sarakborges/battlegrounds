import { cloneTemplate, useStyle } from '../../core/template.js';

const templateUrl = new URL('./resource-counter.html', import.meta.url);
useStyle(new URL('../../design-system/panel/panel.css', import.meta.url));
useStyle(new URL('./resource-counter.css', import.meta.url));

export async function createResourceCounter({ value = '', label = 'Resource' } = {}) {
  const element = await cloneTemplate(templateUrl);
  element.querySelector('[data-field="value"]').textContent = value ?? '';
  element.setAttribute('aria-label', `${label}: ${value ?? ''}`);
  element.title = `${label}: ${value ?? ''}`;
  return element;
}
