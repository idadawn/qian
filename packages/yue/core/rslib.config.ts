import { defineConfig } from '@rslib/core';

// ESM + d.ts（docs/prd/specs/yue.md §4.1、PRD §07 7.15.2「技术落点」）；dependencies / peerDependencies 默认外置。
// 测试文件只在 tsconfig.json 里参与类型检查，不进 dist（tsconfig.build.json）。
export default defineConfig({
  lib: [{ format: 'esm', syntax: 'es2022', dts: true }],
  source: { entry: { index: './src/index.ts' }, tsconfigPath: './tsconfig.build.json' },
  output: { target: 'web' },
});
