import { addClasses, appendChildren, applyAttributes, cloneTemplate, themeRole, useStyle } from '../../core/template.js';

const templateUrl = new URL('./card.html', import.meta.url);
useStyle(new URL('./card.css', import.meta.url));

export async function createCard({ kind = '', name = '', art = null, artAlt = '', badges = [], action = null, variant = 'default', themeRole: explicitRole = null, disabled = false, selected = false, className = '', attributes = {} } = {}) {
  const element = await cloneTemplate(templateUrl);
  element.querySelector('[data-field="kind"]').textContent = kind;
  element.querySelector('[data-field="name"]').textContent = name;

  const image = element.querySelector('[data-field="art"]');
  if (art) {
    image.hidden = false;
    image.src = art;
    image.alt = artAlt;
    image.addEventListener('error', () => { image.hidden = true; }, { once: true });
  }

  appendChildren(element.querySelector('[data-slot="badges"]'), badges);
  element.dataset.variant = variant;
  element.dataset.themeRole = themeRole('card', variant, explicitRole);
  if (action) element.dataset.action = action;
  element.disabled = disabled;
  element.classList.toggle('is-selected', selected);
  addClasses(element, className);
  applyAttributes(element, attributes);
  return element;
}
