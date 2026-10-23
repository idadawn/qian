import { defineConfig, devices } from '@playwright/test';

// 真实流程验收（S1-YUE-002 验收 1 / 3 / 4）：对 tools/Qian.Dev.Shell 拉起的真实 qian.identity + 网关 BFF 跑完整浏览器流程，入口是 scripts/e2e-auth.mjs。
// 网关是自签名 HTTPS；身份宿主是明文回环 HTTP。没有 webServer：宿主由 e2e-auth.mjs（或开发者手动 scripts/dev-shell.sh up）负责。
const baseURL = process.env.QIAN_DEV_SHELL_URL ?? 'https://localhost:5443';
export default defineConfig({
  testDir: './e2e', fullyParallel: false, workers: 1, retries: 0,
  timeout: 240_000, expect: { timeout: 20_000 }, forbidOnly: !!process.env.CI,
  reporter: 'list', outputDir: '../../TestResults/shell-auth/playwright',
  // 认证页族跟随浏览器语言；验收用简体中文文案断言，固定 locale。
  use: { baseURL, locale: 'zh-CN', ignoreHTTPSErrors: true, trace: 'retain-on-failure', screenshot: 'only-on-failure' },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'], viewport: { width: 1440, height: 1000 } } }],
});
