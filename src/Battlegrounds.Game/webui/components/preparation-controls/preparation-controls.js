import { appendChildren, cloneTemplate, useStyle } from '../../core/template.js';
import { createButton } from '../../design-system/button/button.js';
import { createPreparationHost } from '../preparation-host/preparation-host.js';

const templateUrl = new URL('./preparation-controls.html', import.meta.url);
useStyle(new URL('../../design-system/panel/panel.css', import.meta.url));
useStyle(new URL('./preparation-controls.css', import.meta.url));

export async function createPreparationControls({
  labels = {},
  participant = {},
  blocked = false,
  hostName = 'Host',
  hostArt = null
} = {}) {
  const element = await cloneTemplate(templateUrl);
  const tierLabel = labels.tier ?? 'Tier';
  const currentTier = Math.max(1, Math.trunc(Number(participant.tier) || 1));

  const tierField = element.querySelector('[data-field="tier"]');
  tierField.setAttribute('role', 'img');
  tierField.setAttribute('aria-label', `${tierLabel}: ${currentTier}`);
  tierField.querySelectorAll('[data-tier]').forEach(icon => {
    icon.hidden = Number(icon.dataset.tier) !== currentTier;
  });

  const upgrade = await createButton({
    label: '',
    action: 'upgrade',
    disabled: blocked || participant.upgradeCost == null,
    themeRole: 'button.tierUpgrade',
    className: 'preparation-controls__button preparation-controls__button--upgrade',
    attributes: {
      title: labels.upgrade ?? 'Upgrade',
      'aria-label': labels.upgrade ?? 'Upgrade'
    }
  });

  const refresh = await createButton({
    label: '',
    action: 'refresh',
    disabled: blocked,
    themeRole: 'button.offerRefresh',
    className: 'preparation-controls__button preparation-controls__button--refresh',
    attributes: {
      title: labels.refresh ?? 'Refresh',
      'aria-label': labels.refresh ?? 'Refresh'
    }
  });

  const freezeLabel = participant.offerFrozen
    ? (labels.unfreeze ?? 'Unfreeze')
    : (labels.freeze ?? 'Freeze');
  const freeze = await createButton({
    label: '',
    action: 'toggle-freeze',
    disabled: blocked,
    themeRole: 'button.offerFreeze',
    className: 'preparation-controls__button preparation-controls__button--freeze',
    attributes: {
      title: freezeLabel,
      'aria-label': freezeLabel
    }
  });

  const upgradeCost = element.querySelector('[data-field="upgrade-cost"]');
  upgradeCost.textContent = participant.upgradeCost ?? '';
  upgradeCost.hidden = participant.upgradeCost == null;

  const refreshCost = element.querySelector('[data-field="refresh-cost"]');
  refreshCost.textContent = participant.refreshCost ?? '';
  refreshCost.hidden = participant.refreshCost == null;

  appendChildren(element.querySelector('[data-slot="upgrade"]'), [upgrade]);
  appendChildren(element.querySelector('[data-slot="refresh"]'), [refresh]);
  appendChildren(element.querySelector('[data-slot="freeze"]'), [freeze]);
  appendChildren(element.querySelector('[data-slot="host"]'), [
    await createPreparationHost({ name: hostName, art: hostArt })
  ]);

  return element;
}
