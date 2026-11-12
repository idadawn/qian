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
import { loadMembersByIds, memberKeyword, searchMembers } from '../api';

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

  it('组织超过 100 个时树按窗口虚拟化，多选浮层与单选（TreeSelect）同宽，不被绝对定位的行压窄', async () => {
    const many = Array.from({ length: 150 }, (_, index) => ({ ...ORGANIZATIONS[0]!, id: uuid(1000 + index), name: `事业部 ${index + 1}`, path: `/${uuid(1000 + index)}/` }));
    const { client } = createFixture({ routes: [{ method: 'GET', path: '/api/directory/orgs/tree', respond: () => Response.json({ items: many }) }] });
    render(<><OrganizeSelect client={client} i18n={i18n} label="部门" value={null} onChange={() => {}} /><OrganizeSelect client={client} i18n={i18n} label="归属组织" multiple value={[]} onChange={() => {}} /></>);
    await vi.waitFor(() => expect(screen.queryAllByTestId('yue-organize-select-loading')).toHaveLength(0));
    async function popupWidth(label: string) {
      await userEvent.click(screen.getByRole('button', { name: new RegExp(`^${label}`) }));
      const tree = await screen.findByRole('tree', { name: label });
      expect(within(tree).getAllByRole('treeitem').length).toBeLessThan(many.length);
      // offsetWidth 不受打开动画里 scale 的影响。
      const width = tree.closest<HTMLElement>('[data-slot="popover-content"]')!.offsetWidth;
      await userEvent.keyboard('{Escape}');
      await vi.waitFor(() => expect(screen.queryByRole('tree', { name: label })).toBeNull());
      return width;
    }
    const single = await popupWidth('部门');
    expect(single).toBeGreaterThan(200);
    expect(await popupWidth('归属组织')).toBe(single);
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
  it('键盘打开列出第一页、输入后由服务端按姓名或账号检索、回车选中；显示停用状态；回写成员 id', async () => {
    const { client, requests } = createFixture();
    function Example() { const [selected, setSelected] = useState<string | null>(null); return <><UserSelect client={client} i18n={i18n} label="成员" debounceMs={0} value={selected} onChange={setSelected} /><Value value={selected} /></>; }
    render(<Example />);
    await vi.waitFor(() => expect(requests).toHaveLength(1));
    expect(requests[0]!.url).toBe('/api/directory/members?offset=0&limit=51');
    const trigger = screen.getByRole('combobox', { name: '成员' });
    trigger.focus();
    await userEvent.keyboard('{ArrowDown}');
    await screen.findByRole('listbox');
    expect(options()).toEqual(['李四lisi', '钱七qianqi · 已锁定', '王五wangwu', '张三zhangsan', '赵六zhaoliu · 已停用']);
    await userEvent.keyboard('WANG');
    await vi.waitFor(() => expect(requests.at(-1)!.url).toBe('/api/directory/members?q=WANG&offset=0&limit=51'));
    await vi.waitFor(() => expect(options()).toEqual(['王五wangwu']));
    // 本地不再过滤：服务端没匹配上就是没有选项。
    await userEvent.keyboard('{Backspace}{Backspace}{Backspace}{Backspace}李');
    await vi.waitFor(() => expect(options()).toEqual(['李四lisi']));
    await userEvent.keyboard('{ArrowDown}{Enter}');
    expect(value()).toBe(uuid(101));
    await vi.waitFor(() => expect(trigger.textContent).toContain('李四'));
    expect(requests.every((request) => !request.url.includes('ids='))).toBe(true);
  });

  it('按组织（含子树）检索；valueKey=person 回写自然人 id；多选可切换多项', async () => {
    const { client, requests } = createFixture();
    function Example() { const [selected, setSelected] = useState<string[]>([]); return <><UserSelect client={client} i18n={i18n} label="成员" organizationId={uuid(2)} valueKey="person" debounceMs={0} multiple value={selected} onChange={setSelected} /><Value value={selected} /></>; }
    render(<Example />);
    await vi.waitFor(() => expect(requests).toHaveLength(1));
    expect(requests[0]!.url).toBe(`/api/directory/orgs/${uuid(2)}/members?descendants=true&offset=0&limit=51`);
    await userEvent.click(screen.getByRole('combobox', { name: '成员' }));
    await vi.waitFor(() => expect(options()).toHaveLength(3));
    await userEvent.click(screen.getByRole('option', { name: /李四/ }));
    await userEvent.click(screen.getByRole('option', { name: /王五/ }));
    expect(value()).toBe(`${uuid(201)},${uuid(203)}`);
    expect(screen.getByRole('option', { name: /李四/ }).getAttribute('aria-selected')).toBe('true');
    await userEvent.keyboard('wang');
    await vi.waitFor(() => expect(requests.at(-1)!.url).toBe(`/api/directory/orgs/${uuid(2)}/members?descendants=true&q=wang&offset=0&limit=51`));
    await vi.waitFor(() => expect(options()).toEqual(['王五wangwu']));
    // 换了关键字，李四已不在当前结果里：摘要仍用此前检索过的结果显示名字，不显示成 id，也不回查。
    expect(screen.getByRole('combobox', { name: '成员' }).textContent).toContain('李四');
    expect(screen.getByRole('combobox', { name: '成员' }).textContent).not.toContain(uuid(201));
    await expectAccessible();
    await userEvent.keyboard('{Escape}');
    // 关闭后同样显示名字，不回查。
    await vi.waitFor(() => expect(screen.getByRole('combobox', { name: '成员' }).textContent).toContain('李四'));
    expect(requests.every((request) => !request.url.includes('ids='))).toBe(true);
    await userEvent.click(screen.getByRole('button', { name: '清空成员' }));
    expect(value()).toBe('');
  });

  it('已选值不在检索结果里时按 id 批量回查显示名：按 valueKey 用 ids 或 person_ids，查不到的与非 UUID 的值显示原值且不重复请求', async () => {
    const { client, requests } = createFixture();
    const absent = uuid(999);
    render(<>
      <UserSelect client={client} i18n={i18n} label="前端组成员" organizationId={uuid(3)} debounceMs={0} multiple value={[uuid(105), 'not-a-uuid']} onChange={() => {}} />
      <UserSelect client={client} i18n={i18n} label="自然人" organizationId={uuid(3)} valueKey="person" debounceMs={0} value={uuid(204)} onChange={() => {}} />
      <UserSelect client={client} i18n={i18n} label="已删除" organizationId={uuid(3)} debounceMs={0} value={absent} onChange={() => {}} />
    </>);
    // 前端组只有王五：赵六（成员 id）与张三（自然人 id）要回查，回查等第一页回来之后才发。
    await vi.waitFor(() => expect(screen.getByRole('combobox', { name: '前端组成员' }).textContent).toContain('赵六'));
    expect(screen.getByRole('combobox', { name: '前端组成员' }).textContent).toContain('not-a-uuid');
    await vi.waitFor(() => expect(screen.getByRole('combobox', { name: '自然人' }).textContent).toContain('张三'));
    const lookups = () => requests.filter((request) => request.url.startsWith('/api/directory/members?')).map((request) => request.url).sort();
    // 按 id 批量不分页，回查不带 offset / limit。
    await vi.waitFor(() => expect(lookups()).toEqual([`/api/directory/members?ids=${absent}`, `/api/directory/members?ids=${uuid(105)}`, `/api/directory/members?person_ids=${uuid(204)}`].sort()));
    expect(screen.getByRole('combobox', { name: '已删除' }).textContent).toContain(absent);
    await userEvent.click(screen.getByRole('combobox', { name: '前端组成员' }));
    await vi.waitFor(() => expect(document.activeElement?.getAttribute('aria-label')).toBe('搜索前端组成员'));
    await vi.waitFor(() => expect(options()).toEqual(['王五wangwu']));
    await userEvent.keyboard('wang');
    await vi.waitFor(() => expect(requests.at(-1)!.url).toBe(`/api/directory/orgs/${uuid(3)}/members?descendants=true&q=wang&offset=0&limit=51`));
    await userEvent.keyboard('{Escape}');
    expect(lookups()).toHaveLength(3);
  });

  it('检索结果超过一页时只显示前 50 位并提示继续输入，输入关键字后提示消失；页大小在目录接口上限内', async () => {
    const many = Array.from({ length: 120 }, (_, index) => ({ member: directoryMember(1000 + index, `成员${index}`, `user${String(index).padStart(3, '0')}`), org: 1 }));
    const { client, requests } = createFixture({ members: many });
    render(<UserSelect client={client} i18n={i18n} label="成员" debounceMs={0} value={null} onChange={() => {}} />);
    expect(await screen.findByText('只显示前 50 位成员，继续输入可缩小范围')).toBeTruthy();
    await userEvent.click(screen.getByRole('combobox', { name: '成员' }));
    await vi.waitFor(() => expect(document.activeElement?.getAttribute('aria-label')).toBe('搜索成员'));
    await vi.waitFor(() => expect(options()).toHaveLength(50));
    await userEvent.keyboard('user11');
    await vi.waitFor(() => expect(requests.at(-1)!.url).toBe('/api/directory/members?q=user11&offset=0&limit=51'));
    await vi.waitFor(() => expect(options()).toHaveLength(10));
    expect(screen.queryByText('只显示前 50 位成员，继续输入可缩小范围')).toBeNull();
    // 去抖为 0 时逐字输入可能带出中间关键字的请求，不断言请求条数；每次都只比显示的 50 位多取一条，在目录接口每页 200 的上限内。
    expect(requests[0]!.url).toBe('/api/directory/members?offset=0&limit=51');
    expect(requests.every((request) => request.url.endsWith('&limit=51'))).toBe(true);
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

describe('成员检索与按 id 批量的取数函数', () => {
  it('关键字整理：控制字符换空格、去首尾空白、超长截到 64 个码元且不切断代理对，空白不带 q', () => {
    expect(memberKeyword('  张三\t ')).toBe('张三');
    expect(memberKeyword('张\u0000三')).toBe('张 三');
    expect(memberKeyword('   ')).toBeUndefined();
    expect(memberKeyword('长'.repeat(70))).toBe('长'.repeat(64));
    const emoji = '😀'.repeat(40);
    expect(memberKeyword(emoji)).toBe('😀'.repeat(32));
    expect(memberKeyword('a'.repeat(63) + '😀')).toBe('a'.repeat(63));
  });

  it('按 id 批量：去重、跳过非 UUID 与全零 UUID，每批至多 100 个，按 valueKey 选 ids 或 person_ids，不带分页参数', async () => {
    const { client, requests } = createFixture();
    const ids = Array.from({ length: 230 }, (_, index) => uuid(5000 + index));
    expect(await loadMembersByIds(client, [...ids, ids[0]!, 'x', '00000000-0000-0000-0000-000000000000'])).toEqual([]);
    const params = requests.map((request) => new URL(request.url, 'https://tenant.test').searchParams);
    expect(params.map((query) => query.getAll('ids').length)).toEqual([100, 100, 30]);
    expect(params.every((query) => !query.has('offset') && !query.has('limit'))).toBe(true);
    expect((await loadMembersByIds(client, [uuid(203)], 'person')).map((member) => member.membership.account)).toEqual(['wangwu']);
    expect(requests.at(-1)!.url).toBe(`/api/directory/members?person_ids=${uuid(203)}`);
    expect(await loadMembersByIds(client, ['x'])).toEqual([]);
    expect(requests).toHaveLength(4);
    const page = await searchMembers(client, { keyword: ' z ', limit: 2 });
    expect([page.items.map((member) => member.membership.account), page.more]).toEqual([['zhangsan', 'zhaoliu'], false]);
    expect(requests.at(-1)!.url).toBe('/api/directory/members?q=z&offset=0&limit=3');
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
