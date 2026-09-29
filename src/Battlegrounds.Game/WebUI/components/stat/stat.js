import { cloneTemplate, useStyle } from '../../core/template.js';

const templateUrl = new URL('./stat.html', import.meta.url);
useStyle(new URL('./stat.css', import.meta.url));

export async function createStat({ label = '', value = '—' } = {}) {
  const element = await cloneTemplate(templateUrl);
  element.querySelector('[data-field="label"]').textContent = label;
  element.querySelector('[data-field="value"]').textContent = value ?? '—';
  return element;
}
