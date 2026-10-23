import { defineConfig } from '@playwright/test';
export default defineConfig({
  testDir: './e2e', fullyParallel: false, retries: 0, timeout: 30_000,
  use: { baseURL: `http://127.0.0.1:${process.env.QIAN_MF_PORT ?? 5180}`, headless: true, trace: 'retain-on-failure' },
  reporter: [['list'], ['html', { open: 'never' }]],
  webServer: { command: 'node scripts/serve-acceptance.mjs', url: `http://127.0.0.1:${process.env.QIAN_MF_PORT ?? 5180}`, reuseExistingServer: false },
});
