import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';
import { playwright } from '@vitest/browser-playwright';
import { storybookTest } from '@storybook/addon-vitest/vitest-plugin';

export default defineConfig({
  test: {
    projects: [{
      plugins: [storybookTest({ configDir: fileURLToPath(new URL('.storybook', import.meta.url)) })],
      test: {
        name: 'yue-storybook',
        maxWorkers: 2,
        testTimeout: 20_000,
        browser: { enabled: true, headless: true, provider: playwright(), instances: [{ browser: 'chromium' }] },
      },
    }],
  },
});
