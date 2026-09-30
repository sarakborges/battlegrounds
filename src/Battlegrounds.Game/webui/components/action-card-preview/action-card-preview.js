import { cloneTemplate, useStyle } from '../../core/template.js';

const templateUrl = new URL('./action-card-preview.html', import.meta.url);
useStyle(new URL('./action-card-preview.css', import.meta.url));

function setOptionalText(element, value) {
  if (value === undefined || value === null || value === '') {
    element.hidden = true;
    element.textContent = '';
    return;
  }
  element.hidden = false;
  element.textContent = value;
}

export async function createActionCardPreview({ name = '', description = '', art = null, tier = null, cost = null } = {}) {
  const element = await cloneTemplate(templateUrl);
  element.querySelector('[data-field="name"]').textContent = name;
  const fallback = element.querySelector('[data-field="fallback"]');
  fallback.textContent = name.trim().slice(0, 1).toUpperCase() || '✦';
  setOptionalText(element.querySelector('[data-field="tier"]'), tier);
  setOptionalText(element.querySelector('[data-field="cost"]'), cost);
  setOptionalText(element.querySelector('[data-field="description"]'), description);

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
