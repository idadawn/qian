import { describe, expect, it, vi } from 'vitest';
import { PlatformClient } from '@idadawn/yue-sdk';
import { apiPrefixMiddleware, createBffSessionClient, createSessionExpiryGuard, csrfMiddleware, fetchBootstrap, isShellBootstrap, loadAppConfig, mapApiPath, parseAppConfig, toMenu, type BffSession, type NavigationCatalogItem } from './index';
import { createIdentityAuthProvider, PLATFORM_TENANT } from './identity-provider';

const problem = (status: number, code = 'UNAUTHENTICATED') => Response.json({ type: `https://qian.dev/problems/protocol/${code}`, title: code, status, code, message: '未认证', trace_id: 'a'.repeat(32) }, { status, headers: { 'content-type': 'application/problem+json' } });
type Call = { url: string; init: RequestInit | undefined };
function fakeFetch(handler: (url: string, init: RequestInit | undefined, calls: Call[]) => Response | Promise<Response>) {
  const calls: Call[] = [];
  const fetchImpl = vi.fn(async (input: string | URL | Request, init?: RequestInit) => { const url = String(input); calls.push({ url, init }); return handler(url, init, calls); });
  return { fetchImpl: fetchImpl as unknown as typeof fetch, calls };
}
const header = (init: RequestInit | undefined, name: string) => new Headers(init?.headers).get(name);
const control = String.fromCharCode(1);

describe('运行时配置 /app-config.json', () => {
  it('缺省值与合法覆盖', () => {
    expect(parseAppConfig(undefined)).toEqual({ apiBase: '/api', authBase: '/auth', bootstrapUrl: '/api/shell/bootstrap', auditUrl: '/api/shell/audit/remote-rejected' });
    expect(parseAppConfig({ apiBase: '/api-alt', authBase: '/bff', bootstrapUrl: '/bff/bootstrap', auditUrl: '/api/audit', brand: 'Qian 乾', locale: 'en-US', managed: true }))
      .toEqual({ apiBase: '/api-alt', authBase: '/bff', bootstrapUrl: '/bff/bootstrap', auditUrl: '/api/audit', brand: 'Qian 乾', locale: 'en-US', managed: true });
  });
  it('四个地址统一同源路径校验：绝对源、协议相对、结尾斜杠、查询串、片段、反斜杠、空白、控制字符、点段一律拒绝；未知字段与其它非法字段同样拒绝', () => {
    const paths = ['https://evil.test/api', 'http://localhost/api', '//evil.test', '/api/', '/api?x=1', '/api#x', '/a b', '/a\\b', '', 'api', '/', '/api/../x', '/./api', '/api' + control, '/' + 'a'.repeat(256)];
    for (const key of ['apiBase', 'authBase', 'bootstrapUrl', 'auditUrl'])
      for (const value of [...paths, 42, null, true]) expect(() => parseAppConfig({ [key]: value }), `${key}=${JSON.stringify(value)}`).toThrow('shell.error.config');
    for (const bad of [{ apibase: '/api' }, { apiBase: '/api', extra: 1 }, { locale: 'fr-FR' }, { brand: '' }, { brand: '   ' }, { brand: 'x'.repeat(65) }, { brand: 'a' + control }, { managed: 'yes' }, [], 'text', 42])
      expect(() => parseAppConfig(bad), JSON.stringify(bad)).toThrow('shell.error.config');
  });
  it('读取：no-store、同源、不跟随重定向；404 即缺省值，其余失败、非 JSON、坏 JSON 与网络错误都是配置错误', async () => {
    const respond = (response: () => Response) => fakeFetch(() => response());
    const ok = respond(() => Response.json({ apiBase: '/api-alt' }));
    expect(await loadAppConfig({ fetch: ok.fetchImpl })).toMatchObject({ apiBase: '/api-alt', authBase: '/auth' });
    expect(ok.calls[0]!.url).toBe('/app-config.json');
    expect(ok.calls[0]!.init).toMatchObject({ credentials: 'same-origin', redirect: 'error', cache: 'no-store' });
    expect(await loadAppConfig({ fetch: respond(() => new Response('missing', { status: 404 })).fetchImpl })).toEqual(parseAppConfig(undefined));
    for (const response of [() => new Response('{}', { status: 500, headers: { 'content-type': 'application/json' } }), () => new Response('<html>', { headers: { 'content-type': 'text/html' } }),
      () => new Response('{bad', { headers: { 'content-type': 'application/json' } }), () => Response.json({ apiBase: 'https://evil.test/api' })])
      await expect(loadAppConfig({ fetch: respond(response).fetchImpl })).rejects.toThrow('shell.error.config');
    await expect(loadAppConfig({ fetch: (() => Promise.reject(new TypeError('network'))) as unknown as typeof fetch })).rejects.toThrow('shell.error.config');
  });
});

describe('规范路径 /api 的前缀映射与写请求防伪', () => {
  it('只换 /api 段边界下的路径', () => {
    expect(mapApiPath('/api/shell/bootstrap', '/api-alt')).toBe('/api-alt/shell/bootstrap');
    expect(mapApiPath('/api', '/gw/api')).toBe('/gw/api');
    expect(mapApiPath('/apix/y', '/api-alt')).toBe('/apix/y');
    expect(mapApiPath('/auth/session', '/api-alt')).toBe('/auth/session');
    expect(mapApiPath('/api/x', '/api')).toBe('/api/x');
  });
  it('外壳客户端：远程与 bridge 写规范路径，发出时换前缀；写请求每次先取新防伪令牌，读请求不取；请求体与幂等键原样保留', async () => {
    const seen: { url: string; method: string; csrf: string | null; key: string | null; body: string }[] = [];
    let issued = 0; let alive = true;
    const session = { csrf: vi.fn(async () => alive ? { header: 'X-CSRF-TOKEN', token: 'csrf-' + (++issued) } : null) };
    const client = new PlatformClient({ baseUrl: 'https://shell.test', middleware: [apiPrefixMiddleware('/api-alt'), csrfMiddleware(session)],
      transport: async request => { seen.push({ url: request.url, method: request.method, csrf: request.headers.get('X-CSRF-TOKEN'), key: request.headers.get('Idempotency-Key'), body: request.method === 'GET' ? '' : await request.text() }); return Response.json({ ok: true }); } });
    type Paths = { '/api/capabilities/{plugin}/{name}/invoke': { post: { parameters: { path: { plugin: string; name: string }; header: { 'Idempotency-Key': string } }; requestBody: { content: { 'application/json': unknown } }; responses: { 200: { content: { 'application/json': unknown } } } } };
      '/api/shell/bootstrap': { get: { responses: { 200: { content: { 'application/json': unknown } } } } } };
    await client.api<Paths>().GET('/api/shell/bootstrap');
    await client.api<Paths>().POST('/api/capabilities/{plugin}/{name}/invoke', { params: { path: { plugin: 'qian.sample.crm', name: 'customer.create' }, header: { 'Idempotency-Key': 'k-1' } }, body: { name: '客户' } });
    await client.api<Paths>().POST('/api/capabilities/{plugin}/{name}/invoke', { params: { path: { plugin: 'qian.sample.crm', name: 'customer.create' }, header: { 'Idempotency-Key': 'k-2' } }, body: { name: '客户 2' } });
    alive = false;
    await client.api<Paths>().POST('/api/capabilities/{plugin}/{name}/invoke', { params: { path: { plugin: 'qian.sample.crm', name: 'customer.create' }, header: { 'Idempotency-Key': 'k-3' } }, body: { name: '客户 3' } });
    expect(seen).toEqual([
      { url: 'https://shell.test/api-alt/shell/bootstrap', method: 'GET', csrf: null, key: null, body: '' },
      { url: 'https://shell.test/api-alt/capabilities/qian.sample.crm/customer.create/invoke', method: 'POST', csrf: 'csrf-1', key: 'k-1', body: '{"name":"客户"}' },
      { url: 'https://shell.test/api-alt/capabilities/qian.sample.crm/customer.create/invoke', method: 'POST', csrf: 'csrf-2', key: 'k-2', body: '{"name":"客户 2"}' },
      { url: 'https://shell.test/api-alt/capabilities/qian.sample.crm/customer.create/invoke', method: 'POST', csrf: null, key: 'k-3', body: '{"name":"客户 3"}' },
    ]);
    expect(session.csrf).toHaveBeenCalledTimes(3);
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
    expect(await client.csrf()).toEqual({ header: 'X-CSRF-TOKEN', token: 'csrf-1' });
    expect(await client.refreshSession()).toBe(true); expect(refreshes).toBe(1);
    await client.signOut();
    expect(await client.getSession()).toBeNull();
    expect(await client.csrf()).toBeNull();
    expect(await client.refreshSession()).toBe(false); expect(refreshes).toBe(1);
    expect(client.loginUrl('/customers?tab=1')).toBe('/auth/login?returnTo=%2Fcustomers%3Ftab%3D1');
    expect(client.loginUrl('/', { prompt: 'select_account' })).toBe('/auth/login?returnTo=%2F&prompt=select_account');
    for (const unsafe of ['https://evil.test/', '//evil.test', '/ok?access_token=x']) expect(() => client.loginUrl(unsafe)).toThrow('shell.error.navigation');
    expect(() => createBffSessionClient({ authBase: 'https://evil.test/auth' })).toThrow('shell.error.navigation');
  });
  it('身份修订按 int64 保留：超出安全整数的修订原样是十进制字符串，不经 Number() 丢精度', async () => {
    const { fetchImpl } = fakeFetch(() => new Response('{"subject":"p1","kind":"user","tenantId":"t1","membershipId":"m1","identityRevision":9007199254740993,"displayName":null}', { headers: { 'content-type': 'application/json' } }));
    expect((await createBffSessionClient({ fetch: fetchImpl }).getSession())?.identityRevision).toBe('9007199254740993');
  });
  it('非 ErrorDetail 的失败只带状态；畸形快照拒绝', async () => {
    const { fetchImpl } = fakeFetch((url) => url === '/auth/session' ? new Response('<html>', { status: 503 }) : Response.json({ subject: '', kind: 'user', tenantId: null }));
    await expect(createBffSessionClient({ fetch: fetchImpl }).getSession()).rejects.toMatchObject({ name: 'HttpResponseError', status: 503 });
    const { fetchImpl: malformed } = fakeFetch(() => Response.json({ subject: '', kind: 'user', tenantId: null }));
    await expect(createBffSessionClient({ fetch: malformed }).getSession()).rejects.toMatchObject({ name: 'HttpResponseError' });
  });
});

describe('bootstrap 聚合', () => {
  const member = {
    session: { subject: 'p1', kind: 'user', tenantId: 't1', membershipId: 'm1', identityRevision: 2, displayName: '演示用户' },
    context: { tenant: { id: 't1', name: '甲方科技' }, membership: { id: 'm1', permissions: ['qian.sample.crm:customer:read'] }, app: null, locale: 'zh-CN', theme: 'light' },
    remotes: [{ id: 'qian.sample.crm', version: '1.0.0', scope: 'tenant:t1', manifest: { $schema: 'https://qian.dev/schemas/ui/v2' } }],
    mounted: ['tenant:t1|qian.sample.crm'], entitled: ['tenant:t1|qian.sample.crm'], permissions: ['qian.sample.crm:customer:read'],
    navigation: [{ resourceRef: 'qian:t1:qian.sample.crm:page:%2Fcustomers', pluginId: 'qian.sample.crm', route: '/customers', title: '客户', icon: 'users', order: 10 }], managed: false,
  };
  it('契约形状：成员、平台代管（无租户、导航为 null）通过；缺字段与错类型拒绝', () => {
    expect(isShellBootstrap(member)).toBe(true);
    expect(isShellBootstrap({ ...member, session: { ...member.session, tenantId: null, membershipId: null }, context: { ...member.context, tenant: null, membership: null }, remotes: [], mounted: [], entitled: [], permissions: [], navigation: null, managed: true })).toBe(true);
    for (const bad of [{ ...member, session: undefined }, { ...member, context: { ...member.context, theme: 'blue' } }, { ...member, remotes: [{ id: 'x' }] }, { ...member, mounted: [1] },
      { ...member, navigation: [{ route: '/x' }] }, { ...member, managed: 'no' }, { ...member, context: { ...member.context, membership: { id: 'm1', permissions: 'all' } } }, null, []])
      expect(isShellBootstrap(bad), JSON.stringify(bad)).toBe(false);
  });
  it('经外壳客户端取：规范路径随 apiBase 映射；形状不符按 502', async () => {
    const urls: string[] = [];
    const client = (body: unknown) => new PlatformClient({ baseUrl: 'https://shell.test', middleware: [apiPrefixMiddleware('/api-alt')], transport: async request => { urls.push(request.url); return Response.json(body); } });
    expect((await fetchBootstrap(client(member), '/api/shell/bootstrap')).context.tenant?.id).toBe('t1');
    expect(urls).toEqual(['https://shell.test/api-alt/shell/bootstrap']);
    await expect(fetchBootstrap(client({ ...member, managed: undefined }), '/api/shell/bootstrap')).rejects.toMatchObject({ name: 'HttpResponseError', status: 502 });
  });
});

describe('后端菜单', () => {
  const items: NavigationCatalogItem[] = [
    { resourceRef: 'qian:t:crm:page:%2Fcustomers%2Fnew', pluginId: 'qian.sample.crm', route: '/customers/new', title: '新建客户', icon: null, order: 20 },
    { resourceRef: 'qian:t:crm:page:%2Fcustomers', pluginId: 'qian.sample.crm', route: '/customers', title: '客户', icon: 'users', order: 10 },
  ];
  it('按 order 排序、标题作显示文字、路由经 page 类型解析', () => {
    expect(toMenu(items).map(item => [item.label, item.target, item.type, item.id])).toEqual([['客户', '/customers', 'page', 'qian:t:crm:page:%2Fcustomers'], ['新建客户', '/customers/new', 'page', 'qian:t:crm:page:%2Fcustomers%2Fnew']]);
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
    expect(await provider.me(signal)).toMatchObject({ personId: 'p-1', identityRevision: 2 });
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

describe('首载会话过期：跳登录还是弹对话框（第一轮验收复核阻断项）', () => {
  const snapshot: BffSession = { subject: 'p1', kind: 'user', tenantId: 't1', membershipId: 'm1', identityRevision: 3, displayName: null };
  const guardWith = (refresh: () => Promise<boolean>, getSession: () => Promise<BffSession | null>) => {
    const session = { refreshSession: vi.fn(refresh), getSession: vi.fn(getSession) };
    return { guard: createSessionExpiryGuard(session), session };
  };
  type BootstrapPaths = { '/api/shell/bootstrap': { get: { responses: { 200: { content: { 'application/json': unknown } } } } } };

  it('刷新成功、重放仍 401（平台表面拒绝）：弹对话框，不跳登录，也不再问会话快照', async () => {
    const { guard, session } = guardWith(async () => true, async () => null);
    expect(await guard.refreshSession()).toBe(true);
    expect(await guard.onExpired()).toBe('dialog');
    expect(session.getSession).not.toHaveBeenCalled();
  });
  it('依赖方说会话已结束（刷新回答 false）：跳登录，不再问会话快照', async () => {
    const { guard, session } = guardWith(async () => false, async () => snapshot);
    expect(await guard.refreshSession()).toBe(false);
    expect(await guard.onExpired()).toBe('login');
    expect(session.getSession).not.toHaveBeenCalled();
  });
  it('刷新出错：问一次会话快照，没有会话才跳登录；仍有会话或问不到都弹对话框', async () => {
    const cases: [() => Promise<BffSession | null>, string][] = [[async () => snapshot, 'dialog'], [async () => null, 'login'], [async () => { throw new TypeError('network'); }, 'dialog']];
    for (const [getSession, expected] of cases) {
      const { guard, session } = guardWith(async () => { throw new TypeError('network'); }, getSession);
      await expect(guard.refreshSession()).rejects.toThrow('network');
      expect(await guard.onExpired()).toBe(expected);
      expect(session.getSession).toHaveBeenCalledTimes(1);
    }
  });
  it('按最近一次刷新的结论处置：先成功、后回答 false 时跳登录', async () => {
    let alive = true;
    const { guard } = guardWith(async () => alive, async () => snapshot);
    await guard.refreshSession(); alive = false; await guard.refreshSession();
    expect(await guard.onExpired()).toBe('login');
  });
  it('与 SDK 联动：守卫作 sessionProvider，过期通知到达时结论已就位（刷新成功给 dialog，回答 false 给 login）', async () => {
    for (const [alive, expected] of [[true, 'dialog'], [false, 'login']] as const) {
      const { guard, session } = guardWith(async () => alive, async () => null);
      const requests: string[] = [];
      const outcome = new Promise<string>(resolve => {
        const client = new PlatformClient({ baseUrl: 'https://shell.test', sessionProvider: guard, onSessionExpired: () => { void guard.onExpired().then(resolve); },
          transport: async request => { requests.push(new URL(request.url).pathname); return problem(401); } });
        void client.api<BootstrapPaths>().GET('/api/shell/bootstrap').catch(() => undefined);
      });
      expect(await outcome).toBe(expected);
      expect(session.refreshSession).toHaveBeenCalledTimes(1);
      expect(requests).toEqual(alive ? ['/api/shell/bootstrap', '/api/shell/bootstrap'] : ['/api/shell/bootstrap']);
    }
  });
});
