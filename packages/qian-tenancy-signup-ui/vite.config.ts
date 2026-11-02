import { readFileSync } from 'node:fs';
import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

// 申请人公开页与申请人接口同源：构建产物以 /signup/ 为基路径，由身份宿主静态托管（本机联调见 tools/Qian.Dev.Shell 的 QIAN_DEV_SIGNUP_UI_DIST）。
// 开发服务器只把申请人接口代理到本机身份宿主；页面不持有令牌，申请人会话只在宿主写的 HttpOnly Cookie 里。
const api = process.env.QIAN_DEV_SHELL_API_URL ?? 'http://127.0.0.1:5090';
if (!['127.0.0.1', 'localhost', '::1', '[::1]'].includes(new URL(api).hostname)) throw new Error('开发代理仅允许本机身份宿主');

export default defineConfig({
  base: '/signup/',
  plugins: [
    { name: 'yue-first-frame', transformIndexHtml: { order: 'pre', handler: () => [{ tag: 'script', children: readFileSync(new URL(import.meta.resolve('@idadawn/yue-tokens/theme-init.js')), 'utf8'), injectTo: 'head-prepend' }] } },
    react(), tailwindcss(),
  ],
  server: { host: '127.0.0.1', port: Number(process.env.QIAN_DEV_UI_PORT ?? 5179), strictPort: true, proxy: { '/api/signup': { target: api, changeOrigin: false } } },
  build: { outDir: 'dist/public', target: 'es2022', sourcemap: false },
});
