import { addClasses, applyAttributes, cloneTemplate, useStyle } from '../../core/template.js';

const templateUrl = new URL('./character-portrait.html', import.meta.url);
useStyle(new URL('../panel/panel.css', import.meta.url));
useStyle(new URL('./character-portrait.css', import.meta.url));

export async function createCharacterPortrait({
  name = '',
  eyebrow = '',
  art = null,
  artAlt = '',
  hint = '',
  themeRole = 'panel',
  dropKind = null,
  className = '',
  attributes = {}
} = {}) {
  const element = await cloneTemplate(templateUrl);
  element.dataset.themeRole = themeRole;

  const nameElement = element.querySelector('[data-field="name"]');
  nameElement.textContent = name;

  const eyebrowElement = element.querySelector('[data-field="eyebrow"]');
  eyebrowElement.textContent = eyebrow;
  eyebrowElement.hidden = !eyebrow;

  const hintElement = element.querySelector('[data-field="hint"]');
  hintElement.textContent = hint;

  const image = element.querySelector('[data-field="art"]');
  if (art) {
    image.hidden = false;
    image.src = art;
    image.alt = artAlt;
    image.addEventListener('error', () => { image.hidden = true; }, { once: true });
  } else {
    image.hidden = true;
  }

  if (dropKind) {
    element.dataset.dropKind = dropKind;
    element.classList.add('drag-target');
  }

  addClasses(element, className);
  applyAttributes(element, attributes);
  return element;
}
