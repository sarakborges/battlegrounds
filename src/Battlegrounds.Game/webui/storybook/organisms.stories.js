import { expect } from 'storybook/test';
import { createLeaderChoice } from '../organisms/leader-choice/leader-choice.js';
import { renderAsync } from '../.storybook/story-renderer.js';

export default {
  title: 'Atomic Design/Organisms/Game Components',
  tags: ['autodocs', 'test']
};

export const LeaderChoice = {
  args: {
    leader: {
      id: 'leader-choice-demo',
      name: 'Mecha Regent',
      armor: 7,
      power: {
        id: 'power-choice-demo',
        name: 'Overclock',
        description: 'Refresh once for free each turn.',
        cost: 0
      }
    }
  },
  render: args => renderAsync(options => createLeaderChoice(options), args),
  play: async ({ canvas }) => {
    const leaderName = await canvas.findByText('Mecha Regent');
    await expect(leaderName).toBeVisible();
    const choice = leaderName.closest('[data-action="select-leader"]');
    await expect(choice).toHaveAttribute('data-leader-id', 'leader-choice-demo');
  }
};
