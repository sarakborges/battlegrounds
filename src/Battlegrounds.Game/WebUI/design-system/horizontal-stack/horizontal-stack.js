import { addClasses, appendChildren, cloneTemplate, useStyle } from '../../core/template.js';

const templateUrl = new URL('./horizontal-stack.html', import.meta.url);
useStyle(new URL('./horizontal-stack.css', import.meta.url));

export async function createHorizontalStack({ children = [], variant = 'default', className = '' } = {}) {
  const element = await cloneTemplate(templateUrl);
  element.dataset.variant = variant;
  addClasses(element, className);
  appendChildren(element, children);
  return element;
}
