import { addClasses, cloneTemplate, useStyle } from '../../core/template.js';

const templateUrl = new URL('./badge.html', import.meta.url);
useStyle(new URL('./badge.css', import.meta.url));

export async function createBadge({ text = '', variant = 'default', className = '' } = {}) {
  const element = await cloneTemplate(templateUrl);
  element.textContent = text;
  element.dataset.variant = variant;
  addClasses(element, className);
  return element;
}
