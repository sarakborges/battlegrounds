export default {
  stories: ['../storybook/**/*.stories.js'],
  addons: [
    '@storybook/addon-docs',
    '@storybook/addon-a11y',
    '@storybook/addon-vitest'
  ],
  framework: {
    name: '@storybook/html-vite',
    options: {}
  }
};
