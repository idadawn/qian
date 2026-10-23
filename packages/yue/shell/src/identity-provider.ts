import { HttpResponseError, isErrorDetail, parsePlatformJson, PlatformError } from '@idadawn/yue-sdk';
import type { AuthProvider, AuthResult, AuthSession, AuthTenant } from './auth';
import { safeNavigationUrl } from './menu';
import type { PasswordCredentials } from './password-login';

/** 身份服务 `POST /api/identity/login` 等接口返回的可选身份（成员或平台身份）。 */
export interface IdentityChoice { membershipId: string | null; tenantId: string | null; tenantCode: string | null; tenantName: string | null; displayName: string; platform: boolean; tenantAdmin: boolean }
/** `GET /api/identity/me`。 */
export interface IdentityMe { personId: string; tenantId: string | null; membershipId: string | null; identityRevision: number }
/** `GET /api/identity/connectors?tenant=` 的条目：登录页据此渲染连接器按钮。 */
export interface IdentityConnector { id: string; displayName: string; protocol: string; terminals: readonly string[] }
/** 连接器流程（界面落点 `/login/connector/{connectorId}?flow=` 读取）；`next` 为 wait / bind / done / error / restart。 */
export interface ConnectorFlow {
  flow: string; status: string; next: 'wait' | 'bind' | 'done' | 'error' | 'restart';
  connector: { id: string; displayName: string | null; protocol: string | null };
  tenant: { code: string | null; name: string | null };
  binding: { external: { displayName: string | null; email: string | null; username: string | null }; allowJit: boolean; verified: boolean; account: { displayName: string | null; account: string | null } | null } | null;
  error: unknown; returnUrl: string | null;
}

export interface IdentityAuthProviderOptions {
  /** 在身份服务登记的客户端标识（外壳的依赖方 client_id）；来自授权请求的 ReturnUrl 或宿主配置。 */
  clientId: string;
  /** 账号接口前缀，缺省 `/api/identity`（页面与身份服务同源）。 */
  apiBase?: string;
  /** 登录时只在该租户内选择身份。 */
  tenantId?: string | null;
  /** 平台身份在选择列表里的显示文字（`AuthTenant.id` 固定为 `platform`）。 */
  platformLabel?: string;
  fetch?: typeof fetch;
}

/** 平台身份在 `AuthTenant` 列表里的固定 id；成员身份用租户 id。 */
export const PLATFORM_TENANT = 'platform';

/**
 * 认证页族（qian.identity 同源静态制品）用的 AuthProvider：直接调同源 `/api/identity/*`，防伪令牌 cookie-to-header，
 * 账号 Cookie 由服务端写入（HttpOnly）。选择票据、改密票据只留在闭包内存，不进 URL 或存储；密码只存在于本次请求。
 */
export interface IdentityAuthProvider extends AuthProvider {
  changePassword(newPassword: string, signal: AbortSignal): Promise<AuthResult>;
  /** 已登录用户发起切换：返回可选身份，之后用 `selectTenant` 完成（旧会话在选择时吊销）。 */
  beginSwitch(signal: AbortSignal): Promise<readonly AuthTenant[]>;
  me(signal: AbortSignal): Promise<IdentityMe | null>;
  listConnectors(signal: AbortSignal, tenant?: string | null): Promise<IdentityConnector[]>;
  /** 发起连接器登录：返回上游授权地址（绝对 https），页面顶层跳转。 */
  beginConnector(connectorId: string, signal: AbortSignal, options?: { tenant?: string | null; returnUrl?: string }): Promise<string>;
  readFlow(flow: string, signal: AbortSignal): Promise<ConnectorFlow | null>;
  flowSignIn(flow: string, credentials: PasswordCredentials, signal: AbortSignal): Promise<ConnectorFlow>;
  /** 确认绑定第二步：`link` 绑定到已验证账号、`create` JIT 建号、`cancel` 放弃；完成即建立账号会话。 */
  flowBind(flow: string, mode: 'link' | 'create' | 'cancel', signal: AbortSignal): Promise<AuthResult | null>;
}

interface SignInView { status: string; ticket?: string; choices?: readonly RawChoice[]; tenant_id?: string | null; membership_id?: string | null; platform?: boolean }
interface RawChoice { membership_id: string | null; tenant_id: string | null; tenant_code: string | null; tenant_name: string | null; display_name: string; platform: boolean; tenant_admin: boolean }

async function readJson(response: Response): Promise<unknown> {
  const text = await response.text();
  if (text.length === 0) return null;
  try { return parsePlatformJson(text); } catch { throw new HttpResponseError(response.status); }
}

async function fail(response: Response): Promise<never> {
  if ((response.headers.get('content-type') ?? '').includes('json')) {
    const data = await readJson(response);
    if (isErrorDetail(data) && data.status === response.status) throw new PlatformError(data);
  }
  throw new HttpResponseError(response.status);
}

export function createIdentityAuthProvider(options: IdentityAuthProviderOptions): IdentityAuthProvider {
  const base = safeNavigationUrl(options.apiBase ?? '/api/identity');
  const send = (path: string, init?: RequestInit) => (options.fetch ?? globalThis.fetch)(`${base}${path}`, { credentials: 'same-origin', redirect: 'error', cache: 'no-store', ...init });
  // 一次性票据与可选身份只在内存里；换过身份后旧票据作废。
  let ticket: string | null = null;
  let choices: readonly IdentityChoice[] = [];
  const choice = (raw: RawChoice): IdentityChoice => ({ membershipId: raw.membership_id, tenantId: raw.tenant_id, tenantCode: raw.tenant_code, tenantName: raw.tenant_name, displayName: raw.display_name, platform: raw.platform, tenantAdmin: raw.tenant_admin });
  const tenants = (): AuthTenant[] => choices.map(item => item.platform
    ? { id: PLATFORM_TENANT, name: options.platformLabel ?? PLATFORM_TENANT }
    : { id: item.tenantId ?? '', name: item.tenantName ?? item.tenantCode ?? item.tenantId ?? '' });
  async function csrf(signal: AbortSignal): Promise<[string, string]> {
    const response = await send('/csrf', { signal, headers: { Accept: 'application/json' } });
    if (!response.ok) return fail(response);
    const data = await readJson(response) as { header?: unknown; token?: unknown };
    if (typeof data.header !== 'string' || typeof data.token !== 'string') throw new HttpResponseError(response.status);
    return [data.header, data.token];
  }
  async function post<T>(path: string, body: unknown, signal: AbortSignal): Promise<{ response: Response; data: T }> {
    const [header, token] = await csrf(signal);
    const response = await send(path, { method: 'POST', signal, body: JSON.stringify(body), headers: { Accept: 'application/json', 'Content-Type': 'application/json', [header]: token } });
    if (!response.ok) return fail(response);
    return { response, data: await readJson(response) as T };
  }
  async function get<T>(path: string, signal: AbortSignal): Promise<{ response: Response; data: T }> {
    const response = await send(path, { signal, headers: { Accept: 'application/json' } });
    if (!response.ok) return fail(response);
    return { response, data: await readJson(response) as T };
  }
  async function complete(view: SignInView, signal: AbortSignal): Promise<AuthResult> {
    switch (view.status) {
      case 'authenticated': {
        ticket = null;
        const current = choices.find(item => view.platform ? item.platform : item.membershipId === view.membership_id);
        const me = await provider.me(signal);
        const session: AuthSession = {
          subjectId: me?.personId ?? '', displayName: current?.displayName ?? '', tenantId: view.platform ? PLATFORM_TENANT : view.tenant_id ?? '',
          tenants: choices.length > 0 ? tenants() : [{ id: view.platform ? PLATFORM_TENANT : view.tenant_id ?? '', name: view.platform ? options.platformLabel ?? PLATFORM_TENANT : view.tenant_id ?? '' }],
        };
        return { kind: 'authenticated', session };
      }
      case 'selection_required':
        ticket = view.ticket ?? null; choices = (view.choices ?? []).map(choice);
        return { kind: 'select-tenant', tenants: tenants() };
      case 'password_change_required':
        ticket = view.ticket ?? null;
        return { kind: 'change-password' };
      default:
        throw new HttpResponseError(500);
    }
  }
  function membershipFor(tenantId: string): string | null {
    if (tenantId === PLATFORM_TENANT) { if (!choices.some(item => item.platform)) throw new TypeError('shell.error.tenant'); return null; }
    const found = choices.find(item => !item.platform && item.tenantId === tenantId);
    if (!found) throw new TypeError('shell.error.tenant');
    return found.membershipId;
  }
  const provider: IdentityAuthProvider = {
    async signIn(credentials, signal) {
      choices = [];
      const { data } = await post<SignInView>('/login', { client_id: options.clientId, identifier: credentials.username, password: credentials.password, tenant_id: options.tenantId ?? null }, signal);
      return complete(data, signal);
    },
    async selectTenant(tenantId, signal) {
      if (!ticket) throw new TypeError('shell.error.ticket');
      const { data } = await post<SignInView>('/login/select', { ticket, membership_id: membershipFor(tenantId) }, signal);
      const result = await complete(data, signal);
      if (result.kind !== 'authenticated') throw new HttpResponseError(500);
      return result.session;
    },
    async switchTenant(tenantId, signal) {
      await provider.beginSwitch(signal);
      return provider.selectTenant(tenantId, signal);
    },
    async beginSwitch(signal) {
      const { data } = await post<SignInView>('/session/switch', {}, signal);
      const result = await complete(data, signal);
      if (result.kind !== 'select-tenant') throw new HttpResponseError(500);
      return result.tenants;
    },
    async changePassword(newPassword, signal) {
      if (!ticket) throw new TypeError('shell.error.ticket');
      const { data } = await post<SignInView>('/login/password', { ticket, new_password: newPassword }, signal);
      return complete(data, signal);
    },
    async me(signal) {
      const response = await send('/me', { signal, headers: { Accept: 'application/json' } });
      if (response.status === 401) { await response.body?.cancel(); return null; }
      if (!response.ok) return fail(response);
      const data = await readJson(response) as { person_id: string; tenant_id?: string | null; membership_id?: string | null; identity_revision: number | string };
      return { personId: data.person_id, tenantId: data.tenant_id ?? null, membershipId: data.membership_id ?? null, identityRevision: Number(data.identity_revision) };
    },
    async listConnectors(signal, tenant) {
      const { data } = await get<{ items: { id: string; display_name: string; protocol: string; terminals: string[] }[] }>(`/connectors${tenant ? `?tenant=${encodeURIComponent(tenant)}` : ''}`, signal);
      return data.items.map(item => ({ id: item.id, displayName: item.display_name, protocol: item.protocol, terminals: item.terminals }));
    },
    async beginConnector(connectorId, signal, extra) {
      const { data } = await post<{ flow: string; redirect_uri: string }>(`/connectors/${encodeURIComponent(connectorId)}/begin`, { client_id: options.clientId, tenant: extra?.tenant ?? options.tenantId ?? null, return_url: extra?.returnUrl ?? null }, signal);
      return safeNavigationUrl(data.redirect_uri, true);
    },
    async readFlow(flow, signal) {
      const response = await send(`/connector-flows/${encodeURIComponent(flow)}`, { signal, headers: { Accept: 'application/json' } });
      if (response.status === 404) { await response.body?.cancel(); return null; }
      if (!response.ok) return fail(response);
      return toFlow(await readJson(response));
    },
    async flowSignIn(flow, credentials, signal) {
      const { data } = await post<unknown>(`/connector-flows/${encodeURIComponent(flow)}/signin`, { identifier: credentials.username, password: credentials.password }, signal);
      return toFlow(data);
    },
    async flowBind(flow, mode, signal) {
      const { response, data } = await post<unknown>(`/connector-flows/${encodeURIComponent(flow)}/bind`, { mode }, signal);
      if (response.status === 204 || data === null) return null;
      const view = data as SignInView;
      return view.status ? complete(view, signal) : null;
    },
  };
  return provider;
}

function toFlow(raw: unknown): ConnectorFlow {
  const value = raw as Record<string, unknown>;
  const connector = (value.connector ?? {}) as Record<string, unknown>; const tenant = (value.tenant ?? {}) as Record<string, unknown>;
  const binding = value.binding as Record<string, unknown> | null | undefined;
  const external = (binding?.external ?? {}) as Record<string, unknown>; const account = binding?.account as Record<string, unknown> | null | undefined;
  return {
    flow: String(value.flow ?? ''), status: String(value.status ?? ''), next: (value.next as ConnectorFlow['next']) ?? 'error',
    connector: { id: String(connector.id ?? ''), displayName: (connector.display_name as string | null) ?? null, protocol: (connector.protocol as string | null) ?? null },
    tenant: { code: (tenant.code as string | null) ?? null, name: (tenant.name as string | null) ?? null },
    binding: binding ? { external: { displayName: (external.display_name as string | null) ?? null, email: (external.email as string | null) ?? null, username: (external.username as string | null) ?? null }, allowJit: binding.allow_jit === true, verified: binding.verified === true,
      account: account ? { displayName: (account.display_name as string | null) ?? null, account: (account.account as string | null) ?? null } : null } : null,
    error: value.error ?? null, returnUrl: typeof value.return_url === 'string' ? value.return_url : null,
  };
}
