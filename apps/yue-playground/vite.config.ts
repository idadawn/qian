import { resolve } from 'node:path';
import { readFileSync } from 'node:fs';
import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

export default defineConfig({
  plugins: [{ name: 'yue-first-frame', transformIndexHtml: { order: 'pre', handler: () => [{ tag: 'script', children: readFileSync(new URL(import.meta.resolve('@idadawn/yue-tokens/theme-init.js')), 'utf8'), injectTo: 'head-prepend' }] } }, react(), tailwindcss()],
  build: { target: 'es2022', sourcemap: false, rolldownOptions: { input: { main: resolve(import.meta.dirname, 'index.html'), tokens: resolve(import.meta.dirname, 'tokens.html'), core: resolve(import.meta.dirname, 'core.html'), form: resolve(import.meta.dirname, 'form.html'), data: resolve(import.meta.dirname, 'data.html') } } },
});
