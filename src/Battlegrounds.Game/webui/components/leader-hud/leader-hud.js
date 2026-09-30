import { appendChildren, applyAttributes, cloneTemplate, useStyle } from '../../core/template.js';
import { createPowerButton } from '../power-button/power-button.js';

const templateUrl = new URL('./leader-hud.html', import.meta.url);
useStyle(new URL('../../design-system/panel/panel.css', import.meta.url));
useStyle(new URL('./leader-hud.css', import.meta.url));

export async function createLeaderHud({
  labels = {},
  participant = {},
  power = null,
  leaderId = '',
  leaderName = '—',
  leaderDescription = '',
  leaderArt = null,
  blocked = false
} = {}) {
  const element = await cloneTemplate(templateUrl);
  element.querySelector('[data-field="health"]').textContent = participant.health ?? '';
  element.querySelector('[data-field="armor"]').textContent = participant.armor ?? '';

  const portrait = element.querySelector('.leader-hud__portrait');
  if (leaderId) {
    applyAttributes(portrait, {
      'data-inspect-kind': 'leader',
      'data-inspect-id': leaderId,
      'data-inspect-name': leaderName,
      'data-inspect-placement': 'top',
      'data-drop-kind': 'player-leader'
    });
    if (leaderDescription) portrait.dataset.inspectDescription = leaderDescription;
  } else {
    portrait.dataset.dropKind = 'player-leader';
  }
  portrait.classList.add('drag-target');

  const image = element.querySelector('[data-field="art"]');
  const fallback = element.querySelector('[data-field="fallback"]');
  if (leaderArt) {
    image.src = leaderArt;
    image.alt = leaderName;
    image.hidden = false;
    fallback.hidden = true;
    image.addEventListener('error', () => {
      image.hidden = true;
      fallback.hidden = false;
    }, { once: true });
  }

  appendChildren(element.querySelector('[data-slot="power"]'), [
    await createPowerButton({
      power,
      label: labels.usePower ?? 'Use power',
      blocked
    })
  ]);
  return element;
}
