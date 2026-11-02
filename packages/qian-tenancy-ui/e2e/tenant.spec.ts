import { test, expect, request as http } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { randomInt, randomUUID } from 'node:crypto';
import { writeFile, access } from 'node:fs/promises';
import { resolve } from 'node:path';

// 统一登录流水线：身份组件的账号接口（防伪令牌 → 登录 → 必要时修改初始密码 → 账号会话），认证页族调用的也是这一组接口。
async function signIn(identifier: string, password: string, next?: string) {
  if (!process.env.QIAN_DEV_IDENTITY_URL) throw new Error('请经 e2e.mjs 启动独立宿主');
  const session = await http.newContext({ baseURL: process.env.QIAN_DEV_IDENTITY_URL });
  const csrf = await (await session.get('/api/identity/csrf')).json() as { header: string; token: string };
  const headers = { [csrf.header]: csrf.token };
  let result = await session.post('/api/identity/login', { headers, data: { client_id: 'qian-tenant-ops', identifier, password } });
  let body = await result.json();
  if (result.ok() && body.status === 'password_change_required' && next) {
    result = await session.post('/api/identity/login/password', { headers, data: { ticket: body.ticket, new_password: next } });
    body = await result.json();
  }
  return { session, status: result.status(), body };
}

test('R110 真实宿主七步与管理员维护：列表、创建、迁移、并发、配额、管理员登录与重置、身份与错误态', async ({ page, context, baseURL }) => {
  const calls: { method: string; path: string; status: number }[] = [];
  page.on('request', (request) => { if (new URL(request.url()).pathname.startsWith('/api/tenancy')) expect(Boolean(request.headers().authorization)).toBe(false); });
  page.on('response', (response) => { const url = new URL(response.url()); if (url.pathname.startsWith('/api/tenancy')) calls.push({ method: response.request().method(), path: url.pathname, status: response.status() }); });
  // 列表请求的完整查询串：翻页与筛选须以 skip / take / keyword / status 交给宿主，不能在前端过滤。
  const lists: Record<string, string>[] = [];
  page.on('request', (request) => { const url = new URL(request.url()); if (request.method() === 'GET' && url.pathname === '/api/tenancy/tenants') lists.push(Object.fromEntries(url.searchParams)); });
  // 控制请求到达时间以观察 loading，响应仍由真实宿主产生。
  let release!: () => void;
  const gate = new Promise<void>((ok) => { release = ok; });
  await page.route('**/api/tenancy/tenants?*', async (route) => { await gate; await route.continue(); });
  await page.goto('/');
  await expect(page.getByText('加载中', { exact: true }).first()).toBeVisible();
  release();
  await expect(page.getByText('暂无数据', { exact: true })).toBeVisible();
  await page.unroute('**/api/tenancy/tenants?*');
  const create = page.getByRole('button', { name: '新建租户', exact: true });
  const dialog = page.getByRole('dialog');
  const confirmation = page.getByRole('alertdialog');
  const opened = () => calls.filter((item) => item.method === 'POST' && item.path === '/api/tenancy/tenants');
  // 管理员账号（S1-TEN-010）：A 的管理员在开户抽屉里新建；B 的管理员、其余租户共用的账号与替换用的丙先经运营接口建好，抽屉里按登录标识查找。
  const phoneA = '139' + String(randomInt(10_000_000, 99_999_999));
  async function account(displayName: string, identifier: string, initialPassword: string): Promise<string> {
    const created = await context.request.post(baseURL + '/api/tenancy/admin-accounts', { data: { displayName, identifierKind: 'username', identifier, initialPassword } });
    expect(created.status()).toBe(201);
    return (await created.json()).subjectId;
  }
  const shared = await account('公共管理员', 'tenantui', 'Init-Pass-S-2026');
  await account('管理员乙', 'adminb', 'Init-Pass-B-2026');
  const subjectC = await account('管理员丙', 'adminc', 'Init-Pass-C-2026');
  async function pick(identifier: string) {
    await dialog.getByLabel('登录标识').fill(identifier);
    await dialog.getByRole('button', { name: '查找账号', exact: true }).click();
    await dialog.getByRole('button', { name: '选为管理员', exact: true }).click();
  }
  async function form(code: string, name: string, quota = '20', admin = 'tenantui') {
    await create.click();
    await pick(admin);
    await dialog.getByLabel('租户号').fill(code);
    await dialog.getByLabel('租户名称').fill(name);
    await dialog.getByLabel('用户配额').fill(quota);
  }
  await create.click();
  await dialog.getByRole('button', { name: '查找账号', exact: true }).click();
  await expect(dialog.getByText('此项必填', { exact: true }).first()).toBeVisible();
  await dialog.getByLabel('登录标识').fill('nobody');
  await dialog.getByRole('button', { name: '查找账号', exact: true }).click();
  await expect(dialog.getByText('没有找到可以担任管理员的账号，可以新建一个。', { exact: true })).toBeVisible();
  await pick('tenantui');
  await dialog.getByRole('button', { name: '保存', exact: true }).click();
  await expect(dialog.getByText('此项必填', { exact: true }).first()).toBeVisible();
  expect(opened()).toHaveLength(0);
  await dialog.getByRole('button', { name: '关闭', exact: true }).click();
  // 验收 3：开户时经身份组件新建管理员账号；抽屉里只显示掩码，不回显手机号与初始密码。
  await create.click();
  await dialog.getByRole('button', { name: '新建账号', exact: true }).click();
  await dialog.getByLabel('姓名').fill('管理员甲');
  await dialog.getByLabel('登录标识').fill(phoneA);
  await dialog.getByLabel('初始密码').fill('Init-Pass-A-2026');
  await dialog.getByRole('button', { name: '新建账号', exact: true }).click();
  await expect(dialog.getByTestId('tenant-admin-card')).toContainText('管理员甲');
  await expect(dialog.getByTestId('tenant-admin-card')).not.toContainText(phoneA);
  await dialog.getByLabel('租户号').fill('UPPER');
  await dialog.getByLabel('租户名称').fill('租户 A');
  await dialog.getByLabel('用户配额').fill('20');
  await dialog.getByRole('button', { name: '保存', exact: true }).click();
  await expect(dialog.getByLabel('租户号')).toHaveAttribute('aria-invalid', 'true');
  expect(opened()).toHaveLength(0);
  await dialog.getByLabel('租户号').fill('tenanta');
  await dialog.getByRole('button', { name: '保存', exact: true }).click();
  await expect(dialog).toHaveCount(0);
  await form('tenantb', '租户 B', '30', 'adminb');
  await dialog.getByRole('button', { name: '保存', exact: true }).click();
  await expect(dialog).toHaveCount(0);
  const rowA = page.getByRole('row').filter({ hasText: '租户 A' });
  const rowB = page.getByRole('row').filter({ hasText: '租户 B' });
  await expect(rowA.getByText('待激活', { exact: true })).toBeVisible();
  await expect(rowB).toBeVisible();
  const listed = await (await context.request.get(baseURL + '/api/tenancy/tenants')).json();
  expect(listed.total).toBe(2);
  const a = listed.items.find((item: { code: string }) => item.code === 'tenanta');
  await page.getByLabel('搜索租户号或名称').fill('tenantb');
  await expect.poll(() => lists.at(-1)).toEqual({ skip: '0', take: '10', keyword: 'tenantb' });
  await expect(rowB).toBeVisible(); await expect(rowA).toHaveCount(0);
  await page.getByLabel('搜索租户号或名称').fill('');
  await expect(rowA).toBeVisible();

  await form('tenanta', '重复租户');
  await dialog.getByRole('button', { name: '保存', exact: true }).click();
  await expect(dialog.getByText('租户号已存在，请更换后重试。', { exact: true })).toBeVisible();
  await dialog.getByRole('button', { name: '关闭', exact: true }).click();
  // 只改出站输入，保留真实服务端 400 与 pointer 响应，不伪造响应。
  await page.route('**/api/tenancy/tenants', async (route) => {
    if (route.request().method() === 'POST') await route.continue({ postData: JSON.stringify({ ...route.request().postDataJSON(), code: 'INVALID' }) });
    else await route.continue();
  });
  await form('tenantinvalid', '服务端校验');
  await dialog.getByRole('button', { name: '保存', exact: true }).click();
  await expect(dialog.getByLabel('租户号')).toHaveAttribute('aria-invalid', 'true');
  await page.unroute('**/api/tenancy/tenants');
  await dialog.getByRole('button', { name: '关闭', exact: true }).click();

  await rowA.getByRole('button', { name: '激活', exact: true }).click();
  await dialog.getByRole('button', { name: '保存', exact: true }).click();
  await expect(rowA.getByText('正常', { exact: true })).toBeVisible();
  await expect(rowA.getByRole('button', { name: '激活', exact: true })).toHaveCount(0);
  await rowA.getByRole('button', { name: '冻结', exact: true }).click();
  await page.getByRole('alertdialog').getByRole('button', { name: '确认', exact: true }).click();
  await expect(rowA.getByText('已冻结', { exact: true })).toBeVisible();
  await rowA.getByRole('button', { name: '恢复', exact: true }).click();
  await expect(rowA.getByText('正常', { exact: true })).toBeVisible();
  await page.getByRole('combobox', { name: '状态', exact: true }).click();
  await page.getByRole('option', { name: '待激活', exact: true }).click();
  await expect.poll(() => lists.at(-1)).toEqual({ skip: '0', take: '10', status: 'pending' });
  await expect(rowA).toHaveCount(0); await expect(rowB).toBeVisible();
  await page.getByRole('combobox', { name: '状态', exact: true }).click();
  await page.getByRole('option', { name: '全部状态', exact: true }).click();
  await expect(rowA).toBeVisible();

  // 第 3 步「服务端拒绝时显示结构化错误」：另一处先激活再冻结 B，页面仍按待激活给出「激活」，提交得到真实 409。
  const b = listed.items.find((item: { code: string }) => item.code === 'tenantb');
  expect((await context.request.post(baseURL + '/api/tenancy/tenants/' + b.id + ':activate', { data: { trial: false } })).status()).toBe(200);
  expect((await context.request.post(baseURL + '/api/tenancy/tenants/' + b.id + ':suspend')).status()).toBe(200);
  await rowB.getByRole('button', { name: '激活', exact: true }).click();
  const rejected = page.waitForResponse((response) => response.request().method() === 'POST' && new URL(response.url()).pathname === '/api/tenancy/tenants/' + b.id + ':activate');
  await dialog.getByRole('button', { name: '保存', exact: true }).click();
  expect(await (await rejected).json()).toMatchObject({ status: 409, code: 'TENANCY_STATE_CONFLICT', message: '当前租户状态不允许该操作。' });
  // 状态冲突显示服务端原文，只出现一处：没有编辑冲突文案，也没有页面级提示。
  await expect(dialog.getByText('当前租户状态不允许该操作。', { exact: true })).toBeVisible();
  await expect(dialog.getByText(/当前修改未覆盖他人的更新/)).toHaveCount(0);
  await expect(page.locator('main [role="alert"]')).toHaveCount(0);
  await dialog.getByRole('button', { name: '关闭', exact: true }).click();
  await expect(page.getByRole('alert')).toHaveCount(0);
  await page.getByRole('button', { name: '刷新', exact: true }).click();
  await expect(rowB.getByText('已冻结', { exact: true })).toBeVisible();

  // 验收 3：恢复 B 后，两位开户管理员经统一登录流水线登录（首次登录先改初始密码），各自只看到本租户的数据。
  expect((await context.request.post(baseURL + '/api/tenancy/tenants/' + b.id + ':resume')).status()).toBe(200);
  const signedA = await signIn(phoneA, 'Init-Pass-A-2026', 'Next-Pass-A-2026');
  const signedB = await signIn('adminb', 'Init-Pass-B-2026', 'Next-Pass-B-2026');
  for (const [signed, own, other, name] of [[signedA, a, b, '管理员甲'], [signedB, b, a, '管理员乙']] as const) {
    expect(signed).toMatchObject({ status: 200, body: { status: 'authenticated', tenant_id: own.id } });
    expect((await (await signed.session.get('/api/identity/me')).json()).tenant_id).toBe(own.id);
    // 请求里写别的租户也不起作用：目录只按会话的租户读，成员只有本租户的管理员。
    for (const query of ['', '?tenant_id=' + other.id]) {
      const members = await (await signed.session.get('/api/directory/members' + query)).json();
      expect(members.items.map((item: { membership: { display_name: string } }) => item.membership.display_name)).toEqual([name]);
    }
  }
  await signedB.session.dispose();
  await page.getByRole('button', { name: '刷新', exact: true }).click();
  await expect(rowB.getByText('正常', { exact: true })).toBeVisible();

  await rowA.getByRole('button', { name: '编辑', exact: true }).click();
  await dialog.getByLabel('租户名称').fill('租户 A 已编辑');
  await dialog.getByLabel('用户配额').fill('25');
  await dialog.getByRole('button', { name: '保存', exact: true }).click();
  await expect(rowA.getByRole('button', { name: '租户 A 已编辑', exact: true })).toBeVisible();
  await rowA.getByRole('button', { name: '编辑', exact: true }).click();
  await expect(dialog.getByLabel('租户名称')).toHaveValue('租户 A 已编辑');
  const current = await (await context.request.get(baseURL + '/api/tenancy/tenants/' + a.id)).json();
  const external = await context.request.put(baseURL + '/api/tenancy/tenants/' + a.id, { data: { name: '租户 A 他人更新', expectedRevision: current.revision } });
  expect(external.status()).toBe(200);
  await dialog.getByLabel('租户名称').fill('不得覆盖');
  await dialog.getByRole('button', { name: '保存', exact: true }).click();
  await expect(dialog.getByText(/当前修改未覆盖他人的更新/)).toBeVisible();
  expect((await (await context.request.get(baseURL + '/api/tenancy/tenants/' + a.id)).json()).name).toBe('租户 A 他人更新');
  await dialog.getByRole('button', { name: '刷新', exact: true }).click();
  await expect(page.getByRole('alert')).toHaveCount(0);
  await rowA.getByRole('button', { name: '租户 A 他人更新', exact: true }).click();
  // 用量来自身份组件的真实成员数：只有开户管理员一人。
  await expect(dialog.getByText('25', { exact: true })).toBeVisible();
  await expect(dialog.getByText('1', { exact: true })).toBeVisible();
  const admin = dialog.getByTestId('tenant-admin-details');
  await expect(admin).toContainText('管理员甲'); await expect(admin).not.toContainText(phoneA);
  // 验收 2 的页面部分：重置管理员密码要二次确认，取消时不发请求；确认后旧密码与原会话立即失效，临时密码登录必须先改密。
  const resets = () => calls.filter((item) => item.path.endsWith(':reset-password'));
  await admin.getByRole('button', { name: '重置密码', exact: true }).click();
  await dialog.getByLabel('临时密码').fill('Temp-Pass-A-2026');
  await dialog.getByRole('button', { name: '重置密码', exact: true }).click();
  await confirmation.getByRole('button', { name: '取消', exact: true }).click();
  expect(resets()).toHaveLength(0);
  await dialog.getByRole('button', { name: '重置密码', exact: true }).click();
  await confirmation.getByRole('button', { name: '确认', exact: true }).click();
  await expect(page.getByText('管理员密码已重置', { exact: true })).toBeVisible();
  expect(resets().map((item) => item.status)).toEqual([204]);
  expect((await signedA.session.get('/api/identity/me')).status()).toBe(401);
  await signedA.session.dispose();
  const stale = await signIn(phoneA, 'Next-Pass-A-2026');
  expect(stale).toMatchObject({ status: 401, body: { code: 'INVALID_CREDENTIALS' } });
  const temporary = await signIn(phoneA, 'Temp-Pass-A-2026');
  expect(temporary.body).toMatchObject({ status: 'password_change_required' });
  for (const signed of [stale, temporary]) await signed.session.dispose();
  // 更换管理员：查找已有账号、选为管理员、二次确认；B 的管理员换成丙。
  await rowB.getByRole('button', { name: '租户 B', exact: true }).click();
  await dialog.getByTestId('tenant-admin-details').getByRole('button', { name: '更换管理员', exact: true }).click();
  await pick('adminc');
  await confirmation.getByRole('button', { name: '确认', exact: true }).click();
  await expect(page.getByText('管理员已更换', { exact: true })).toBeVisible();
  const admins = await (await context.request.get(baseURL + '/api/tenancy/tenants/' + b.id + '/admins')).json();
  expect(admins.items.map((item: { subjectId: string }) => item.subjectId)).toEqual([subjectC]);

  // 第 1 步服务端分页：再建 13 个（租户号排在 tenanta / tenantb 之后），共 15 个。
  // 总数取自响应而不是本页行数；第 2 页与每页条数都以 skip / take 交给宿主。
  for (let index = 1; index <= 13; index++) {
    const code = 'zpage' + String(index).padStart(2, '0');
    const created = await context.request.post(baseURL + '/api/tenancy/tenants', { data: { code, name: '分页租户 ' + code, isolation: 'row', administrator: { membershipId: randomUUID(), subjectId: shared } } });
    expect(created.status()).toBe(201);
  }
  await page.getByRole('button', { name: '刷新', exact: true }).click();
  await expect(page.getByText('共 15 条', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: '第 2 页', exact: true }).click();
  await expect.poll(() => lists.at(-1)).toEqual({ skip: '10', take: '10' });
  await expect(page.getByRole('row').filter({ hasText: 'zpage13' })).toBeVisible(); await expect(rowA).toHaveCount(0);
  await page.getByRole('combobox', { name: '每页条数', exact: true }).click();
  await page.getByRole('option', { name: '20 条/页', exact: true }).click();
  await expect.poll(() => lists.at(-1)).toEqual({ skip: '0', take: '20' });
  await expect(page.getByRole('row').filter({ hasText: 'zpage13' })).toBeVisible(); await expect(rowA).toBeVisible();

  const accessibility = await new AxeBuilder({ page }).analyze();
  expect(accessibility.violations.filter((item) => ['critical', 'serious'].includes(item.impact ?? ''))).toEqual([]);
  await page.screenshot({ path: resolve('../../TestResults/tenant-ui/tenants.png'), fullPage: true });
  // 第 6 步的抽屉部分：编辑途中身份换成租户主体，保存得到真实 403。非 409 错误同样只在抽屉内显示服务端原文，不报页面级提示。
  await rowA.getByRole('button', { name: '编辑', exact: true }).click();
  await expect(dialog.getByLabel('租户名称')).toBeVisible();
  await context.setExtraHTTPHeaders({ 'x-qian-dev-principal': 'tenant', 'x-qian-dev-tenant': a.id });
  const forbidden = page.waitForResponse((response) => response.request().method() === 'PUT' && new URL(response.url()).pathname === '/api/tenancy/tenants/' + a.id);
  await dialog.getByRole('button', { name: '保存', exact: true }).click();
  const denial = await (await forbidden).json();
  expect(denial).toMatchObject({ status: 403, code: 'AUTHZ_DENIED' });
  await expect(dialog.getByText(denial.message, { exact: true })).toBeVisible();
  await expect(dialog.getByRole('alert')).toHaveCount(1);
  await expect(page.locator('main [role="alert"]')).toHaveCount(0);
  await dialog.getByRole('button', { name: '关闭', exact: true }).click();
  await expect(page.getByRole('alert')).toHaveCount(0);
  // 行内动作没有抽屉可承载，出错报页面级提示：身份仍是租户主体，冻结 A 得到真实 403。提示显示状态码与服务端原文，A 仍为正常。
  await rowA.getByRole('button', { name: '冻结', exact: true }).click();
  const refused = page.waitForResponse((response) => response.request().method() === 'POST' && new URL(response.url()).pathname === '/api/tenancy/tenants/' + a.id + ':suspend');
  await page.getByRole('alertdialog').getByRole('button', { name: '确认', exact: true }).click();
  const refusal = await (await refused).json();
  expect(refusal).toMatchObject({ status: 403, code: 'AUTHZ_DENIED' });
  await expect(page.locator('main [role="alert"]')).toHaveText('403' + refusal.message);
  await expect(rowA.getByText('正常', { exact: true })).toBeVisible();
  await context.setExtraHTTPHeaders({ 'x-qian-dev-principal': 'tenant', 'x-qian-dev-tenant': a.id });
  await page.reload(); await expect(page.getByText('无权限访问租户管理', { exact: true })).toBeVisible();
  await expect(page.getByText('403 AUTHZ_DENIED', { exact: true })).toBeVisible();
  await context.setExtraHTTPHeaders({ 'x-qian-dev-principal': 'none' });
  await page.reload(); await expect(page.getByText('需要身份凭据', { exact: true })).toBeVisible();
  await context.setExtraHTTPHeaders({}); await page.reload(); await expect(rowA).toBeVisible();
  expect(await page.evaluate(() => ({ local: Object.keys(localStorage), session: Object.keys(sessionStorage), frames: document.querySelectorAll('iframe').length }))).toEqual({ local: [], session: [], frames: 0 });
  expect(page.url()).not.toMatch(/token|jwt|bearer/i);
  if (!process.env.QIAN_UI_STOP_FILE || !process.env.QIAN_UI_STOPPED_FILE) throw new Error('请经 e2e.mjs 启动独立宿主');
  // 先打开编辑抽屉再停宿主：抽屉里的保存走真实断线，只在抽屉内显示连接失败文案，不报页面级提示。
  await rowA.getByRole('button', { name: '编辑', exact: true }).click();
  await expect(dialog.getByLabel('租户名称')).toBeVisible();
  await writeFile(process.env.QIAN_UI_STOP_FILE, 'stop');
  await expect.poll(async () => { try { await access(process.env.QIAN_UI_STOPPED_FILE!); return true; } catch { return false; } }).toBe(true);
  await dialog.getByRole('button', { name: '保存', exact: true }).click();
  await expect(dialog.getByText('暂时无法连接服务，请稍后重试。', { exact: true })).toBeVisible();
  await expect(dialog.getByRole('alert')).toHaveCount(1);
  await expect(page.locator('main [role="alert"]')).toHaveCount(0);
  await dialog.getByRole('button', { name: '关闭', exact: true }).click();
  await page.getByRole('button', { name: '刷新', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('暂时无法连接服务，请稍后重试。');
  await expect(page.getByRole('button', { name: '重试', exact: true })).toBeVisible();
  for (const status of [201, 400, 401, 403, 409]) expect(calls.some((item) => item.status === status)).toBe(true);
  await writeFile(resolve('../../TestResults/tenant-ui/http-evidence.json'), JSON.stringify(calls, null, 2) + '\n');
});
