import { createRoot } from 'react-dom/client';
import { createHostFederation } from './shared';
import { PlatformClient, ShellHost, type RemoteRegistration, type ShellContext, type ShellScope } from '@idadawn/qian-plugin-sdk';
import { ShellApplication } from './app';
import './style.css';

interface Bootstrap { context: ShellContext; remotes: RemoteRegistration[]; mounted: string[]; entitled: string[]; permissions: string[] }
declare global {
  interface Window {
    __APP_CONF__?: { apiBase: string; bootstrapUrl: string; auditUrl: string };
    __qian?: { bridge: ShellHost['bridge'] };
  }
}
const root = createRoot(document.getElementById('root')!);
async function start() {
  const config = window.__APP_CONF__;
  if (!config) throw new Error('缺少外壳运行时配置');
  const apiBase = new URL(config.apiBase, location.origin);
  const bootstrapUrl = new URL(config.bootstrapUrl, apiBase);
  const auditUrl = new URL(config.auditUrl, apiBase);
  if ([apiBase, bootstrapUrl, auditUrl].some((url) => url.origin !== location.origin || url.username || url.password || url.search || url.hash || !['http:', 'https:'].includes(url.protocol))) throw new Error('外壳 API 必须同源');
  const response = await fetch(bootstrapUrl, { credentials: 'same-origin', redirect: 'error' });
  if (!response.ok) throw new Error('外壳上下文不可用');
  const bootstrap = await response.json() as Bootstrap;
  if (!bootstrap.context || !Array.isArray(bootstrap.remotes) || !Array.isArray(bootstrap.mounted) || !Array.isArray(bootstrap.entitled) || !Array.isArray(bootstrap.permissions)) throw new Error('外壳上下文不符合契约');
  const client = new PlatformClient({ baseUrl: apiBase.href, getTenantId: () => bootstrap.context.tenant?.id });
  const key = (id: string, scope: ShellScope) => `${scope}|${id}`;
  const federation = createHostFederation();
  const host = new ShellHost({ yueVersion: __YUE_VERSION__, baseUrl: location.origin,
    federation: federation.transport, client, getContext: () => bootstrap.context,
    isMounted: (id, scope) => bootstrap.mounted.includes(key(id, scope)), isEntitled: (id, scope) => bootstrap.entitled.includes(key(id, scope)),
    hasPermission: (permission) => bootstrap.permissions.includes(permission),
    navigate: (target) => { history.pushState(null, '', target); dispatchEvent(new PopStateEvent('popstate')); },
    notify: (_level, message) => { const event = new CustomEvent('qian:notification', { detail: message }); dispatchEvent(event); },
    audit: async (event) => { const result = await fetch(auditUrl, { method: 'POST', credentials: 'same-origin', redirect: 'error', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(event) }); if (!result.ok) throw new Error('审计记录失败'); },
  });
  await host.start();
  for (const remote of bootstrap.remotes) host.registerRemote(remote);
  Object.defineProperty(window, '__qian', { configurable: true, value: Object.freeze({ bridge: host.bridge }) });
  addEventListener('pagehide', () => { void host.dispose(); }, { once: true });
  if (import.meta.env.MODE === 'acceptance') { const { installAcceptance } = await import('./acceptance'); await installAcceptance(host, bootstrap, federation.instance); }
  root.render(<ShellApplication host={host} client={client} />);
}
void start().catch(() => root.render(<main className="p-6"><h1>Qian 控制台</h1><p role="alert">当前会话不可用，请登录后重试。</p></main>));
