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

/** 成员检索每页显示的条数；请求时多取一条，用来判断后面是否还有。 */
export const MEMBER_SEARCH_LIMIT = 50;
/** 关键字长度上限，与 Identity Server 一致（按 UTF-16 码元计）。 */
export const MEMBER_KEYWORD_MAX_LENGTH = 64;
/** 按 id 批量查询每批的上限，与 Identity Server 一致。 */
export const MEMBER_IDS_MAX = 100;

/**
 * 把输入整理成目录接口接受的关键字：控制字符换成空格、去掉首尾空白，过长时截到 64 个码元（不切断代理对）。
 * 整理后为空返回 undefined，调用方据此不带 q、按页列出成员。
 */
export function memberKeyword(input: string): string | undefined {
  let keyword = input.replace(/\p{Cc}/gu, ' ').trim();
  if (keyword.length > MEMBER_KEYWORD_MAX_LENGTH) {
    keyword = keyword.slice(0, MEMBER_KEYWORD_MAX_LENGTH);
    if (/[\uD800-\uDBFF]$/.test(keyword)) keyword = keyword.slice(0, -1);
    keyword = keyword.trim();
  }
  return keyword || undefined;
}

export interface MemberSearch {
  /** 只在该组织下检索；不传在全租户检索。 */
  organizationId?: string | null | undefined;
  /** 组织范围是否含整棵子树，默认 true。 */
  descendants?: boolean | undefined;
  /** 关键字，按显示名、账号与拼音由服务端匹配；空白时按页列出。 */
  keyword?: string | undefined;
  /** 本页条数，默认 50。 */
  limit?: number | undefined;
}

export interface MemberSearchPage { items: DirectoryMember[]; more: boolean }

/** 成员检索（S1-IDP-010 的 q 参数）：取第一页，按账号、成员 id 排序；more 为 true 表示后面还有，应提示继续输入。 */
export async function searchMembers(client: PlatformClient, { organizationId, descendants = true, keyword, limit = MEMBER_SEARCH_LIMIT }: MemberSearch = {}, signal?: AbortSignal): Promise<MemberSearchPage> {
  const api = client.api<paths>();
  const q = memberKeyword(keyword ?? '');
  const size = Math.min(DIRECTORY_PAGE_LIMIT - 1, Math.max(1, Math.floor(limit)));
  const query = { ...(q === undefined ? {} : { q }), offset: 0, limit: size + 1 };
  const rows = (organizationId
    ? await api.GET('/api/directory/orgs/{id}/members', { params: { path: { id: organizationId }, query: { descendants, ...query } }, signal })
    : await api.GET('/api/directory/members', { params: { query }, signal })).data?.items ?? [];
  return { items: rows.slice(0, size), more: rows.length > size };
}

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const NIL_UUID = '00000000-0000-0000-0000-000000000000';

/**
 * 按 id 批量取成员，选择器用它回显已选值的显示名：membership 用 ids、person 用 person_ids，每批至多 100 个。
 * 不是 UUID 的值不发送；别的租户、不存在或已从目录删除的 id 不在结果里。
 */
export async function loadMembersByIds(client: PlatformClient, ids: readonly string[], key: 'membership' | 'person' = 'membership', signal?: AbortSignal): Promise<DirectoryMember[]> {
  const wanted = [...new Set(ids.filter((id) => UUID.test(id) && id !== NIL_UUID))];
  const api = client.api<paths>();
  const items: DirectoryMember[] = [];
  for (let start = 0; start < wanted.length; start += MEMBER_IDS_MAX) {
    const batch = wanted.slice(start, start + MEMBER_IDS_MAX);
    const query = key === 'person' ? { person_ids: batch, limit: batch.length } : { ids: batch, limit: batch.length };
    items.push(...(await api.GET('/api/directory/members', { params: { query }, signal })).data?.items ?? []);
  }
  return items;
}

export async function loadRoles(client: PlatformClient, signal?: AbortSignal): Promise<AuthzRole[]> {
  const { data } = await client.api<paths>().GET('/api/authz/roles', { signal });
  return data?.items ?? [];
}

export async function loadPermissionCatalog(client: PlatformClient, signal?: AbortSignal): Promise<PermissionCatalog> {
  const { data } = await client.api<paths>().GET('/api/authz/permissions', { signal });
  return { items: data?.items ?? [], resources: data?.resources ?? [] };
}
