import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { playwright } from '@vitest/browser-playwright';
import { defineConfig } from 'vitest/config';

// 认证页族的组件测试（Vitest browser mode + Playwright chromium，与 packages/yue/* 同一形态）：连接器落点与登录页的连接器区按注入的替身 provider 验证，
// 顶层跳转经 src/navigation.ts 的出口记录而不真的离开页面。完整真实流程在 apps/qian-shell-auth 的 E2E。
export default defineConfig({
  plugins: [react(), tailwindcss()],
  test: { name: 'identity-ui-browser', setupFiles: ['./src/test/setup.ts'], include: ['src/**/*.browser.test.tsx'], browser: { enabled: true, headless: true, provider: playwright(), instances: [{ browser: 'chromium' }], screenshotFailures: false } },
});
