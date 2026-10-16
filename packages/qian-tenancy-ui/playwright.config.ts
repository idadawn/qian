import { defineConfig, devices } from '@playwright/test';
const baseURL = `http://127.0.0.1:${process.env.QIAN_DEV_UI_PORT ?? 5177}`;
export default defineConfig({
  testDir: './e2e', fullyParallel: false, workers: 1, retries: 0,
  timeout: 180_000, expect: { timeout: 15_000 }, forbidOnly: !!process.env.CI,
  reporter: 'list', outputDir: '../../TestResults/tenant-ui/playwright',
  use: { baseURL, trace: 'retain-on-failure', screenshot: 'only-on-failure' },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'], viewport: { width: 1440, height: 1000 } } }],
  webServer: { command: 'pnpm dev', url: baseURL, reuseExistingServer: false, timeout: 60_000 },
});
