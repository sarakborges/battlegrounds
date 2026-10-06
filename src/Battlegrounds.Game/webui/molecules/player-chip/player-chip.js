import { cloneTemplate, useStyle } from '../../core/template.js';

const templateUrl = new URL('./player-chip.html', import.meta.url);
useStyle(new URL('./player-chip.css', import.meta.url));

export async function createPlayerChip({
  id = '',
  leaderId = '',
  leader = '—',
  leaderDescription = '',
  health = '—',
  portrait = null,
  human = false,
  eliminated = false
} = {}) {
  const element = await cloneTemplate(templateUrl);
  element.querySelector('[data-field="id"]').textContent = `${Number(id) + 1}`;
  element.querySelector('[data-field="health"]').textContent = health ?? '—';
  element.title = `${leader} · ${health ?? '—'} HP`;

  const image = element.querySelector('[data-field="portrait"]');
  if (portrait) {
    image.src = portrait;
    image.alt = leader;
  } else {
    image.hidden = true;
  }

  element.dataset.variant = human ? 'human' : 'default';
  element.dataset.eliminated = String(eliminated);
  if (leaderId) {
    element.dataset.inspectKind = 'leader';
    element.dataset.inspectId = leaderId;
    element.dataset.inspectName = leader;
    element.dataset.inspectPlacement = 'right';
    if (leaderDescription) element.dataset.inspectDescription = leaderDescription;
  }
  return element;
}
