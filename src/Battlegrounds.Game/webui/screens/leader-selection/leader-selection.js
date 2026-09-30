import { appendChildren, cloneTemplate, useStyle } from '../../core/template.js';
import { createLeaderChoice } from '../../components/leader-choice/leader-choice.js';
import { cosmeticsForState, leaderArtUrl } from '../../theme/cosmetics.js';

const templateUrl = new URL('./leader-selection.html', import.meta.url);
useStyle(new URL('./leader-selection.css', import.meta.url));

export async function createLeaderSelectionScreen(state) {
  const element = await cloneTemplate(templateUrl);
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
