import { expect } from 'storybook/test';
import { createActionToken } from '../components/action-token/action-token.js';
import { createPowerButton } from '../components/power-button/power-button.js';
import { createResourceCounter } from '../components/resource-counter/resource-counter.js';
import { createUnitToken } from '../components/unit-token/unit-token.js';
import { createBadge } from '../design-system/badge/badge.js';
import { createButton } from '../design-system/button/button.js';
import { renderAsync } from '../.storybook/story-renderer.js';

export default {
  title: 'Storybook/Component Gallery',
  tags: ['autodocs', 'test']
};

export const MixedStates = {
  render: () => renderAsync(async () => {
    const gallery = document.createElement('div');
    gallery.className = 'story-gallery';

    const items = await Promise.all([
      createButton({ label: 'Buy', variant: 'primary' }),
      createButton({ label: 'Upgrade', disabled: true }),
      createBadge({ text: 'Tier 5' }),
      createResourceCounter({ value: 8, maximum: 10, label: 'Gold' }),
      createUnitToken({ id: 'gallery-unit', name: 'Iron Sentry', tier: 5, attack: 11, health: 14 }),
      createActionToken({ id: 'gallery-action', name: 'Quick Study', tier: 2, cost: 1 }),
      createPowerButton({
        label: 'Use hero power',
        power: { id: 'gallery-power', name: 'Tune Up', description: 'Buff a unit.', cost: 1, activatable: true }
      })
    ]);

    gallery.append(...items);
    return gallery;
  }, {}, { wide: true }),
  play: async ({ canvas }) => {
    await expect(await canvas.findByRole('button', { name: 'Buy' })).toBeEnabled();
    await expect(await canvas.findByRole('button', { name: 'Upgrade' })).toBeDisabled();
    await expect(await canvas.findByRole('button', { name: 'Iron Sentry' })).toBeVisible();
    await expect(await canvas.findByLabelText('Gold: 8/10')).toBeVisible();
  }
};
