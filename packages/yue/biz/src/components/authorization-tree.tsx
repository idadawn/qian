import { useMemo } from 'react';
import { Empty, cn, type TestIdProps } from '@idadawn/yue-core';
import { Tree, type TreeNode } from '@idadawn/yue-data';
import type { PlatformClient, PlatformI18n } from '@idadawn/yue-sdk';
import { loadPermissionCatalog, type PermissionCatalog, type PermissionDefinition } from '../api';
import { useRemoteResource } from '../remote-options';
import { SelectStatus } from './select-status';

// 菜单 / 按钮授权树（PRD 7.5 最小角色授权页）建在 @yue/data 的 Tree 复选上，不另做一套树。
// 口径：一个资源类型是一个菜单（UI 清单的页面以 <resource>:read 为门槛权限），菜单下的按钮是该资源的各个动作，read 排第一表示「菜单可见」。
// 勾选任一按钮自动带上同菜单的 read；取消 read 同时取消该菜单的其余按钮——没有菜单就没有按钮。值只含权限点全名，分组节点不进入值。

const MENU_PREFIX = 'menu:';
const READ = 'read';

export interface AuthorizationMenu {
  /** 资源类型全名 <plugin-id>:<resource>。 */
  resource: string;
  /** 菜单可见的权限点（该资源的 read）；资源没有 read 时为 undefined，按钮之间不联动。 */
  read: string | undefined;
  permissions: PermissionDefinition[];
}

/** 按资源类型把权限点目录分成菜单，菜单与按钮都沿用目录顺序（read 置顶）。 */
export function groupPermissionCatalog(catalog: PermissionCatalog): AuthorizationMenu[] {
  const menus = new Map<string, PermissionDefinition[]>();
  for (const item of catalog.items) {
    const list = menus.get(item.resource);
    if (list) list.push(item); else menus.set(item.resource, [item]);
  }
  return [...menus].map(([resource, items]) => {
    const permissions = [...items].sort((a, b) => Number(b.action === READ) - Number(a.action === READ));
    return { resource, read: permissions.find((item) => item.action === READ)?.permission, permissions };
  });
}

export function permissionLabel(item: PermissionDefinition, i18n: PlatformI18n): string {
  const key = `biz.action.${item.action}`;
  const notes = [item.risk === 'high' || item.risk === 'critical' ? i18n.t(`biz.risk.${item.risk}`) : undefined, item.stepUp ? i18n.t('biz.stepUp') : undefined];
  return [i18n.instance.exists(key) ? i18n.t(key) : item.action, ...notes].filter((part) => !!part).join(' · ');
}

/** 授权树节点：菜单节点 id 为 menu:<资源>，按钮节点 id 为权限点全名。 */
export function buildAuthorizationTree(catalog: PermissionCatalog, i18n: PlatformI18n): TreeNode[] {
  const names = new Map(catalog.resources.map((resource) => [resource.resource, resource.name]));
  return groupPermissionCatalog(catalog).map((menu) => ({
    id: MENU_PREFIX + menu.resource,
    label: names.get(menu.resource) ?? menu.resource,
    children: menu.permissions.map((item) => ({ id: item.permission, label: permissionLabel(item, i18n) })),
  }));
}

/** 把 Tree 复选的结果规整为授予值：去掉分组节点，按「按钮依赖菜单」联动 read；目录里没有的已授予权限点原样保留。 */
export function linkAuthorization(menus: readonly AuthorizationMenu[], previous: readonly string[], next: readonly string[]): string[] {
  const before = new Set(previous);
  const after = new Set(next.filter((id) => !id.startsWith(MENU_PREFIX)));
  for (const menu of menus) {
    if (!menu.read) continue;
    const buttons = menu.permissions.map((item) => item.permission).filter((permission) => permission !== menu.read);
    if (before.has(menu.read) && !after.has(menu.read)) for (const permission of buttons) after.delete(permission);
    else if (buttons.some((permission) => after.has(permission) && !before.has(permission))) after.add(menu.read);
  }
  return [...after];
}

export interface AuthorizationTreeProps extends TestIdProps {
  /** 宿主注入的 PlatformClient；权限点目录经它读 GET /api/authz/permissions。 */
  client: PlatformClient;
  i18n: PlatformI18n;
  /** 树的可访问名称。 */
  label: string;
  /** 已授予的权限点全名。 */
  value: readonly string[];
  onChange: (value: string[]) => void;
  className?: string | undefined;
}

export function AuthorizationTree({ client, i18n, label, value, onChange, className, testId }: AuthorizationTreeProps) {
  const id = testId ?? 'yue-authorization-tree';
  const remote = useRemoteResource({ client, load: (current, { signal }) => loadPermissionCatalog(current, signal) });
  const catalog = remote.data;
  const menus = useMemo(() => (catalog ? groupPermissionCatalog(catalog) : []), [catalog]);
  const nodes = catalog ? buildAuthorizationTree(catalog, i18n) : [];
  // Tree 的分支状态由分支自身 id 与后代共同决定：全部按钮已授予的菜单补上分组 id，才显示为全选而不是半选。
  const granted = new Set(value);
  const checked = [...value, ...menus.filter((menu) => menu.permissions.every((item) => granted.has(item.permission))).map((menu) => MENU_PREFIX + menu.resource)];
  return <div data-slot="authorization-tree" data-testid={id} className={cn('grid gap-2', className)}>
    {catalog && (nodes.length
      ? <Tree nodes={nodes} checkedIds={checked} onCheckedChange={(next) => onChange(linkAuthorization(menus, value, next))} defaultExpandedIds={nodes.map((node) => node.id)} aria-label={label} testId={`${id}-tree`} />
      : <Empty title={i18n.t('biz.empty')} testId={`${id}-empty`} />)}
    <SelectStatus slot="authorization-tree" testId={id} i18n={i18n} loading={remote.loading && !catalog} error={remote.error} onRetry={remote.reload} />
  </div>;
}
