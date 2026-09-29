import { appendChildren, cloneTemplate, useStyle } from '../../core/template.js';
import { createBadge } from '../../components/badge/badge.js';
import { createCard } from '../../components/card/card.js';
import { cosmeticsForState, leaderArtUrl } from '../../theme/cosmetics.js';

const templateUrl = new URL('./leader-selection.html', import.meta.url);
useStyle(new URL('./leader-selection.css', import.meta.url));

export async function createLeaderSelectionScreen(state) {
  const element = await cloneTemplate(templateUrl);
  const labels = state.labels ?? {};
  const cosmetics = cosmeticsForState(state);
  element.querySelector('[data-field="mod-name"]').textContent = state.mod?.name ?? '';
  element.querySelector('[data-field="title"]').textContent = labels.chooseLeader ?? 'Choose a leader';
  element.querySelector('[data-field="seed"]').textContent = state.seed ?? '';

  const leaders = [];
  for (const leader of state.leaders ?? []) {
    const meta = [];
    if ((leader.armor ?? 0) > 0) {
      meta.push(await createBadge({ text: leader.armor, variant: 'armor', className: 'leader-armor-token' }));
    }

    leaders.push(await createCard({
      kind: labels.leader ?? 'Leader',
      name: leader.name,
      art: await leaderArtUrl(cosmetics, leader.id),
      artAlt: leader.name,
      meta,
      action: 'select-leader',
      variant: 'leader',
      className: 'leader-card',
      attributes: { 'data-leader-id': leader.id }
    }));
  }

  appendChildren(element.querySelector('[data-slot="leaders"]'), leaders);
  return element;
}
