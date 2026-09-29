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

export async function createUnitCardPreview({
  name = '', description = '', art = null, tier = null, attack = null, health = null, frozen = false
} = {}) {
  const element = await cloneTemplate(templateUrl);
  element.querySelector('[data-field="name"]').textContent = name;
  const fallback = element.querySelector('[data-field="fallback"]');
  fallback.textContent = name.trim().slice(0, 1).toUpperCase() || '•';
  setOptionalText(element.querySelector('[data-field="tier"]'), tier);
  setOptionalText(element.querySelector('[data-field="description"]'), description);
  setOptionalText(element.querySelector('[data-field="attack"]'), attack);
  setOptionalText(element.querySelector('[data-field="health"]'), health);
  setOptionalText(element.querySelector('[data-field="status"]'), frozen ? 'Frozen' : null);

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
