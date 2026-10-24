import type { ShellMenuItem } from '@idadawn/yue-shell/menu';

/** 能力导航目录 `GET /api/capabilities/navigation` 的条目（服务端已按调用者权限、租户 scope 与菜单偏好过滤排序）；bootstrap 的 `navigation` 与之逐项相同。 */
export interface NavigationCatalogItem { resourceRef: string; pluginId: string; route: string; title: string; icon: string | null; order: number }

/** 后端菜单 → 外壳菜单项：资源引用作 id，页面标题作显示文字，路由经 `page` 类型解析（URL 安全边界在注册表里）。 */
export function toMenu(items: readonly NavigationCatalogItem[]): ShellMenuItem[] {
  return [...items].sort((a, b) => a.order - b.order || a.resourceRef.localeCompare(b.resourceRef))
    .map(item => ({ id: item.resourceRef, labelKey: item.resourceRef, label: item.title, type: 'page', target: item.route }));
}

export function isNavigationItem(value: unknown): value is NavigationCatalogItem {
  if (!value || typeof value !== 'object') return false;
  const v = value as Record<string, unknown>;
  return typeof v.resourceRef === 'string' && typeof v.pluginId === 'string' && typeof v.route === 'string' && typeof v.title === 'string'
    && (v.icon === null || v.icon === undefined || typeof v.icon === 'string') && typeof v.order === 'number';
}
