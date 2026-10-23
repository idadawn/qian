import { HttpResponseError, type PlatformClient } from '@idadawn/yue-sdk';
import type { RemoteRegistration, ShellContext } from '@idadawn/qian-plugin-sdk';
import { isBffSession, toBffSession, type BffSession } from './bff-session';
import { isNavigationItem, type NavigationCatalogItem } from './navigation';

/**
 * 网关 bootstrap 聚合端点（`GET /api/shell/bootstrap`，Qian.Gateway 的 MapQianShellBootstrap）的响应：会话快照（与 `GET /auth/session` 同形）、
 * 外壳上下文、本租户实际挂载且带 UI 清单的远程、挂载与权益键（`scope|组件 id`）、已放行的权限码、能力导航（下游失败时为 null）、是否代管。不含任何令牌。
 */
export interface ShellBootstrap {
  session: BffSession;
  context: ShellContext;
  remotes: RemoteRegistration[];
  mounted: string[];
  entitled: string[];
  permissions: string[];
  navigation: NavigationCatalogItem[] | null;
  managed: boolean;
}

type BootstrapPaths = Record<string, { get: { responses: { 200: { content: { 'application/json': unknown } } } } }>;

const strings = (value: unknown): value is string[] => Array.isArray(value) && value.every(item => typeof item === 'string');
const named = (value: unknown) => value === null || (!!value && typeof value === 'object' && typeof (value as Record<string, unknown>).id === 'string' && typeof (value as Record<string, unknown>).name === 'string');

/** 形状校验：只认契约字段的类型，远程清单的细节由 ShellHost.registerRemote 再校验。 */
export function isShellBootstrap(value: unknown): value is ShellBootstrap {
  if (!value || typeof value !== 'object') return false;
  const v = value as Record<string, unknown>;
  const context = v.context as Record<string, unknown> | null | undefined;
  if (!isBffSession(v.session) || !context || typeof context !== 'object') return false;
  const membership = context.membership as Record<string, unknown> | null | undefined;
  if (!named(context.tenant) || !named(context.app) || typeof context.locale !== 'string' || (context.theme !== 'light' && context.theme !== 'dark')) return false;
  if (!(membership === null || (!!membership && typeof membership.id === 'string' && strings(membership.permissions)))) return false;
  if (!Array.isArray(v.remotes) || !v.remotes.every(remote => !!remote && typeof remote === 'object' && typeof remote.id === 'string' && typeof remote.version === 'string'
    && typeof remote.scope === 'string' && !!remote.manifest && typeof remote.manifest === 'object')) return false;
  return strings(v.mounted) && strings(v.entitled) && strings(v.permissions) && typeof v.managed === 'boolean'
    && (v.navigation === null || (Array.isArray(v.navigation) && v.navigation.every(isNavigationItem)));
}

/**
 * 经外壳客户端取 bootstrap：与其它平台调用同一条 Cookie 会话通道（401 由 SDK 合并成一次会话刷新后重放，过期只通知一次）；
 * `url` 是运行时配置的 bootstrapUrl（规范路径随 apiBase 映射）。形状不符按 502 处理。
 */
export async function fetchBootstrap(client: PlatformClient, url: string, signal?: AbortSignal): Promise<ShellBootstrap> {
  const result = await client.api<BootstrapPaths>().GET(url, { signal });
  const data = result.data;
  if (!isShellBootstrap(data)) throw new HttpResponseError(502);
  return { ...data, session: toBffSession(data.session) };
}
