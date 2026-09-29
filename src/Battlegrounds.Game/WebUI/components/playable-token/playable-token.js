import { addClasses, applyAttributes, cloneTemplate, useStyle } from '../../core/template.js';

const templateUrl = new URL('./playable-token.html', import.meta.url);
useStyle(new URL('./playable-token.css', import.meta.url));

function setOptionalText(element, value) {
  if (value === undefined || value === null || value === '') {
    element.hidden = true;
    element.textContent = '';
    return;
  }
  element.hidden = false;
  element.textContent = value;
}

function inspectAttributes({ kind, id, name, description, tier, attack, health, cost, frozen }) {
  const attributes = {
    'data-inspect-kind': kind,
    'data-inspect-id': id ?? '',
    'data-inspect-name': name ?? ''
  };
  if (description) attributes['data-inspect-description'] = description;
  if (tier != null) attributes['data-inspect-tier'] = tier;
  if (attack != null) attributes['data-inspect-attack'] = attack;
  if (health != null) attributes['data-inspect-health'] = health;
  if (cost != null) attributes['data-inspect-cost'] = cost;
  if (frozen) attributes['data-inspect-frozen'] = 'true';
  return attributes;
}

export async function createPlayableToken({
  kind = 'unit',
  id = '',
  name = '',
  description = '',
  art = null,
  artAlt = '',
  tier = null,
  attack = null,
  health = null,
  cost = null,
  frozen = false,
  action = null,
  location = 'field',
  disabled = false,
  selected = false,
  className = '',
  attributes = {}
} = {}) {
  const element = await cloneTemplate(templateUrl);
  element.dataset.variant = kind;
  element.dataset.location = location;
  element.dataset.disabled = disabled ? 'true' : 'false';
  element.setAttribute('aria-disabled', disabled ? 'true' : 'false');
  element.querySelector('[data-field="name"]').textContent = name;
  element.querySelector('[data-field="fallback"]').textContent = name.trim().slice(0, 1).toUpperCase() || '•';
  setOptionalText(element.querySelector('[data-field="tier"]'), tier == null ? null : `T${tier}`);
  setOptionalText(element.querySelector('[data-field="cost"]'), cost);
  setOptionalText(element.querySelector('[data-field="attack"]'), attack);
  setOptionalText(element.querySelector('[data-field="health"]'), health);
  element.querySelector('[data-field="frozen"]').hidden = !frozen;

  const image = element.querySelector('[data-field="art"]');
  if (art) {
    image.hidden = false;
    image.src = art;
    image.alt = artAlt || name;
    image.addEventListener('error', () => { image.hidden = true; }, { once: true });
  }

  if (action) element.dataset.action = action;
  element.classList.toggle('is-selected', selected);
  addClasses(element, className);
  applyAttributes(element, inspectAttributes({ kind, id, name, description, tier, attack, health, cost, frozen }));
  applyAttributes(element, attributes);
  return element;
}
