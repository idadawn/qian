import { execFileSync } from 'node:child_process';
import { resolve } from 'node:path';
import { statSync } from 'node:fs';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { defineConfig, type Plugin } from 'vite';

const root = resolve(import.meta.dirname, '../..');
const loopback = (host: unknown) => typeof host === 'string' && ['127.0.0.1', 'localhost', '::1', '[::1]'].includes(host);
const target = process.env.QIAN_DEV_OPS_URL ?? 'http://127.0.0.1:5080';
const targetUrl = new URL(target);
if (!loopback(targetUrl.hostname)) throw new Error('开发代理仅允许本机运营宿主');
// 代理默认以平台超管身份转发。命令行 --host 会覆盖下面的 server.host，所以按最终解析出的监听地址再拦一次；
// vite preview 继承 server.proxy，同样拦住。
function loopbackOnly(): Plugin {
  const check = (host: unknown) => { if (!loopback(host)) throw new Error(`开发代理以平台超管身份转发，只允许监听回环地址（127.0.0.1、localhost、::1），当前为 ${String(host)}`); };
  return { name: 'qian-dev-loopback-only', configureServer: (server) => check(server.config.server.host), configurePreviewServer: (server) => check(server.config.preview.host) };
}
const state = process.env.QIAN_DEV_STATE ?? resolve(root, '.qian-dev/tenant-ops');
const tokens = new Map<string, { value: string; expires: number; stamp: number }>();
function token(mode: string, tenant: string): string | undefined {
  if (mode === 'none') return;
  if (mode !== 'platform' && mode !== 'tenant') throw new Error('未知开发身份');
  if (mode === 'tenant' && !/^[0-9a-f-]{36}$/i.test(tenant)) throw new Error('开发租户 ID 无效');
  const stamp = statSync(resolve(state, 'ops-signing-key.pem')).mtimeMs;
  const key = mode + ':' + tenant;
  const cached = tokens.get(key);
  if (cached && cached.stamp === stamp && cached.expires > Date.now()) return cached.value;
  const args = ['token', ...(mode === 'tenant' ? ['--tenant', tenant] : [])];
  // 令牌只留在 Vite 的服务端内存，不输出到日志、不返回给浏览器。
  const value = execFileSync(resolve(root, 'scripts/dev-tenant-ops.sh'), args, { cwd: root, encoding: 'utf8', env: { ...process.env, QIAN_DEV_STATE: state }, stdio: ['ignore', 'pipe', 'pipe'] }).trim();
  tokens.set(key, { value, stamp, expires: Date.now() + 3_600_000 });
  return value;
}

export default defineConfig({
  plugins: [react(), tailwindcss(), loopbackOnly()],
  server: {
    host: '127.0.0.1', port: Number(process.env.QIAN_DEV_UI_PORT ?? 5177), strictPort: true,
    proxy: { '/api': { target, changeOrigin: true, configure(proxy) {
      proxy.on('proxyReq', (outgoing, incoming) => {
        const mode = String(incoming.headers['x-qian-dev-principal'] ?? 'platform');
        const tenant = String(incoming.headers['x-qian-dev-tenant'] ?? '11111111-1111-4111-8111-111111111111');
        outgoing.removeHeader('authorization');
        outgoing.removeHeader('x-qian-dev-principal'); outgoing.removeHeader('x-qian-dev-tenant');
        try { const value = token(mode, tenant); if (value) outgoing.setHeader('Authorization', 'Bearer ' + value); }
        catch { outgoing.removeHeader('authorization'); }
      });
    } } },
  },
  build: { target: 'es2022', sourcemap: false },
});
