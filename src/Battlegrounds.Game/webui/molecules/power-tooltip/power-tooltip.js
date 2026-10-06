import { cloneTemplate, useStyle } from '../../core/template.js';

const templateUrl = new URL('./power-tooltip.html', import.meta.url);
useStyle(new URL('./power-tooltip.css', import.meta.url));

export async function createPowerTooltip({ name = '', description = '', cost = null } = {}) {
  const element = await cloneTemplate(templateUrl);
  element.querySelector('[data-field="name"]').textContent = name;
  const descriptionElement = element.querySelector('[data-field="description"]');
  descriptionElement.textContent = description;
  descriptionElement.hidden = !description;
  const costElement = element.querySelector('[data-field="cost"]');
  costElement.textContent = cost ?? '';
  costElement.hidden = cost == null;
  return element;
}
