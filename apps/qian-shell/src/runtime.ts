import { HttpResponseError, PlatformClient, PlatformError } from '@idadawn/yue-sdk';
import { ShellHost, type RemoteRegistration, type ShellContext, type ShellScope } from '@idadawn/qian-plugin-sdk';
import { createBrowserRouter, type ShellRouter } from '@idadawn/yue-shell/router';
import { apiPrefixMiddleware, createBffSessionClient, csrfMiddleware, fetchBootstrap, type AppConfig, type BffSessionClient, type ShellBootstrap } from '@qian/shell-kit';
import { createHostFederation } from './shared';

export type LoadStatus = 'loading' | 'ready' | 403 | 500;
export interface ShellState {
  status: LoadStatus;
  /** 最近一次成功的 bootstrap；重拉进行中仍保留上一份，避免整页闪烁。 */
  bootstrap: ShellBootstrap | null;
  /** 可见能力条数（辅助接口）：undefined 为加载中，null 为不可用——失败不影响整页状态。 */
  capabilities: number | null | undefined;
  reloading: boolean;
  expired: boolean;
  signedOut: boolean;
}
interface CatalogPaths { '/api/capabilities': { get: { responses: { 200: { content: { 'application/json': { items: unknown[] } } } } } } }
type AuditPaths = Record<string, { post: { requestBody: { content: { 'application/json': unknown } }; responses: { 204: { content: never } } } }>;

const EMPTY_CONTEXT: ShellContext = { tenant: null, membership: null, app: null, locale: 'zh-CN', theme: 'light' };
const key = (id: string, scope: ShellScope) => `${scope}|${id}`;
const status = (error: unknown) => error instanceof PlatformError || error instanceof HttpResponseError ? error.status : 0;

/**
 * 外壳运行时（React 之外，整页只建一次）：一份运行时配置、一个依赖方会话客户端、一个平台客户端（规范路径 /api 映射到 apiBase、写请求带防伪令牌、
 * 并发 401 合并成一次会话刷新）、一个 MF2 宿主。工作区数据只经 bootstrap 聚合端点取得（会话、上下文、远程、权限、能力导航），重拉时按差异卸载 / 登记远程。
 *
 * 会话结束对外壳是粘性的：SDK 通知 onSessionExpired 之后，刷新通知与排队的重拉都不再发起请求；只有整页重新登录（新文档、新运行时）才复位。
 * 否则「依赖方刷新成功、平台表面仍 401」会无界循环（刷新成功的通知在重放之前到达 → 排队重拉 → 重放 401、弹过期对话框 → 排队的重拉照常发出 → 再 401 → 再刷新……）。
 * 首次加载就没有会话（从未成功过）时不弹对话框，顶层跳到依赖方登录端点。
 */
export function createShellRuntime(config: AppConfig) {
  const router: ShellRouter = createBrowserRouter();
  const bff: BffSessionClient = createBffSessionClient({ authBase: config.authBase });
  const listeners = new Set<() => void>();
  let state: ShellState = { status: 'loading', bootstrap: null, capabilities: undefined, reloading: false, expired: false, signedOut: false };
  // 宿主（上下文、挂载 / 权益 / 权限判定、请求的租户头）看到的 bootstrap：先于 React 状态更新，远程按它登记完再一次性通知界面。
  let view: ShellBootstrap | null = null;
  let presentation: Pick<ShellContext, 'locale' | 'theme'> | null = null;
  let ended = false; let inFlight = false; let queued = false; let everReady = false;
  const registered = new Map<string, string>();
  const set = (patch: Partial<ShellState>) => { state = { ...state, ...patch }; for (const listener of listeners) listener(); };
  const context = (): ShellContext => {
    const current = view?.context ?? EMPTY_CONTEXT;
    return presentation ? { ...current, ...presentation } : current;
  };

  const client = new PlatformClient({
    baseUrl: location.origin, sessionProvider: bff,
    middleware: [apiPrefixMiddleware(config.apiBase), csrfMiddleware(bff)],
    getTenantId: () => view?.context.tenant?.id,
    onSessionExpired: () => {
      ended = true;
      if (!everReady) { location.assign(bff.loginUrl(router.getPath())); return; }
      set({ expired: true });
    },
    onSessionRefreshed: () => { if (!ended) void load(); },
  });
  const federation = createHostFederation();
  const host = new ShellHost({
    yueVersion: __YUE_VERSION__, baseUrl: location.origin, federation: federation.transport, client, getContext: context,
    isMounted: (id, scope) => view?.mounted.includes(key(id, scope)) ?? false,
    isEntitled: (id, scope) => view?.entitled.includes(key(id, scope)) ?? false,
    hasPermission: permission => view?.permissions.includes(permission) ?? false,
    navigate: target => router.navigate(target),
    notify: (level, message) => { dispatchEvent(new CustomEvent('qian:notification', { detail: { level, message } })); },
    audit: async event => { await client.api<AuditPaths>().POST(config.auditUrl, { body: event as never }); },
  });

  // 远程按「scope|id」对比版本与清单：消失的卸载、新出现的登记、变了的先卸后登；单个远程清单不合格只记控制台，不拖垮整页。
  async function reconcile(remotes: readonly RemoteRegistration[]) {
    const next = new Map(remotes.map(remote => [key(remote.id, remote.scope), JSON.stringify([remote.version, remote.manifest])]));
    for (const [identity, fingerprint] of [...registered]) {
      if (next.get(identity) === fingerprint) continue;
      const [scope, id] = identity.split('|') as [ShellScope, string];
      await host.unloadRemote(id, scope).catch(() => undefined);
      registered.delete(identity);
    }
    for (const remote of remotes) {
      const identity = key(remote.id, remote.scope);
      if (registered.has(identity)) continue;
      try { host.registerRemote(remote); registered.set(identity, next.get(identity)!); }
      catch (error) { console.error('qian.shell.remote.invalid', { remoteId: remote.id, scope: remote.scope, error }); }
    }
  }

  async function capabilities(): Promise<number | null> {
    try { return (await client.api<CatalogPaths>().GET('/api/capabilities')).data?.items.length ?? 0; }
    catch { return null; }
  }

  /** 拉一轮工作区（bootstrap，再对成员拉能力目录条数）；进行中再来一次就排队、结束后再完整拉一次，不与重放并行。 */
  async function load(): Promise<void> {
    if (ended) return;
    if (inFlight) { queued = true; return; }
    inFlight = true; set({ reloading: true });
    try {
      const bootstrap = await fetchBootstrap(client, config.bootstrapUrl);
      if (ended) return;
      view = bootstrap;
      await reconcile(bootstrap.remotes);
      host.refreshContext();
      everReady = true;
      set({ status: 'ready', bootstrap, capabilities: bootstrap.session.tenantId === null ? undefined : state.capabilities });
      if (bootstrap.session.tenantId !== null) {
        const count = await capabilities();
        if (!ended) set({ capabilities: count });
      }
    } catch (error) {
      const code = status(error);
      // 401 已由 SDK 走完会话刷新与过期通知；外壳只处理整页状态。辅助接口失败不在这里（见 capabilities）。
      if (code !== 401 && !ended) set({ status: code === 403 ? 403 : state.bootstrap ? state.status : 500 });
    } finally {
      inFlight = false; set({ reloading: false });
      if (queued) { queued = false; void load(); }
    }
  }

  return {
    config, router, bff, client, host, federation,
    subscribe(listener: () => void) { listeners.add(listener); return () => { listeners.delete(listener); }; },
    getState: () => state,
    load,
    /** 当前语言与主题进入外壳上下文（远程经 bridge.getContext 读到用户偏好，而不是 bootstrap 的缺省值）。 */
    present(locale: string, theme: 'light' | 'dark') {
      if (presentation?.locale === locale && presentation.theme === theme) return;
      presentation = { locale, theme };
      if (view) host.refreshContext();
    },
    async signOut() {
      ended = true;
      try { await bff.signOut(); } finally { set({ signedOut: true }); }
    },
    /** 切换租户：顶层跳到依赖方登录端点并带 prompt=select_account，由身份服务的同源选择页换会话，回调后整页回到工作台。 */
    switchTenant() { location.assign(bff.loginUrl('/', { prompt: 'select_account' })); },
    signIn() { location.assign(bff.loginUrl(router.getPath())); },
  };
}
export type ShellRuntime = ReturnType<typeof createShellRuntime>;
