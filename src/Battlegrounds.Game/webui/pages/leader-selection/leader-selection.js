import { appendChildren } from '../../core/template.js';
import { createLeaderChoice } from '../../organisms/leader-choice/leader-choice.js';
import { createLeaderSelectionTemplate } from '../../templates/leader-selection/leader-selection.js';
import { cosmeticsForState, leaderArtUrl } from '../../theme/cosmetics.js';

export async function createLeaderSelectionPage(state) {
  const element = await createLeaderSelectionTemplate();
  const labels = state.labels ?? {};
  const cosmetics = cosmeticsForState(state);
  element.querySelector('[data-field="mod-name"]').textContent = state.mod?.name ?? '';
  element.querySelector('[data-field="title"]').textContent = labels.chooseLeader ?? 'Choose a leader';

  const leaders = [];
  for (const leader of state.leaders ?? []) {
    leaders.push(await createLeaderChoice({
      leader: {
        ...leader,
        power: cosmetics?.leaderPowers?.[leader.id] ?? null
      },
      art: await leaderArtUrl(cosmetics, leader.id)
    }));
  }

  appendChildren(element.querySelector('[data-slot="leaders"]'), leaders);
  return element;
}
