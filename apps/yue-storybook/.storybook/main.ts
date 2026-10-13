import { defineMain } from '@storybook/react-vite/node';
import tailwindcss from '@tailwindcss/vite';
import { mergeConfig } from 'vite';

export default defineMain({
  framework: '@storybook/react-vite',
  stories: ['../../../packages/yue/core/src/storybook/*.stories.tsx'],
  addons: ['@storybook/addon-docs', '@storybook/addon-a11y', '@storybook/addon-vitest'],
  core: { disableTelemetry: true },
  viteFinal: config => mergeConfig(config, { plugins: [tailwindcss()] }),
});
