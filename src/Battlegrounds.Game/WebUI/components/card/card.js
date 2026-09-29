import { addClasses, appendChildren, applyAttributes, cloneTemplate, themeRole, useStyle } from '../../core/template.js';

const templateUrl = new URL('./card.html', import.meta.url);
useStyle(new URL('./card.css', import.meta.url));

export async function createCard({ kind = '', name = '', meta = [], action = null, variant = 'default', themeRole: explicitRole = null, disabled = false, selected = false, className = '', attributes = {} } = {}) {
  const element = await cloneTemplate(templateUrl);
  element.querySelector('[data-field="kind"]').textContent = kind;
  element.querySelector('[data-field="name"]').textContent = name;
  appendChildren(element.querySelector('[data-slot="meta"]'), meta);
  element.dataset.variant = variant;
  element.dataset.themeRole = themeRole('card', variant, explicitRole);
  if (action) element.dataset.action = action;
  element.disabled = disabled;
  element.classList.toggle('is-selected', selected);
  addClasses(element, className);
  applyAttributes(element, attributes);
  return element;
}
