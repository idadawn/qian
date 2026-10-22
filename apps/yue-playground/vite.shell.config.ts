import { resolve } from 'node:path';
import { defineConfig } from 'vite';
import base from './vite.config.ts';

// 外壳靶场独立构建：认证路由的懒加载不改变既有 core/form 靶场的共享块与首屏预算。
// 仍输出同一静态目录，Lighthouse 逐页测量实际网络传输，所有预算保持不变。
export default defineConfig({
  ...base,
  build: { ...base.build, emptyOutDir: false, rolldownOptions: { input: { shell: resolve(import.meta.dirname, 'shell.html') } } },
});
