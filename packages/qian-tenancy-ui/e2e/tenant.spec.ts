import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { writeFile, access } from 'node:fs/promises';
import { resolve } from 'node:path';

test('R110 真实宿主七步：列表、创建、迁移、并发、配额、身份与错误态', async ({ page, context, baseURL }) => {
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
  async function form(code: string, name: string, quota = '20') {
    await create.click();
    await dialog.getByLabel('租户号').fill(code);
    await dialog.getByLabel('租户名称').fill(name);
    await dialog.getByLabel('管理员成员 ID').fill('01900000-0000-7000-8000-000000000101');
    await dialog.getByLabel('管理员主体 ID').fill('tenant-ui-owner-' + code);
    await dialog.getByLabel('用户配额').fill(quota);
  }
  await create.click();
  await dialog.getByRole('button', { name: '保存', exact: true }).click();
  await expect(dialog.getByText('此项必填', { exact: true }).first()).toBeVisible();
  expect(calls.filter((item) => item.method === 'POST')).toHaveLength(0);
  await dialog.getByRole('button', { name: '关闭', exact: true }).click();
  await form('tenanta', '租户 A');
  await dialog.getByLabel('租户号').fill('UPPER');
  await dialog.getByRole('button', { name: '保存', exact: true }).click();
  await expect(dialog.getByLabel('租户号')).toHaveAttribute('aria-invalid', 'true');
  expect(calls.filter((item) => item.method === 'POST')).toHaveLength(0);
  await dialog.getByLabel('租户号').fill('tenanta');
  await dialog.getByRole('button', { name: '保存', exact: true }).click();
  await expect(dialog).toHaveCount(0);
  await form('tenantb', '租户 B', '30');
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
  await expect(dialog.getByText('用量不可用', { exact: true })).toBeVisible();
  await expect(dialog.getByText('25', { exact: true })).toBeVisible();
  // 只读抽屉没有表单，底部另有一个「关闭」按钮；与上面几处一样点表头关闭按钮。
  await dialog.getByTestId('yue-form-drawer-close').click();

  // 第 1 步服务端分页：再建 13 个（租户号排在 tenanta / tenantb 之后），共 15 个。
  // 总数取自响应而不是本页行数；第 2 页与每页条数都以 skip / take 交给宿主。
  for (let index = 1; index <= 13; index++) {
    const code = 'zpage' + String(index).padStart(2, '0');
    const created = await context.request.post(baseURL + '/api/tenancy/tenants', { data: { code, name: '分页租户 ' + code, isolation: 'row', administrator: { membershipId: '01900000-0000-7000-8000-000000000101', subjectId: 'tenant-ui-owner-' + code } } });
    expect(created.status()).toBe(201);
  }
  await page.getByRole('button', { name: '刷新', exact: true }).click();
  await expect(page.getByText('共 15 条', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: '第 2 页', exact: true }).click();
  await expect.poll(() => lists.at(-1)).toEqual({ skip: '10', take: '10' });
  await expect(page.getByRole('row').filter({ hasText: 'zpage13' })).toBeVisible(); await expect(rowA).toHaveCount(0);
  await page.getByRole('combobox', { name: '每页条数', exact: true }).click();
  await page.getByRole('option', { name: '20', exact: true }).click();
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
  await expect(page.locator('main [role="alert"]')).toHaveCount(0);
  await dialog.getByRole('button', { name: '关闭', exact: true }).click();
  await expect(page.getByRole('alert')).toHaveCount(0);
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
  await expect(page.locator('main [role="alert"]')).toHaveCount(0);
  await dialog.getByRole('button', { name: '关闭', exact: true }).click();
  await page.getByRole('button', { name: '刷新', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('暂时无法连接服务，请稍后重试。');
  await expect(page.getByRole('button', { name: '重试', exact: true })).toBeVisible();
  for (const status of [201, 400, 401, 403, 409]) expect(calls.some((item) => item.status === status)).toBe(true);
  await writeFile(resolve('../../TestResults/tenant-ui/http-evidence.json'), JSON.stringify(calls, null, 2) + '\n');
});
