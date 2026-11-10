import { cleanup, render, screen, within } from '@testing-library/react';
import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { userEvent } from 'vitest/browser';
import { ICON_NAMES, type IconName } from '@idadawn/yue-core';
import { builtinLanguagePacks, createI18n, type PlatformClient, type PlatformI18n } from '@idadawn/yue-sdk';
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

  it('换一个注入的客户端，请求随之改走新客户端；旧客户端不再收到请求', async () => {
    const first = createFixture({ routes: [customersRoute] });
    const second = createFixture({ routes: [customersRoute] });
    const i18n = createBizI18n();
    const { rerender } = render(<AllSelectors client={first.client} i18n={i18n} />);
    await settle(first.requests);
    const before = first.requests.length;
    rerender(<AllSelectors client={second.client} i18n={i18n} />);
    await settle(second.requests);
    expect(first.requests).toHaveLength(before);
    expect(blocked).not.toHaveBeenCalled();
  });
});

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

  it('伪语言包下，组件界面上的文字与可访问名称只有 key 标记、注入的 label 与目录数据，没有硬编码文案', async () => {
    // 把 SDK、data 与 biz 的全部 key 译成 [[key]]：凡是组件自带的文字都会显示成标记，硬编码的文字会原样漏出来。
    const i18n = createI18n('zh-CN');
    addBizLanguagePacks(i18n);
    const keys = new Set([...Object.keys(builtinLanguagePacks['zh-CN']), ...Object.keys(i18n.instance.getResourceBundle('zh-CN', 'translation') as Record<string, string>)]);
    i18n.addLanguagePack('zh-CN', Object.fromEntries([...keys].map((key) => [key, `[[${key}]]`])));
    const { client, requests } = createFixture({ routes: [customersRoute] });
    render(<AllSelectors client={client} i18n={i18n} />);
    await vi.waitFor(() => expect([...new Set(requests.map((request) => request.url))].sort()).toEqual(EXPECTED));
    await screen.findByRole('tree', { name: 'L-tree' });
    await vi.waitFor(() => expect(screen.queryAllByText('[[biz.loading]]')).toHaveLength(0));
    // 依次打开弹层，把弹层里的文字也收进来。
    const texts: string[] = [];
    const collect = (root: Element) => {
      const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
      for (let node = walker.nextNode(); node; node = walker.nextNode()) texts.push(node.textContent ?? '');
      for (const element of [root, ...root.querySelectorAll('*')]) for (const name of ['aria-label', 'placeholder', 'title', 'alt']) { const value = element.getAttribute(name); if (value) texts.push(value); }
    };
    collect(document.body);
    for (const label of ['L-user', 'L-role', 'L-dict', 'L-api', 'L-icon']) {
      await userEvent.click(screen.getByRole('combobox', { name: label }));
      await screen.findByRole('listbox');
      collect(document.body);
      await userEvent.keyboard('{Escape}');
      await vi.waitFor(() => expect(screen.queryByRole('listbox')).toBeNull());
    }
    await userEvent.click(screen.getByRole('button', { name: /^L-org/ }));
    await screen.findByRole('tree', { name: 'L-org' });
    collect(document.body);
    const data = new Set<string>([
      ...ORGANIZATIONS.map((item) => item.name), ...MEMBERS.flatMap(({ member }) => [member.membership.display_name, member.membership.account]),
      ...ROLES.flatMap((role) => [role.name ?? '', role.code]), ...CATALOG.items.map((item) => item.resource), ...CATALOG.resources.map((item) => item.name),
      ...CUSTOMERS.map((item) => item.name), '公司', '部门', 'L-org', 'L-user', 'L-role', 'L-dict', 'L-api', 'L-tree', 'L-icon',
      // IconPicker 的选项就是 core Icon 的登记名本身（数据，不是文案）。
      ...ICON_NAMES,
    ]);
    const leaks = texts.flatMap((text) => text.split(/\[\[[a-zA-Z0-9.\-_]+\]\]|[\s·,，、/]+/u)).filter((part) => /[\p{L}\p{N}]/u.test(part) && !data.has(part));
    expect([...new Set(leaks)]).toEqual([]);
    expect(texts.some((text) => text.includes('[[biz.select]]') || text.includes('[[data.select]]') || text.includes('[[biz.clear]]'))).toBe(true);
    expect(within(screen.getByRole('tree', { name: 'L-tree' })).getAllByRole('treeitem').some((item) => item.textContent?.includes('[[biz.action.read]]'))).toBe(true);
  });
});
