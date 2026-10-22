import { defineConfig } from '@playwright/test';

// 统一 Linux 浏览器与系统字体环境；本机和 CI 均由 scripts/yue-visual.mjs 启动。
export default defineConfig({
  testDir: './visual', fullyParallel: true, workers: 2, timeout: 30_000, retries: 0,
  forbidOnly: true, reporter: 'list', outputDir: './.visual-results/results',
  snapshotPathTemplate: '{testDir}/__snapshots__/{arg}{ext}',
  updateSnapshots: 'none',
  expect: { toHaveScreenshot: { animations: 'disabled', maxDiffPixelRatio: 0.001, maxDiffPixels: 0 } },
  use: { baseURL: 'http://127.0.0.1:4179', viewport: { width: 1280, height: 900 }, deviceScaleFactor: 1, locale: 'zh-CN', timezoneId: 'Asia/Shanghai', reducedMotion: 'reduce', headless: true, trace: 'retain-on-failure' },
  webServer: { command: 'node scripts/serve-visual.mjs', url: 'http://127.0.0.1:4179', reuseExistingServer: false },
});
