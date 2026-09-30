import { cloneTemplate, useStyle } from '../../core/template.js';

const templateUrl = new URL('./empty-state.html', import.meta.url);
useStyle(new URL('./empty-state.css', import.meta.url));

export async function createEmptyState({ label = '' } = {}) {
  const element = await cloneTemplate(templateUrl);
  element.querySelector('[data-field="label"]').textContent = label;
  return element;
}
