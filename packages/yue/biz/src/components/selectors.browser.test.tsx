import { cleanup, render, screen, within } from '@testing-library/react';
import { useState } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { userEvent } from 'vitest/browser';
import type { IconName } from '@idadawn/yue-core';
import { blockingViolations, describeViolations, runAxe } from '../test/axe';
import { ORGANIZATIONS, createFixture, directoryMember, uuid } from '../test/fixtures';
import { createBizI18n } from '../locales';
import { ApiSelect } from './api-select';
import { DictSelect, defineDictionary, invalidateDictionary, loadDictionary, orgTypeDictionary } from './dict-select';
import { IconPicker } from './icon-picker';
import { OrganizeSelect, buildOrganizationTree } from './organize-select';
import { RoleSelect } from './role-select';
import { UserSelect } from './user-select';

afterEach(cleanup);
const i18n = createBizI18n();

function Value({ value }: { value: string | string[] | null }) {
  return <output data-testid="value">{Array.isArray(value) ? value.join(',') : value ?? ''}</output>;
}
const value = () => screen.getByTestId('value').textContent;
const options = () => within(screen.getByRole('listbox')).getAllByRole('option').map((option) => option.textContent);
async function expectAccessible() {
  const violations = blockingViolations(await runAxe());
  expect(violations, describeViolations(violations)).toHaveLength(0);
}

describe('OrganizeSelect', () => {
  it('扁平组织列表按 parent_id 组装成树，兄弟顺序沿用接口顺序，缺上级的作为根', () => {
    const nodes = buildOrganizationTree(ORGANIZATIONS);
    expect(nodes.map((node) => node.label)).toEqual(['总部']);
    expect(nodes[0]!.children!.map((node) => node.label)).toEqual(['研发中心', '销售部']);
    expect(nodes[0]!.children![0]!.children!.map((node) => node.label)).toEqual(['前端组', '后端组']);
    expect(buildOrganizationTree([{ ...ORGANIZATIONS[2]!, parent_id: uuid(999) }]).map((node) => node.label)).toEqual(['前端组']);
  });

  it('单选：经 PlatformClient 读组织树，键盘展开并选中部门，回写组织 id', async () => {
    const { client, requests } = createFixture();
    function Example() { const [selected, setSelected] = useState<string | null>(null); return <><OrganizeSelect client={client} i18n={i18n} label="部门" value={selected} onChange={setSelected} /><Value value={selected} /></>; }
    render(<Example />);
    await vi.waitFor(() => expect(screen.queryByTestId('yue-organize-select-loading')).toBeNull());
    expect(requests).toEqual([{ method: 'GET', url: '/api/directory/orgs/tree', tenant: expect.any(String) }]);
    await userEvent.click(screen.getByRole('button', { name: /^部门/ }));
    const root = await screen.findByRole('treeitem', { name: '总部' });
    root.focus();
    await userEvent.keyboard('{ArrowRight}{ArrowDown}{ArrowRight}{ArrowDown}');
    expect(document.activeElement).toBe(screen.getByRole('treeitem', { name: '前端组' }));
    await userEvent.keyboard('{Enter}');
    expect(value()).toBe(uuid(3));
    await vi.waitFor(() => expect(screen.getByRole('button', { name: '部门 前端组' })).toBeTruthy());
    await expectAccessible();
  });

  it('多选：同一 Tree 不级联复选，选总部不会带上下级；清空回到空数组', async () => {
    const { client } = createFixture();
    function Example() { const [selected, setSelected] = useState<string[]>([uuid(5)]); return <><OrganizeSelect client={client} i18n={i18n} label="归属组织" multiple value={selected} onChange={setSelected} /><Value value={selected} /></>; }
    render(<Example />);
    await vi.waitFor(() => expect(screen.getByRole('button', { name: /^归属组织/ }).textContent).toContain('销售部'));
    await userEvent.click(screen.getByRole('button', { name: /^归属组织/ }));
    const tree = await screen.findByRole('tree', { name: '归属组织' });
    expect(tree.getAttribute('aria-multiselectable')).toBe('true');
    expect(screen.getByRole('treeitem', { name: '销售部' }).getAttribute('aria-checked')).toBe('true');
    screen.getByRole('treeitem', { name: '总部' }).focus();
    await userEvent.keyboard(' ');
    expect(value()).toBe(`${uuid(5)},${uuid(1)}`);
    expect(screen.getByRole('treeitem', { name: '研发中心' }).getAttribute('aria-checked')).toBe('false');
    await expectAccessible();
    await userEvent.click(screen.getByRole('button', { name: '清空' }));
    expect(value()).toBe('');
  });

  it('加载失败显示可访问的错误与重试，重试后可用', async () => {
    const { client } = createFixture({ failOnce: ['/api/directory/orgs/tree'] });
    render(<OrganizeSelect client={client} i18n={i18n} label="部门" value={null} onChange={() => {}} />);
    const alert = await screen.findByRole('alert');
    expect(alert.textContent).toContain('加载失败');
    await userEvent.click(within(alert).getByRole('button', { name: '重试' }));
    await vi.waitFor(() => expect(screen.queryByRole('alert')).toBeNull());
    await userEvent.click(screen.getByRole('button', { name: /^部门/ }));
    expect(await screen.findByRole('treeitem', { name: '总部' })).toBeTruthy();
  });
});

describe('UserSelect', () => {
  it('键盘打开、按姓名或账号过滤、回车选中；显示停用状态；回写成员 id', async () => {
    const { client, requests } = createFixture();
    function Example() { const [selected, setSelected] = useState<string | null>(null); return <><UserSelect client={client} i18n={i18n} label="成员" value={selected} onChange={setSelected} /><Value value={selected} /></>; }
    render(<Example />);
    await vi.waitFor(() => expect(requests).toHaveLength(1));
    expect(requests[0]!.url).toBe('/api/directory/members?offset=0&limit=200');
    const trigger = screen.getByRole('combobox', { name: '成员' });
    trigger.focus();
    await userEvent.keyboard('{ArrowDown}');
    await screen.findByRole('listbox');
    expect(options()).toEqual(['李四lisi', '钱七qianqi · 已锁定', '王五wangwu', '张三zhangsan', '赵六zhaoliu · 已停用']);
    await userEvent.keyboard('wang');
    await vi.waitFor(() => expect(options()).toEqual(['王五wangwu']));
    await userEvent.keyboard('{ArrowDown}{Enter}');
    expect(value()).toBe(uuid(103));
    await vi.waitFor(() => expect(trigger.textContent).toContain('王五'));
  });

  it('按组织（含子树）取成员；valueKey=person 回写自然人 id；多选可切换多项', async () => {
    const { client, requests } = createFixture();
    function Example() { const [selected, setSelected] = useState<string[]>([]); return <><UserSelect client={client} i18n={i18n} label="成员" organizationId={uuid(2)} valueKey="person" multiple value={selected} onChange={setSelected} /><Value value={selected} /></>; }
    render(<Example />);
    await vi.waitFor(() => expect(requests).toHaveLength(1));
    expect(requests[0]!.url).toBe(`/api/directory/orgs/${uuid(2)}/members?descendants=true&offset=0&limit=200`);
    await userEvent.click(screen.getByRole('combobox', { name: '成员' }));
    await vi.waitFor(() => expect(options()).toHaveLength(3));
    await userEvent.click(screen.getByRole('option', { name: /李四/ }));
    await userEvent.click(screen.getByRole('option', { name: /王五/ }));
    expect(value()).toBe(`${uuid(201)},${uuid(203)}`);
    expect(screen.getByRole('option', { name: /李四/ }).getAttribute('aria-selected')).toBe('true');
    await expectAccessible();
    await userEvent.keyboard('{Escape}');
    await vi.waitFor(() => expect(screen.getByRole('combobox', { name: '成员' }).textContent).toContain('李四'));
    await userEvent.click(screen.getByRole('button', { name: '清空成员' }));
    expect(value()).toBe('');
  });

  it('成员超过上限时停在上限并提示缩小范围，页大小不超过目录接口的 200', async () => {
    const many = Array.from({ length: 450 }, (_, index) => ({ member: directoryMember(1000 + index, `成员${index}`, `user${String(index).padStart(3, '0')}`), org: 1 }));
    const { client, requests } = createFixture({ members: many });
    render(<UserSelect client={client} i18n={i18n} label="成员" max={300} value={null} onChange={() => {}} />);
    expect(await screen.findByText('只加载了前 300 位成员，可按组织缩小范围')).toBeTruthy();
    expect(requests.map((request) => request.url)).toEqual(['/api/directory/members?offset=0&limit=200', '/api/directory/members?offset=200&limit=100', '/api/directory/members?offset=300&limit=1']);
  });

  it('请求失败：触发器旁出现错误与重试按钮，重试后加载成功', async () => {
    const { client } = createFixture({ failOnce: ['/api/directory/members'] });
    render(<UserSelect client={client} i18n={i18n} label="成员" value={null} onChange={() => {}} />);
    const alert = await screen.findByRole('alert');
    await userEvent.click(within(alert).getByRole('button', { name: '重试' }));
    await vi.waitFor(() => expect(screen.queryByRole('alert')).toBeNull());
    await userEvent.click(screen.getByRole('combobox', { name: '成员' }));
    await vi.waitFor(() => expect(options()).toHaveLength(5));
  });
});

describe('RoleSelect', () => {
  it('内置角色按 code 取文案、自定义角色用名称；选中值是角色 code，多选回写数组', async () => {
    const { client, requests } = createFixture();
    function Example() { const [selected, setSelected] = useState<string[]>([]); return <><RoleSelect client={client} i18n={i18n} label="角色" multiple value={selected} onChange={setSelected} /><Value value={selected} /></>; }
    render(<Example />);
    await vi.waitFor(() => expect(requests.map((request) => request.url)).toEqual(['/api/authz/roles']));
    await userEvent.click(screen.getByRole('combobox', { name: '角色' }));
    await vi.waitFor(() => expect(options()).toEqual(['租户所有者内置', '租户管理员内置', '使用者内置', '销售经理sales.manager']));
    await userEvent.click(screen.getByRole('option', { name: /销售经理/ }));
    await userEvent.click(screen.getByRole('option', { name: /使用者/ }));
    expect(value()).toBe('sales.manager,tenant.user');
  });

  it('英文语言包下显示英文名称', async () => {
    const { client } = createFixture();
    render(<RoleSelect client={client} i18n={createBizI18n('en-US')} label="Role" value="tenant.admin" onChange={() => {}} />);
    await vi.waitFor(() => expect(screen.getByRole('combobox', { name: 'Role' }).textContent).toContain('Tenant admin'));
  });
});

describe('DictSelect', () => {
  it('组织类型字典经 PlatformClient 读一次并按客户端缓存，两个选择器共享一次请求；失效后重新加载', async () => {
    const { client, requests } = createFixture();
    function Example() { const [selected, setSelected] = useState<string | null>('dept'); return <><DictSelect client={client} i18n={i18n} label="组织类型" dictionary={orgTypeDictionary} value={selected} onChange={setSelected} /><DictSelect client={client} i18n={i18n} label="上级类型" dictionary={orgTypeDictionary} value={null} onChange={() => {}} /><Value value={selected} /></>; }
    render(<Example />);
    await vi.waitFor(() => expect(screen.getByRole('combobox', { name: '组织类型' }).textContent).toContain('部门'));
    expect(requests.filter((request) => request.url === '/api/directory/org-types')).toHaveLength(1);
    await userEvent.click(screen.getByRole('combobox', { name: '组织类型' }));
    await userEvent.click(await screen.findByRole('option', { name: '公司' }));
    expect(value()).toBe('company');
    invalidateDictionary(client, orgTypeDictionary.id);
    await loadDictionary(client, orgTypeDictionary);
    expect(requests.filter((request) => request.url === '/api/directory/org-types')).toHaveLength(2);
  });

  it('内置字典项可用 labelKey 走 i18n；失败的加载不留在缓存里', async () => {
    const { client } = createFixture();
    let calls = 0;
    const flaky = defineDictionary('test.flaky', async () => { calls++; if (calls === 1) throw new Error('boom'); return [{ value: 'read', labelKey: 'biz.action.read' }]; });
    await expect(loadDictionary(client, flaky)).rejects.toThrow('boom');
    expect(await loadDictionary(client, flaky)).toEqual([{ value: 'read', labelKey: 'biz.action.read' }]);
    render(<DictSelect client={client} i18n={i18n} label="动作" dictionary={flaky} value="read" onChange={() => {}} />);
    await vi.waitFor(() => expect(screen.getByRole('combobox', { name: '动作' }).textContent).toContain('查看'));
  });
});

describe('ApiSelect / useRemoteOptions', () => {
  interface Customer { id: string; name: string }
  const CUSTOMERS: Customer[] = [{ id: 'c1', name: '华东贸易' }, { id: 'c2', name: '华南物流' }, { id: 'c3', name: '北方制造' }];
  interface Paths { '/api/customers': { get: { parameters: { query?: { keyword?: string } }; responses: { 200: { content: { 'application/json': { items: Customer[] } } } } } } }

  it('输入去抖后作为 keyword 经 load 由服务端过滤；只采纳最新结果，已选项在搜索后仍显示名称', async () => {
    const { client, requests } = createFixture({ routes: [{ method: 'GET', path: '/api/customers', respond: (request) => {
      const keyword = new URL(request.url).searchParams.get('keyword') ?? '';
      return Response.json({ items: CUSTOMERS.filter((item) => item.name.includes(keyword)) });
    } }] });
    function Example() {
      const [selected, setSelected] = useState<string | null>(null);
      return <><ApiSelect<Customer> client={client} i18n={i18n} label="客户" value={selected} onChange={setSelected} debounceMs={50}
        load={async (current, { keyword, signal }) => (await current.api<Paths>().GET('/api/customers', { params: { query: { keyword } }, signal })).data?.items ?? []}
        getValue={(item) => item.id} getLabel={(item) => item.name} /><Value value={selected} /></>;
    }
    render(<Example />);
    await userEvent.click(screen.getByRole('combobox', { name: '客户' }));
    await vi.waitFor(() => expect(options()).toEqual(['华东贸易', '华南物流', '北方制造']));
    await userEvent.click(screen.getByRole('option', { name: '北方制造' }));
    expect(value()).toBe('c3');
    await userEvent.click(screen.getByRole('combobox', { name: '客户' }));
    await userEvent.keyboard('华');
    await vi.waitFor(() => expect(options()).toEqual(['华东贸易', '华南物流']));
    expect(requests.map((request) => request.url)).toEqual(['/api/customers?keyword=', `/api/customers?keyword=${encodeURIComponent('华')}`]);
    expect(screen.getByRole('combobox', { name: '客户' }).textContent).toContain('北方制造');
    await expectAccessible();
  });

  it('client 模式只加载一次并在本地过滤；load 抛错时显示错误', async () => {
    const { client } = createFixture();
    let calls = 0;
    render(<ApiSelect<Customer> client={client} i18n={i18n} label="客户" value={null} onChange={() => {}} search="client"
      load={async () => { calls++; if (calls === 1) throw new Error('down'); return CUSTOMERS; }} getValue={(item) => item.id} getLabel={(item) => item.name} />);
    await userEvent.click(within(await screen.findByRole('alert')).getByRole('button', { name: '重试' }));
    await userEvent.click(screen.getByRole('combobox', { name: '客户' }));
    await vi.waitFor(() => expect(document.activeElement).toBe(screen.getByRole('combobox', { name: '搜索客户' })));
    await userEvent.keyboard('物流');
    await vi.waitFor(() => expect(options()).toEqual(['华南物流']));
    expect(calls).toBe(2);
  });
});

describe('IconPicker', () => {
  it('列出 core Icon 登记表的图标，键盘过滤并选中，触发器显示所选图标', async () => {
    function Example() { const [icon, setIcon] = useState<IconName | null>(null); return <><IconPicker i18n={i18n} label="菜单图标" value={icon} onChange={setIcon} /><Value value={icon} /></>; }
    render(<Example />);
    const trigger = screen.getByRole('combobox', { name: '菜单图标' });
    trigger.focus();
    await userEvent.keyboard('{ArrowDown}');
    await screen.findByRole('listbox');
    expect(options().length).toBeGreaterThan(40);
    await userEvent.keyboard('build');
    await vi.waitFor(() => expect(options()).toEqual(['building']));
    await userEvent.keyboard('{ArrowDown}{Enter}');
    expect(value()).toBe('building');
    await vi.waitFor(() => expect(trigger.querySelector('[data-slot="icon"]')).not.toBeNull());
    await expectAccessible();
  });
});
