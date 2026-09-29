import { appendChildren, cloneTemplate, useStyle } from '../../core/template.js';
import { createBadge } from '../../design-system/badge/badge.js';
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
    const badges = [];
    if ((leader.armor ?? 0) > 0) {
      badges.push(await createBadge({
        text: leader.armor,
        variant: 'armor',
        className: 'leader-armor-token'
      }));
    }

    const attributes = {
      'data-leader-id': leader.id,
      'data-inspect-kind': 'leader',
      'data-inspect-id': leader.id,
      'data-inspect-name': leader.name
    };
    if (leader.description) attributes['data-inspect-description'] = leader.description;

    leaders.push(await createCard({
      kind: labels.leader ?? 'Leader',
      name: leader.name,
      art: await leaderArtUrl(cosmetics, leader.id),
      artAlt: leader.name,
      badges,
      action: 'select-leader',
      variant: 'leader',
      className: 'leader-card',
      attributes
    }));
  }

  appendChildren(element.querySelector('[data-slot="leaders"]'), leaders);
  return element;
}
