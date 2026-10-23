import { readFileSync } from 'node:fs';
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';


export default defineConfig({
  define: { __YUE_VERSION__: JSON.stringify(JSON.parse(readFileSync(new URL('../../packages/yue/shell/package.json', import.meta.url), 'utf8')).version) },
  plugins: [react(), tailwindcss()],
  server: { host: '127.0.0.1', port: 5180, strictPort: true },
  preview: { host: '127.0.0.1', port: 5180, strictPort: true },
  build: { outDir: 'dist/host', target: 'es2022', sourcemap: false },
});
