import { expect } from 'storybook/test';
import { createActionToken } from '../molecules/action-token/action-token.js';
import { createPlayerChip } from '../molecules/player-chip/player-chip.js';
import { createPowerButton } from '../molecules/power-button/power-button.js';
import { createResourceCounter } from '../molecules/resource-counter/resource-counter.js';
import { createUnitToken } from '../molecules/unit-token/unit-token.js';
import { renderAsync } from '../.storybook/story-renderer.js';

export default {
  title: 'Atomic Design/Molecules/Game Components',
  tags: ['autodocs', 'test']
};

export const UnitToken = {
  args: {
    id: 'unit-demo',
    name: 'Clockwork Guardian',
    description: 'A sturdy test unit used by Storybook.',
    tier: 4,
    attack: 9,
    health: 12,
    frozen: false,
    disabled: false,
    selected: false
  },
  render: args => renderAsync(options => createUnitToken(options), args),
  play: async ({ canvas }) => {
    const token = await canvas.findByRole('button', { name: 'Clockwork Guardian' });
    await expect(token).toHaveAttribute('aria-disabled', 'false');
    await expect(token).toHaveAttribute('data-inspect-kind', 'unit');
  }
};

export const FrozenUnitToken = {
  args: {
    id: 'unit-frozen',
    name: 'Frozen Bruiser',
    tier: 2,
    attack: 5,
    health: 7,
    frozen: true,
    disabled: true
  },
  render: args => renderAsync(options => createUnitToken(options), args),
  play: async ({ canvas }) => {
    const token = await canvas.findByRole('button', { name: 'Frozen Bruiser' });
    await expect(token).toHaveAttribute('aria-disabled', 'true');
    await expect(token.querySelector('[data-field="frozen"]')).not.toHaveAttribute('hidden');
  }
};

export const ActionToken = {
  args: {
    id: 'action-demo',
    name: 'Arcane Coupon',
    description: 'Reduce the cost of the next purchase.',
    tier: 3,
    cost: 2,
    disabled: false
  },
  render: args => renderAsync(options => createActionToken(options), args),
  play: async ({ canvas }) => {
    const token = await canvas.findByRole('button', { name: 'Arcane Coupon' });
    await expect(token).toHaveAttribute('data-inspect-kind', 'action');
  }
};

export const ResourceCounter = {
  args: {
    value: 7,
    maximum: 10,
    label: 'Gold'
  },
  render: args => renderAsync(options => createResourceCounter(options), args),
  play: async ({ canvas }) => {
    await expect(await canvas.findByLabelText('Gold: 7/10')).toBeVisible();
  }
};

export const PlayerChip = {
  args: {
    id: 0,
    leaderId: 'leader-demo',
    leader: 'The Curator',
    leaderDescription: 'Storybook fixture leader.',
    health: 31,
    human: true,
    eliminated: false
  },
  render: args => renderAsync(options => createPlayerChip(options), args),
  play: async ({ canvas }) => {
    const chip = await canvas.findByTitle('The Curator · 31 HP');
    await expect(chip).toHaveAttribute('data-variant', 'human');
    await expect(chip).toHaveAttribute('data-inspect-kind', 'leader');
  }
};

export const PowerButton = {
  args: {
    label: 'Use hero power',
    blocked: false,
    power: {
      id: 'power-demo',
      name: 'Reconfigure',
      description: 'Give a friendly unit +1/+1.',
      cost: 2,
      activatable: true
    }
  },
  render: args => renderAsync(options => createPowerButton(options), args),
  play: async ({ canvas }) => {
    const control = await canvas.findByRole('button', { name: 'Use hero power' });
    await expect(control).toBeEnabled();
    await expect(control).toHaveAttribute('data-action', 'use-power');
  }
};
