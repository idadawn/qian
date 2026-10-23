import createClient, { type Client } from 'openapi-fetch';
import { parse } from 'lossless-json';
import type { ErrorDetail } from '@idadawn/yue-contracts';

export type RequestTransport = (request: Request) => Promise<Response>;
export type RequestMiddleware = (request: Request, next: RequestTransport) => Promise<Response>;

/** 刷新由宿主实现（HttpOnly cookie）；返回新令牌并更新内存，SDK 不持久化凭据。 */
export interface TokenProvider {
  getAccessToken(): string | null | Promise<string | null>;
  refreshAccessToken?(): Promise<string | null>;
}

/**
 * BFF Cookie 会话（浏览器零令牌）：401 后由宿主向会话端点强制刷新一次服务端访问令牌。
 * 返回 true 表示会话仍在、请求重放一次；false 表示会话已结束，只通知一次 `onSessionExpired`。并发 401 合并为一次调用。
 */
export interface SessionProvider {
  refreshSession(): Promise<boolean>;
}

export interface PlatformClientOptions {
  baseUrl: string;
  /** 浏览器持令牌的宿主（令牌只在内存）；与 sessionProvider 同时给出时以它为准。 */
  tokenProvider?: TokenProvider;
  /** HttpOnly Cookie 会话的宿主；浏览器不接触令牌，401 后经它探测一次。 */
  sessionProvider?: SessionProvider;
  getTenantId?: () => string | null | undefined;
  transport?: RequestTransport;
  middleware?: readonly RequestMiddleware[];
  onSessionExpired?: () => void;
}

export class PlatformError extends Error {
  readonly detail: ErrorDetail;
  readonly status: number;
  constructor(detail: ErrorDetail) {
    super(detail.message);
    this.name = 'PlatformError';
    this.detail = detail;
    this.status = detail.status;
  }
}

/** 非 ErrorDetail 响应只暴露状态，避免 HTML 错误页或堆栈进入产品提示。 */
export class HttpResponseError extends Error {
  readonly status: number;
  constructor(status: number) {
    super('sdk.error.http');
    this.name = 'HttpResponseError';
    this.status = status;
  }
}

export function isErrorDetail(value: unknown): value is ErrorDetail {
  if (!value || typeof value !== 'object') return false;
  const v = value as Record<string, unknown>;
  return typeof v.code === 'string' && /^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$/.test(v.code)
    && typeof v.type === 'string' && /^https:\/\/qian\.dev\/problems\/(protocol|business)\//.test(v.type)
    && v.type.endsWith('/' + v.code)
    && typeof v.title === 'string' && typeof v.message === 'string'
    && typeof v.status === 'number' && Number.isInteger(v.status) && v.status >= 400 && v.status <= 599
    && typeof v.trace_id === 'string' && /^[0-9a-f]{32}$/.test(v.trace_id)
    && (v.pointer === undefined || typeof v.pointer === 'string')
    && (v.errors === undefined || (Array.isArray(v.errors) && v.errors.every((entry: unknown) => {
      if (!entry || typeof entry !== 'object') return false;
      const field = entry as Record<string, unknown>;
      return typeof field.code === 'string' && typeof field.message === 'string' && typeof field.pointer === 'string';
    })));
}

/** RFC 6901 URI 片段；返回路径段，保留字段名中的点、斜线与波浪线。 */
export function pointerSegments(pointer: string): string[] {
  const hash = pointer.indexOf('#');
  if (hash < 0) return [];
  const fragment = decodeURIComponent(pointer.slice(hash + 1));
  if (!fragment) return [];
  if (!fragment.startsWith('/')) throw new TypeError('sdk.error.pointer');
  return fragment.slice(1).split('/').map((part) => part.replaceAll('~1', '/').replaceAll('~0', '~'));
}

function parseNumber(value: string): number | string {
  const number = Number(value);
  return !Number.isFinite(number) || (Number.isInteger(number) && !Number.isSafeInteger(number)) ? value : number;
}

export function parsePlatformJson(text: string): unknown {
  return parse(text, undefined, parseNumber);
}

function abortable<T>(promise: Promise<T>, signal: AbortSignal): Promise<T> {
  signal.throwIfAborted();
  return new Promise((resolve, reject) => {
    const abort = () => reject(signal.reason);
    signal.addEventListener('abort', abort, { once: true });
    promise.then(resolve, reject).finally(() => signal.removeEventListener('abort', abort));
  });
}

/** 调用方传入从 OpenAPI 生成的 paths；参数、请求体和响应类型不另写一套。 */
export class PlatformClient {
  readonly baseUrl: string;
  private readonly options: PlatformClientOptions;
  private readonly send: RequestTransport;
  private refreshing: Promise<string | null> | undefined;
  private refreshingSession: Promise<boolean> | undefined;
  private sessionExpired = false;

  constructor(options: PlatformClientOptions) {
    this.options = options;
    this.baseUrl = new URL(options.baseUrl, typeof location === 'undefined' ? undefined : location.origin).href.replace(/\/$/, '');
    const transport = options.transport ?? ((request: Request) => fetch(request));
    this.send = (options.middleware ?? []).reduceRight<RequestTransport>(
      (next, middleware) => (request) => middleware(request, next), transport,
    );
  }

  api<Paths extends object>(): Client<Paths> {
    return createClient<Paths>({
      baseUrl: this.baseUrl,
      credentials: 'same-origin',
      redirect: 'error',
      fetch: (request: Request) => this.dispatch(request),
      bodySerializer: (body: unknown) => JSON.stringify(body, (_key, value: unknown) => typeof value === 'bigint' ? value.toString() : value),
    });
  }

  private expire(): void {
    if (this.sessionExpired) return;
    this.sessionExpired = true;
    this.options.onSessionExpired?.();
  }

  /** 会话刷新的单飞队列：同一时刻只有一次会话端点调用，所有等待的 401 共享其结果。 */
  private refreshSession(): Promise<boolean> {
    if (!this.refreshingSession) {
      this.refreshingSession = Promise.resolve().then(() => this.options.sessionProvider?.refreshSession() ?? false)
        .then((alive) => { if (!alive) this.expire(); return alive; }, (error: unknown) => { this.expire(); throw error; })
        .finally(() => { this.refreshingSession = undefined; });
    }
    return this.refreshingSession;
  }

  private refresh(): Promise<string | null> {
    if (!this.refreshing) {
      this.refreshing = Promise.resolve().then(() => this.options.tokenProvider?.refreshAccessToken?.() ?? null)
        .catch((error: unknown) => { this.expire(); throw error; })
        .finally(() => { this.refreshing = undefined; });
    }
    return this.refreshing;
  }

  private async dispatch(request: Request): Promise<Response> {
    if (new URL(request.url).origin !== new URL(this.baseUrl).origin) throw new TypeError('sdk.error.origin');
    const provider = this.options.tokenProvider;
    const tenant = this.options.getTenantId?.();
    const token = await provider?.getAccessToken();
    const run = (accessToken: string | null | undefined) => {
      request.signal.throwIfAborted();
      // 第一次取令牌也可能异步；不能把 A 租户创建的请求改写成 B 租户请求。
      if (tenant !== this.options.getTenantId?.()) throw new Error('sdk.error.contextChanged');
      const headers = new Headers(request.headers);
      headers.delete('Authorization');
      headers.delete('X-Tenant-Id');
      if (accessToken) headers.set('Authorization', `Bearer ${accessToken}`);
      if (tenant) headers.set('X-Tenant-Id', tenant);
      return this.send(new Request(request.clone(), { headers, redirect: 'error' }));
    };
    let response = await run(token);
    if (response.status === 401 && provider?.refreshAccessToken) {
      const current = await provider.getAccessToken();
      const fresh = current && current !== token ? current : await abortable(this.refresh(), request.signal);
      // 切换租户后不得重放旧租户的写请求。
      if (tenant !== this.options.getTenantId?.()) throw new Error('sdk.error.contextChanged');
      if (fresh) {
        await response.body?.cancel();
        response = await run(fresh);
      }
    } else if (response.status === 401 && this.options.sessionProvider) {
      // Cookie 会话：并发 401 合并成一次会话刷新；会话仍在才重放一次，重放仍 401 或刷新失败都只通知一次过期。
      const alive = await abortable(this.refreshSession(), request.signal);
      if (tenant !== this.options.getTenantId?.()) throw new Error('sdk.error.contextChanged');
      if (alive) {
        await response.body?.cancel();
        response = await run(token);
      }
    }
    if (response.status === 401) this.expire();
    else if (response.ok) this.sessionExpired = false;
    const contentType = response.headers.get('content-type') ?? '';
    if (contentType.includes('json') && response.status !== 204) {
      let data: unknown;
      try { data = parsePlatformJson(await response.text()); }
      catch { throw new HttpResponseError(response.status); }
      if (!response.ok) {
        if (isErrorDetail(data) && data.status === response.status) throw new PlatformError(data);
        throw new HttpResponseError(response.status);
      }
      const headers = new Headers(response.headers);
      headers.delete('content-length');
      headers.delete('content-encoding');
      return new Response(JSON.stringify(data), { status: response.status, statusText: response.statusText, headers });
    }
    if (!response.ok) throw new HttpResponseError(response.status);
    return response;
  }
}

export interface MockRoute {
  method: string;
  path: string | RegExp;
  respond: (request: Request, match: RegExpMatchArray | null) => Response | Promise<Response>;
}

/** 未命中即失败；不会悄悄访问真实服务。路径不包含 query，处理器通过 URL 读取参数。 */
export function createMockTransport(routes: readonly MockRoute[]): RequestTransport {
  return async (request) => {
    request.signal.throwIfAborted();
    const path = new URL(request.url).pathname;
    for (const route of routes) {
      const match = typeof route.path === 'string' ? null : path.match(new RegExp(route.path.source, route.path.flags.replace(/[gy]/g, '')));
      if (route.method.toUpperCase() === request.method && (route.path === path || match)) return route.respond(request, match);
    }
    throw new Error(`sdk.error.unhandledMock: ${request.method} ${path}`);
  };
}
