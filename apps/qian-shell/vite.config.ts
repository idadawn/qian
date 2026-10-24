import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';
import { defineConfig, type Plugin } from 'vite';

// Qian 外壳（S1-YUE-012）：构建产物不含任何环境值。运行时配置是同源 /app-config.json（K8s ConfigMap；本机联调是 tools/Qian.Dev.Shell 状态目录里的文件），
// 安全策略是构建期写进 index.html 的 meta CSP（内联脚本只有首帧主题脚本，按内容哈希放行）；frame-ancestors 只能走响应头，由网关静态托管统一下发。
const yueVersion = (JSON.parse(readFileSync(new URL('../../packages/yue/shell/package.json', import.meta.url), 'utf8')) as { version: string }).version;
const gateway = process.env.QIAN_DEV_SHELL_URL ?? 'https://localhost:5443';
if (!['127.0.0.1', 'localhost', '::1', '[::1]'].includes(new URL(gateway).hostname)) throw new Error('开发代理仅允许本机网关');

/** 首帧主题：样式与模块脚本之前按已存偏好设好 data-theme，避免暗色用户看到一帧亮色。放在 charset / viewport 之后，不挤出前 1024 字节。 */
function firstFrame(): Plugin {
  const script = readFileSync(new URL(import.meta.resolve('@idadawn/yue-tokens/theme-init.js')), 'utf8');
  return { name: 'qian-shell-first-frame', transformIndexHtml: { order: 'pre', handler: html => html.replace('<meta name="referrer" content="no-referrer" />', match => `${match}\n    <script>${script}</script>`) } };
}

/**
 * 构建期 meta CSP：只允许同源脚本与按哈希列出的内联脚本（首帧主题脚本）；连接、图片、字体都只许同源（data: 用于内联图标与字体）。
 * 'unsafe-eval' 是已登记的例外：@idadawn/yue-form 的 SchemaForm 在运行期用 Ajv 编译表单 Schema（低代码表单的 Schema 来自运行期，无法预编译），
 * 共享给远程页面（如租户运营）后在外壳源执行。外壳自身与插件 SDK 的启动路径已不在运行期生成代码（清单校验构建期预编译）；
 * yue-form 换成解释型校验后去掉这一项。它不让配置文件变成代码：运行时配置是 /app-config.json 数据，只解析、校验，从不求值。
 */
const CSP_DIRECTIVES = (hashes: readonly string[]) => [
  "default-src 'self'", `script-src 'self' 'unsafe-eval' ${hashes.join(' ')}`.trim(), "style-src 'self' 'unsafe-inline'", "img-src 'self' data:", "font-src 'self' data:",
  "connect-src 'self'", "object-src 'none'", "base-uri 'none'", "form-action 'self'", "frame-src 'none'", "worker-src 'self'", "manifest-src 'self'",
];
function contentSecurityPolicy(): Plugin {
  return {
    name: 'qian-shell-csp', apply: 'build',
    transformIndexHtml: { order: 'post', handler(html) {
      const inline = [...html.matchAll(/<script(?![^>]*\bsrc=)[^>]*>([\s\S]*?)<\/script>/g)].map(([, body]) => `'sha256-${createHash('sha256').update(body!).digest('base64')}'`);
      if (inline.length === 0) throw new Error('首帧主题脚本缺失');
      const meta = `<meta http-equiv="Content-Security-Policy" content="${CSP_DIRECTIVES(inline).join('; ')}" />`;
      if (!html.includes('<meta charset="UTF-8" />')) throw new Error('index.html 缺少 charset');
      return html.replace('<meta charset="UTF-8" />', match => `${match}\n    ${meta}`);
    } },
  };
}

/** 开发服务器：/app-config.json 用缺省品牌顶替；BFF、API、远程制品代理到本机网关（只许回环地址）。 */
function devAppConfig(): Plugin {
  return { name: 'qian-shell-dev-app-config', configureServer(server) { server.middlewares.use('/app-config.json', (_request, response) => {
    response.setHeader('Content-Type', 'application/json; charset=utf-8'); response.setHeader('Cache-Control', 'no-store');
    response.end(JSON.stringify({ brand: 'Qian 乾' }));
  }); } };
}

export default defineConfig({
  define: { __YUE_VERSION__: JSON.stringify(yueVersion) },
  plugins: [firstFrame(), contentSecurityPolicy(), devAppConfig(), react(), tailwindcss()],
  server: {
    host: '127.0.0.1', port: Number(process.env.QIAN_DEV_UI_PORT ?? 5179), strictPort: true,
    proxy: Object.fromEntries(['/api', '/api-alt', '/auth', '/__dev', '/remotes'].map(prefix => [prefix, { target: gateway, changeOrigin: false, secure: false }])),
  },
  preview: { host: '127.0.0.1', port: 5180, strictPort: true },
  build: { outDir: 'dist/host', target: 'es2022', sourcemap: false },
});
