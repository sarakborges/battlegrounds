import { addClasses, applyAttributes, cloneTemplate, useStyle } from '../../core/template.js';
import { inspectionAttributes } from '../../core/inspection.js';

const templateUrl = new URL('./action-token.html', import.meta.url);
useStyle(new URL('./action-token.css', import.meta.url));

function setOptionalText(element, value) {
  if (value === undefined || value === null || value === '') {
    element.hidden = true;
    element.textContent = '';
    return;
  }
  element.hidden = false;
  element.textContent = value;
}

export async function createActionToken({
  id = '',
  name = '',
  description = '',
  art = null,
  tier = null,
  cost = null,
  action = null,
  disabled = false,
  className = '',
  attributes = {}
} = {}) {
  const element = await cloneTemplate(templateUrl);
  element.dataset.disabled = disabled ? 'true' : 'false';
  element.setAttribute('aria-disabled', disabled ? 'true' : 'false');
  element.setAttribute('aria-label', name || 'Action');

  const fallback = element.querySelector('[data-field="fallback"]');
  fallback.textContent = name.trim().slice(0, 1).toUpperCase() || '✦';
  setOptionalText(element.querySelector('[data-field="tier"]'), tier);
  setOptionalText(element.querySelector('[data-field="cost"]'), cost);

  const image = element.querySelector('[data-field="art"]');
  if (art) {
    fallback.hidden = true;
    image.hidden = false;
    image.src = art;
    image.alt = name;
    image.addEventListener('error', () => {
      image.hidden = true;
      fallback.hidden = false;
    }, { once: true });
  }

  if (action) element.dataset.action = action;
  addClasses(element, className);
  applyAttributes(element, inspectionAttributes({ kind: 'action', id, name, description, tier, cost }));
  applyAttributes(element, attributes);
  return element;
}
