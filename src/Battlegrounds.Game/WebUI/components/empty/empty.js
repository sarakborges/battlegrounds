import { cloneTemplate, useStyle } from '../../core/template.js';

const templateUrl = new URL('./empty.html', import.meta.url);
useStyle(new URL('./empty.css', import.meta.url));

export async function createEmpty({ label = '' } = {}) {
  const element = await cloneTemplate(templateUrl);
  element.querySelector('[data-field="label"]').textContent = label;
  return element;
}
