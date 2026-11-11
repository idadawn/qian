import { cleanup, render, screen, within } from '@testing-library/react';
import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { userEvent } from 'vitest/browser';
import { ICON_NAMES, type IconName } from '@idadawn/yue-core';
import { builtinLanguagePacks, createI18n, type PlatformClient, type PlatformI18n } from '@idadawn/yue-sdk';
import type { AuthzRole, PermissionCatalog } from './api';
import { ApiSelect, AuthorizationTree, DictSelect, IconPicker, OrganizeSelect, RoleSelect, UserSelect, addBizLanguagePacks, bizLanguagePacks, createBizI18n, orgTypeDictionary } from './index';
import { CATALOG, MEMBERS, ORGANIZATIONS, ROLES, TENANT, createFixture } from './test/fixtures';

// S1-YUE-010 验收 1（原 S1-YUE-009 验收 3）：组件不直接发请求，只经注入的 PlatformClient；i18n 全部走 key。

interface Customer { id: string; name: string }
interface CustomerPaths { '/api/customers': { get: { parameters: { query?: { keyword?: string } }; responses: { 200: { content: { 'application/json': { items: Customer[] } } } } } } }
const CUSTOMERS: Customer[] = [{ id: 'c1', name: '华东贸易' }];

function AllSelectors({ client, i18n }: { client: PlatformClient; i18n: PlatformI18n }) {
  const [organization, setOrganization] = useState<string | null>(ORGANIZATIONS[1]!.id);
  const [users, setUsers] = useState<string[]>([MEMBERS[0]!.member.membership.id]);
  const [role, setRole] = useState<string | null>('tenant.admin');
  const [type, setType] = useState<string | null>('dept');
  const [customer, setCustomer] = useState<string | null>(null);
  const [grants, setGrants] = useState<string[]>([CATALOG.items[1]!.permission]);
  const [icon, setIcon] = useState<IconName | null>('building');
  return <div>
    <OrganizeSelect client={client} i18n={i18n} label="L-org" value={organization} onChange={setOrganization} />
    <UserSelect client={client} i18n={i18n} label="L-user" multiple value={users} onChange={setUsers} />
    <RoleSelect client={client} i18n={i18n} label="L-role" value={role} onChange={setRole} />
    <DictSelect client={client} i18n={i18n} label="L-dict" dictionary={orgTypeDictionary} value={type} onChange={setType} />
    <ApiSelect<Customer> client={client} i18n={i18n} label="L-api" value={customer} onChange={setCustomer} getValue={(item) => item.id} getLabel={(item) => item.name}
      load={async (current, { keyword, signal }) => (await current.api<CustomerPaths>().GET('/api/customers', { params: { query: { keyword } }, signal })).data?.items ?? []} />
    <AuthorizationTree client={client} i18n={i18n} label="L-tree" value={grants} onChange={setGrants} />
    <IconPicker i18n={i18n} label="L-icon" value={icon} onChange={setIcon} />
  </div>;
}

const customersRoute = { method: 'GET', path: '/api/customers', respond: () => Response.json({ items: CUSTOMERS }) };
const EXPECTED = ['/api/authz/permissions', '/api/authz/roles', '/api/customers?keyword=', '/api/directory/members?offset=0&limit=200', '/api/directory/org-types', '/api/directory/orgs/tree'];

async function settle(requests: { url: string }[]) {
  await vi.waitFor(() => expect([...new Set(requests.map((request) => request.url))].sort()).toEqual(EXPECTED));
  await vi.waitFor(() => expect(screen.queryAllByText(/加载中|biz\.loading/)).toHaveLength(0));
  await screen.findByRole('tree', { name: 'L-tree' });
}

describe('组件只经注入的 PlatformClient 取数', () => {
  const blocked = vi.fn();
  beforeEach(() => {
    blocked.mockReset();
    // 任何绕过 PlatformClient 的网络出口都记下并失败：fetch、XHR、WebSocket、EventSource、sendBeacon。
    vi.stubGlobal('fetch', (...args: unknown[]) => { blocked('fetch', args); return Promise.reject(new Error('fetch 不得被组件直接调用')); });
    vi.spyOn(XMLHttpRequest.prototype, 'open').mockImplementation(function open() { blocked('xhr'); throw new Error('XHR 不得被组件直接调用'); });
    vi.stubGlobal('WebSocket', function WebSocketBlocked() { blocked('websocket'); throw new Error('WebSocket 不得被组件直接调用'); });
    vi.stubGlobal('EventSource', function EventSourceBlocked() { blocked('eventsource'); throw new Error('EventSource 不得被组件直接调用'); });
    vi.spyOn(navigator, 'sendBeacon').mockImplementation(() => { blocked('beacon'); return false; });
  });
  afterEach(() => { cleanup(); vi.unstubAllGlobals(); vi.restoreAllMocks(); });

  it('七个组件的全部请求都经过宿主客户端的中间件（带租户头），没有任何直接网络调用', async () => {
    const { client, requests } = createFixture({ routes: [customersRoute] });
    render(<AllSelectors client={client} i18n={createBizI18n()} />);
    await settle(requests);
    expect(requests.every((request) => request.method === 'GET' && request.tenant === TENANT)).toBe(true);
    // 交互后（打开选择器、服务端搜索）仍只经同一客户端。
    await userEvent.click(screen.getByRole('combobox', { name: 'L-api' }));
    await vi.waitFor(() => expect(document.activeElement?.getAttribute('aria-label')).toBe('搜索L-api'));
    await userEvent.keyboard('华');
    await vi.waitFor(() => expect(requests.at(-1)!.url).toBe(`/api/customers?keyword=${encodeURIComponent('华')}`));
    expect(blocked).not.toHaveBeenCalled();
  });

  it('换一个注入的客户端，请求随之改走新客户端，旧客户端不再收到请求；新数据到达前界面不沿用旧客户端的结果', async () => {
    const first = createFixture({ routes: [customersRoute] });
    let release!: () => void;
    const gate = new Promise<void>((resolve) => { release = resolve; });
    // 新客户端的权限点目录先挂起：这段时间里授权树只能是加载态，不能还显示旧客户端的目录。
    const orders: PermissionCatalog = { items: [{ permission: 'qian.sample.crm:order:read', resource: 'qian.sample.crm:order', action: 'read', risk: 'low', stepUp: false, terminals: [] }], resources: [{ resource: 'qian.sample.crm:order', name: '订单' }] };
    const second = createFixture({ routes: [customersRoute, { method: 'GET', path: '/api/authz/permissions', respond: async () => { await gate; return Response.json(orders); } }] });
    const i18n = createBizI18n();
    const { rerender } = render(<AllSelectors client={first.client} i18n={i18n} />);
    await settle(first.requests);
    expect(screen.getByRole('treeitem', { name: '客户' })).toBeTruthy();
    const before = first.requests.length;
    rerender(<AllSelectors client={second.client} i18n={i18n} />);
    await vi.waitFor(() => expect(second.requests.map((request) => request.url)).toContain('/api/authz/permissions'));
    expect(screen.queryByRole('tree', { name: 'L-tree' })).toBeNull();
    expect(screen.getByTestId('yue-authorization-tree-loading')).toBeTruthy();
    release();
    await settle(second.requests);
    expect(screen.getByRole('treeitem', { name: '订单' })).toBeTruthy();
    expect(screen.queryByRole('treeitem', { name: '客户' })).toBeNull();
    expect(first.requests).toHaveLength(before);
    expect(blocked).not.toHaveBeenCalled();
  });
});

/** 伪语言包：把 SDK、data 与 biz 的全部 key 译成 [[key]]。凡是组件自带的文字都会显示成标记，硬编码的文字会原样漏出来。 */
function pseudoI18n(): PlatformI18n {
  const i18n = createI18n('zh-CN');
  addBizLanguagePacks(i18n);
  const keys = new Set([...Object.keys(builtinLanguagePacks['zh-CN']), ...Object.keys(i18n.instance.getResourceBundle('zh-CN', 'translation') as Record<string, string>)]);
  i18n.addLanguagePack('zh-CN', Object.fromEntries([...keys].map((key) => [key, `[[${key}]]`])));
  return i18n;
}

// 让按 code / 动作拼出的文案 key 全部有机会显示：内置角色取语言包里登记的全部 code，权限点目录含全部动作，并带高风险、关键操作与二次验证。
const BIZ_KEYS = Object.keys(bizLanguagePacks['zh-CN']);
const ALL_ROLES: AuthzRole[] = [
  ...BIZ_KEYS.filter((key) => key.startsWith('biz.role.') && key !== 'biz.role.builtin').map((key) => ({ code: key.slice('biz.role.'.length), builtin: true })),
  ...ROLES.filter((role) => !role.builtin),
];
const FULL_CATALOG: PermissionCatalog = {
  items: BIZ_KEYS.filter((key) => key.startsWith('biz.action.')).map((key) => key.slice('biz.action.'.length)).map((action) => ({
    permission: `qian.sample.crm:customer:${action}`, resource: 'qian.sample.crm:customer', action,
    risk: action === 'export' ? 'high' : action === 'delete' ? 'critical' : 'low', stepUp: action === 'delete', terminals: [],
  })),
  resources: CATALOG.resources,
};

describe('i18n 全部走 key', () => {
  afterEach(cleanup);

  it('三种内置语言的 biz 文案 key 集合一致，且值都是非空字符串', () => {
    const keys = (locale: keyof typeof bizLanguagePacks) => Object.keys(bizLanguagePacks[locale]).sort();
    expect(keys('zh-TW')).toEqual(keys('zh-CN'));
    expect(keys('en-US')).toEqual(keys('zh-CN'));
    for (const pack of Object.values(bizLanguagePacks)) for (const [key, text] of Object.entries(pack)) {
      expect(key).toMatch(/^biz\./);
      expect(text.trim()).not.toBe('');
    }
  });

  it('伪语言包下，组件各个状态（正常、已选多项、无匹配、空、加载中、错误与重试、成员截断）的文字与可访问名称只有 key 标记、注入的 label 与目录数据', async () => {
    const i18n = pseudoI18n();
    const texts: string[] = [];
    const collect = () => {
      const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
      for (let node = walker.nextNode(); node; node = walker.nextNode()) texts.push(node.textContent ?? '');
      for (const element of document.body.querySelectorAll('*')) for (const name of ['aria-label', 'placeholder', 'title', 'alt']) { const value = element.getAttribute(name); if (value) texts.push(value); }
    };
    const open = async (label: string) => {
      await userEvent.click(screen.getByRole('combobox', { name: label }));
      await vi.waitFor(() => expect(document.activeElement?.getAttribute('aria-label')).toBe('[[biz.search]]'));
    };
    const close = async () => {
      await userEvent.keyboard('{Escape}');
      await vi.waitFor(() => expect(screen.queryByRole('listbox')).toBeNull());
    };
    const emptyText = async (testId: string) => vi.waitFor(() => expect(screen.getByTestId(testId).textContent).not.toBe(''));

    // ① 正常态：七个组件同时渲染，依次打开弹层，把弹层里的文字也收进来。
    {
      const { client, requests } = createFixture({ routes: [customersRoute,
        { method: 'GET', path: '/api/authz/roles', respond: () => Response.json({ items: ALL_ROLES }) },
        { method: 'GET', path: '/api/authz/permissions', respond: () => Response.json(FULL_CATALOG) }] });
      render(<AllSelectors client={client} i18n={i18n} />);
      await vi.waitFor(() => expect([...new Set(requests.map((request) => request.url))].sort()).toEqual(EXPECTED));
      await screen.findByRole('tree', { name: 'L-tree' });
      await vi.waitFor(() => expect(screen.queryAllByText('[[biz.loading]]')).toHaveLength(0));
      collect();
      for (const label of ['L-user', 'L-role', 'L-dict', 'L-api', 'L-icon']) {
        await open(label);
        await screen.findAllByRole('option');
        collect();
        await close();
      }
      await userEvent.click(screen.getByRole('button', { name: /^L-org/ }));
      await screen.findByRole('tree', { name: 'L-org' });
      collect();
      expect(within(screen.getByRole('tree', { name: 'L-tree' })).getAllByRole('treeitem').some((item) => item.textContent?.includes('[[biz.action.read]]'))).toBe(true);
      cleanup();
    }

    // ② 多选的已选摘要：两项用 Intl 的连接词，三项以上是「已选 n 项」；多选组织的弹层里有清空按钮。
    {
      const { client } = createFixture();
      const [o1, o2, o3] = ORGANIZATIONS.map((item) => item.id) as [string, string, string];
      const [m1, m2, m3] = MEMBERS.map(({ member }) => member.membership.id) as [string, string, string];
      render(<>
        <OrganizeSelect client={client} i18n={i18n} label="L-org-two" multiple value={[o1, o2]} onChange={() => {}} />
        <OrganizeSelect client={client} i18n={i18n} label="L-org-three" multiple value={[o1, o2, o3]} onChange={() => {}} />
        <UserSelect client={client} i18n={i18n} label="L-user-three" multiple value={[m1, m2, m3]} onChange={() => {}} />
      </>);
      await vi.waitFor(() => expect(screen.getByRole('button', { name: /^L-org-two/ }).textContent).toContain(ORGANIZATIONS[1]!.name));
      await vi.waitFor(() => expect(screen.queryAllByText('[[biz.loading]]')).toHaveLength(0));
      collect();
      await userEvent.click(screen.getByRole('button', { name: /^L-org-three/ }));
      await screen.findByRole('tree', { name: 'L-org-three' });
      collect();
      cleanup();
    }

    // ③ 无匹配与空：客户端过滤没有结果；服务端返回空列表；权限点目录为空。
    {
      const { client } = createFixture({ routes: [{ method: 'GET', path: '/api/authz/permissions', respond: () => Response.json({ items: [], resources: [] }) }] });
      render(<>
        <RoleSelect client={client} i18n={i18n} label="L-role" value={null} onChange={() => {}} />
        <ApiSelect<Customer> client={client} i18n={i18n} label="L-api" value={null} onChange={() => {}} getValue={(item) => item.id} getLabel={(item) => item.name} load={async () => []} />
        <AuthorizationTree client={client} i18n={i18n} label="L-tree" value={[]} onChange={() => {}} />
      </>);
      await screen.findByTestId('yue-authorization-tree-empty');
      collect();
      await open('L-role');
      await screen.findAllByRole('option');
      await userEvent.keyboard('zzzz');
      await vi.waitFor(() => expect(screen.queryAllByRole('option')).toHaveLength(0));
      await emptyText('yue-role-select-empty');
      collect();
      await close();
      await open('L-api');
      await emptyText('yue-api-select-empty');
      collect();
      cleanup();
    }

    // ④ 加载中：请求挂起时，触发器旁与弹层里的加载态。
    {
      const hang = () => new Promise<Response>(() => {});
      const { client } = createFixture({ routes: ['/api/directory/orgs/tree', '/api/authz/roles', '/api/authz/permissions'].map((path) => ({ method: 'GET', path, respond: hang })) });
      render(<>
        <OrganizeSelect client={client} i18n={i18n} label="L-org" value={null} onChange={() => {}} />
        <RoleSelect client={client} i18n={i18n} label="L-role" value={null} onChange={() => {}} />
        <AuthorizationTree client={client} i18n={i18n} label="L-tree" value={[]} onChange={() => {}} />
      </>);
      await screen.findByTestId('yue-organize-select-loading');
      await screen.findByTestId('yue-authorization-tree-loading');
      collect();
      await open('L-role');
      await emptyText('yue-role-select-empty');
      collect();
      cleanup();
    }

    // ⑤ 错误与重试：每个取数的组件在触发器旁给出错误与重试，弹层里也显示错误。
    {
      const { client } = createFixture({
        failOnce: ['/api/directory/orgs/tree', '/api/directory/members', '/api/authz/roles', '/api/authz/permissions'],
        routes: [{ method: 'GET', path: '/api/directory/org-types', respond: () => new Response(null, { status: 503 }) }, { method: 'GET', path: '/api/customers', respond: () => new Response(null, { status: 503 }) }],
      });
      render(<AllSelectors client={client} i18n={i18n} />);
      await vi.waitFor(() => expect(screen.getAllByRole('alert')).toHaveLength(6));
      collect();
      await open('L-role');
      await emptyText('yue-role-select-empty');
      collect();
      cleanup();
    }

    // ⑥ 成员超过上限：触发器旁的截断提示。
    {
      const { client } = createFixture();
      render(<UserSelect client={client} i18n={i18n} label="L-user" value={null} onChange={() => {}} max={2} />);
      await screen.findByTestId('yue-user-select-notice');
      collect();
      cleanup();
    }

    // biz 语言包里的每个 key 都至少显示过一次：新增了状态或文案却没在这里渲染，这一条就会失败。TreeSelect 的两个 data 文案同样要出现。
    const rendered = new Set(texts.flatMap((text) => [...text.matchAll(/\[\[([a-zA-Z0-9.\-_]+)\]\]/g)].map((match) => match[1]!)));
    expect(BIZ_KEYS.filter((key) => !rendered.has(key)), '没有渲染到的 biz 文案 key').toEqual([]);
    expect(['data.select', 'data.clear'].filter((key) => !rendered.has(key))).toEqual([]);
    // 除 key 标记外只允许：注入的 label（L- 开头）、目录与业务数据、图标登记名，以及两项摘要里 Intl.ListFormat 给出的本地化连接词。
    // 加载失败时选项为空，触发器显示选中值本身（成员 id、类型编码），也是数据。
    const data = new Set<string>([
      ...ORGANIZATIONS.map((item) => item.name), ...MEMBERS.flatMap(({ member }) => [member.membership.display_name, member.membership.account, member.membership.id]),
      ...ALL_ROLES.flatMap((role) => [role.name ?? '', role.code]), ...FULL_CATALOG.items.map((item) => item.resource), ...FULL_CATALOG.resources.map((item) => item.name),
      ...CUSTOMERS.map((item) => item.name), '公司', '部门', 'company', 'dept', ...ICON_NAMES,
    ]);
    const conjunctions = new Intl.ListFormat('zh-CN', { type: 'conjunction', style: 'short' }).formatToParts(['a', 'b', 'c']).filter((part) => part.type === 'literal').map((part) => part.value.trim()).filter(Boolean);
    const separator = new RegExp(['\\[\\[[a-zA-Z0-9.\\-_]+\\]\\]', '[\\s·,，、/]+', ...conjunctions.map((value) => value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'))].join('|'), 'u');
    const leaks = texts.flatMap((text) => text.split(separator)).filter((part) => /[\p{L}\p{N}]/u.test(part) && !data.has(part) && !/^L-[a-z-]+$/.test(part));
    expect([...new Set(leaks)]).toEqual([]);
  });
});
