import { appendChildren, cloneTemplate, useStyle } from '../../core/template.js';

const templateUrl = new URL('./dialog.html', import.meta.url);
useStyle(new URL('../panel/panel.css', import.meta.url));
useStyle(new URL('./dialog.css', import.meta.url));

export async function createDialog({ eyebrow = '', title = '', body = [], actions = [], variant = 'default' } = {}) {
  const element = await cloneTemplate(templateUrl);
  element.dataset.variant = variant;
  element.querySelector('[data-field="eyebrow"]').textContent = eyebrow;
  element.querySelector('[data-field="title"]').textContent = title;
  appendChildren(element.querySelector('[data-slot="header-actions"]'), actions);
  appendChildren(element.querySelector('[data-slot="body"]'), body);
  return element;
}
