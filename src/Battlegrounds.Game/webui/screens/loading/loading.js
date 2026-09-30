import { cloneTemplate, useStyle } from '../../core/template.js';

const templateUrl = new URL('./loading.html', import.meta.url);
useStyle(new URL('../../components/panel/panel.css', import.meta.url));
useStyle(new URL('./loading.css', import.meta.url));

export async function createLoadingScreen(state) {
  const element = await cloneTemplate(templateUrl);
  element.querySelector('[data-field="title"]').textContent = `Loading ${state?.mod?.name ?? 'mod'}…`;
  return element;
}
