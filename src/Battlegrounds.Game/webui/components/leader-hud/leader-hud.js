import { appendChildren, cloneTemplate, useStyle } from '../../core/template.js';
import { createCharacterPortrait } from '../character-portrait/character-portrait.js';
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

  const portraitAttributes = {
    'data-drop-kind': 'player-leader',
    'data-inspect-kind': 'power',
    'data-inspect-id': power?.id ?? '',
    'data-inspect-name': power?.name ?? '',
    'data-inspect-description': power?.description ?? '',
    'data-inspect-cost': power?.cost ?? '',
    'data-inspect-placement': 'top'
  };

  const portrait = await createCharacterPortrait({
    name: leaderName,
    art: leaderArt,
    artAlt: leaderName,
    showName: false,
    themeRole: 'panel.leaderPortrait',
    className: 'leader-hud__portrait',
    attributes: portraitAttributes
  });
  appendChildren(element.querySelector('[data-slot="portrait"]'), [portrait]);

  appendChildren(element.querySelector('[data-slot="power"]'), [
    await createPowerButton({
      power,
      label: labels.usePower ?? 'Use power',
      blocked
    })
  ]);
  return element;
}
