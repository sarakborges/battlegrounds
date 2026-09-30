import { addClasses, applyAttributes, cloneTemplate, useStyle } from '../../core/template.js';
import { inspectionAttributes } from '../../core/inspection.js';

const templateUrl = new URL('./unit-token.html', import.meta.url);
useStyle(new URL('./unit-token.css', import.meta.url));

function setOptionalText(element, value) {
  if (value === undefined || value === null || value === '') {
    element.hidden = true;
    element.textContent = '';
    return;
  }
  element.hidden = false;
  element.textContent = value;
}

export async function createUnitToken({
  id = '',
  name = '',
  description = '',
  art = null,
  tier = null,
  attack = null,
  health = null,
  frozen = false,
  action = null,
  disabled = false,
  selected = false,
  className = '',
  attributes = {}
} = {}) {
  const element = await cloneTemplate(templateUrl);
  element.dataset.disabled = disabled ? 'true' : 'false';
  element.setAttribute('aria-disabled', disabled ? 'true' : 'false');
  element.setAttribute('aria-label', name || 'Unit');

  const fallback = element.querySelector('[data-field="fallback"]');
  fallback.textContent = name.trim().slice(0, 1).toUpperCase() || '•';
  setOptionalText(element.querySelector('[data-field="tier"]'), tier);
  setOptionalText(element.querySelector('[data-field="attack"]'), attack);
  setOptionalText(element.querySelector('[data-field="health"]'), health);
  element.querySelector('[data-field="frozen"]').hidden = !frozen;

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
  element.classList.toggle('is-selected', selected);
  addClasses(element, className);
  applyAttributes(element, inspectionAttributes({
    kind: 'unit', id, name, description, tier, attack, health, frozen
  }));
  applyAttributes(element, attributes);
  return element;
}
