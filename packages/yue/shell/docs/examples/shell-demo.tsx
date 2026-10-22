import { useEffect, useState } from 'react';
import { Button, Card } from '@idadawn/yue-core';
import { createI18n, createMockTransport, PlatformClient } from '@idadawn/yue-sdk';
import { initializeTheme } from '@idadawn/yue-tokens';
import { AppShell, Authentication, ColPage, FallbackPage, initPreferences, installShellLanguagePacks, LeftTree, OfflineBanner, Page, PageHeader, PreferenceControls, PreferencesProvider, SessionExpiredDialog, ShellErrorBoundary, TenantSwitcher, UpdateBanner, usePreferences, type AuthProvider, type AuthSession, type FallbackStatus } from '@idadawn/yue-shell';

interface DemoProps { kind?: string; status?: number | string; authenticated?: boolean; multiTenant?: boolean; callbackFailure?: boolean; managed?: boolean; offline?: boolean; invalid?: boolean; disabled?: boolean }
type Post<Input, Output> = { post: { requestBody: { content: { 'application/json': Input } }; responses: { 200: { content: { 'application/json': Output } } } } };
type SessionResponse = Omit<AuthSession, 'tenants'> & { tenants: { id: string; name: string }[] };
type AuthResponse = { kind: 'authenticated'; session: SessionResponse } | { kind: 'select-tenant'; tenants: { id: string; name: string }[] };
interface Paths {
  '/login': Post<{ username: string; password: string }, AuthResponse>;
  '/sso/start': Post<object, { url: string }>;
  '/sso/callback': Post<{ code?: string; state?: string; error?: string }, AuthResponse>;
  '/tenants/select': Post<{ tenantId: string }, SessionResponse>;
  '/tenants/switch': Post<{ tenantId: string }, SessionResponse>;
  '/session/refresh': Post<object, { accessToken: string }>;
  '/session/ping': { get: { responses: { 200: { content: { 'application/json': { tenantId: string; authorized: boolean } } } } } };
}
const tenants = [{ id: 'tenant-a', name: '研发租户' }, { id: 'tenant-b', name: '生产租户' }];
const initialSession: AuthSession = { subjectId: 'demo-user', displayName: '演示用户', tenantId: 'tenant-a', tenants };

/** 示例只替换 SDK transport；真实宿主实现 AuthProvider，无需改变认证 UI。 */
function createDemoProvider(props: DemoProps, onExpired: () => void) {
  let token: string | null = props.authenticated ? 'mock-access-only-in-memory' : null;
  let tenantId = 'tenant-a';
  let forceExpired = false;
  const response = () => ({ ...initialSession, tenantId, tenants });
  const problem = (status: number) => Response.json({ type: 'https://qian.dev/problems/protocol/INPUT_INVALID', title: '输入无效', status, code: 'INPUT_INVALID', message: '演示请求未通过', trace_id: 'a'.repeat(32) }, { status });
  const authenticated = (): AuthResponse => {
    forceExpired = false;
    if (props.multiTenant) return { kind: 'select-tenant', tenants };
    token = 'mock-access-only-in-memory'; return { kind: 'authenticated', session: response() };
  };
  const transport = createMockTransport([
    { method: 'POST', path: '/login', respond: async request => { const value = await request.json() as { username: string; password: string }; return value.username === 'demo' && value.password === 'demo-password' ? Response.json(authenticated()) : problem(400); } },
    { method: 'POST', path: '/sso/start', respond: () => {
      const callback = new URL(location.href); callback.searchParams.set('code', 'mock-code'); callback.searchParams.set('auth_state', 'mock-state');
      if (props.callbackFailure) callback.searchParams.set('error', 'access_denied');
      return Response.json({ url: callback.pathname + callback.search });
    } },
    { method: 'POST', path: '/sso/callback', respond: async request => { const value = await request.json() as { code: string; state: string }; return value.code === 'mock-code' && value.state === 'mock-state' && !props.callbackFailure ? Response.json(authenticated()) : problem(400); } },
    ...['/tenants/select', '/tenants/switch'].map(path => ({ method: 'POST', path, respond: async (request: Request) => {
      const value = await request.json() as { tenantId: string };
      if (!tenants.some(tenant => tenant.id === value.tenantId)) return problem(400);
      tenantId = value.tenantId; token = 'mock-access-only-in-memory'; forceExpired = false; return Response.json(response());
    } })),
    { method: 'POST', path: '/session/refresh', respond: () => forceExpired ? problem(401) : Response.json({ accessToken: 'mock-refreshed-only-in-memory' }) },
    { method: 'GET', path: '/session/ping', respond: request => forceExpired || !request.headers.get('Authorization') ? problem(401) : Response.json({ tenantId: request.headers.get('X-Tenant-Id'), authorized: true }) },
  ]);
  const auth = new PlatformClient({ baseUrl: 'https://shell.mock', transport }).api<Paths>();
  const api = new PlatformClient({ baseUrl: 'https://shell.mock', transport, getTenantId: () => tenantId, onSessionExpired: onExpired, tokenProvider: {
    getAccessToken: () => token,
    refreshAccessToken: async () => { const result = await auth.POST('/session/refresh', { body: {} }); token = result.data!.accessToken; return token; },
  } }).api<Paths>();
  const provider: AuthProvider = {
    signIn: async (credentials, signal) => (await auth.POST('/login', { body: credentials, signal })).data!,
    beginSso: async signal => (await auth.POST('/sso/start', { body: {}, signal })).data!.url,
    completeSso: async (callback, signal) => (await auth.POST('/sso/callback', { body: callback, signal })).data!,
    selectTenant: async (id, signal) => (await auth.POST('/tenants/select', { body: { tenantId: id }, signal })).data!,
    switchTenant: async (id, signal) => (await auth.POST('/tenants/switch', { body: { tenantId: id }, signal })).data!,
  };
  return { provider, ping: () => api.GET('/session/ping'), expire: () => { forceExpired = true; }, signOut: () => { token = null; } };
}

function BrokenPage(): never { throw new Error('internal-stack-must-not-be-rendered'); }
function Demo({ initial }: { initial: DemoProps }) {
  const { preferences } = usePreferences();
  const [i18n] = useState(() => { const value = createI18n(preferences.locale); installShellLanguagePacks(value); value.addLanguagePack('zh-CN', { 'demo.home': '工作台', 'demo.settings': '应用设置', 'demo.docs': '开发文档' }); return value; });
  const [expired, setExpired] = useState(false);
  const [mock] = useState(() => createDemoProvider(initial, () => setExpired(true)));
  const [session, setSession] = useState<AuthSession | null>(initial.authenticated ? initialSession : null);
  const [path, setPath] = useState('/workspace');
  const [ping, setPing] = useState('');
  const [update, setUpdate] = useState(true);
  const [broken, setBroken] = useState(false);
  const [fallback, setFallback] = useState<FallbackStatus | null>((initial.status as FallbackStatus | undefined) ?? null);
  const [callback] = useState(() => { const query = new URLSearchParams(location.search); return query.has('code') || query.has('error') ? { code: query.get('code') ?? undefined, state: query.get('auth_state') ?? undefined, error: query.get('error') ?? undefined } : undefined; });
  useEffect(() => { const controller = initializeTheme({ storageKey: 'yue-shell-demo-theme', defaultTheme: preferences.theme }); controller.setTheme(preferences.theme); return () => controller.dispose(); }, [preferences.theme]);
  const clearCallback = () => { const url = new URL(location.href); for (const key of ['code', 'auth_state', 'error']) url.searchParams.delete(key); history.replaceState(null, '', url.pathname + url.search + url.hash); };
  const authenticate = (next: AuthSession) => { setSession(next); clearCallback(); };
  if (fallback) return <main><FallbackPage status={fallback} i18n={i18n} onHome={() => setFallback(null)} onRetry={() => { setFallback(null); setPing('已重试'); }} /></main>;
  if (!session && initial.kind !== 'FallbackPage') return <main className="space-y-4 p-4"><Authentication provider={mock.provider} i18n={i18n} callback={callback} onAuthenticated={authenticate} onRedirect={url => location.assign(url)} onCallbackConsumed={clearCallback} /><p className="text-center text-sm text-muted-foreground">演示账号 demo，密码 demo-password。认证请求使用 SDK mock。</p></main>;
  if (!session) return <main><p role="status">{ping || '已返回工作台'}</p></main>;
  return <><AppShell i18n={i18n} brand="Qian 工作台" className="min-h-96" menu={[{ id: 'home', labelKey: 'demo.home', type: 'page', target: '/workspace' }, { id: 'settings', labelKey: 'demo.settings', type: 'page', target: '/settings' }, { id: 'docs', labelKey: 'demo.docs', type: 'external', target: 'https://qian.dev/' }]} activePath={path} onNavigate={setPath} managed={initial.managed} userName={session.displayName} onSignOut={() => { mock.signOut(); setSession(null); }} toolbar={<><TenantSwitcher i18n={i18n} provider={mock.provider} session={session} onChanged={next => { setSession(next); setPing(''); }} /><PreferenceControls i18n={i18n} /></>}>
    <Page header={<PageHeader title={path === '/settings' ? '应用设置' : '工作台'} description="同一外壳维护导航、租户上下文与页面布局。" />}>
      <OfflineBanner i18n={i18n} offline={initial.offline} />
      <UpdateBanner i18n={i18n} available={update} onReload={() => setUpdate(false)} />
      <ColPage aside={<LeftTree label="资源目录"><p className="font-medium">资源目录</p><p className="mt-2 text-sm text-muted-foreground">由消费方在此插入 data Tree。</p></LeftTree>}>
        <Card className="space-y-4 p-5"><h2 className="text-lg font-semibold">当前会话</h2><p>当前租户：{session.tenants.find(tenant => tenant.id === session.tenantId)?.name}</p>
          <div className="flex flex-wrap gap-2"><Button onClick={() => { void mock.ping().then(({ data }) => setPing(`请求租户：${data!.tenantId}`)).catch(() => setPing('请求失败')); }}>读取受保护数据</Button><Button variant="outline" onClick={() => { mock.expire(); void mock.ping().catch(() => setPing('刷新失败')); }}>模拟会话过期</Button><Button variant="outline" onClick={() => setBroken(true)}>模拟渲染失败</Button></div><p role="status">{ping || '等待请求'}</p>
        </Card>
      </ColPage>
      <ShellErrorBoundary i18n={i18n} resetKey={broken} onReset={() => setBroken(false)}>{broken ? <BrokenPage /> : null}</ShellErrorBoundary>
    </Page>
  </AppShell><SessionExpiredDialog open={expired} i18n={i18n} onSignIn={() => { mock.signOut(); setExpired(false); setSession(null); }} /></>;
}

export default function ShellDemo(props: DemoProps) {
  const [preferences] = useState(() => initPreferences({ namespace: 'shell-demo', overrides: { theme: typeof document !== 'undefined' && document.documentElement.dataset.theme === 'dark' ? 'dark' : 'light' } }));
  return <PreferencesProvider store={preferences}><Demo initial={props} /></PreferencesProvider>;
}
export const SHELL_DEMOS = { AppShell: ShellDemo, Authentication: ShellDemo, FallbackPage: ShellDemo };
