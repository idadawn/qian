import { defineConfig } from 'vitest/config';
export default defineConfig({ test: { name: 'yue-contracts', environment: 'node', include: ['src/**/*.test.ts'] } });
