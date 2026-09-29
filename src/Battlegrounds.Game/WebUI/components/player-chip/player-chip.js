import { cloneTemplate, useStyle } from '../../core/template.js';

const templateUrl = new URL('./player-chip.html', import.meta.url);
useStyle(new URL('./player-chip.css', import.meta.url));

export async function createPlayerChip({ id = '', leader = '—', health = '—', portrait = null, human = false, eliminated = false } = {}) {
  const element = await cloneTemplate(templateUrl);
  element.querySelector('[data-field="id"]').textContent = `${Number(id) + 1}`;
  element.querySelector('[data-field="leader"]').textContent = leader;
  element.querySelector('[data-field="health"]').textContent = health ?? '—';

  const image = element.querySelector('[data-field="portrait"]');
  if (portrait) image.src = portrait;
  else image.hidden = true;

  element.dataset.variant = human ? 'human' : 'default';
  element.dataset.eliminated = String(eliminated);
  return element;
}
