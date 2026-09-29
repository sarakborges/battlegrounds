import { addClasses, appendChildren, cloneTemplate, useStyle } from '../../core/template.js';

const templateUrl = new URL('./row.html', import.meta.url);
useStyle(new URL('./row.css', import.meta.url));

export async function createRow({ children = [], variant = 'default', className = '' } = {}) {
  const element = await cloneTemplate(templateUrl);
  element.dataset.variant = variant;
  addClasses(element, className);
  appendChildren(element, children);
  return element;
}
