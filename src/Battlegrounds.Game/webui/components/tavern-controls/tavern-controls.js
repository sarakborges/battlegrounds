import { appendChildren, cloneTemplate, useStyle } from '../../core/template.js';
import { createButton } from '../../design-system/button/button.js';
import { createShopkeeper } from '../shopkeeper/shopkeeper.js';

const templateUrl = new URL('./tavern-controls.html', import.meta.url);
useStyle(new URL('../../design-system/panel/panel.css', import.meta.url));
useStyle(new URL('./tavern-controls.css', import.meta.url));

export async function createTavernControls({
  labels = {},
  human = {},
  blocked = false,
  shopkeeperName = 'Shopkeeper',
  shopkeeperArt = null
} = {}) {
  const element = await cloneTemplate(templateUrl);
  element.querySelector('[data-field="tier-label"]').textContent = labels.tier ?? 'Tier';
  element.querySelector('[data-field="tier"]').textContent = human.tier ?? '';

  const upgrade = await createButton({
    label: `★ ${human.upgradeCost ?? '—'}`,
    action: 'upgrade',
    disabled: blocked || human.upgradeCost == null,
    themeRole: 'button.tavernUpgrade',
    className: 'tavern-controls__button tavern-controls__button--upgrade',
    attributes: { title: labels.upgrade ?? 'Upgrade' }
  });

  const refresh = await createButton({
    label: '↻',
    action: 'refresh',
    disabled: blocked,
    themeRole: 'button.tavernRefresh',
    className: 'tavern-controls__button',
    attributes: { title: labels.refresh ?? 'Refresh' }
  });

  const freeze = await createButton({
    label: '❄',
    action: 'toggle-freeze',
    disabled: blocked,
    themeRole: 'button.tavernFreeze',
    className: 'tavern-controls__button',
    attributes: {
      title: human.offerFrozen
        ? (labels.unfreeze ?? 'Unfreeze')
        : (labels.freeze ?? 'Freeze')
    }
  });

  appendChildren(element.querySelector('[data-slot="upgrade"]'), [upgrade]);
  appendChildren(element.querySelector('[data-slot="refresh"]'), [refresh]);
  appendChildren(element.querySelector('[data-slot="freeze"]'), [freeze]);
  appendChildren(element.querySelector('[data-slot="shopkeeper"]'), [
    await createShopkeeper({ name: shopkeeperName, art: shopkeeperArt })
  ]);

  return element;
}
