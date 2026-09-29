import { addClasses, cloneTemplate, useStyle } from '../../core/template.js';

const templateUrl = new URL('./combat-card.html', import.meta.url);
useStyle(new URL('./combat-card.css', import.meta.url));

export async function createCombatCard({ name = '', attack = '—', health = '—', tier = null, status = '', marker = '', highlight = '', instanceId = '' } = {}) {
  const element = await cloneTemplate(templateUrl);
  element.querySelector('[data-field="name"]').textContent = name;
  element.querySelector('[data-field="attack"]').textContent = attack ?? '—';
  element.querySelector('[data-field="health"]').textContent = health ?? '—';
  element.querySelector('[data-field="status"]').textContent = status;
  element.dataset.variant = highlight || 'default';
  element.dataset.instanceId = instanceId;
  addClasses(element, highlight);

  const markerElement = element.querySelector('[data-field="marker"]');
  markerElement.textContent = marker;
  markerElement.hidden = !marker;

  const tierElement = element.querySelector('[data-field="tier"]');
  tierElement.textContent = tier > 0 ? `T${tier}` : '';
  tierElement.hidden = !(tier > 0);
  return element;
}
