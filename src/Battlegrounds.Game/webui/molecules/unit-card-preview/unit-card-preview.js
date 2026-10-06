import { cloneTemplate, useStyle } from '../../core/template.js';

const templateUrl = new URL('./unit-card-preview.html', import.meta.url);
useStyle(new URL('./unit-card-preview.css', import.meta.url));

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

export async function createUnitCardPreview({
  name = '', description = '', art = null, tier = null, attack = null, health = null, frozen = false
} = {}) {
  const element = await cloneTemplate(templateUrl);
  element.querySelector('[data-field="name"]').textContent = name;
  const fallback = element.querySelector('[data-field="fallback"]');
  fallback.textContent = name.trim().slice(0, 1).toUpperCase() || '';

  setTierIcon(element.querySelector('[data-field="tier"]'), tier);
  setOptionalText(element.querySelector('[data-field="description"]'), description);

  const attackField = element.querySelector('[data-field="attack"]');
  attackField.dataset.component = 'panel';
  attackField.dataset.themeRole = 'panel.attackBadge';
  setOptionalText(attackField, attack);

  const healthField = element.querySelector('[data-field="health"]');
  healthField.dataset.component = 'panel';
  healthField.dataset.themeRole = 'panel.healthBadge';
  setOptionalText(healthField, health);

  const status = element.querySelector('[data-field="status"]');
  status.dataset.component = 'icon';
  status.dataset.themeRole = 'icon.frozenOverlay';
  status.textContent = '';
  status.hidden = !frozen;

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
  return element;
}
