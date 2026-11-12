import { execFileSync } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { resolve } from 'node:path';
import { expect, request as http, test, type APIRequestContext, type APIResponse, type Page } from '@playwright/test';

// S1-IDP-010 验收 2（R109）：OrganizeSelect 选部门、UserSelect 选人，经 PlatformClient 访问真实目录 API，数据来自本租户目录。
// 宿主是 dev-tenant-ops.sh 装配的真实身份组件（真实 PostgreSQL、生产迁移），由 scripts/directory-e2e.mjs 启动。
// 租户 A、B 经运营接口开通；目录没有新建成员的接口，成员经运营的「更换租户管理员」接口建立——换下来的管理员留作普通成员；
// 组织与主归属经真实目录写接口建立。B 的数据只作隔离对照。
const root = resolve(import.meta.dirname, '../../..');
const PASSWORD = 'Init-Pass-2026';
const NEXT_PASSWORD = 'Next-Pass-2026';

interface Member { membership: { id: string; account: string; display_name: string } }
interface Organization { id: string; name: string; path: string; depth: number }
type Headers = Record<string, string>;

function platformToken(): string {
  // 令牌只在测试进程内存里，不写日志。
  return execFileSync(resolve(root, 'scripts/dev-tenant-ops.sh'), ['token'], { cwd: root, env: process.env, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }).trim();
}

async function ok<T>(response: APIResponse, what: string): Promise<T> {
  expect(response.status(), `${what}：${await response.text()}`).toBeLessThan(300);
  return await response.json() as T;
}

// 统一登录流水线：防伪令牌 → 登录 → 首次登录修改初始密码 → 账号会话；写目录前按会话重新取一次防伪令牌。
async function signIn(api: APIRequestContext, identifier: string): Promise<{ tenant: string; headers: Headers }> {
  const csrf = async () => { const value = await ok<{ header: string; token: string }>(await api.get('/api/identity/csrf'), '防伪令牌'); return { [value.header]: value.token }; };
  const headers = await csrf();
  let result = await ok<{ status: string; ticket?: string; tenant_id?: string }>(await api.post('/api/identity/login', { headers, data: { client_id: 'qian-tenant-ops', identifier, password: PASSWORD } }), '登录 ' + identifier);
  if (result.status === 'password_change_required') result = await ok(await api.post('/api/identity/login/password', { headers, data: { ticket: result.ticket, new_password: NEXT_PASSWORD } }), '修改初始密码 ' + identifier);
  expect(result.status).toBe('authenticated');
  return { tenant: result.tenant_id!, headers: await csrf() };
}

async function seedDirectory(api: APIRequestContext, headers: Headers, orgs: [key: string, name: string, type: string, parent?: string][], primary: [account: string, org: string][]) {
  for (const [code, name] of [['company', '公司'], ['dept', '部门'], ['team', '小组']]) await ok(await api.put('/api/directory/org-types', { headers, data: { code, name } }), '组织类型 ' + code);
  const ids: Record<string, string> = {};
  for (const [key, name, type, parent] of orgs)
    ids[key] = (await ok<Organization>(await api.post('/api/directory/orgs', { headers, data: { name, type_code: type, ...(parent ? { parent_id: ids[parent] } : {}) } }), '组织 ' + name)).id;
  const listed = await ok<{ items: Member[] }>(await api.get('/api/directory/members?limit=200'), '成员列表');
  const members = Object.fromEntries(listed.items.map((item) => [item.membership.account, item.membership.id]));
  for (const [account, org] of primary)
    await ok(await api.put(`/api/directory/members/${members[account]}/relations`, { headers, data: { items: [{ target_type: 'organization', target_id: ids[org], is_primary: true }] } }), '主归属 ' + account);
  return { orgs: ids, members };
}

const optionTexts = (page: Page) => page.getByRole('option').allInnerTexts().then((texts) => texts.map((text) => text.replace(/\s+/g, '')));

test('R109 真实目录：OrganizeSelect 选部门、UserSelect 选人，经 PlatformClient 只读本租户目录，关键字由服务端检索', async ({ page }) => {
  // 1. 运营开通租户 A、B，经更换管理员建立成员，最后换回各自的管理员。
  const ops = await http.newContext({ baseURL: process.env.QIAN_DEV_OPS_URL, extraHTTPHeaders: { authorization: 'Bearer ' + platformToken() } });
  const person = async (displayName: string, identifier: string) =>
    (await ok<{ subjectId: string }>(await ops.post('/api/tenancy/admin-accounts', { data: { displayName, identifierKind: 'username', identifier, initialPassword: PASSWORD } }), '新建账号 ' + identifier)).subjectId;
  async function openTenant(code: string, name: string, admin: string, members: string[]) {
    const tenant = await ok<{ id: string }>(await ops.post('/api/tenancy/tenants', { data: { code, name, isolation: 'row', administrator: { membershipId: randomUUID(), subjectId: admin }, quotas: { users: 20 } } }), '开通 ' + code);
    await ok(await ops.post(`/api/tenancy/tenants/${tenant.id}:activate`, { data: { trial: false } }), '激活 ' + code);
    for (const subjectId of [...members, admin]) await ok(await ops.put(`/api/tenancy/tenants/${tenant.id}/admins`, { data: { items: [{ subjectId }] } }), '更换管理员 ' + code);
    return tenant.id;
  }
  const tenantA = await openTenant('direa', '目录租户 A', await person('张三', 'zhangsan'), [await person('李四', 'lisi'), await person('王五', 'wangwu'), await person('赵六', 'zhaoliu')]);
  const tenantB = await openTenant('direb', '目录租户 B', await person('张外', 'zhangwai'), [await person('王外', 'wangwai')]);
  await ops.dispose();

  // 2. B 的管理员直接对身份宿主登录并写 B 的目录，作隔离对照。
  const identityB = await http.newContext({ baseURL: process.env.QIAN_DEV_IDENTITY_URL });
  const b = await signIn(identityB, 'zhangwai');
  expect(b.tenant).toBe(tenantB);
  await seedDirectory(identityB, b.headers, [['hq', '外部集团', 'company'], ['rd', '外部研发', 'dept', 'hq']], [['zhangwai', 'hq'], ['wangwai', 'rd']]);

  // 3. A 的管理员在本页源上登录（会话 Cookie 落在页面源），写 A 的目录：总部 → 研发中心 → 前端组；总部 → 销售部。
  const a = await signIn(page.request, 'zhangsan');
  expect(a.tenant).toBe(tenantA);
  const seeded = await seedDirectory(page.request, a.headers, [['hq', '总部', 'company'], ['rd', '研发中心', 'dept', 'hq'], ['fe', '前端组', 'team', 'rd'], ['sales', '销售部', 'dept', 'hq']],
    [['zhangsan', 'hq'], ['lisi', 'rd'], ['wangwu', 'fe'], ['zhaoliu', 'sales']]);
  expect(Object.keys(seeded.members).sort()).toEqual(['lisi', 'wangwu', 'zhangsan', 'zhaoliu']);
  const tree = (await ok<{ items: Organization[] }>(await page.request.get('/api/directory/orgs/tree'), 'A 的组织树')).items;

  // 4. 页面：每个请求都是同源的目录接口，不带令牌、不带租户头。
  const calls: string[] = [];
  page.on('request', (request) => {
    const url = new URL(request.url());
    if (!url.pathname.startsWith('/api/')) return;
    calls.push(url.pathname + url.search);
    expect(url.origin).toBe(new URL(page.url()).origin);
    expect(request.headers().authorization).toBeUndefined();
    expect(request.headers()['x-tenant-id']).toBeUndefined();
  });
  await page.goto('/directory.html');
  const state = async () => JSON.parse(await page.getByTestId('directory-state').textContent() ?? '{}') as { organization: string | null; member: string | null; anyone: string | null };

  // OrganizeSelect：展开后的组织树就是 A 的真实组织树（接口按 path 排序即先序），没有 B 的组织；方向键选中「研发中心」。
  const organization = page.getByRole('button', { name: /^部门/ });
  await expect(organization).toHaveAccessibleName('部门 请选择');
  await organization.focus();
  await page.keyboard.press('Enter');
  const orgTree = page.getByRole('tree', { name: '部门' });
  await expect(orgTree.getByRole('treeitem', { name: '总部' })).toBeFocused();
  await page.keyboard.press('ArrowRight');
  await page.keyboard.press('ArrowDown');
  await expect(orgTree.getByRole('treeitem', { name: '研发中心' })).toBeFocused();
  await page.keyboard.press('ArrowRight');
  // 节点名称不含 aria-hidden 的展开符号；层级就是组织的 depth。
  const rendered = () => orgTree.getByRole('treeitem').evaluateAll((items) => items.map((item) => [
    [...item.childNodes].filter((child) => !(child instanceof Element && child.getAttribute('aria-hidden') === 'true')).map((child) => child.textContent).join('').trim(),
    Number(item.getAttribute('aria-level')),
  ]));
  await expect.poll(rendered).toEqual(tree.map((item) => [item.name, item.depth]));
  expect(tree.map((item) => item.name)).toEqual(['总部', '研发中心', '前端组', '销售部']);
  await page.keyboard.press('Enter');
  await expect(organization).toHaveAccessibleName('部门 研发中心');
  expect((await state()).organization).toBe(seeded.orgs.rd);
  expect(calls).toContain('/api/directory/orgs/tree');

  // UserSelect（限定部门）：只列研发中心整棵子树的成员；输入关键字后由服务端检索，选中值是真实目录成员 id。
  const scoped = `/api/directory/orgs/${seeded.orgs.rd}/members?descendants=true`;
  await expect.poll(() => calls).toContain(`${scoped}&offset=0&limit=51`);
  await page.getByRole('combobox', { name: '部门成员', exact: true }).click();
  await expect(page.getByRole('combobox', { name: '搜索部门成员' })).toBeFocused();
  await expect.poll(() => optionTexts(page)).toEqual(['李四lisi', '王五wangwu']);
  await page.keyboard.type('王');
  await expect.poll(() => calls).toContain(`${scoped}&q=${encodeURIComponent('王')}&offset=0&limit=51`);
  await expect.poll(() => optionTexts(page)).toEqual(['王五wangwu']);
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('Enter');
  await expect(page.getByRole('combobox', { name: '部门成员', exact: true })).toContainText('王五');
  await expect(page.getByRole('combobox', { name: '部门成员', exact: true })).toBeFocused();
  expect((await state()).member).toBe(seeded.members.wangwu);

  // UserSelect（全租户）：只有 A 的四位成员；B 的成员按关键字也检索不到。
  await page.getByRole('combobox', { name: '全部成员', exact: true }).click();
  await expect(page.getByRole('combobox', { name: '搜索全部成员' })).toBeFocused();
  await expect.poll(() => optionTexts(page)).toEqual(['李四lisi', '王五wangwu', '张三zhangsan', '赵六zhaoliu']);
  await page.keyboard.type('外');
  await expect.poll(() => calls).toContain(`/api/directory/members?q=${encodeURIComponent('外')}&offset=0&limit=51`);
  await expect(page.getByTestId('yue-user-select-empty').last()).toHaveText('暂无数据');
  await expect(page.getByRole('option')).toHaveCount(0);
  await page.keyboard.press('Backspace');
  await page.keyboard.type('张');
  await expect.poll(() => optionTexts(page)).toEqual(['张三zhangsan']);
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('Enter');
  expect((await state()).anyone).toBe(seeded.members.zhangsan);
  expect(calls.filter((call) => !call.startsWith('/api/directory/'))).toEqual([]);

  // 对照：B 的会话只看到 B 的成员与组织，A 的会话看不到 B 的组织。
  const membersB = await ok<{ items: Member[] }>(await identityB.get('/api/directory/members'), 'B 的成员');
  expect(membersB.items.map((item) => item.membership.display_name).sort()).toEqual(['张外', '王外'].sort());
  expect(tree.map((item) => item.name)).not.toContain('外部集团');
  await identityB.dispose();
});
