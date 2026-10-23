import { readFileSync } from 'node:fs';
import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';
import { defineConfig, type Plugin } from 'vite';

// 外壳薄入口：构建产物不含任何环境值，运行时配置由宿主以同源 /app-conf.js 注入（K8s ConfigMap；本机联调是 tools/Qian.Dev.Shell 状态目录里的文件）。
// 开发服务器用缺省配置顶替 /app-conf.js，并把 BFF 与 API 代理到本机网关；代理只许回环地址。
const gateway = process.env.QIAN_DEV_SHELL_URL ?? 'https://localhost:5443';
if (!['127.0.0.1', 'localhost', '::1', '[::1]'].includes(new URL(gateway).hostname)) throw new Error('开发代理仅允许本机网关');
function devAppConf(): Plugin {
  return { name: 'qian-shell-dev-app-conf', configureServer(server) { server.middlewares.use('/app-conf.js', (_request, response) => {
    response.setHeader('Content-Type', 'text/javascript; charset=utf-8'); response.setHeader('Cache-Control', 'no-store');
    response.end('window.__APP_CONF__ = { "apiBase": "/api", "authBase": "/auth", "brand": "Qian 乾" };\n');
  }); } };
}

export default defineConfig({
  plugins: [
    { name: 'yue-first-frame', transformIndexHtml: { order: 'pre', handler: () => [{ tag: 'script', children: readFileSync(new URL(import.meta.resolve('@idadawn/yue-tokens/theme-init.js')), 'utf8'), injectTo: 'head-prepend' }] } },
    devAppConf(), react(), tailwindcss(),
  ],
  server: {
    host: '127.0.0.1', port: Number(process.env.QIAN_DEV_UI_PORT ?? 5179), strictPort: true,
    proxy: Object.fromEntries(['/api', '/api-alt', '/auth', '/__dev'].map(prefix => [prefix, { target: gateway, changeOrigin: false, secure: false }])),
  },
  build: { target: 'es2022', sourcemap: false },
});
