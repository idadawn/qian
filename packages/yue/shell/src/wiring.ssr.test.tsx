import { describe, expect, it, vi } from 'vitest';
import { createBffSessionClient, createBrowserRouter, createIdentityAuthProvider, matchMenuItem, PLATFORM_TENANT, readAppConf, toMenu, type NavigationCatalogItem } from './index';

const problem = (status: number, code = 'UNAUTHENTICATED') => Response.json({ type: `https://qian.dev/problems/protocol/${code}`, title: code, status, code, message: '未认证', trace_id: 'a'.repeat(32) }, { status, headers: { 'content-type': 'application/problem+json' } });
type Call = { url: string; init: RequestInit | undefined };
function fakeFetch(handler: (url: string, init: RequestInit | undefined, calls: Call[]) => Response | Promise<Response>) {
  const calls: Call[] = [];
  const fetchImpl = vi.fn(async (input: string | URL | Request, init?: RequestInit) => { const url = String(input); calls.push({ url, init }); return handler(url, init, calls); });
  return { fetchImpl: fetchImpl as unknown as typeof fetch, calls };
}
const header = (init: RequestInit | undefined, name: string) => new Headers(init?.headers).get(name);

describe('运行时配置 window.__APP_CONF__', () => {
  it('缺省值、合法覆盖与拒绝的形状（跨源地址、结尾斜杠、查询串、非法语言）', () => {
    expect(readAppConf(undefined)).toEqual({ apiBase: '/api', authBase: '/auth' });
    expect(readAppConf({ apiBase: '/api-alt', authBase: '/bff', brand: 'Qian 乾', locale: 'en-US', managed: true, extra: 'ignored' })).toEqual({ apiBase: '/api-alt', authBase: '/bff', brand: 'Qian 乾', locale: 'en-US', managed: true });
    for (const bad of [{ apiBase: 'https://evil.test/api' }, { apiBase: '//evil.test' }, { apiBase: '/api/' }, { apiBase: '/api?x=1' }, { apiBase: '/api#x' }, { apiBase: '/a b' }, { apiBase: '' }, { authBase: 'auth' }, { locale: 'fr-FR' }, { brand: '' }, { managed: 'yes' }, [], 'text'])
      expect(() => readAppConf(bad), JSON.stringify(bad)).toThrow('shell.error.config');
  });
});

describe('BFF 会话客户端（浏览器零令牌）', () => {
  it('快照来自 GET session、401 即无会话、写请求先取防伪令牌并带头、刷新失败返回 false', async () => {
    let alive = true; let refreshes = 0;
    const { fetchImpl, calls } = fakeFetch((url, init) => {
      if (url === '/auth/session') return alive ? Response.json({ subject: 'p1', kind: 'user', tenantId: 't1', membershipId: 'm1', identityRevision: 3, displayName: '演示用户' }) : problem(401);
      if (url === '/auth/csrf') return alive ? Response.json({ headerName: 'X-CSRF-TOKEN', token: 'csrf-1' }) : problem(401);
      if (url === '/auth/session/refresh') { refreshes += 1; expect(init?.method).toBe('POST'); expect(header(init, 'X-CSRF-TOKEN')).toBe('csrf-1'); return alive ? Response.json({ subject: 'p1', kind: 'user', tenantId: 't1', membershipId: 'm1', identityRevision: 4, displayName: '演示用户' }) : problem(401); }
      if (url === '/auth/logout') { expect(header(init, 'X-CSRF-TOKEN')).toBe('csrf-1'); alive = false; return new Response(null, { status: 204 }); }
      throw new Error('unexpected ' + url);
    });
    const client = createBffSessionClient({ authBase: '/auth', fetch: fetchImpl });
    expect(await client.getSession()).toEqual({ subject: 'p1', kind: 'user', tenantId: 't1', membershipId: 'm1', identityRevision: 3, displayName: '演示用户' });
    expect(calls[0]?.init?.credentials).toBe('same-origin'); expect(calls[0]?.init?.redirect).toBe('error'); expect(calls[0]?.init?.cache).toBe('no-store');
    expect(await client.refreshSession()).toBe(true); expect(refreshes).toBe(1);
    await client.signOut();
    expect(await client.getSession()).toBeNull();
    expect(await client.refreshSession()).toBe(false); expect(refreshes).toBe(1);
    expect(client.loginUrl('/customers?tab=1')).toBe('/auth/login?returnTo=%2Fcustomers%3Ftab%3D1');
    expect(client.loginUrl('/', { prompt: 'select_account' })).toBe('/auth/login?returnTo=%2F&prompt=select_account');
    for (const unsafe of ['https://evil.test/', '//evil.test', '/ok?access_token=x']) expect(() => client.loginUrl(unsafe)).toThrow('shell.error.navigation');
    expect(() => createBffSessionClient({ authBase: 'https://evil.test/auth' })).toThrow('shell.error.navigation');
  });
  it('非 ErrorDetail 的失败只带状态；畸形快照拒绝', async () => {
    const { fetchImpl } = fakeFetch((url) => url === '/auth/session' ? new Response('<html>', { status: 503 }) : Response.json({ subject: '', kind: 'user', tenantId: null }));
    await expect(createBffSessionClient({ fetch: fetchImpl }).getSession()).rejects.toMatchObject({ name: 'HttpResponseError', status: 503 });
    const { fetchImpl: malformed } = fakeFetch(() => Response.json({ subject: '', kind: 'user', tenantId: null }));
    await expect(createBffSessionClient({ fetch: malformed }).getSession()).rejects.toMatchObject({ name: 'HttpResponseError' });
  });
});

describe('后端菜单 → 路由', () => {
  const items: NavigationCatalogItem[] = [
    { resourceRef: 'qian:t:crm:page:%2Fcustomers%2Fnew', pluginId: 'qian.sample.crm', route: '/customers/new', title: '新建客户', icon: null, order: 20 },
    { resourceRef: 'qian:t:crm:page:%2Fcustomers', pluginId: 'qian.sample.crm', route: '/customers', title: '客户', icon: 'users', order: 10 },
  ];
  it('按 order 排序、标题作显示文字、路由经 page 类型解析；当前路径精确或按最长前缀命中，外链不参与', () => {
    const menu = toMenu(items);
    expect(menu.map(item => [item.label, item.target, item.type])).toEqual([['客户', '/customers', 'page'], ['新建客户', '/customers/new', 'page']]);
    const withExternal = [...menu, { id: 'docs', labelKey: 'docs', type: 'external', target: 'https://docs.example/customers' }];
    expect(matchMenuItem(withExternal, '/customers')?.item.label).toBe('客户');
    expect(matchMenuItem(withExternal, '/customers/new?x=1')?.item.label).toBe('新建客户');
    expect(matchMenuItem(withExternal, '/customers/42')?.item.label).toBe('客户');
    expect(matchMenuItem(withExternal, '/orders')).toBeUndefined();
    expect(() => toMenu([{ ...items[0]!, route: 'javascript:alert(1)' }]).map(item => matchMenuItem([item], '/'))).toThrow();
  });
  it('历史路由：导航校验同源地址并通知订阅者，popstate 回退', () => {
    const listeners = new Map<string, () => void>(); const history: string[] = [];
    const win = { location: { pathname: '/', search: '', hash: '' }, history: { pushState: (_: unknown, __: string, url?: string | URL | null) => { history.push('push:' + url); win.location.pathname = String(url); }, replaceState: (_: unknown, __: string, url?: string | URL | null) => { history.push('replace:' + url); } },
      addEventListener: (type: string, listener: EventListenerOrEventListenerObject) => { listeners.set(type, listener as () => void); }, removeEventListener: (type: string) => { listeners.delete(type); } };
    const router = createBrowserRouter(win);
    const seen: string[] = []; const stop = router.subscribe(() => seen.push(router.getPath()));
    router.navigate('/customers'); expect(history).toEqual(['push:/customers']); expect(seen).toEqual(['/customers']);
    expect(() => router.navigate('https://evil.test')).toThrow('shell.error.navigation'); expect(() => router.navigate('/x?access_token=1')).toThrow();
    win.location.pathname = '/'; listeners.get('popstate')!(); expect(seen).toEqual(['/customers', '/']);
    router.navigate('/again', { replace: true }); expect(history.at(-1)).toBe('replace:/again');
    stop(); expect(listeners.has('popstate')).toBe(false);
  });
});

describe('身份服务同源 AuthProvider', () => {
  const choices = [
    { membership_id: 'm-a', tenant_id: 't-a', tenant_code: 'alpha', tenant_name: '甲公司', display_name: '甲方成员', platform: false, tenant_admin: true },
    { membership_id: null, tenant_id: null, tenant_code: null, tenant_name: null, display_name: '运营', platform: true, tenant_admin: false },
  ];
  it('登录 → 选择 → /me；票据只在内存，每次写请求取新防伪令牌，密码不进 URL', async () => {
    let csrfCount = 0; let selected: unknown = null;
    const { fetchImpl, calls } = fakeFetch(async (url, init) => {
      if (url === '/api/identity/csrf') return Response.json({ header: 'X-CSRF-TOKEN', token: 'csrf-' + (++csrfCount) });
      if (init?.method === 'POST') expect(header(init, 'X-CSRF-TOKEN')).toBe('csrf-' + csrfCount); else expect(header(init, 'X-CSRF-TOKEN')).toBeNull();
      if (url === '/api/identity/login') { expect(JSON.parse(String(init?.body))).toEqual({ client_id: 'qian-web', identifier: 'demo', password: 'pw', tenant_id: null }); return Response.json({ status: 'selection_required', ticket: 'ticket-1', expires_at: '2026-10-23T00:05:00Z', choices }); }
      if (url === '/api/identity/login/select') { selected = JSON.parse(String(init?.body)); return Response.json({ status: 'authenticated', tenant_id: 't-a', membership_id: 'm-a', platform: false }); }
      if (url === '/api/identity/me') return Response.json({ person_id: 'p-1', tenant_id: 't-a', membership_id: 'm-a', identity_revision: 2, amr: ['pwd'], acr: null, client_id: 'qian-web', device_type: 'web', expires_at: '2026-11-22T00:00:00Z' });
      throw new Error('unexpected ' + url);
    });
    const provider = createIdentityAuthProvider({ clientId: 'qian-web', fetch: fetchImpl, platformLabel: '平台身份' });
    const signal = new AbortController().signal;
    const first = await provider.signIn({ username: 'demo', password: 'pw' }, signal);
    expect(first).toEqual({ kind: 'select-tenant', tenants: [{ id: 't-a', name: '甲公司' }, { id: PLATFORM_TENANT, name: '平台身份' }] });
    await expect(provider.selectTenant('t-unknown', signal)).rejects.toThrow('shell.error.tenant');
    const session = await provider.selectTenant('t-a', signal);
    expect(selected).toEqual({ ticket: 'ticket-1', membership_id: 'm-a' });
    expect(session).toEqual({ subjectId: 'p-1', displayName: '甲方成员', tenantId: 't-a', tenants: [{ id: 't-a', name: '甲公司' }, { id: PLATFORM_TENANT, name: '平台身份' }] });
    expect(calls.every(call => !call.url.includes('pw') && !call.url.includes('ticket'))).toBe(true);
    // 票据一次性：会话建立后即作废，再选只能重新登录。
    await expect(provider.selectTenant('t-a', signal)).rejects.toThrow('shell.error.ticket');
  });
  it('强制改密与平台身份；连接器列表、发起与流程读取', async () => {
    const { fetchImpl } = fakeFetch(async (url, init) => {
      if (url === '/api/identity/csrf') return Response.json({ header: 'X-CSRF-TOKEN', token: 'csrf' });
      if (url === '/api/identity/login') return Response.json({ status: 'password_change_required', ticket: 'change-1', expires_at: '2026-10-23T00:05:00Z' });
      if (url === '/api/identity/login/password') { expect(JSON.parse(String(init?.body))).toEqual({ ticket: 'change-1', new_password: 'Longer-Pass-9' }); return Response.json({ status: 'authenticated', platform: true }); }
      if (url === '/api/identity/me') return Response.json({ person_id: 'p-2', identity_revision: '0' });
      if (url === '/api/identity/connectors?tenant=alpha') return Response.json({ items: [{ id: 'oidc', display_name: '企业 OIDC', protocol: 'oidc', terminals: ['web'] }] });
      if (url === '/api/identity/connectors/oidc/begin') return Response.json({ flow: 'f-1', redirect_uri: 'https://idp.example/authorize?state=abc', expires_at: '2026-10-23T00:05:00Z' });
      if (url === '/api/identity/connector-flows/f-1') return Response.json({ flow: 'f-1', status: 'bind', next: 'bind', expires_at: '2026-10-23T00:05:00Z', connector: { id: 'oidc', display_name: '企业 OIDC', protocol: 'oidc' }, tenant: { code: 'alpha', name: '甲公司' }, binding: { external: { display_name: '外部用户', email: 'u***@example.com', username: null }, expires_at: '2026-10-23T00:05:00Z', allow_jit: true, verified: false, account: null }, error: null, upstream_error_description: null, return_url: null });
      if (url === '/api/identity/connector-flows/f-2') return problem(404, 'NOT_FOUND');
      throw new Error('unexpected ' + url);
    });
    const provider = createIdentityAuthProvider({ clientId: 'qian-web', fetch: fetchImpl });
    const signal = new AbortController().signal;
    expect(await provider.signIn({ username: 'demo', password: 'initial' }, signal)).toEqual({ kind: 'change-password' });
    const changed = await provider.changePassword('Longer-Pass-9', signal);
    expect(changed.kind).toBe('authenticated');
    if (changed.kind === 'authenticated') expect(changed.session.tenantId).toBe(PLATFORM_TENANT);
    expect(await provider.listConnectors(signal, 'alpha')).toEqual([{ id: 'oidc', displayName: '企业 OIDC', protocol: 'oidc', terminals: ['web'] }]);
    expect(await provider.beginConnector('oidc', signal, { tenant: 'alpha' })).toBe('https://idp.example/authorize?state=abc');
    const flow = await provider.readFlow('f-1', signal);
    expect(flow?.next).toBe('bind'); expect(flow?.binding?.external.email).toBe('u***@example.com'); expect(flow?.binding?.allowJit).toBe(true);
    expect(await provider.readFlow('f-2', signal)).toBeNull();
  });
  it('服务端 ErrorDetail 原样成为 PlatformError，不吞掉状态码', async () => {
    const { fetchImpl } = fakeFetch((url) => url === '/api/identity/csrf' ? Response.json({ header: 'X-CSRF-TOKEN', token: 'csrf' }) : problem(401, 'INVALID_CREDENTIALS'));
    const provider = createIdentityAuthProvider({ clientId: 'qian-web', fetch: fetchImpl });
    await expect(provider.signIn({ username: 'demo', password: 'bad' }, new AbortController().signal)).rejects.toMatchObject({ name: 'PlatformError', status: 401, detail: { code: 'INVALID_CREDENTIALS' } });
    await expect(provider.changePassword('x', new AbortController().signal)).rejects.toThrow('shell.error.ticket');
  });
});
