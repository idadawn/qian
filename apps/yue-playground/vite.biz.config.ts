import { resolve } from 'node:path';
import { defineConfig } from 'vite';
import base from './vite.config.ts';

// 业务选择器靶场独立构建：biz 引用 core / data 的组合（Combobox、Popover、Tree 等）不进既有 core/form/data 靶场的共享块，
// 否则共享块被切得更碎、core 首屏传输体积随之上涨。与外壳靶场同理，仍输出同一静态目录，Lighthouse 逐页测量，预算不变。
export default defineConfig({
  ...base,
  build: { ...base.build, emptyOutDir: false, rolldownOptions: { input: { biz: resolve(import.meta.dirname, 'biz.html') } } },
});
