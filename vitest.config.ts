import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

// 前端工作区的测试入口（pnpm yue:test）：
// - yue-gates：门禁自测（分层边界、循环依赖、包形状），Node 环境；
// - yue-ssr：组件在 Node 里 renderToString（SSR 兼容是验证门的一项）；
// - packages/yue/*/vitest.config.ts：各包的浏览器测试（Vitest browser mode + Playwright）。
export default defineConfig({
  test: {
    projects: [
      { test: { name: 'yue-gates', environment: 'node', include: ['tests/gates/yue/**/*.test.ts'], testTimeout: 120_000, hookTimeout: 120_000 } },
      { plugins: [react()], test: { name: 'yue-ssr', environment: 'node', include: ['packages/yue/*/src/**/*.ssr.test.tsx'] } },
      { test: { name: 'core-contracts', environment: 'node', include: ['packages/yue/core/src/**/*.manifest.test.ts'] } },
      { test: { name: 'qian-plugin-sdk', environment: 'node', include: ['packages/qian-plugin-sdk/test/**/*.test.ts'] } },
      'packages/yue/*/vitest.config.ts',
    ],
  },
});
