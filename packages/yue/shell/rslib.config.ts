import { defineConfig } from '@rslib/core';

// ESM + d.ts（docs/prd/specs/yue.md §4.1、PRD §07 7.15.2「技术落点」）；dependencies / peerDependencies 默认外置。
export default defineConfig({
  lib: [{ format: 'esm', syntax: 'es2022', dts: true, bundle: false }],
  // 保留认证表单的动态 import 边界，npm 制品不能重新把 form 装回外壳首屏。
  source: { entry: { index: ['./src/*.{ts,tsx}', '!./src/*.test.{ts,tsx}'] }, tsconfigPath: './tsconfig.build.json' },
  tools: { swc: { jsc: { transform: { react: { runtime: 'automatic' } } } } },
  output: { target: 'web' },
});
