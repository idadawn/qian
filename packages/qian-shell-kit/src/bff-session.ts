import { HttpResponseError, isErrorDetail, parsePlatformJson, PlatformError, type SessionProvider } from '@idadawn/yue-sdk';
import { safeNavigationUrl } from '@idadawn/yue-shell/menu';

/**
 * 依赖方（Identity.Client）`GET {authBase}/session` 的快照：规范身份字段加显示名，不含令牌。
 * 身份修订是 int64：安全整数范围内是 number，超出时按平台 JSON 策略保留为十进制字符串，不经 `Number()` 丢精度。
 */
export interface BffSession {
  subject: string;
  kind: 'user' | 'service';
  tenantId: string | null;
  membershipId: string | null;
  identityRevision: number | string | null;
  displayName: string | null;
}

export interface BffSessionClientOptions {
  /** 依赖方端点前缀，来自运行时配置 `authBase`（缺省 `/auth`）。 */
  authBase?: string;
  fetch?: typeof fetch;
}

/** 防伪令牌：Identity.Client `GET {authBase}/csrf` 的头名与值（绑定当前会话主体，Kun Cookie 中间件对已认证的非安全方法校验）。 */
export interface CsrfToken { header: string; token: string }

/** 浏览器零令牌的会话客户端：快照、强制刷新（供 SDK sessionProvider）、防伪令牌、登出与登录 / 切换租户的跳转地址。 */
export interface BffSessionClient extends SessionProvider {
  getSession(signal?: AbortSignal): Promise<BffSession | null>;
  /** 取一枚新的防伪令牌；没有会话（401）时为 null。 */
  csrf(signal?: AbortSignal): Promise<CsrfToken | null>;
  signOut(signal?: AbortSignal): Promise<void>;
  /** `GET {authBase}/login?returnTo=…[&prompt=select_account]`：同源相对地址，顶层跳转后由依赖方发起 OIDC code + PKCE。 */
  loginUrl(returnTo: string, options?: { prompt?: 'select_account' }): string;
}

/** 会话快照的形状校验（bootstrap 聚合端点的 `session` 与 `GET {authBase}/session` 同形）。 */
export function isBffSession(value: unknown): value is BffSession {
  if (!value || typeof value !== 'object') return false;
  const v = value as Record<string, unknown>;
  return typeof v.subject === 'string' && v.subject.length > 0 && (v.kind === 'user' || v.kind === 'service')
    && (v.tenantId === null || typeof v.tenantId === 'string') && (v.membershipId === null || v.membershipId === undefined || typeof v.membershipId === 'string')
    && (v.identityRevision === null || v.identityRevision === undefined || typeof v.identityRevision === 'number' || (typeof v.identityRevision === 'string' && /^\d{1,19}$/.test(v.identityRevision)))
    && (v.displayName === null || v.displayName === undefined || typeof v.displayName === 'string');
}

/** 规范化会话快照：缺省字段补 null；身份修订原样保留。 */
export function toBffSession(value: BffSession): BffSession {
  return { subject: value.subject, kind: value.kind, tenantId: value.tenantId, membershipId: value.membershipId ?? null,
    identityRevision: value.identityRevision ?? null, displayName: value.displayName ?? null };
}

async function readJson(response: Response): Promise<unknown> {
  const text = await response.text();
  try { return parsePlatformJson(text); } catch { throw new HttpResponseError(response.status); }
}

/** 非 2xx 响应：ErrorDetail → PlatformError，其余只带状态。 */
async function fail(response: Response): Promise<never> {
  const contentType = response.headers.get('content-type') ?? '';
  if (contentType.includes('json')) {
    const data = await readJson(response);
    if (isErrorDetail(data) && data.status === response.status) throw new PlatformError(data);
  }
  throw new HttpResponseError(response.status);
}

export function createBffSessionClient({ authBase = '/auth', fetch: fetchImpl }: BffSessionClientOptions = {}): BffSessionClient {
  const base = safeNavigationUrl(authBase);
  const send = (input: string, init?: RequestInit) => (fetchImpl ?? globalThis.fetch)(input, { credentials: 'same-origin', redirect: 'error', cache: 'no-store', ...init });
  // 防伪令牌绑定当前会话主体：每次写请求前取一枚新的；没有会话（401）时返回 null。
  async function csrf(signal?: AbortSignal): Promise<CsrfToken | null> {
    const response = await send(`${base}/csrf`, { signal, headers: { Accept: 'application/json' } });
    if (response.status === 401) { await response.body?.cancel(); return null; }
    if (!response.ok) return fail(response);
    const data = await readJson(response) as { headerName?: unknown; token?: unknown };
    if (typeof data.headerName !== 'string' || typeof data.token !== 'string') throw new HttpResponseError(response.status);
    return { header: data.headerName, token: data.token };
  }
  async function post(path: string, signal?: AbortSignal): Promise<Response | null> {
    const token = await csrf(signal);
    if (!token) return null;
    const response = await send(`${base}${path}`, { method: 'POST', signal, headers: { Accept: 'application/json', [token.header]: token.token } });
    if (response.status === 401) { await response.body?.cancel(); return null; }
    return response;
  }
  return {
    csrf,
    async getSession(signal) {
      const response = await send(`${base}/session`, { signal, headers: { Accept: 'application/json' } });
      if (response.status === 401) { await response.body?.cancel(); return null; }
      if (!response.ok) return fail(response);
      const data = await readJson(response);
      if (!isBffSession(data)) throw new HttpResponseError(response.status);
      return toBffSession(data);
    },
    async refreshSession() {
      const response = await post('/session/refresh');
      if (!response) return false;
      if (!response.ok) return fail(response);
      await response.body?.cancel();
      return true;
    },
    async signOut(signal) {
      const response = await post('/logout', signal);
      if (!response) return;
      if (!response.ok) return fail(response);
      await response.body?.cancel();
    },
    loginUrl(returnTo, options) {
      const target = safeNavigationUrl(returnTo);
      return safeNavigationUrl(`${base}/login?returnTo=${encodeURIComponent(target)}${options?.prompt ? `&prompt=${options.prompt}` : ''}`);
    },
  };
}
