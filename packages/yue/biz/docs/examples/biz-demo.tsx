import { useState } from 'react';
import type { IconName } from '@idadawn/yue-core';
import { createMockTransport, PlatformClient, type MockRoute } from '@idadawn/yue-sdk';
import { ApiSelect, AuthorizationTree, DictSelect, IconPicker, OrganizeSelect, RoleSelect, UserSelect, createBizI18n, orgTypeDictionary } from '@idadawn/yue-biz';

// 示例只替换 SDK 网络层：mock 中间件按 PRD 7.4.3 的目录 API（与 Identity Server 一致的 snake_case）与 PRD 7.5 的角色 / 权限点目录草案返回数据，
// 组件代码与接真实服务时相同。靶场 E2E、Rspress 场景与站点 E2E 共用这一份示例。
const uuid = (n: number) => `0193a000-0000-7000-8000-${String(n).padStart(12, '0')}`;
const TENANT = uuid(900);
const org = (n: number, name: string, type: string, parent?: number) => ({ n, name, type, parent });
const ORG_ROWS = [org(1, '总部', 'company'), org(2, '研发中心', 'dept', 1), org(3, '前端组', 'team', 2), org(4, '后端组', 'team', 2), org(5, '销售部', 'dept', 1)];
const pathOf = (n: number): string => { const row = ORG_ROWS.find((item) => item.n === n)!; return `${row.parent ? pathOf(row.parent) : '/'}${uuid(n)}/`; };
const ORGANIZATIONS = ORG_ROWS.map((row) => ({ id: uuid(row.n), name: row.name, type_code: row.type, ...(row.parent ? { parent_id: uuid(row.parent) } : {}), path: pathOf(row.n), depth: pathOf(row.n).split('/').length - 2 }));
const member = (n: number, name: string, account: string, orgId: number, status = 'active', admin = false) =>
  ({ orgId, membership: { id: uuid(100 + n), person_id: uuid(200 + n), tenant_id: TENANT, display_name: name, status, is_tenant_administrator: admin, account, identity_revision: 1 } });
const MEMBERS = [member(1, '李四', 'lisi', 2), member(2, '钱七', 'qianqi', 4, 'locked'), member(3, '王五', 'wangwu', 3), member(4, '张三', 'zhangsan', 1, 'active', true), member(5, '赵六', 'zhaoliu', 5, 'disabled')];
const ROLES = [{ code: 'tenant.owner', builtin: true }, { code: 'tenant.admin', builtin: true }, { code: 'tenant.developer', builtin: true }, { code: 'tenant.user', builtin: true }, { code: 'sales.manager', name: '销售经理', builtin: false }];
const permission = (resource: string, action: string, risk = 'low', stepUp = false) => ({ permission: `${resource}:${action}`, resource, action, risk, stepUp, terminals: [] });
const CATALOG = {
  items: [
    permission('qian.sample.crm:customer', 'read'), permission('qian.sample.crm:customer', 'write', 'medium'), permission('qian.sample.crm:customer', 'export', 'high'),
    permission('qian.identity:member', 'read'), permission('qian.identity:member', 'create', 'medium'), permission('qian.identity:member', 'disable', 'high'),
    permission('qian.authz:role', 'read'), permission('qian.authz:role', 'assign', 'critical', true),
  ],
  resources: [{ resource: 'qian.sample.crm:customer', name: '客户' }, { resource: 'qian.identity:member', name: '成员' }, { resource: 'qian.authz:role', name: '角色' }],
};
const GRANTS: Record<string, string[]> = { 'tenant.user': ['qian.sample.crm:customer:read'], 'sales.manager': ['qian.sample.crm:customer:read', 'qian.sample.crm:customer:write'] };
const CUSTOMERS = ['华东贸易', '华南物流', '北方制造', '西部能源', '东海科技', '南山食品'].map((name, index) => ({ id: `customer-${index + 1}`, name }));

/** 与 Identity Server 相同的 ErrorDetail：limit 越界返回 400。 */
const problem = (status: number, code: string, message: string) => new Response(JSON.stringify({ type: `https://qian.dev/problems/protocol/${code}`, title: message, status, code, message, trace_id: '0123456789abcdef0123456789abcdef', pointer: '#/page' }), { status, headers: { 'content-type': 'application/problem+json' } });
function page(request: Request, rows: typeof MEMBERS) {
  const query = new URL(request.url).searchParams;
  const offset = Number(query.get('offset') ?? 0); const limit = Number(query.get('limit') ?? 50);
  if (!Number.isInteger(offset) || offset < 0 || !Number.isInteger(limit) || limit < 1 || limit > 200) return problem(400, 'BAD_REQUEST', '请求参数无效');
  return Response.json({ items: rows.slice(offset, offset + limit).map((row) => ({ membership: row.membership, deleted: false })) });
}

export function createBizMockClient({ failMembersOnce = false } = {}): PlatformClient {
  let failed = !failMembersOnce;
  const members = (request: Request, rows: typeof MEMBERS) => { if (!failed) { failed = true; return new Response(null, { status: 503 }); } return page(request, rows); };
  const routes: MockRoute[] = [
    { method: 'GET', path: '/api/directory/orgs/tree', respond: () => Response.json({ items: ORGANIZATIONS }) },
    { method: 'GET', path: '/api/directory/org-types', respond: () => Response.json({ items: [{ code: 'company', name: '公司' }, { code: 'dept', name: '部门' }, { code: 'team', name: '小组' }] }) },
    { method: 'GET', path: '/api/directory/members', respond: (request) => members(request, MEMBERS) },
    { method: 'GET', path: /^\/api\/directory\/orgs\/([^/]+)\/members$/, respond: (request, match) => {
      const target = ORGANIZATIONS.find((item) => item.id === match![1]);
      if (!target) return problem(404, 'NOT_FOUND', '组织不存在');
      const subtree = new URL(request.url).searchParams.get('descendants') === 'true';
      return members(request, MEMBERS.filter((row) => { const path = pathOf(row.orgId); return subtree ? path.startsWith(target.path) : path === target.path; }));
    } },
    { method: 'GET', path: '/api/authz/roles', respond: () => Response.json({ items: ROLES }) },
    { method: 'GET', path: '/api/authz/permissions', respond: () => Response.json(CATALOG) },
    { method: 'GET', path: '/api/sample-crm/customers', respond: (request) => { const keyword = new URL(request.url).searchParams.get('keyword') ?? ''; return Response.json({ items: CUSTOMERS.filter((item) => item.name.includes(keyword)) }); } },
  ];
  return new PlatformClient({ baseUrl: 'https://tenant.mock', getTenantId: () => TENANT, transport: createMockTransport(routes) });
}

interface CustomerPaths { '/api/sample-crm/customers': { get: { parameters: { query?: { keyword?: string } }; responses: { 200: { content: { 'application/json': { items: { id: string; name: string }[] } } } } } } }
const i18n = createBizI18n();
export const BIZ_COMPONENTS = ['ApiSelect', 'DictSelect', 'OrganizeSelect', 'UserSelect', 'RoleSelect', 'AuthorizationTree', 'IconPicker'] as const;

function Value({ value }: { value: string | string[] | null }) {
  return <output aria-live="polite" data-testid="biz-demo-value" className="block text-sm text-muted-foreground">{Array.isArray(value) ? value.join(', ') : value ?? ''}</output>;
}

/** 角色授予的组合示意（S1-AZ-005 的页面形状）：选部门 → 部门内选人 → 选角色 → 勾选该角色的菜单 / 按钮权限。 */
function RoleGrant({ client }: { client: PlatformClient }) {
  const [organization, setOrganization] = useState<string | null>(null);
  const [users, setUsers] = useState<string[]>([]);
  const [role, setRole] = useState<string | null>(null);
  const [grants, setGrants] = useState<string[]>([]);
  return <form aria-label="角色授予" className="grid max-w-2xl gap-4" onSubmit={(event) => event.preventDefault()}>
    <div className="grid gap-1"><span className="text-sm font-medium">部门</span><OrganizeSelect client={client} i18n={i18n} label="部门" value={organization} onChange={(next) => { setOrganization(next); setUsers([]); }} /></div>
    <div className="grid gap-1"><span className="text-sm font-medium">成员</span><UserSelect client={client} i18n={i18n} label="成员" organizationId={organization} valueKey="person" multiple value={users} onChange={setUsers} /></div>
    <div className="grid gap-1"><span className="text-sm font-medium">角色</span><RoleSelect client={client} i18n={i18n} label="角色" value={role} onChange={(next) => { setRole(next); setGrants(next ? GRANTS[next] ?? [] : []); }} /></div>
    {role && <AuthorizationTree client={client} i18n={i18n} label="角色权限" value={grants} onChange={setGrants} />}
    <output aria-live="polite" data-testid="biz-demo-grant" className="text-sm text-muted-foreground">{JSON.stringify({ organization, users, role, grants })}</output>
  </form>;
}

export default function BizDemo({ kind = 'OrganizeSelect', multiple = false, invalid = false }: { kind?: string; multiple?: boolean; invalid?: boolean }) {
  const [client] = useState(() => createBizMockClient({ failMembersOnce: invalid }));
  const [single, setSingle] = useState<string | null>(null);
  const [many, setMany] = useState<string[]>([]);
  const [icon, setIcon] = useState<IconName | null>('building');
  const [grants, setGrants] = useState<string[]>(['qian.sample.crm:customer:read']);
  const selection = multiple ? { multiple: true as const, value: many, onChange: setMany } : { value: single, onChange: setSingle };
  const shown = multiple ? many : single;
  if (kind === 'RoleGrant') return <RoleGrant client={client} />;
  if (kind === 'ApiSelect') return <div className="grid gap-2"><ApiSelect client={client} i18n={i18n} label="客户" {...selection}
    load={async (current, { keyword, signal }) => (await current.api<CustomerPaths>().GET('/api/sample-crm/customers', { params: { query: { keyword } }, signal })).data?.items ?? []}
    getValue={(item) => item.id} getLabel={(item) => item.name} /><Value value={shown} /></div>;
  if (kind === 'DictSelect') return <div className="grid gap-2"><DictSelect client={client} i18n={i18n} label="组织类型" dictionary={orgTypeDictionary} {...selection} /><Value value={shown} /></div>;
  if (kind === 'UserSelect') return <div className="grid gap-2"><UserSelect client={client} i18n={i18n} label="成员" {...selection} /><Value value={shown} /></div>;
  if (kind === 'RoleSelect') return <div className="grid gap-2"><RoleSelect client={client} i18n={i18n} label="角色" {...selection} /><Value value={shown} /></div>;
  if (kind === 'AuthorizationTree') return <div className="grid gap-2"><AuthorizationTree client={client} i18n={i18n} label="角色权限" value={grants} onChange={setGrants} /><Value value={grants} /></div>;
  if (kind === 'IconPicker') return <div className="grid gap-2"><IconPicker i18n={i18n} label="菜单图标" value={icon} onChange={setIcon} /><Value value={icon} /></div>;
  return <div className="grid gap-2"><OrganizeSelect client={client} i18n={i18n} label="部门" {...selection} /><Value value={shown} /></div>;
}

export const BIZ_DEMOS = Object.fromEntries(BIZ_COMPONENTS.map((name) => [name, BizDemo]));
