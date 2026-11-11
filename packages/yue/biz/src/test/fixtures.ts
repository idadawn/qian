import { createMockTransport, PlatformClient, type MockRoute } from '@idadawn/yue-sdk';
import type { AuthzRole, DirectoryMember, DirectoryOrganization, PermissionCatalog } from '../api';

// 测试夹具：形状与 Identity Server 目录 API（PRD 7.4.3）和 PRD 7.5 角色 / 权限点目录草案一致；取数只经 SDK 的 mock 传输层。
export const uuid = (n: number) => `0193a000-0000-7000-8000-${String(n).padStart(12, '0')}`;
export const TENANT = uuid(900);

const tree: [number, string, number?][] = [[1, '总部'], [2, '研发中心', 1], [3, '前端组', 2], [4, '后端组', 2], [5, '销售部', 1]];
const pathOf = (n: number): string => { const parent = tree.find(([id]) => id === n)?.[2]; return `${parent ? pathOf(parent) : '/'}${uuid(n)}/`; };
export const ORGANIZATIONS: DirectoryOrganization[] = tree.map(([n, name, parent]) => ({ id: uuid(n), name, type_code: n === 1 ? 'company' : 'dept', ...(parent ? { parent_id: uuid(parent) } : {}), path: pathOf(n), depth: pathOf(n).split('/').length - 2 }));

export function directoryMember(n: number, name: string, account: string, status: 'active' | 'disabled' | 'locked' = 'active'): DirectoryMember {
  return { membership: { id: uuid(100 + n), person_id: uuid(200 + n), tenant_id: TENANT, display_name: name, status, is_tenant_administrator: false, account, identity_revision: 1 }, deleted: false };
}
/** 成员及其主归属组织（按账号排序，与服务端页序一致）。 */
export const MEMBERS: { member: DirectoryMember; org: number }[] = [
  { member: directoryMember(1, '李四', 'lisi'), org: 2 }, { member: directoryMember(2, '钱七', 'qianqi', 'locked'), org: 4 },
  { member: directoryMember(3, '王五', 'wangwu'), org: 3 }, { member: directoryMember(4, '张三', 'zhangsan'), org: 1 }, { member: directoryMember(5, '赵六', 'zhaoliu', 'disabled'), org: 5 },
];
export const ROLES: AuthzRole[] = [{ code: 'tenant.owner', builtin: true }, { code: 'tenant.admin', builtin: true }, { code: 'tenant.user', builtin: true }, { code: 'sales.manager', name: '销售经理', builtin: false }];
const permission = (resource: string, action: string, risk: 'low' | 'medium' | 'high' | 'critical' = 'low', stepUp = false) => ({ permission: `${resource}:${action}`, resource, action, risk, stepUp, terminals: [] });
export const CATALOG: PermissionCatalog = {
  items: [
    permission('qian.sample.crm:customer', 'write', 'medium'), permission('qian.sample.crm:customer', 'read'), permission('qian.sample.crm:customer', 'export', 'high'),
    permission('qian.authz:role', 'read'), permission('qian.authz:role', 'assign', 'critical', true), permission('qian.audit:log', 'export'),
  ],
  resources: [{ resource: 'qian.sample.crm:customer', name: '客户' }, { resource: 'qian.authz:role', name: '角色' }],
};

export interface FixtureOptions {
  /** 覆盖成员列表（例如生成大量成员测「后面还有」的提示）。 */
  members?: { member: DirectoryMember; org: number }[] | undefined;
  /** 这些路径第一次请求返回 503（非 ErrorDetail），之后正常。 */
  failOnce?: string[] | undefined;
  routes?: MockRoute[] | undefined;
}

export interface Fixture {
  client: PlatformClient;
  /** 经过 PlatformClient 中间件的请求：方法、路径与查询、租户头。 */
  requests: { method: string; url: string; tenant: string | null }[];
}

export function createFixture({ members = MEMBERS, failOnce = [], routes = [] }: FixtureOptions = {}): Fixture {
  const requests: Fixture['requests'] = [];
  const pending = new Set(failOnce);
  const fail = (path: string) => { if (!pending.has(path)) return false; pending.delete(path); return true; };
  const problem = (code: string, pointer: string) => new Response(JSON.stringify({ type: `https://qian.dev/problems/protocol/${code}`, title: '请求无效', status: 400, code, message: '请求无效', trace_id: '0123456789abcdef0123456789abcdef', pointer }), { status: 400, headers: { 'content-type': 'application/problem+json' } });
  // 与 Identity Server 相同的检索口径：q 按显示名与账号忽略大小写包含匹配（夹具没有拼音），ids / person_ids 按 id 过滤；三者至多一个，越界即 400。
  const page = (request: Request, rows: typeof members) => {
    const query = new URL(request.url).searchParams;
    const offset = Number(query.get('offset') ?? 0); const limit = Number(query.get('limit') ?? 50);
    if (limit > 200 || limit < 1 || offset < 0) return problem('BAD_REQUEST', '#/page');
    const keyword = query.get('q')?.trim().toLocaleLowerCase(); const ids = query.getAll('ids'); const persons = query.getAll('person_ids');
    if ([query.has('q'), ids.length > 0, persons.length > 0].filter(Boolean).length > 1 || keyword !== undefined && (keyword.length < 1 || keyword.length > 64) || ids.length > 100 || persons.length > 100) return problem('INPUT_INVALID', '#/q');
    const matched = rows.filter(({ member: { membership } }) => (keyword === undefined || membership.display_name.toLocaleLowerCase().includes(keyword) || membership.account.toLocaleLowerCase().includes(keyword))
      && (ids.length === 0 || ids.includes(membership.id)) && (persons.length === 0 || persons.includes(membership.person_id)));
    return Response.json({ items: matched.slice(offset, offset + limit).map((row) => row.member) });
  };
  const transport = createMockTransport([
    ...routes,
    { method: 'GET', path: '/api/directory/orgs/tree', respond: () => fail('/api/directory/orgs/tree') ? new Response(null, { status: 503 }) : Response.json({ items: ORGANIZATIONS }) },
    { method: 'GET', path: '/api/directory/org-types', respond: () => Response.json({ items: [{ code: 'company', name: '公司' }, { code: 'dept', name: '部门' }] }) },
    { method: 'GET', path: '/api/directory/members', respond: (request) => fail('/api/directory/members') ? new Response(null, { status: 503 }) : page(request, members) },
    { method: 'GET', path: /^\/api\/directory\/orgs\/([^/]+)\/members$/, respond: (request, match) => {
      const target = ORGANIZATIONS.find((item) => item.id === match![1])!;
      const subtree = new URL(request.url).searchParams.get('descendants') === 'true';
      return page(request, members.filter((row) => { const path = pathOf(row.org); return subtree ? path.startsWith(target.path) : path === target.path; }));
    } },
    { method: 'GET', path: '/api/authz/roles', respond: () => fail('/api/authz/roles') ? new Response(null, { status: 503 }) : Response.json({ items: ROLES }) },
    { method: 'GET', path: '/api/authz/permissions', respond: () => fail('/api/authz/permissions') ? new Response(null, { status: 503 }) : Response.json(CATALOG) },
  ]);
  const client = new PlatformClient({
    baseUrl: 'https://tenant.test', getTenantId: () => TENANT, transport,
    middleware: [(request, next) => { const url = new URL(request.url); requests.push({ method: request.method, url: url.pathname + url.search, tenant: request.headers.get('X-Tenant-Id') }); return next(request); }],
  });
  return { client, requests };
}
