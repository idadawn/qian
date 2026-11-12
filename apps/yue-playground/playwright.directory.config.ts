import { defineConfig, devices } from '@playwright/test';

// 真实目录 E2E（S1-IDP-010 验收 2）：由 scripts/directory-e2e.mjs 启动——它先用 dev-tenant-ops.sh 起本机真实宿主，再跑这里的用例。
// 与 a11y 门禁的 playwright.config.ts 分开：那份对构建后的靶场跑，不起宿主。
const baseURL = `http://127.0.0.1:${process.env.QIAN_DIRECTORY_UI_PORT ?? 5178}`;
export default defineConfig({
  testDir: './e2e-directory', fullyParallel: false, workers: 1, retries: 0,
  timeout: 180_000, expect: { timeout: 15_000 }, forbidOnly: !!process.env.CI,
  reporter: 'list', outputDir: '../../TestResults/directory-e2e/playwright',
  use: { baseURL, trace: 'retain-on-failure', screenshot: 'only-on-failure' },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: { command: 'pnpm exec vite --config vite.directory.config.ts', url: baseURL + '/directory.html', reuseExistingServer: false, timeout: 60_000 },
});
