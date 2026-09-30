import { addClasses, applyAttributes, cloneTemplate, themeRole, useStyle } from '../../core/template.js';

const templateUrl = new URL('./button.html', import.meta.url);
useStyle(new URL('./button.css', import.meta.url));

export async function createButton({ label = '', action = null, variant = 'default', themeRole: explicitRole = null, disabled = false, className = '', attributes = {} } = {}) {
  const element = await cloneTemplate(templateUrl);
  element.querySelector('[data-field="label"]').textContent = label;
  element.dataset.variant = variant;
  element.dataset.themeRole = themeRole('button', variant, explicitRole);
  if (action) element.dataset.action = action;
  element.disabled = disabled;
  addClasses(element, className);
  applyAttributes(element, attributes);
  return element;
}
