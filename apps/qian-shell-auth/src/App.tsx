import { useEffect, useMemo, useState } from 'react';
import { Alert, Button, Card, MenuItem, Spinner } from '@idadawn/yue-core';
import { createI18n, HttpResponseError, PlatformClient, PlatformError, type PlatformI18n } from '@idadawn/yue-sdk';
import { initializeTheme } from '@idadawn/yue-tokens';
import { AppShell, createBffSessionClient, createBrowserRouter, FallbackPage, fetchNavigation, fetchPermissions, InboxPopover, initPreferences, installShellLanguagePacks, matchMenuItem, OfflineBanner, Page, PageHeader, PreferenceControls, PreferencesProvider, SessionExpiredDialog, ShellErrorBoundary, toMenu, usePreferences, useRoutePath, type AppConf, type BffSession, type BffSessionClient, type NavigationCatalogItem, type PermissionGrant, type ShellMenuItem, type PreferenceStorage } from '@idadawn/yue-shell';
import { installAppLanguagePacks } from './locales';

/** 能力目录（GET {apiBase}/capabilities）：首页只显示可见能力条数，作为第三路并发请求参与会话刷新合并。 */
interface CatalogPaths { '/capabilities': { get: { responses: { 200: { content: { 'application/json': { items: unknown[] } } } } } } }

type LoadStatus = 'idle' | 'loading' | 'ready' | 403 | 500;
interface Catalog { navigation: NavigationCatalogItem[]; permissions: PermissionGrant[]; capabilities: number }

interface WorkspaceHandlers { onStatus(status: LoadStatus): void; onCatalog(catalog: Catalog): void; onSession(session: BffSession): void; onExpired(): void }

/**
 * 租户表面的客户端与目录加载器。菜单、权限与能力目录三路并发，一轮进行中再来一次就排队、结束后再完整拉一次（会话刷新成功的通知常到在一轮途中——这轮的
 * 401 正是刷新的起因，重放只覆盖这轮里 401 的那几路，排队的整轮才保证三路都是新修订下的结果），不与重放并行。
 * 会话结束是粘性的：SDK 通知 onSessionExpired 之后，刷新通知与排队的整轮都不再发起请求、也不再重读会话快照。否则「依赖方刷新成功、平台表面仍 401」会无界
 * 循环（刷新成功的通知在重放之前到达 → 排队整轮 → 重放 401、弹过期对话框 → 排队的整轮照常发出 → 三路 401 → SDK 按约定重新探测，刷新仍成功 → 再通知……）。
 * 过期后只有整页重新登录（新文档、新客户端）才复位。
 */
function createWorkspace(baseUrl: string, bff: BffSessionClient, handlers: WorkspaceHandlers): () => Promise<void> {
  let ended = false; let inFlight = false; let queued = false;
  const client = new PlatformClient({
    baseUrl, sessionProvider: bff,
    onSessionExpired: () => { ended = true; handlers.onExpired(); },
    onSessionRefreshed: () => {
      if (ended) return;
      void bff.getSession().then(value => { if (value && !ended) handlers.onSession(value); }).catch(() => undefined);
      void load();
    },
  });
  const load = async (): Promise<void> => {
    if (ended) return;
    if (inFlight) { queued = true; return; }
    inFlight = true; handlers.onStatus('loading');
    try {
      const [navigation, permissions, catalog] = await Promise.all([fetchNavigation(client), fetchPermissions(client), client.api<CatalogPaths>().GET('/capabilities')]);
      handlers.onCatalog({ navigation, permissions, capabilities: catalog.data?.items.length ?? 0 }); handlers.onStatus('ready');
    } catch (error) {
      const code = error instanceof PlatformError || error instanceof HttpResponseError ? error.status : 0;
      handlers.onStatus(code === 403 ? 403 : code === 401 ? 'idle' : 500);
    } finally {
      inFlight = false;
      if (queued) { queued = false; void load(); }
    }
  };
  return load;
}

/** 运行时配置非法时的唯一界面：不建客户端、不发请求。 */
export function ConfigError() {
  const [i18n] = useState(() => { const value = createI18n(); installShellLanguagePacks(value); installAppLanguagePacks(value); return value; });
  return <main className="flex min-h-dvh items-center justify-center p-4"><section role="alert" data-testid="shell-config-error" className="w-full max-w-md space-y-3 rounded-xl border border-border bg-card p-6">
    <h1 className="text-xl font-semibold">{i18n.t('app.configError')}</h1><p className="text-sm text-muted-foreground">{i18n.t('app.configErrorHelp')}</p>
  </section></main>;
}

/** 偏好只存非敏感字段；运行时配置的 locale 只在浏览器没有保存过偏好时作缺省，用户偏好优先。 */
function preferencesFor(conf: AppConf) {
  let saved = false;
  const storage: PreferenceStorage | null = (() => {
    try {
      const local = window.localStorage;
      return { getItem: key => { const value = local.getItem(key); saved = value !== null; return value; }, setItem: (key, value) => local.setItem(key, value) };
    } catch { return null; }
  })();
  const store = initPreferences({ namespace: 'qian-shell', storage });
  if (!saved && conf.locale) store.update({ locale: conf.locale });
  return store;
}

export function App({ conf }: { conf: AppConf }) {
  const [preferences] = useState(() => preferencesFor(conf));
  return <PreferencesProvider store={preferences}><Shell conf={conf} /></PreferencesProvider>;
}

function Shell({ conf }: { conf: AppConf }) {
  const { preferences } = usePreferences();
  const [i18n] = useState(() => { const value = createI18n(preferences.locale); installShellLanguagePacks(value); installAppLanguagePacks(value); return value; });
  useEffect(() => { const controller = initializeTheme({ defaultTheme: preferences.theme }); controller.setTheme(preferences.theme); return () => controller.dispose(); }, [preferences.theme]);
  const [expired, setExpired] = useState(false);
  const router = useMemo(() => createBrowserRouter(), []);
  const path = useRoutePath(router);
  const [session, setSession] = useState<BffSession | null | undefined>(undefined);
  const [signedOut, setSignedOut] = useState(false);
  const [catalog, setCatalog] = useState<Catalog>({ navigation: [], permissions: [], capabilities: -1 });
  const [status, setStatus] = useState<LoadStatus>('idle');
  // 浏览器零令牌：会话只是 HttpOnly Cookie；SDK 的 sessionProvider 把并发 401 合并成一次 POST {authBase}/session/refresh，失败只弹一次过期对话框；
  // 成功（令牌换新、身份修订可能已变）则重拉菜单、权限与能力目录并刷新会话快照（裁决 ④）。目录在登录、切换租户（回调后整页重载）、会话刷新成功后
  // 与手动刷新时一起重拉；平台身份（tenantId 为空）没有租户表面，不拉。过期对话框弹出之后不再发起新一轮（见 createWorkspace）。
  const bff = useMemo(() => createBffSessionClient({ authBase: conf.authBase }), [conf.authBase]);
  const load = useMemo(() => createWorkspace(location.origin + conf.apiBase, bff, {
    onStatus: setStatus, onCatalog: setCatalog, onSession: setSession, onExpired: () => setExpired(true),
  }), [conf.apiBase, bff]);
  useEffect(() => {
    const controller = new AbortController();
    bff.getSession(controller.signal).then(value => {
      if (controller.signal.aborted) return;
      if (!value) { location.assign(bff.loginUrl(router.getPath())); return; }
      setSession(value);
      if (value.tenantId === null) setStatus('ready'); else void load();
    }).catch(() => { if (!controller.signal.aborted) { setSession(null); setStatus(500); } });
    return () => controller.abort();
  }, [bff, router, load]);
  const menu = useMemo(() => toMenu(catalog.navigation), [catalog.navigation]);
  const match = useMemo(() => matchMenuItem(menu, path), [menu, path]);
  const signOut = () => { void bff.signOut().finally(() => { setSignedOut(true); setSession(null); }); };
  // 切换租户：顶层跳到依赖方登录端点并带 prompt=select_account，由身份服务的同源选择页换会话，回调重建 Cookie 会话后整页回到工作台。
  const switchTenant = () => location.assign(bff.loginUrl('/', { prompt: 'select_account' }));

  if (signedOut) return <main className="flex min-h-dvh items-center justify-center p-4"><Card className="w-full max-w-md space-y-4 p-6" testId="shell-signed-out">
    <p>{i18n.t('app.signedOut')}</p><Button onClick={() => location.assign(bff.loginUrl('/'))}>{i18n.t('shell.login')}</Button>
  </Card></main>;
  if (session === undefined) return <main className="p-6"><Spinner label={i18n.t('common.loading')} /></main>;
  if (session === null) return <main className="p-6"><FallbackPage status={500} i18n={i18n} onRetry={() => location.reload()} /></main>;
  // 平台身份由平台代管：只有代管横幅与会话卡片，没有租户的菜单、权限与能力目录，也不会发出会触发过期对话框的请求。
  const platform = session.tenantId === null;
  const content = status === 403 ? <FallbackPage status={403} i18n={i18n} onRetry={() => void load()} />
    : status === 500 ? <FallbackPage status={500} i18n={i18n} onRetry={() => void load()} />
    : path === '/' ? <Home i18n={i18n} session={session} menu={menu} permissions={catalog.permissions} capabilities={catalog.capabilities} loading={status === 'loading'} platform={platform} onReload={() => void load()} />
    : match ? <RoutePage i18n={i18n} item={match.item} />
    : <FallbackPage status={404} i18n={i18n} onHome={() => router.navigate('/')} />;
  return <>
    <AppShell i18n={i18n} brand={conf.brand ?? 'Qian'} menu={menu} activePath={match?.href ?? path} onNavigate={href => router.navigate(href)}
      toolbar={<><InboxPopover i18n={i18n} messages={[]} /><PreferenceControls i18n={i18n} /></>}
      userName={session.displayName ?? session.subject} onSignOut={signOut} managed={conf.managed ?? platform}
      userMenu={<MenuItem onClick={switchTenant} testId="shell-switch-tenant">{i18n.t('shell.tenant')}</MenuItem>}>
      <OfflineBanner i18n={i18n} />
      <ShellErrorBoundary i18n={i18n} resetKey={path}>{content}</ShellErrorBoundary>
    </AppShell>
    <SessionExpiredDialog open={expired} i18n={i18n} onSignIn={() => location.assign(bff.loginUrl(router.getPath()))} />
  </>;
}

interface HomeProps { i18n: PlatformI18n; session: BffSession; menu: readonly ShellMenuItem[]; permissions: readonly PermissionGrant[]; capabilities: number; loading: boolean; platform: boolean; onReload: () => void }
function Home({ i18n, session, menu, permissions, capabilities, loading, platform, onReload }: HomeProps) {
  return <Page header={<PageHeader title={i18n.t('app.workspace')} description={i18n.t(platform ? 'app.platformHelp' : 'app.workspaceHelp')}
    actions={platform ? undefined : <Button variant="outline" loading={loading} onClick={onReload} testId="shell-reload">{i18n.t('app.refresh')}</Button>} />}>
    <div className="grid gap-4 md:grid-cols-2">
      <Card className="space-y-2 p-5" testId="shell-session">
        <h2 className="text-lg font-semibold">{i18n.t('app.session')}</h2>
        <dl className="grid grid-cols-[auto_minmax(0,1fr)] gap-x-4 gap-y-1 text-sm">
          <dt className="text-muted-foreground">{i18n.t('app.displayName')}</dt><dd data-field="displayName">{session.displayName ?? '—'}</dd>
          <dt className="text-muted-foreground">{i18n.t('app.subject')}</dt><dd className="truncate" data-field="subject">{session.subject}</dd>
          <dt className="text-muted-foreground">{i18n.t('app.tenant')}</dt><dd className="truncate" data-field="tenantId">{session.tenantId ?? i18n.t('shell.platform')}</dd>
          <dt className="text-muted-foreground">{i18n.t('app.kind')}</dt><dd data-field="kind">{session.kind}</dd>
          <dt className="text-muted-foreground">{i18n.t('app.revision')}</dt><dd data-field="identityRevision">{session.identityRevision ?? '—'}</dd>
        </dl>
      </Card>
      {!platform && <Card className="space-y-2 p-5" testId="shell-permissions">
        <h2 className="text-lg font-semibold">{i18n.t('app.permissions')}</h2>
        <p className="text-sm text-muted-foreground" data-testid="shell-capabilities">{capabilities < 0 ? i18n.t('common.loading') : i18n.t('app.capabilities', { count: capabilities })}</p>
        {permissions.length === 0 ? <p className="text-sm text-muted-foreground">{i18n.t('common.empty')}</p>
          : <ul className="space-y-1 text-sm">{permissions.map(grant => <li key={grant.permission} data-permission={grant.permission} data-allowed={grant.allowed}><code>{grant.permission}</code>{!grant.allowed && <span className="ml-2 text-muted-foreground">{grant.reason}</span>}</li>)}</ul>}
      </Card>}
      {!platform && <Card className="space-y-2 p-5 md:col-span-2" testId="shell-pages">
        <h2 className="text-lg font-semibold">{i18n.t('app.pages')}</h2>
        {menu.length === 0 ? <p className="text-sm text-muted-foreground">{i18n.t('common.empty')}</p>
          : <ul className="space-y-1 text-sm">{menu.map(item => <li key={item.id} data-route={item.target}>{item.label ?? item.labelKey}<code className="ml-2 text-muted-foreground">{item.target}</code></li>)}</ul>}
      </Card>}
    </div>
  </Page>;
}

/** 菜单命中的页面：内容由该组件的远程模块承载（S1-YUE-001 接线），薄入口只给出页头与占位。 */
function RoutePage({ i18n, item }: { i18n: PlatformI18n; item: ShellMenuItem }) {
  return <Page header={<PageHeader title={item.label ?? i18n.t(item.labelKey)} description={item.target} />}>
    <Alert testId="shell-remote-pending">{i18n.t('app.remotePending')}</Alert>
  </Page>;
}
