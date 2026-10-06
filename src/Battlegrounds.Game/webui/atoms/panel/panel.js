import { addClasses, appendChildren, applyAttributes, cloneTemplate, themeRole, useStyle } from '../../core/template.js';

const templateUrl = new URL('./panel.html', import.meta.url);
useStyle(new URL('./panel.css', import.meta.url));

export async function createPanel({ children = [], className = '', variant = 'default', themeRole: explicitRole = null, attributes = {} } = {}) {
  const element = await cloneTemplate(templateUrl);
  element.dataset.variant = variant;
  element.dataset.themeRole = themeRole('panel', variant, explicitRole);
  appendChildren(element.querySelector('[data-slot="content"]'), children);
  addClasses(element, className);
  applyAttributes(element, attributes);
  return element;
}
