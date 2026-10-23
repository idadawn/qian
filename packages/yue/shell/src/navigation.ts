import type { PlatformClient } from '@idadawn/yue-sdk';
import type { ShellMenuItem } from './menu';

/** `GET /capabilities/navigation` 的条目（能力导航目录，服务端已按调用者权限与租户 scope 过滤）。 */
export interface NavigationCatalogItem { resourceRef: string; pluginId: string; route: string; title: string; icon: string | null; order: number }
/** `GET /authz/me/permissions` 的条目：当前主体在本租户的有效权限与判定。 */
export interface PermissionGrant { permission: string; resource: string; action: string; risk: string; stepUp: boolean; terminals: string[]; allowed: boolean; reason: string }

interface NavigationPaths {
  '/capabilities/navigation': { get: { responses: { 200: { content: { 'application/json': { cacheScope: string; items: NavigationCatalogItem[] } } } } } };
  '/authz/me/permissions': { get: { responses: { 200: { content: { 'application/json': { items: PermissionGrant[] } } } } } };
}

/** 后端菜单 → 外壳菜单项：资源引用作 id，页面标题作显示文字，路由经 `page` 类型解析（URL 安全边界在注册表里）。 */
export function toMenu(items: readonly NavigationCatalogItem[]): ShellMenuItem[] {
  return [...items].sort((a, b) => a.order - b.order || a.resourceRef.localeCompare(b.resourceRef))
    .map(item => ({ id: item.resourceRef, labelKey: item.resourceRef, label: item.title, type: 'page', target: item.route }));
}

/** 当前主体可见的菜单；登录、切换租户、会话刷新后都重新拉取。 */
export async function fetchNavigation(client: PlatformClient, signal?: AbortSignal): Promise<NavigationCatalogItem[]> {
  const result = await client.api<NavigationPaths>().GET('/capabilities/navigation', { signal });
  return result.data?.items ?? [];
}

/** 当前主体在本租户的有效权限。 */
export async function fetchPermissions(client: PlatformClient, signal?: AbortSignal): Promise<PermissionGrant[]> {
  const result = await client.api<NavigationPaths>().GET('/authz/me/permissions', { signal });
  return result.data?.items ?? [];
}
