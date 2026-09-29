import { cloneTemplate, useStyle } from '../../core/template.js';

const templateUrl = new URL('./leader-inspector.html', import.meta.url);
useStyle(new URL('./leader-inspector.css', import.meta.url));

export async function createLeaderInspector({ name = '', description = '', art = null } = {}) {
  const element = await cloneTemplate(templateUrl);
  element.querySelector('[data-field="name"]').textContent = name;
  const descriptionElement = element.querySelector('[data-field="description"]');
  descriptionElement.textContent = description;
  descriptionElement.hidden = !description;

  const fallback = element.querySelector('[data-field="fallback"]');
  fallback.textContent = name.trim().slice(0, 1).toUpperCase() || '•';
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
