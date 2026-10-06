import { expect } from 'storybook/test';
import { createBadge } from '../atoms/badge/badge.js';
import { createButton } from '../atoms/button/button.js';
import { createEmptyState } from '../atoms/empty-state/empty-state.js';
import { createModalDialog } from '../atoms/modal-dialog/modal-dialog.js';
import { createPanel } from '../atoms/panel/panel.js';
import { renderAsync } from '../.storybook/story-renderer.js';

export default {
  title: 'Atomic Design/Atoms/Primitives',
  tags: ['autodocs', 'test']
};

export const PrimaryButton = {
  args: {
    label: 'Recruit unit',
    disabled: false
  },
  render: args => renderAsync(async options => {
    const button = await createButton({ ...options, variant: 'primary' });
    return button;
  }, args),
  play: async ({ canvas }) => {
    const button = await canvas.findByRole('button', { name: 'Recruit unit' });
    await expect(button).toBeEnabled();
    await expect(button).toHaveAttribute('data-variant', 'primary');
  }
};

export const DisabledButton = {
  args: {
    label: 'Upgrade tier',
    disabled: true
  },
  render: args => renderAsync(options => createButton(options), args),
  play: async ({ canvas }) => {
    await expect(await canvas.findByRole('button', { name: 'Upgrade tier' })).toBeDisabled();
  }
};

export const Badge = {
  args: {
    text: 'Tier 4',
    variant: 'default'
  },
  render: args => renderAsync(options => createBadge(options), args),
  play: async ({ canvas }) => {
    await expect(await canvas.findByText('Tier 4')).toBeVisible();
  }
};

export const Panel = {
  render: () => renderAsync(async () => {
    const copy = document.createElement('p');
    copy.className = 'story-copy';
    copy.textContent = 'Panels stay generic. Gameplay-specific composition belongs in components.';
    return createPanel({ children: [copy] });
  }),
  play: async ({ canvas }) => {
    await expect(await canvas.findByText(/Panels stay generic/)).toBeVisible();
  }
};

export const EmptyState = {
  args: { label: 'No legal targets available.' },
  render: args => renderAsync(options => createEmptyState(options), args),
  play: async ({ canvas }) => {
    await expect(await canvas.findByText('No legal targets available.')).toBeVisible();
  }
};

export const ModalDialog = {
  render: () => renderAsync(async () => {
    const body = document.createElement('p');
    body.className = 'story-copy';
    body.textContent = 'Choose one option to resolve the pending effect.';
    const action = await createButton({ label: 'Confirm', variant: 'primary' });
    return createModalDialog({
      eyebrow: 'Pending choice',
      title: 'Select a reward',
      body: [body],
      actions: [action]
    });
  }, {}, { wide: true }),
  play: async ({ canvas }) => {
    await expect(await canvas.findByText('Select a reward')).toBeVisible();
    await expect(await canvas.findByRole('button', { name: 'Confirm' })).toBeEnabled();
  }
};
