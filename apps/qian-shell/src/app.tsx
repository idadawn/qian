import { lazy, Suspense, useEffect, useState, useSyncExternalStore, type ComponentType } from 'react';
import { Alert, Avatar, AvatarFallback, Breadcrumb, Button, Icon, MenuItem, Spinner } from '@idadawn/yue-core/shell';
import { createI18n, type PlatformClient, type PlatformI18n } from '@idadawn/yue-sdk';
import { initializeTheme } from '@idadawn/yue-tokens';
import { AppShell, LocaleMenu, Page, PreferenceControls, WorkspaceCard } from '@idadawn/yue-shell/layout';
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
import type { TenantSummary } from './welcome';

// 会话过期对话框只在会话结束时需要：按需加载，不进首屏。
const SessionExpiredDialog = lazy(() => import('@idadawn/yue-shell/session-dialog').then(module => ({ default: module.SessionExpiredDialog })));
// 平台身份的首页（平台控制台，S1-YUE-018）只给平台会话：同样按需加载，不进租户会话的首屏。
const PlatformHome = lazy(() => import('./console').then(module => ({ default: module.PlatformHome })));
// 企业首页的欢迎区要等本租户摘要（bootstrap 之后才拉），会话信息页从账号菜单进入：两者都按需加载，不占首屏预算。
const Welcome = lazy(() => import('./welcome').then(module => ({ default: module.Welcome })));
const SessionPage = lazy(() => import('./session').then(module => ({ default: module.SessionPage })));
type PageComponent = ComponentType<{ client: PlatformClient; bridge: RemoteBridge }>;
type RemotePage = { route: string; contribution?: SlotContribution<PageComponent>; failed?: boolean };
// 外壳自带的两个页面：首页（工作台）是侧栏第一项；会话信息（会话、有效权限与可用页面）从账号菜单进入，菜单里有同路由的远程页面时让给远程。
const HOME = 'qian:home';
const SESSION_PATH = '/session';

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
  const nav: ShellMenuItem[] = bootstrap?.navigation ? toMenu(bootstrap.navigation)
    : runtime.host.catalog('qian.shell.nav/v1').map(({ descriptor }) => ({ id: descriptor.id, labelKey: descriptor.id, label: descriptor.title, type: 'page', target: descriptor.route! }));
  // 侧栏第一项固定是工作台（根路径只精确命中，见 matchMenuItem）；能力导航跟在后面。
  const menu: ShellMenuItem[] = [{ id: HOME, labelKey: 'app.workspace', label: i18n.t('app.workspace'), type: 'page', target: '/', icon: 'home' }, ...nav];
  const match = matchMenuItem(menu, path);
  const remote = match && match.item.id !== HOME ? match : undefined;
  const page = useRemotePage(runtime, remote?.item.target);

  if (state.signedOut) return <main className="flex min-h-dvh items-center justify-center p-4"><section className="w-full max-w-md space-y-4 rounded-xl border border-border bg-card p-6" data-testid="shell-signed-out">
    <p>{i18n.t('app.signedOut')}</p><Button onClick={() => runtime.signIn()}>{i18n.t('shell.login')}</Button>
  </section></main>;
  // 工作台从未就绪就结束了会话（例如平台表面持续 401）：只留过期对话框，背后不再转加载圈。
  if (!bootstrap) return <main className="p-6">{state.expired ? null : state.status === 'loading' ? <Spinner label={i18n.t('common.loading')} />
    : <FallbackPage status={state.status === 403 ? 403 : 500} i18n={i18n} onRetry={() => location.reload()} />}{state.expired && <Expired runtime={runtime} i18n={i18n} />}</main>;

  const session = bootstrap.session;
  const platform = session.tenantId === null;
  const sessionPage = !platform && !remote && pathname === SESSION_PATH;
  const content = state.status === 403 ? <FallbackPage status={403} i18n={i18n} onRetry={() => void runtime.load()} />
    : state.status === 500 ? <FallbackPage status={500} i18n={i18n} onRetry={() => void runtime.load()} />
    : pathname === '/' ? platform
      ? <Suspense fallback={<Spinner label={i18n.t('common.loading')} />}><PlatformHome i18n={i18n} session={session} menu={nav} managed={bootstrap.managed} onNavigate={href => runtime.router.navigate(href)} /></Suspense>
      : <Home i18n={i18n} session={session} menu={nav} summary={state.summary} onNavigate={target => runtime.router.navigate(target)} />
    : remote ? <Remote i18n={i18n} page={page} client={runtime.client} />
    : sessionPage ? <Suspense fallback={<Page><Spinner label={i18n.t('common.loading')} /></Page>}><SessionPage i18n={i18n} session={session} menu={nav} permissions={bootstrap.permissions} capabilities={state.capabilities} loading={state.reloading} onReload={() => void runtime.load()} /></Suspense>
    : <FallbackPage status={404} i18n={i18n} onHome={() => runtime.router.navigate('/')} />;
  // 面包屑：分组（有的话）› 当前页；首页与会话信息页只有一级。
  const current = pathname === '/' ? i18n.t('app.workspace') : remote ? remote.item.label ?? i18n.t(remote.item.labelKey) : sessionPage ? i18n.t('app.sessionInfo') : undefined;
  const breadcrumb = current === undefined ? undefined
    : <Breadcrumb label={i18n.t('app.breadcrumb')} testId="shell-breadcrumb" items={[...(remote?.item.group ? [{ key: 'group', label: remote.item.group }] : []), { key: 'page', label: current }]} />;
  return <>
    <AppShell i18n={i18n} brand={runtime.config.brand ?? 'Qian'} menu={menu} activePath={match?.href ?? pathname} onNavigate={href => runtime.router.navigate(href)}
      workspace={<Workspace i18n={i18n} platform={platform} summary={state.summary} onSwitch={() => runtime.switchTenant()} />} breadcrumb={breadcrumb}
      toolbar={<><InboxPopover i18n={i18n} messages={[]} /><PreferenceControls i18n={i18n} locale={false} /></>}
      userName={session.displayName ?? session.subject} userDescription={platform ? i18n.t('shell.platform') : undefined} onSignOut={() => void runtime.signOut()} managed={runtime.config.managed ?? bootstrap.managed}
      userMenu={<>
        <MenuItem onClick={() => runtime.switchTenant()} testId="shell-switch-tenant"><Icon name="building" />{i18n.t('shell.tenant')}</MenuItem>
        {!platform && <MenuItem onClick={() => runtime.router.navigate(SESSION_PATH)} testId="shell-session-info"><Icon name="user" />{i18n.t('app.sessionInfo')}</MenuItem>}
        <LocaleMenu i18n={i18n} testId="shell-locale" />
      </>}>
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

/**
 * 侧栏的工作空间卡片：平台会话是「平台控制台 · 平台运营」，租户会话是企业名称与「状态 · 租户号」（本租户摘要不可用时只写「企业工作空间」）。
 * 点按经 prompt=select_account 回到身份服务的选择页切换。
 */
function Workspace({ i18n, platform, summary, onSwitch }: { i18n: PlatformI18n; platform: boolean; summary: TenantSummary | null | undefined; onSwitch(): void }) {
  if (platform) return <WorkspaceCard title={i18n.t('app.platformConsole')} description={i18n.t('app.platformOperations')} label={i18n.t('app.switchWorkspace')} onClick={onSwitch} testId="shell-workspace"
    tile={<span className="inline-flex size-8 items-center justify-center rounded-md bg-primary text-primary-foreground"><Icon name="shield" size={18} /></span>} />;
  const initial = Array.from(summary?.name.trim() ?? '')[0];
  return <WorkspaceCard title={summary?.name ?? i18n.t('app.tenantWorkspace')} label={i18n.t('app.switchWorkspace')} onClick={onSwitch} testId="shell-workspace"
    description={summary ? <>{i18n.t(`app.workspaceStatus.${summary.status}`)} · <span className="font-mono">{summary.code}</span></> : undefined}
    tile={<Avatar shape="square" tint="primary"><AvatarFallback className="font-bold">{initial ?? <Icon name="building" />}</AvatarFallback></Avatar>} />;
}

/** 租户会话的首页（平台身份的首页是 console.tsx 的平台控制台）：问候，本租户摘要可用时再给试用提示、开通引导与企业信息（S1-TEN-021）。 */
function Home({ i18n, session, menu, summary, onNavigate }: { i18n: PlatformI18n; session: BffSession; menu: readonly ShellMenuItem[]; summary: ShellState['summary']; onNavigate(target: string): void }) {
  // 试用剩余天数按进入首页的时刻算一次。
  const [now] = useState(() => Date.now());
  return <Page testId="shell-home"><Suspense fallback={null}><Welcome i18n={i18n} summary={summary} displayName={session.displayName ?? session.subject} menu={menu} now={now} onNavigate={onNavigate} /></Suspense></Page>;
}
