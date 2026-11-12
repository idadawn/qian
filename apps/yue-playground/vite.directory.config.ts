import { defineConfig, type Plugin } from 'vite';
import base from './vite.config.ts';

// 真实目录验收页的开发服务器（S1-IDP-010 验收 2）：只给 scripts/directory-e2e.mjs 用，不参与靶场构建，也就不影响首屏预算。
// /api/identity 与 /api/directory 原样代理到本机真实身份宿主，不注入任何凭据：登录 Cookie 落在本页源上，PlatformClient 同源携带。
// 监听地址与代理目标都只能是回环地址。
const loopback = (host: unknown) => typeof host === 'string' && ['127.0.0.1', 'localhost', '::1', '[::1]'].includes(host);
const target = process.env.QIAN_DEV_IDENTITY_URL ?? 'http://127.0.0.1:5081';
if (!loopback(new URL(target).hostname)) throw new Error('真实目录页只允许代理到本机身份宿主');
// 命令行 --host 会覆盖下面的 server.host，所以按最终解析出的监听地址再拦一次；vite preview 继承 server.proxy，同样拦住。
function loopbackOnly(): Plugin {
  const check = (host: unknown) => { if (!loopback(host)) throw new Error(`真实目录页只允许监听回环地址（127.0.0.1、localhost、::1），当前为 ${String(host)}`); };
  return { name: 'qian-directory-loopback-only', configureServer: (server) => check(server.config.server.host), configurePreviewServer: (server) => check(server.config.preview.host) };
}

export default defineConfig({
  ...base,
  plugins: [...(base.plugins ?? []), loopbackOnly()],
  server: {
    host: '127.0.0.1', port: Number(process.env.QIAN_DIRECTORY_UI_PORT ?? 5178), strictPort: true,
    proxy: { '/api/identity': { target, changeOrigin: true }, '/api/directory': { target, changeOrigin: true } },
  },
});
