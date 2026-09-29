import { cloneTemplate, useStyle } from '../../core/template.js';

const templateUrl = new URL('./card-preview.html', import.meta.url);
useStyle(new URL('./card-preview.css', import.meta.url));

function setOptionalText(element, value) {
  if (value === undefined || value === null || value === '') {
    element.hidden = true;
    element.textContent = '';
    return;
  }
  element.hidden = false;
  element.textContent = value;
}

export async function createCardPreview({
  kind = 'unit',
  name = '',
  description = '',
  art = null,
  tier = null,
  attack = null,
  health = null,
  cost = null,
  frozen = false
} = {}) {
  const element = await cloneTemplate(templateUrl);
  element.dataset.variant = kind;
  element.querySelector('[data-field="kind"]').textContent = kind;
  element.querySelector('[data-field="name"]').textContent = name;
  element.querySelector('[data-field="fallback"]').textContent = name.trim().slice(0, 1).toUpperCase() || '•';
  setOptionalText(element.querySelector('[data-field="description"]'), description);
  setOptionalText(element.querySelector('[data-field="tier"]'), tier == null ? null : `T${tier}`);
  setOptionalText(element.querySelector('[data-field="cost"]'), cost);
  setOptionalText(element.querySelector('[data-field="attack"]'), attack);
  setOptionalText(element.querySelector('[data-field="health"]'), health);
  setOptionalText(element.querySelector('[data-field="status"]'), frozen ? 'Frozen' : null);

  const image = element.querySelector('[data-field="art"]');
  if (art) {
    image.hidden = false;
    image.src = art;
    image.alt = name;
    image.addEventListener('error', () => { image.hidden = true; }, { once: true });
  }

  return element;
}
