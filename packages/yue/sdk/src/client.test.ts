import { describe, expect, it, vi } from 'vitest';
import { createMockTransport, HttpResponseError, PlatformClient, PlatformError, parsePlatformJson, pointerSegments } from './client';

// 测试服务的路径契约；产品消费方使用 OpenAPI 生成类型。
interface Paths {
  '/items': {
    get: { parameters: { query?: { page?: number } }; responses: { 200: { content: { 'application/json': { value: string } } } } };
    post: { requestBody: { content: { 'application/json': { value: string } } }; responses: { 200: { content: { 'application/json': { value: string } } } } };
  };
}
const problem = (status: number, code = 'INPUT_INVALID') => ({
  type: `https://qian.dev/problems/${status === 409 ? 'business' : 'protocol'}/${code}`,
  title: code, status, code, message: '测试错误', trace_id: 'a'.repeat(32),
  pointer: '#/items/0/name', errors: [{ code, message: '字段错误', pointer: '#/items/0/name' }],
});
const deferred = <T,>() => {
  let resolve!: (value: T) => void;
  let reject!: (reason: unknown) => void;
  const promise = new Promise<T>((a, b) => { resolve = a; reject = b; });
  return { promise, resolve, reject };
};

describe('PlatformClient', () => {
  it('并发 401 只刷新一次，并重放 POST 请求体与租户头', async () => {
    let token = 'expired';
    const release = deferred<string>();
    const refreshAccessToken = vi.fn(async () => { token = await release.promise; return token; });
    const bodies: string[] = [];
    const transport = createMockTransport([{ method: 'POST', path: '/items', respond: async (request) => {
      expect(request.headers.get('X-Tenant-Id')).toBe('tenant-a');
      expect(request.redirect).toBe('error');
      bodies.push(await request.text());
      return request.headers.get('Authorization') === 'Bearer fresh'
        ? Response.json({ value: 'ok' }) : Response.json(problem(401), { status: 401 });
    } }]);
    const api = new PlatformClient({ baseUrl: 'https://example.test', transport, getTenantId: () => 'tenant-a', tokenProvider: { getAccessToken: () => token, refreshAccessToken } }).api<Paths>();
    const pending = Promise.all(Array.from({ length: 12 }, () => api.POST('/items', { body: { value: 'body' } })));
    await vi.waitFor(() => expect(refreshAccessToken).toHaveBeenCalledTimes(1));
    release.resolve('fresh');
    expect((await pending).every((result) => result.data?.value === 'ok')).toBe(true);
    expect(refreshAccessToken).toHaveBeenCalledTimes(1);
    expect(bodies).toHaveLength(24);
    expect(new Set(bodies)).toEqual(new Set(['{"value":"body"}']));
  });

  it('晚到的旧令牌 401 复用已刷新的令牌，不再刷新', async () => {
    let token = 'old';
    const late = deferred<Response>();
    const refreshAccessToken = vi.fn(async () => { token = 'new'; return token; });
    let old = 0;
    const api = new PlatformClient({ baseUrl: 'https://example.test', tokenProvider: { getAccessToken: () => token, refreshAccessToken }, transport: async (request) => {
      if (request.headers.get('Authorization') === 'Bearer new') return Response.json({ value: 'ok' });
      if (++old === 1) return late.promise;
      return Response.json(problem(401), { status: 401 });
    } }).api<Paths>();
    const first = api.GET('/items');
    await api.GET('/items');
    late.resolve(Response.json(problem(401), { status: 401 }));
    await first;
    expect(refreshAccessToken).toHaveBeenCalledTimes(1);
  });

  it('刷新失败释放队列并只通知一次，下一批可以再次刷新', async () => {
    const release = deferred<string>();
    const onSessionExpired = vi.fn();
    const refreshAccessToken = vi.fn(() => release.promise);
    const api = new PlatformClient({ baseUrl: 'https://example.test', onSessionExpired, tokenProvider: { getAccessToken: () => 'old', refreshAccessToken }, transport: async () => Response.json(problem(401), { status: 401 }) }).api<Paths>();
    const pending = Promise.allSettled([api.GET('/items'), api.GET('/items')]);
    await vi.waitFor(() => expect(refreshAccessToken).toHaveBeenCalledTimes(1));
    release.reject(new Error('expired'));
    expect((await pending).every((entry) => entry.status === 'rejected')).toBe(true);
    expect(onSessionExpired).toHaveBeenCalledTimes(1);
    await expect(api.GET('/items')).rejects.toThrow('expired');
    expect(refreshAccessToken).toHaveBeenCalledTimes(2);
  });

  it('刷新等待中的取消不取消其他请求，也不重放取消的请求', async () => {
    const release = deferred<string>();
    const controller = new AbortController();
    const refreshAccessToken = vi.fn(() => release.promise);
    const transport = vi.fn(async (request: Request) => request.headers.get('Authorization') === 'Bearer new'
      ? Response.json({ value: 'ok' }) : Response.json(problem(401), { status: 401 }));
    const api = new PlatformClient({ baseUrl: 'https://example.test', transport, tokenProvider: { getAccessToken: () => 'old', refreshAccessToken } }).api<Paths>();
    const cancelled = api.GET('/items', { signal: controller.signal }).catch((error: unknown) => error);
    const kept = api.GET('/items');
    await vi.waitFor(() => expect(refreshAccessToken).toHaveBeenCalledTimes(1));
    controller.abort();
    expect(await cancelled).toBeInstanceOf(DOMException);
    release.resolve('new');
    expect((await kept).data?.value).toBe('ok');
    expect(transport).toHaveBeenCalledTimes(3);
  });

  it('刷新期间切换租户不会重放旧租户写入', async () => {
    let tenant = 'a';
    const api = new PlatformClient({ baseUrl: 'https://example.test', getTenantId: () => tenant, tokenProvider: { getAccessToken: () => 'old', refreshAccessToken: async () => { tenant = 'b'; return 'new'; } }, transport: async () => Response.json(problem(401), { status: 401 }) }).api<Paths>();
    await expect(api.POST('/items', { body: { value: 'body' } })).rejects.toThrow('sdk.error.contextChanged');
  });

  it('Cookie 会话：并发 401 合并成一次会话刷新，会话仍在则全部重放一次且不带 Authorization', async () => {
    let alive = false;
    const release = deferred<boolean>();
    const refreshSession = vi.fn(async () => { alive = await release.promise; return alive; });
    const transport = vi.fn(async (request: Request) => {
      expect(request.headers.has('Authorization')).toBe(false);
      expect(request.credentials).toBe('same-origin');
      return alive ? Response.json({ value: 'ok' }) : Response.json(problem(401), { status: 401 });
    });
    const onSessionExpired = vi.fn();
    const api = new PlatformClient({ baseUrl: 'https://example.test', transport, onSessionExpired, sessionProvider: { refreshSession } }).api<Paths>();
    const pending = Promise.all(Array.from({ length: 12 }, () => api.GET('/items')));
    await vi.waitFor(() => expect(refreshSession).toHaveBeenCalledTimes(1));
    release.resolve(true);
    expect((await pending).every((result) => result.data?.value === 'ok')).toBe(true);
    expect(refreshSession).toHaveBeenCalledTimes(1);
    expect(transport).toHaveBeenCalledTimes(24);
    expect(onSessionExpired).not.toHaveBeenCalled();
  });

  it('Cookie 会话：刷新回答会话已结束时不重放、全部按 401 失败且只通知一次过期；下一批可以再探测', async () => {
    const release = deferred<boolean>();
    const refreshSession = vi.fn(() => release.promise);
    const onSessionExpired = vi.fn();
    const transport = vi.fn(async () => Response.json(problem(401, 'UNAUTHENTICATED'), { status: 401 }));
    const api = new PlatformClient({ baseUrl: 'https://example.test', transport, onSessionExpired, sessionProvider: { refreshSession } }).api<Paths>();
    const pending = Promise.allSettled([api.GET('/items'), api.GET('/items'), api.POST('/items', { body: { value: 'x' } })]);
    await vi.waitFor(() => expect(refreshSession).toHaveBeenCalledTimes(1));
    release.resolve(false);
    const settled = await pending;
    expect(settled.every((entry) => entry.status === 'rejected' && entry.reason instanceof PlatformError && entry.reason.status === 401)).toBe(true);
    expect(transport).toHaveBeenCalledTimes(3);
    expect(onSessionExpired).toHaveBeenCalledTimes(1);
    await expect(api.GET('/items')).rejects.toBeInstanceOf(PlatformError);
    expect(refreshSession).toHaveBeenCalledTimes(2);
    expect(onSessionExpired).toHaveBeenCalledTimes(1);
  });

  it('Cookie 会话：刷新调用本身出错也只通知一次，错误原样抛给等待的请求', async () => {
    const onSessionExpired = vi.fn();
    const api = new PlatformClient({ baseUrl: 'https://example.test', onSessionExpired, sessionProvider: { refreshSession: async () => { throw new Error('network'); } }, transport: async () => Response.json(problem(401), { status: 401 }) }).api<Paths>();
    const results = await Promise.allSettled([api.GET('/items'), api.GET('/items')]);
    expect(results.every((entry) => entry.status === 'rejected' && (entry.reason as Error).message === 'network')).toBe(true);
    expect(onSessionExpired).toHaveBeenCalledTimes(1);
  });

  it('首次异步读取令牌期间切换租户，在发送请求前拒绝旧上下文写入', async () => {
    let tenant = 'a';
    const token = deferred<string>();
    const getAccessToken = vi.fn(() => token.promise);
    const transport = vi.fn(async () => Response.json({ value: 'ok' }));
    const api = new PlatformClient({ baseUrl: 'https://example.test', getTenantId: () => tenant, tokenProvider: { getAccessToken }, transport }).api<Paths>();
    const pending = api.POST('/items', { body: { value: '属于租户 A' } });
    await vi.waitFor(() => expect(getAccessToken).toHaveBeenCalledOnce());
    tenant = 'b'; token.resolve('fresh-b');
    await expect(pending).rejects.toThrow('sdk.error.contextChanged');
    expect(transport).not.toHaveBeenCalled();
  });

  it.each([400, 409, 403])('保留 %i ErrorDetail 与字段 pointer', async (status) => {
    const api = new PlatformClient({ baseUrl: 'https://example.test', transport: async () => Response.json(problem(status), { status }) }).api<Paths>();
    const error = await api.GET('/items').catch((error: unknown) => error);
    expect(error).toBeInstanceOf(PlatformError);
    expect((error as PlatformError).detail).toEqual(problem(status));
    expect(pointerSegments((error as PlatformError).detail.pointer!)).toEqual(['items', '0', 'name']);
  });

  it('非法响应只带状态，不把 HTML 或堆栈当作 ErrorDetail', async () => {
    const api = new PlatformClient({ baseUrl: 'https://example.test', transport: async () => new Response('<html>stack</html>', { status: 500 }) }).api<Paths>();
    await expect(api.GET('/items')).rejects.toMatchObject({ name: 'HttpResponseError', status: 500, message: 'sdk.error.http' });
    const invalid = new PlatformClient({ baseUrl: 'https://example.test', transport: async () => Response.json({ message: 'stack' }, { status: 500 }) }).api<Paths>();
    await expect(invalid.GET('/items')).rejects.toBeInstanceOf(HttpResponseError);
  });

  it('int64 精度不丢失，字符串中的数字和安全数字不变', async () => {
    const raw = '{"value":9223372036854775807,"small":3,"negative":-9223372036854775808,"text":"9223372036854775807","decimal":0.25}';
    expect(parsePlatformJson(raw)).toEqual({ value: '9223372036854775807', small: 3, negative: '-9223372036854775808', text: '9223372036854775807', decimal: 0.25 });
    const api = new PlatformClient({ baseUrl: 'https://example.test', transport: async () => new Response(raw, { headers: { 'content-type': 'application/json' } }) }).api<Paths>();
    expect((await api.GET('/items')).data?.value).toBe('9223372036854775807');
  });

  it('拦截器按注册顺序进入、逆序返回；mock 替换后调用方不变且不漏到网络', async () => {
    const order: string[] = [];
    const call = (client: PlatformClient) => client.api<Paths>().GET('/items', { params: { query: { page: 2 } } });
    const client = new PlatformClient({ baseUrl: 'https://example.test', middleware: [async (request, next) => { order.push('a'); const response = await next(request); order.push('d'); return response; }, async (request, next) => { order.push('b'); const response = await next(request); order.push('c'); return response; }], transport: createMockTransport([{ method: 'GET', path: '/items', respond: (request) => Response.json({ value: new URL(request.url).searchParams.get('page') }) }]) });
    expect((await call(client)).data?.value).toBe('2');
    expect(order).toEqual(['a', 'b', 'c', 'd']);
    await expect(client.api<Paths>().POST('/items', { body: { value: 'x' } })).rejects.toThrow('sdk.error.unhandledMock');
  });

  it('拒绝跨源 baseUrl 覆盖，凭据不会发给其他站点', async () => {
    const transport = vi.fn();
    const client = new PlatformClient({ baseUrl: 'https://example.test', transport });
    await expect(client.api<Paths>().GET('/items', { baseUrl: 'https://other.test' })).rejects.toThrow('sdk.error.origin');
    expect(transport).not.toHaveBeenCalled();
  });

  it('RFC 6901 路径段保留转义字符和带点的字段名', () => {
    expect(pointerSegments('#/a~1b/~0key/a.b/%E5%90%8D%E7%A7%B0')).toEqual(['a/b', '~key', 'a.b', '名称']);
  });
});
