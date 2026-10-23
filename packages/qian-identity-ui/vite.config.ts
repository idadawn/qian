import { readFileSync } from 'node:fs';
import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

// 认证页族与 qian.identity 同源：构建产物以 /login/ 为基路径，由身份宿主静态托管（本机联调见 tools/Qian.Dev.Shell）。
// 开发服务器只把账号接口与协议端点代理到本机身份宿主；页面本身不持有令牌，也没有任何环境值进入构建产物。
const api = process.env.QIAN_DEV_SHELL_API_URL ?? 'http://127.0.0.1:5090';
if (!['127.0.0.1', 'localhost', '::1', '[::1]'].includes(new URL(api).hostname)) throw new Error('开发代理仅允许本机身份宿主');
const proxy = Object.fromEntries(['/api/identity', '/connect', '/.well-known'].map(prefix => [prefix, { target: api, changeOrigin: false }]));

export default defineConfig({
  base: '/login/',
  plugins: [
    { name: 'yue-first-frame', transformIndexHtml: { order: 'pre', handler: () => [{ tag: 'script', children: readFileSync(new URL(import.meta.resolve('@idadawn/yue-tokens/theme-init.js')), 'utf8'), injectTo: 'head-prepend' }] } },
    react(), tailwindcss(),
  ],
  server: { host: '127.0.0.1', port: Number(process.env.QIAN_DEV_UI_PORT ?? 5178), strictPort: true, proxy },
  build: { target: 'es2022', sourcemap: false },
});
