import { defineConfig } from '@rslib/core';

// ESM + d.ts（docs/prd/specs/yue.md §4.1、PRD §07 7.15.2「技术落点」）；dependencies / peerDependencies 默认外置。
export default defineConfig({
  lib: [{ format: 'esm', syntax: 'es2022', dts: true }],
  source: { entry: { index: './src/index.ts' } },
  output: { target: 'web' },
});
