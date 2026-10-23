import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { writeFileSync } from 'node:fs';
import { gzipSync } from 'node:zlib';
import { settleVisuals } from '../../yue-playground/e2e/settle';

interface Control {
  identity(): Promise<{ button: boolean; react: boolean; client: boolean; sdk: boolean }>;
  bridgeGuards(): { unknownRejected: boolean; scopeRejected: boolean; frozen: boolean };
  dump(): { plugin: string }[];
  unload(id: string, scope: 'global' | `tenant:${string}`): Promise<void>;
  preload(): Promise<void>;
  windowCalls(): Promise<{ tenant: string; frozen: boolean; pathname: string; result: { channel: string; idempotencyKey: string; tenant: string } }>;
  windowCallerRejected(): boolean;
  setTenant(id: string): void;
}
declare global { interface Window { __qianAcceptance: Control } }

test('真实官方租户 MF 制品按页面加载，同一 React / SDK 实例与桥接拒绝规则', async ({ page }) => {
  const requested: string[] = [];
  const scripts: Promise<{ url: string; gzip: number }>[] = [];
  page.on('response', (response) => { if (response.request().resourceType() === 'script') scripts.push(response.body().then((body) => ({ url: response.url(), gzip: gzipSync(body).byteLength }))); });
  page.on('request', (request) => { if (request.url().includes('/remotes/')) requested.push(request.url()); });
  await page.goto('/'); await expect(page.getByRole('link', { name: '租户管理' })).toBeVisible();
  await page.waitForLoadState('networkidle');
  const initial = await Promise.all(scripts);
  expect(initial.reduce((total, script) => total + script.gzip, 0)).toBeLessThanOrEqual(250 * 1024);
  writeFileSync(new URL('../acceptance-results/first-screen.json', import.meta.url), JSON.stringify({ total: initial.reduce((total, script) => total + script.gzip, 0), scripts: initial }, null, 2));
  expect(requested).toEqual([]);
  await page.getByRole('link', { name: '租户管理' }).click();
  await expect(page.getByTestId('qian-tenancy-remote')).toBeVisible();
  await expect(page.getByTestId('qian-tenancy-remote')).toHaveAttribute('data-scope', '11111111-1111-4111-8111-111111111111');
  expect(requested.some((url) => url.endsWith('/mf-manifest.json'))).toBe(true);
  expect(requested.some((url) => url.endsWith('/remoteEntry.js'))).toBe(true);
  expect(await page.evaluate(() => window.__qianAcceptance.identity())).toEqual({ button: true, react: true, client: true, sdk: true });
  expect(await page.evaluate(() => window.__qianAcceptance.bridgeGuards())).toEqual({ unknownRejected: true, scopeRejected: true, frozen: true });
  expect((await page.evaluate(() => window.__qianAcceptance.dump())).map((entry) => entry.plugin)).toEqual(['qian.shell', 'qian.tenancy']);
  await expect(page.getByRole('button', { name: '新建租户' })).toBeEnabled();
  await settleVisuals(page);
  const violations = (await new AxeBuilder({ page }).analyze()).violations.filter((entry) => ['critical', 'serious'].includes(entry.impact ?? ''));
  expect(violations).toEqual([]);
  await page.screenshot({ path: new URL('../acceptance-results/official-tenant-remote.png', import.meta.url).pathname, fullPage: true });
  await page.evaluate(() => window.__qianAcceptance.unload('qian.tenancy', 'tenant:11111111-1111-4111-8111-111111111111'));
  await expect(page.getByTestId('qian-tenancy-remote')).toHaveCount(0);
  await expect(page.getByRole('link', { name: '租户管理' })).toHaveCount(0);
});
test('实际 MF 远程通过 window bridge 正向调用；跨 scope 与卸载后同一句柄被撤销', async ({ page, context }) => {
  await context.addCookies([{ name: 'mf-scenario', value: 'bridge', url: 'http://127.0.0.1:5180' }]);
  await page.goto('/tenants'); await expect(page.getByTestId('qian-tenancy-remote')).toBeVisible();
  const invoked = page.waitForRequest((request) => request.url().endsWith('/api/capabilities/qian.acceptance/ping/invoke'));
  const result = await page.evaluate(() => window.__qianAcceptance.windowCalls());
  expect((await invoked).method()).toBe('POST');
  expect(result).toEqual({ tenant: '11111111-1111-4111-8111-111111111111', frozen: true, pathname: '/tenants', result: { channel: 'window-bridge-http', idempotencyKey: '11111111-1111-4111-8111-111111111111', tenant: '11111111-1111-4111-8111-111111111111' } });
  expect(await page.evaluate(() => window.__qianAcceptance.bridgeGuards())).toEqual({ unknownRejected: true, scopeRejected: true, frozen: true });
  await page.evaluate(() => window.__qianAcceptance.setTenant('22222222-2222-4222-8222-222222222222'));
  expect(await page.evaluate(() => window.__qianAcceptance.windowCallerRejected())).toBe(true);
  await page.evaluate(() => window.__qianAcceptance.setTenant('11111111-1111-4111-8111-111111111111'));
  await page.evaluate(() => window.__qianAcceptance.unload('qian.tenancy', 'tenant:11111111-1111-4111-8111-111111111111'));
  expect(await page.evaluate(() => window.__qianAcceptance.windowCallerRejected())).toBe(true);
});
test('MF 预加载读取真实 manifest/types，运行时贡献仍等待页面需要', async ({ page, request }) => {
  await page.goto('/'); await expect(page.getByTestId('yue-app-shell')).toBeVisible();
  const manifestResponse = page.waitForResponse((response) => response.url().endsWith('/remotes/qian.tenancy/mf-manifest.json'));
  await page.evaluate(() => window.__qianAcceptance.preload());
  const manifest = await (await manifestResponse).json();
  expect(manifest.exposes.map((item: { name: string }) => item.name)).toEqual(['TenantPage']);
  expect(manifest.metaData.types.zip).toBe('@mf-types.zip');
  expect((await request.get('/remotes/qian.tenancy/@mf-types.zip')).status()).toBe(200);
  expect((await request.get('/remotes/qian.tenancy/@mf-types.d.ts')).status()).toBe(200);
  expect(await page.evaluate(() => window.__qianAcceptance.dump())).toHaveLength(1);
  await expect(page.getByTestId('qian-tenancy-remote')).toHaveCount(0);
});
test('真实浏览器的租户贡献遮蔽全局，卸载后全局导航和远程恢复', async ({ page, context }) => {
  await context.addCookies([{ name: 'mf-scenario', value: 'shadow', url: 'http://127.0.0.1:5180' }]);
  await page.goto('/tenants'); await expect(page.getByTestId('qian-tenancy-remote')).toBeVisible();
  await expect(page.getByRole('link', { name: '租户专属管理' })).toBeVisible();
  await expect(page.getByRole('link', { name: '租户管理', exact: true })).toHaveCount(0);
  await page.evaluate(() => window.__qianAcceptance.unload('qian.tenancy', 'tenant:11111111-1111-4111-8111-111111111111'));
  await expect(page.getByRole('link', { name: '租户管理', exact: true })).toBeVisible();
  await expect(page.getByTestId('qian-tenancy-remote')).toBeVisible();
  expect((await page.evaluate(() => window.__qianAcceptance.dump())).map((entry) => entry.plugin)).toEqual(['qian.shell', 'qian.tenancy']);
});
test('租户 B 未挂载，导航和预加载都不请求 mf-manifest.json', async ({ page, context }) => {
  await context.addCookies([{ name: 'mf-scenario', value: 'b', url: 'http://127.0.0.1:5180' }]);
  const remotes: string[] = []; page.on('request', (request) => { if (request.url().includes('/remotes/')) remotes.push(request.url()); });
  await page.goto('/tenants'); await expect(page.getByTestId('yue-app-shell')).toBeVisible();
  await page.evaluate(() => window.__qianAcceptance.preload());
  await expect(page.getByRole('link', { name: '租户管理' })).toHaveCount(0); expect(remotes).toEqual([]);
});
test('不匹配 yue 版本先拒绝，控制台与真实 PostgreSQL Audit 链留痕且没有远程网络', async ({ page, context, request }) => {
  const before = await (await request.get('/__acceptance/audit')).json();
  await context.addCookies([{ name: 'mf-scenario', value: 'bad', url: 'http://127.0.0.1:5180' }]);
  const remotes: string[] = [], errors: string[] = [];
  page.on('request', (request) => { if (request.url().includes('/remotes/')) remotes.push(request.url()); });
  page.on('console', (message) => { if (message.type() === 'error') errors.push(message.text()); });
  await page.goto('/tenants'); await expect(page.getByRole('alert')).toContainText('插件未能加载');
  expect(remotes).toEqual([]); expect(errors.some((message) => message.includes('qian.shell.remote.rejected'))).toBe(true);
  const after = await (await request.get('/__acceptance/audit')).json();
  expect(after.valid).toBe(true); expect(after.checkedEntries).toBe(before.checkedEntries + 1);
  const record = after.records.at(-1);
  expect(record.tenantId).toBe('11111111-1111-4111-8111-111111111111');
  expect(record.actorId).toBe('shell-acceptance-user');
  expect(record.payload).toMatchObject({ name: 'shell.remote.rejected', remoteId: 'qian.tenancy', scope: 'tenant:11111111-1111-4111-8111-111111111111', reason: 'engines.yue', requiredVersion: '^99.0.0', code: 'INPUT_INVALID' });
  expect(record.contentHash).toMatch(/^[0-9a-f]{64}$/);
  writeFileSync(new URL('../acceptance-results/audit-chain.json', import.meta.url), JSON.stringify({ before: before.checkedEntries, after }, null, 2));
});
