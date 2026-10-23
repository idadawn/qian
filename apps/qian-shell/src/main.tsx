import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import type { ShellHost } from '@idadawn/qian-plugin-sdk';
import { loadAppConfig, type AppConfig } from '@qian/shell-kit';
import { ConfigError, ShellApplication } from './app';
import { createShellRuntime } from './runtime';
import './style.css';

declare global { interface Window { __qian?: { bridge: ShellHost['bridge'] } } }

const root = createRoot(document.getElementById('root')!);
async function start() {
  // 运行时配置先于一切：同源 /app-config.json 读不到或不合法时只渲染配置错误页，不建客户端、不再发任何请求。
  let config: AppConfig;
  try { config = await loadAppConfig(); }
  catch { root.render(<StrictMode><ConfigError /></StrictMode>); return; }
  const runtime = createShellRuntime(config);
  await runtime.host.start();
  // 全局入口只暴露受控的桥接调用（远程以宿主交付的 caller 调用）；页面渲染的远程拿到的是宿主闭包绑定的 bridge。
  Object.defineProperty(window, '__qian', { configurable: true, value: Object.freeze({ bridge: runtime.host.bridge }) });
  addEventListener('pagehide', () => { void runtime.host.dispose(); }, { once: true });
  if (import.meta.env.MODE === 'acceptance') { const { installAcceptance } = await import('./acceptance'); installAcceptance(runtime); }
  void runtime.load();
  root.render(<StrictMode><ShellApplication runtime={runtime} /></StrictMode>);
}
void start();
