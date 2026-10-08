import { defineConfig, devices } from '@playwright/test';

// a11y 门禁（pnpm yue:a11y）：对构建后的靶场页跑 axe + 键盘路径，critical / serious = 0。先 pnpm --filter yue-playground build。
export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['github'], ['html', { open: 'never' }]] : 'list',
  use: { baseURL: 'http://localhost:4173', trace: 'retain-on-failure' },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: { command: 'pnpm preview', url: 'http://localhost:4173', reuseExistingServer: !process.env.CI, timeout: 30_000 },
});
