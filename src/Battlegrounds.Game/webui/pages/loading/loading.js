import { createLoadingTemplate } from '../../templates/loading/loading.js';

export async function createLoadingPage(state) {
  const element = await createLoadingTemplate();
  element.querySelector('[data-field="title"]').textContent = `Loading ${state?.mod?.name ?? 'mod'}…`;
  return element;
}
