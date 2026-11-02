import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { playwright } from '@vitest/browser-playwright';
import { defineConfig } from 'vitest/config';

// 开通页面的组件测试（Vitest browser mode + Playwright chromium）：PlatformClient 的 transport 换成按契约应答的替身，
// 只验证页面行为与可访问性；真实宿主上的完整流程在 apps/qian-shell 的真实流程 E2E（e2e-auth/）。
export default defineConfig({
  plugins: [react(), tailwindcss()],
  test: { name: 'tenancy-signup-ui-browser', setupFiles: ['./src/test/setup.ts'], include: ['src/**/*.browser.test.tsx'], browser: { enabled: true, headless: true, provider: playwright(), instances: [{ browser: 'chromium' }], screenshotFailures: false } },
});
