import { lazy, Suspense, useEffect, useState, useSyncExternalStore, type ComponentType } from 'react';
import { Alert, Button, MenuItem, Spinner } from '@idadawn/yue-core/shell';
import { createI18n, type PlatformClient, type PlatformI18n } from '@idadawn/yue-sdk';
import { initializeTheme } from '@idadawn/yue-tokens';
import { AppShell, Page, PageHeader, PreferenceControls } from '@idadawn/yue-shell/layout';
import { initPreferences, PreferencesProvider, usePreferences } from '@idadawn/yue-shell/preferences';
import { FallbackPage, OfflineBanner, ShellErrorBoundary } from '@idadawn/yue-shell/fallback';
import { installShellLanguagePacks } from '@idadawn/yue-shell/locales';
import { InboxPopover } from '@idadawn/yue-shell/inbox';
import { matchMenuItem, useRoutePath } from '@idadawn/yue-shell/router';
import type { ShellMenuItem } from '@idadawn/yue-shell/menu';
import { toMenu, type BffSession } from '@qian/shell-kit';
import type { RemoteBridge, SlotContribution } from '@idadawn/qian-plugin-sdk';
import { installAppLanguagePacks } from './locales';
import type { ShellRuntime, ShellState } from './runtime';

// 会话过期对话框只在会话结束时需要：按需加载，不进首屏。
const SessionExpiredDialog = lazy(() => import('@idadawn/yue-shell/session-dialog').then(module => ({ default: module.SessionExpiredDialog })));
type PageComponent = ComponentType<{ client: PlatformClient; bridge: RemoteBridge }>;
type RemotePage = { route: string; contribution?: SlotContribution<PageComponent>; failed?: boolean };

function languages(locale?: Parameters<typeof createI18n>[0]): PlatformI18n {
  const i18n = createI18n(locale); installShellLanguagePacks(i18n); installAppLanguagePacks(i18n);
  return i18n;
}

/** 运行时配置非法时的唯一界面：不建客户端、不发请求。 */
export function ConfigError() {
  const [i18n] = useState(() => languages());
  return <main className="flex min-h-dvh items-center justify-center p-4"><section role="alert" data-testid="shell-config-error" className="w-full max-w-md space-y-3 rounded-xl border border-border bg-card p-6">
    <h1 className="text-xl font-semibold">{i18n.t('app.configError')}</h1><p className="text-sm text-muted-foreground">{i18n.t('app.configErrorHelp')}</p>
  </section></main>;
}

/** 偏好只存三项非敏感字段（命名空间 qian-shell）；运行时配置的 locale 只是部署缺省：用户保存过的偏好优先，且不写进偏好存储。 */
export function ShellApplication({ runtime }: { runtime: ShellRuntime }) {
  const [preferences] = useState(() => initPreferences({ namespace: 'qian-shell', defaults: runtime.config.locale ? { locale: runtime.config.locale } : undefined }));
  return <PreferencesProvider store={preferences}><Shell runtime={runtime} /></PreferencesProvider>;
}

function Shell({ runtime }: { runtime: ShellRuntime }) {
  const { preferences } = usePreferences();
  const [i18n] = useState(() => languages(preferences.locale));
  const state = useSyncExternalStore(runtime.subscribe, runtime.getState, runtime.getState);
  // 订阅宿主 revision：远程登记 / 卸载、上下文刷新后重算菜单与当前页面（返回值本身不用）。
  useSyncExternalStore(runtime.host.subscribe, runtime.host.getRevision, runtime.host.getRevision);
  const path = useRoutePath(runtime.router);
  const pathname = new URL(path, location.origin).pathname;
  // 语言：html[lang] 与 i18n 都跟随偏好（顶栏切换、或另一处改了偏好）；主题：首帧脚本已按存储设好，这里接管之后的切换。
  useEffect(() => {
    document.documentElement.lang = preferences.locale;
    if (i18n.instance.language !== preferences.locale) void i18n.changeLanguage(preferences.locale);
  }, [i18n, preferences.locale]);
  useEffect(() => {
    const controller = initializeTheme({ defaultTheme: preferences.theme }); controller.setTheme(preferences.theme);
    runtime.present(preferences.locale, document.documentElement.dataset.theme === 'dark' ? 'dark' : 'light');
    return () => controller.dispose();
  }, [runtime, preferences.theme, preferences.locale]);

  const bootstrap = state.bootstrap;
  // 菜单：真实平台给出能力导航（含菜单偏好与权限过滤）时以它为准；没有（下游失败或验收夹具）时由宿主按已登记远程的清单、挂载、权益与权限算出（随宿主 revision 变化）。
  const menu: ShellMenuItem[] = bootstrap?.navigation ? toMenu(bootstrap.navigation)
    : runtime.host.catalog('qian.shell.nav/v1').map(({ descriptor }) => ({ id: descriptor.id, labelKey: descriptor.id, label: descriptor.title, type: 'page', target: descriptor.route! }));
  const match = matchMenuItem(menu, path);
  const page = useRemotePage(runtime, match?.item.target);

  if (state.signedOut) return <main className="flex min-h-dvh items-center justify-center p-4"><section className="w-full max-w-md space-y-4 rounded-xl border border-border bg-card p-6" data-testid="shell-signed-out">
    <p>{i18n.t('app.signedOut')}</p><Button onClick={() => runtime.signIn()}>{i18n.t('shell.login')}</Button>
  </section></main>;
  // 工作台从未就绪就结束了会话（例如平台表面持续 401）：只留过期对话框，背后不再转加载圈。
  if (!bootstrap) return <main className="p-6">{state.expired ? null : state.status === 'loading' ? <Spinner label={i18n.t('common.loading')} />
    : <FallbackPage status={state.status === 403 ? 403 : 500} i18n={i18n} onRetry={() => location.reload()} />}{state.expired && <Expired runtime={runtime} i18n={i18n} />}</main>;

  const session = bootstrap.session;
  const platform = session.tenantId === null;
  const content = state.status === 403 ? <FallbackPage status={403} i18n={i18n} onRetry={() => void runtime.load()} />
    : state.status === 500 ? <FallbackPage status={500} i18n={i18n} onRetry={() => void runtime.load()} />
    : pathname === '/' ? <Home i18n={i18n} session={session} menu={menu} permissions={bootstrap.permissions} capabilities={state.capabilities} loading={state.reloading} platform={platform} onReload={() => void runtime.load()} />
    : match ? <Remote i18n={i18n} page={page} client={runtime.client} />
    : <FallbackPage status={404} i18n={i18n} onHome={() => runtime.router.navigate('/')} />;
  return <>
    <AppShell i18n={i18n} brand={runtime.config.brand ?? 'Qian'} menu={menu} activePath={match?.href ?? pathname} onNavigate={href => runtime.router.navigate(href)}
      toolbar={<><InboxPopover i18n={i18n} messages={[]} /><PreferenceControls i18n={i18n} /></>}
      userName={session.displayName ?? session.subject} onSignOut={() => void runtime.signOut()} managed={runtime.config.managed ?? bootstrap.managed}
      userMenu={<MenuItem onClick={() => runtime.switchTenant()} testId="shell-switch-tenant">{i18n.t('shell.tenant')}</MenuItem>}>
      <OfflineBanner i18n={i18n} />
      <ShellErrorBoundary i18n={i18n} resetKey={pathname}>{content}</ShellErrorBoundary>
    </AppShell>
    {state.expired && <Expired runtime={runtime} i18n={i18n} />}
  </>;
}

function Expired({ runtime, i18n }: { runtime: ShellRuntime; i18n: PlatformI18n }) {
  return <Suspense fallback={null}><SessionExpiredDialog open i18n={i18n} onSignIn={() => runtime.signIn()} /></Suspense>;
}

/**
 * 菜单命中的页面：在宿主目录里按路由找到对应远程的页面贡献，按需 loadSlot（未挂载、无权益或无权限的远程完全不请求）。
 * 已加载的贡献只要还在宿主里（同一个 bridge：没有被卸载、换版本或切走 scope）就继续用，重拉 bootstrap 不会让页面重新挂载；
 * 宿主 revision 变化会让外壳重渲染、这里随之重算是否仍有效。只在「页面变了」或「已加载的失效了」时才再加载——
 * bootstrap 期间宿主 revision 会连跳几次，若随 revision 重跑，同一页面会并发加载两次（被拒的远程就会上报两条审计）。
 * 加载失败对同一页面是粘性的，离开再回来（或整页刷新）才重试。
 */
function useRemotePage(runtime: ShellRuntime, route: string | undefined): RemotePage | undefined {
  const [loaded, setLoaded] = useState<RemotePage & { key: string }>();
  const descriptor = route ? runtime.host.catalog('qian.shell.nav/v1').find(item => item.descriptor.route === route) : undefined;
  const key = descriptor ? `${descriptor.scope}|${descriptor.remoteId}|${descriptor.descriptor.id}` : undefined;
  const live = !!loaded && loaded.key === key
    && (!!loaded.failed || runtime.host.list<PageComponent>('qian.shell.nav/v1').some(item => item.bridge === loaded.contribution?.bridge));
  // 副作用只依赖字符串原值：宿主 revision 变化会换掉目录项对象，但页面标识不变，不会重新加载（见上）。
  const slotId = descriptor?.descriptor.id; const slotScope = descriptor?.scope; const slotRemote = descriptor?.remoteId; const slotRoute = descriptor?.descriptor.route;
  useEffect(() => {
    if (slotId === undefined || slotRoute === undefined || !key || live) return;
    let cancelled = false;
    void runtime.host.loadSlot('qian.shell.nav/v1', slotId).then(() => {
      if (cancelled) return;
      const contribution = runtime.host.list<PageComponent>('qian.shell.nav/v1').find(item => item.descriptor.id === slotId && item.scope === slotScope && item.remoteId === slotRemote);
      setLoaded({ key, route: slotRoute, contribution, failed: !contribution });
    }).catch(() => { if (!cancelled) setLoaded({ key, route: slotRoute, failed: true }); });
    return () => { cancelled = true; };
  }, [runtime, key, live, slotId, slotScope, slotRemote, slotRoute]);
  if (!route) return undefined;
  if (!descriptor) return { route, failed: true };
  return live ? loaded : { route };
}

function Remote({ i18n, page, client }: { i18n: PlatformI18n; page: RemotePage | undefined; client: PlatformClient }) {
  if (page?.failed) return <Page><Alert tone="error" role="alert">{i18n.t('app.remoteFailed')}</Alert></Page>;
  const contribution = page?.contribution;
  if (!contribution) return <Page><Spinner label={i18n.t('common.loading')} /></Page>;
  const Component = contribution.implementation;
  return <Component key={`${contribution.scope}:${contribution.remoteId}`} client={client} bridge={contribution.bridge} />;
}

interface HomeProps { i18n: PlatformI18n; session: BffSession; menu: readonly ShellMenuItem[]; permissions: readonly string[]; capabilities: ShellState['capabilities']; loading: boolean; platform: boolean; onReload: () => void }
function Home({ i18n, session, menu, permissions, capabilities, loading, platform, onReload }: HomeProps) {
  const card = 'space-y-2 rounded-xl border border-border bg-card p-5';
  return <Page header={<PageHeader title={i18n.t('app.workspace')} description={i18n.t(platform ? 'app.platformHelp' : 'app.workspaceHelp')}
    actions={platform ? undefined : <Button variant="outline" loading={loading} onClick={onReload} testId="shell-reload">{i18n.t('app.refresh')}</Button>} />}>
    <div className="grid gap-4 md:grid-cols-2">
      <section className={card} data-testid="shell-session">
        <h2 className="text-lg font-semibold">{i18n.t('app.session')}</h2>
        <dl className="grid grid-cols-[auto_minmax(0,1fr)] gap-x-4 gap-y-1 text-sm">
          <dt className="text-muted-foreground">{i18n.t('app.displayName')}</dt><dd data-field="displayName">{session.displayName ?? '—'}</dd>
          <dt className="text-muted-foreground">{i18n.t('app.subject')}</dt><dd className="truncate" data-field="subject">{session.subject}</dd>
          <dt className="text-muted-foreground">{i18n.t('app.tenant')}</dt><dd className="truncate" data-field="tenantId">{session.tenantId ?? i18n.t('shell.platform')}</dd>
          <dt className="text-muted-foreground">{i18n.t('app.kind')}</dt><dd data-field="kind">{session.kind}</dd>
          <dt className="text-muted-foreground">{i18n.t('app.revision')}</dt><dd data-field="identityRevision">{session.identityRevision === null ? '—' : String(session.identityRevision)}</dd>
        </dl>
      </section>
      {!platform && <section className={card} data-testid="shell-permissions">
        <h2 className="text-lg font-semibold">{i18n.t('app.permissions')}</h2>
        <p className="text-sm text-muted-foreground" data-testid="shell-capabilities">{capabilities === undefined ? i18n.t('common.loading') : capabilities === null ? i18n.t('app.capabilitiesUnavailable') : i18n.t('app.capabilities', { count: capabilities })}</p>
        {permissions.length === 0 ? <p className="text-sm text-muted-foreground">{i18n.t('common.empty')}</p>
          : <ul className="space-y-1 text-sm">{permissions.map(permission => <li key={permission} data-permission={permission} data-allowed="true"><code>{permission}</code></li>)}</ul>}
      </section>}
      {!platform && <section className={`${card} md:col-span-2`} data-testid="shell-pages">
        <h2 className="text-lg font-semibold">{i18n.t('app.pages')}</h2>
        {menu.length === 0 ? <p className="text-sm text-muted-foreground">{i18n.t('common.empty')}</p>
          : <ul className="space-y-1 text-sm">{menu.map(item => <li key={item.id} data-route={item.target}>{item.label ?? item.labelKey}<code className="ml-2 text-muted-foreground">{item.target}</code></li>)}</ul>}
      </section>}
    </div>
  </Page>;
}
