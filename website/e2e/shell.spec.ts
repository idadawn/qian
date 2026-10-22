import { expect, test } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import scenarios from '../../packages/yue/shell/docs/scenarios.json' with { type: 'json' };
import { settleVisuals } from '../../apps/yue-playground/e2e/settle';

for (const scenario of scenarios) for (const theme of ['light', 'dark']) test(`${scenario.id} ${theme} Rspress 场景与真实操作`, async ({ page }) => {
  await page.goto(`yue-demo/index.html?component=${scenario.component}&state=${scenario.state}&theme=${theme}`);
  if (scenario.component === 'AppShell') {
    await page.getByRole('link', { name: '应用设置', exact: true }).click(); await expect(page.getByRole('heading', { name: '应用设置', exact: true })).toBeVisible();
    await page.getByRole('button', { name: '读取受保护数据' }).click(); await expect(page.getByText('请求租户：tenant-a', { exact: true })).toBeVisible();
    if (scenario.state === 'Managed') await expect(page.getByTestId('yue-app-shell-managed')).toBeVisible();
    if (scenario.state === 'Offline') await expect(page.getByTestId('yue-offline-banner')).toBeVisible();
    await page.getByRole('button', { name: '模拟会话过期' }).click(); await expect(page.getByRole('alertdialog', { name: '登录已过期' })).toBeVisible();
  } else if (scenario.component === 'Authentication') {
    if (scenario.state === 'CallbackFailure') {
      await page.getByRole('button', { name: '企业单点登录' }).click(); await expect(page.getByRole('alert')).toContainText('单点登录未完成');
    } else {
      await page.getByLabel(/账号/).fill('demo'); await page.getByLabel(/密码/).fill('demo-password'); await page.getByRole('button', { name: '登录', exact: true }).click();
      if (scenario.state === 'MultiTenant') await page.getByRole('button', { name: '生产租户', exact: true }).click();
      await expect(page.getByRole('heading', { name: '工作台', exact: true })).toBeVisible();
    }
  } else {
    await expect(page.getByTestId('yue-fallback-page')).toBeVisible(); await page.getByRole('button', { name: '重试' }).focus();
  }
  await settleVisuals(page); const axe = await new AxeBuilder({ page }).analyze(); expect(axe.violations.filter(value => ['critical', 'serious'].includes(value.impact ?? ''))).toEqual([]);
  await expect(page.locator('html')).toHaveAttribute('data-theme', theme);
  expect(page.url()).not.toMatch(/access_token|refresh_token|selection_token/);
});

test('静态 Rspress 外壳文档可发现，三个 iframe 使用同源示例且交互通过', async ({ page }) => {
  await page.goto('components/yue/shell.html'); await expect(page.getByRole('heading', { level: 1 })).toContainText('外壳、页面与认证');
  const shell = page.frameLocator('iframe[title="AppShell 交互预览"]');
  await shell.getByRole('link', { name: '应用设置', exact: true }).click(); await expect(shell.getByRole('heading', { name: '应用设置', exact: true })).toBeVisible();
  const auth = page.frameLocator('iframe[title="Authentication 交互预览"]');
  await expect(page.locator('iframe[title="Authentication 交互预览"]')).toHaveAttribute('sandbox', 'allow-scripts allow-same-origin allow-forms');
  for (const name of ['AppShell', 'FallbackPage']) await expect(page.locator(`iframe[title="${name} 交互预览"]`)).toHaveAttribute('sandbox', 'allow-scripts allow-same-origin');
  await auth.getByLabel(/账号/).fill('demo'); await auth.getByLabel(/密码/).fill('demo-password'); await auth.getByRole('button', { name: '登录', exact: true }).click(); await expect(auth.getByRole('heading', { name: '工作台', exact: true })).toBeVisible();
  await auth.getByRole('button', { name: '用户菜单' }).click(); await auth.getByRole('menuitem', { name: '退出登录' }).click();
  await auth.getByRole('button', { name: '企业单点登录' }).click(); await expect(auth.getByRole('heading', { name: '工作台', exact: true })).toBeVisible();
  const fallback = page.frameLocator('iframe[title="FallbackPage 交互预览"]');
  for (const state of ['Forbidden', 'NotFound', 'ServerError', 'Offline']) { await fallback.getByLabel('示例状态').selectOption(state); await expect(fallback.getByTestId('yue-fallback-page')).toBeVisible(); }
  const result = await new AxeBuilder({ page }).analyze(); expect(result.violations.filter(value => ['critical', 'serious'].includes(value.impact ?? ''))).toEqual([]);
});

test('SchemaForm 可信 iframe 可实际提交，其他示例不扩大 sandbox 权限', async ({ page }) => {
  await page.goto('components/yue/form.html');
  const form = page.frameLocator('iframe[title="SchemaForm 交互预览"]');
  await form.getByRole('button', { name: '新建客户', exact: true }).click();
  await form.getByRole('textbox', { name: /客户号/ }).fill('doc1'); await form.getByRole('textbox', { name: /客户名称/ }).fill('文档客户');
  await form.getByRole('button', { name: '保存', exact: true }).click(); await expect(form.getByRole('status').filter({ hasText: '已保存：文档客户' })).toBeVisible();
  await page.goto('components/yue/core/button.html'); await expect(page.locator('iframe[title="Button 交互预览"]')).toHaveAttribute('sandbox', 'allow-scripts allow-same-origin');
});

test('外壳示例断外网后可编辑并运行', async ({ page, baseURL }) => {
  await page.route('**/*', route => new URL(route.request().url()).origin === new URL(baseURL!).origin ? route.continue() : route.abort());
  await page.goto('yue-demo/index.html?component=AppShell'); await page.getByRole('button', { name: '编辑代码', exact: true }).click();
  await expect(page.getByRole('button', { name: '运行', exact: true })).toBeEnabled(); await page.getByRole('button', { name: '运行', exact: true }).click();
  await expect(page.getByRole('heading', { name: '工作台', exact: true })).toBeVisible(); await expect(page.locator('.demo-error')).toHaveCount(0);
  await page.getByRole('button', { name: '读取受保护数据' }).click(); await expect(page.getByText('请求租户：tenant-a', { exact: true })).toBeVisible();
});
