import { appendChildren, cloneTemplate, useStyle } from '../../core/template.js';
import { createBadge } from '../../components/badge/badge.js';
import { createCard } from '../../components/card/card.js';
import { leaderArtUrl, loadCosmetics } from '../../theme/cosmetics.js';

const templateUrl = new URL('./leader-selection.html', import.meta.url);
useStyle(new URL('./leader-selection.css', import.meta.url));

export async function createLeaderSelectionScreen(state) {
  const element = await cloneTemplate(templateUrl);
  const labels = state.labels ?? {};
  const modId = state.mod?.id;
  const cosmetics = await loadCosmetics(modId);
  element.querySelector('[data-field="mod-name"]').textContent = state.mod?.name ?? '';
  element.querySelector('[data-field="title"]').textContent = labels.chooseLeader ?? 'Choose a leader';
  element.querySelector('[data-field="seed"]').textContent = state.seed ?? '';

  const leaders = [];
  for (const leader of state.leaders ?? []) {
    const meta = [
      await createBadge({ text: `${labels.health ?? 'Health'} ${leader.healthModifier >= 0 ? '+' : ''}${leader.healthModifier}` }),
      await createBadge({ text: `${labels.armor ?? 'Armor'} ${leader.armor}` })
    ];
    leaders.push(await createCard({
      kind: labels.leader ?? 'Leader',
      name: leader.name,
      art: leaderArtUrl(modId, cosmetics, leader.id),
      artAlt: leader.name,
      meta,
      action: 'select-leader',
      variant: 'leader',
      attributes: { 'data-leader-id': leader.id }
    }));
  }

  appendChildren(element.querySelector('[data-slot="leaders"]'), leaders);
  return element;
}
