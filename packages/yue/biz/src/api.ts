import type { PlatformClient } from '@idadawn/yue-sdk';
import type { components, paths } from './biz.generated';

// 业务选择器只经注入的 PlatformClient 取数：路径、参数与响应类型都由 contracts/biz.openapi.json 生成，组件里没有第二条网络路径。
export type DirectoryOrganization = components['schemas']['Organization'];
export type DirectoryOrganizationType = components['schemas']['OrganizationType'];
export type DirectoryMember = components['schemas']['DirectoryMember'];
export type DirectoryMembership = components['schemas']['Membership'];
export type AuthzRole = components['schemas']['Role'];
export type PermissionDefinition = components['schemas']['PermissionDefinition'];
export type PermissionResourceType = components['schemas']['ResourceType'];
export type PermissionCatalog = components['schemas']['PermissionCatalog'];
export type { paths as BizPaths } from './biz.generated';

/** 目录成员接口单页上限（Identity Server 拒绝大于 200 的 limit）。 */
export const DIRECTORY_PAGE_LIMIT = 200;

export async function loadOrganizations(client: PlatformClient, signal?: AbortSignal): Promise<DirectoryOrganization[]> {
  const { data } = await client.api<paths>().GET('/api/directory/orgs/tree', { signal });
  return data?.items ?? [];
}

export async function loadOrganizationTypes(client: PlatformClient, signal?: AbortSignal): Promise<DirectoryOrganizationType[]> {
  const { data } = await client.api<paths>().GET('/api/directory/org-types', { signal });
  return data?.items ?? [];
}

export interface MemberScope {
  /** 只取该组织下的成员；不传取全租户。 */
  organizationId?: string | null | undefined;
  /** 组织范围是否含整棵子树，默认 true。 */
  descendants?: boolean | undefined;
  /** 最多加载的成员数，默认 1000；达到上限且后面还有成员时 truncated 为 true。 */
  max?: number | undefined;
}

export interface MemberPage { items: DirectoryMember[]; truncated: boolean }

/** 目录接口没有关键字与按 id 查询，选择器按页读到上限后在本地过滤；页序与服务端一致（账号、成员 id）。 */
export async function loadMembers(client: PlatformClient, { organizationId, descendants = true, max = 1000 }: MemberScope = {}, signal?: AbortSignal): Promise<MemberPage> {
  const api = client.api<paths>();
  const page = async (offset: number, limit: number) => (organizationId
    ? await api.GET('/api/directory/orgs/{id}/members', { params: { path: { id: organizationId }, query: { descendants, offset, limit } }, signal })
    : await api.GET('/api/directory/members', { params: { query: { offset, limit } }, signal })).data?.items ?? [];
  const cap = Math.max(1, Math.floor(max));
  const items: DirectoryMember[] = [];
  while (items.length < cap) {
    const limit = Math.min(DIRECTORY_PAGE_LIMIT, cap - items.length);
    const rows = await page(items.length, limit);
    items.push(...rows);
    if (rows.length < limit) return { items, truncated: false };
  }
  return { items, truncated: (await page(cap, 1)).length > 0 };
}

export async function loadRoles(client: PlatformClient, signal?: AbortSignal): Promise<AuthzRole[]> {
  const { data } = await client.api<paths>().GET('/api/authz/roles', { signal });
  return data?.items ?? [];
}

export async function loadPermissionCatalog(client: PlatformClient, signal?: AbortSignal): Promise<PermissionCatalog> {
  const { data } = await client.api<paths>().GET('/api/authz/permissions', { signal });
  return { items: data?.items ?? [], resources: data?.resources ?? [] };
}
