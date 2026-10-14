import { defineConfig } from '@playwright/test';
const base = process.env.DOCS_BASE ?? '/';
export default defineConfig({
  testDir: './e2e', maxFailures: 5, fullyParallel: true, workers: process.env.CI ? 2 : 3,
  timeout: 45_000, retries: process.env.CI ? 1 : 0,
  use: { baseURL: process.env.DOCS_URL ?? `http://127.0.0.1:6014${base}`, headless: true, viewport: { width: 1440, height: 1000 }, trace: 'retain-on-failure' },
  reporter: [['list'], ['html', { open: 'never' }]],
  webServer: process.env.DOCS_URL ? undefined : { command: 'node scripts/serve-docs.mjs', port: 6014, reuseExistingServer: !process.env.CI },
});
