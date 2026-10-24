import type { RequestMiddleware } from '@idadawn/yue-sdk';
import type { BffSessionClient } from './bff-session';

/** 规范路径 `/api/...` 换到网关上的 API 挂载前缀；不在 `/api` 下的路径原样返回。 */
export function mapApiPath(pathname: string, apiBase: string): string {
  if (apiBase === '/api' || !(pathname === '/api' || pathname.startsWith('/api/'))) return pathname;
  return apiBase + pathname.slice('/api'.length);
}

/**
 * 外壳客户端的前缀映射：契约、远程模块与 bridge.invoke 一律写规范路径 `/api/...`，发出前按运行时配置 apiBase 换前缀
 * （改 ConfigMap 后刷新即走新前缀，远程模块不改、不重编译）。
 */
export function apiPrefixMiddleware(apiBase: string): RequestMiddleware {
  return async (request, next) => {
    const url = new URL(request.url);
    const mapped = mapApiPath(url.pathname, apiBase);
    if (mapped === url.pathname) return next(request);
    url.pathname = mapped;
    return next(await rebuild(request, url, request.headers));
  };
}

/**
 * Cookie 会话的写请求带防伪令牌：非安全方法先向依赖方取一枚新的（绑定当前会话主体，每次重放都重新取）；没有会话时原样发出，
 * 由服务端 401 进入 SDK 的会话刷新。令牌只在这一次请求的头里，不缓存、不落存储。
 */
export function csrfMiddleware(session: Pick<BffSessionClient, 'csrf'>): RequestMiddleware {
  return async (request, next) => {
    if (['GET', 'HEAD', 'OPTIONS'].includes(request.method)) return next(request);
    const token = await session.csrf(request.signal);
    if (!token) return next(request);
    const headers = new Headers(request.headers);
    headers.set(token.header, token.token);
    return next(await rebuild(request, new URL(request.url), headers));
  };
}

async function rebuild(request: Request, url: URL, headers: Headers): Promise<Request> {
  const body = request.method === 'GET' || request.method === 'HEAD' ? undefined : await request.clone().arrayBuffer();
  return new Request(url, { method: request.method, headers, body, credentials: request.credentials, redirect: request.redirect, cache: request.cache, signal: request.signal, referrerPolicy: request.referrerPolicy });
}
