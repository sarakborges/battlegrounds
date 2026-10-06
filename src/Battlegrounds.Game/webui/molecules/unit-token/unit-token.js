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

function setTierIcon(element, tier) {
  const value = Math.trunc(Number(tier));
  if (!Number.isFinite(value) || value <= 0) {
    element.hidden = true;
    return;
  }
  element.hidden = false;
  element.textContent = '';
  element.dataset.component = 'icon';
  element.dataset.themeRole = `icon.tier.${value}`;
  element.setAttribute('aria-hidden', 'true');
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
  fallback.textContent = name.trim().slice(0, 1).toUpperCase() || '';

  setTierIcon(element.querySelector('[data-field="tier"]'), tier);

  const attackField = element.querySelector('[data-field="attack"]');
  attackField.dataset.component = 'panel';
  attackField.dataset.themeRole = 'panel.attackBadge';
  setOptionalText(attackField, attack);

  const healthField = element.querySelector('[data-field="health"]');
  healthField.dataset.component = 'panel';
  healthField.dataset.themeRole = 'panel.healthBadge';
  setOptionalText(healthField, health);

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
