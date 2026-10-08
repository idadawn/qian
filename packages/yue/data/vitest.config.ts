import react from '@vitejs/plugin-react';
import { playwright } from '@vitest/browser-playwright';
import { defineConfig } from 'vitest/config';

// 浏览器测试（Vitest browser mode + Playwright Chromium）：键盘路径、焦点管理、ARIA 与 axe 都在真浏览器里跑。
export default defineConfig({
  plugins: [react()],
  test: {
    name: 'data-browser',
    include: ['src/**/*.browser.test.tsx'],
    browser: {
      enabled: true,
      headless: true,
      provider: playwright(),
      instances: [{ browser: 'chromium' }],
      screenshotFailures: false,
    },
  },
});
