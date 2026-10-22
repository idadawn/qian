import { expect, test, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { settleVisuals } from './settle';

async function login(page: Page) {
  await page.getByLabel(/账号/).fill('demo'); await page.getByLabel(/密码/).fill('demo-password');
  await expect(page.getByLabel(/密码/)).toHaveAttribute('type', 'password');
  await page.getByRole('button', { name: '登录', exact: true }).click();
}
async function accessible(page: Page) { await settleVisuals(page); const result = await new AxeBuilder({ page }).analyze(); expect(result.violations.filter(value => ['critical', 'serious'].includes(value.impact ?? ''))).toEqual([]); }
async function noCredentials(page: Page) {
  const exposed = await page.evaluate(() => ({ url: location.href, frames: [...document.querySelectorAll('iframe')].map(frame => frame.src), local: JSON.stringify(localStorage), session: JSON.stringify(sessionStorage) }));
  expect(JSON.stringify(exposed)).not.toMatch(/mock-(?:access|refreshed)-only-in-memory|access_token|refresh_token|selection_token|demo-password/);
}

test('外壳首屏不请求表单引擎，进入登录页才加载', async ({ page }) => {
  const urls: string[] = []; page.on('request', request => urls.push(request.url()));
  await page.goto('/shell.html'); await expect(page.getByRole('heading', { name: '工作台', exact: true })).toBeVisible();
  expect(urls.some(url => /schema-form-|password-login-/.test(url))).toBe(false);
  await page.getByRole('button', { name: '用户菜单' }).click(); await page.getByRole('menuitem', { name: '退出登录' }).click();
  await expect(page.getByLabel(/密码/)).toBeVisible(); expect(urls.some(url => /schema-form-|password-login-/.test(url))).toBe(true);
});

test('密码登录校验与错误恢复，经 SDK mock 成功后进入单侧栏外壳', async ({ page }) => {
  await page.goto('/shell.html?component=Authentication');
  await page.getByRole('button', { name: '登录', exact: true }).click(); await expect(page.getByLabel(/账号/)).toBeFocused();
  await page.getByLabel(/账号/).fill('demo'); await page.getByLabel(/密码/).fill('wrong'); await page.getByRole('button', { name: '登录', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('登录失败'); await expect(page.getByLabel(/密码/)).toHaveValue(''); await accessible(page);
  await login(page); await expect(page.getByRole('heading', { name: '工作台', exact: true })).toBeVisible();
  await page.getByRole('button', { name: '读取受保护数据' }).click(); await expect(page.getByRole('status').filter({ hasText: '请求租户：tenant-a' })).toBeVisible();
  await accessible(page); await noCredentials(page);
});

test('多租户二次选择及切换后，UI 与 SDK 请求头一起更新', async ({ page }) => {
  await page.goto('/shell.html?component=Authentication&state=MultiTenant'); await login(page);
  await expect(page.getByRole('heading', { name: '选择租户', exact: true })).toBeVisible(); await accessible(page);
  await page.getByRole('button', { name: '生产租户', exact: true }).focus(); await page.keyboard.press('Enter');
  await page.getByRole('button', { name: '读取受保护数据' }).click(); await expect(page.getByText('请求租户：tenant-b', { exact: true })).toBeVisible();
  await page.getByRole('combobox', { name: '切换租户' }).click(); await accessible(page); await page.getByRole('option', { name: '研发租户' }).click();
  await page.getByRole('button', { name: '读取受保护数据' }).click(); await expect(page.getByText('请求租户：tenant-a', { exact: true })).toBeVisible(); await noCredentials(page);
});

for (const state of ['Default', 'MultiTenant']) test(`SSO 真跳转到回调页并完成 ${state}`, async ({ page }) => {
  await page.goto(`/shell.html?component=Authentication&state=${state}`);
  const navigations: string[] = []; page.on('framenavigated', frame => { if (frame === page.mainFrame()) navigations.push(frame.url()); });
  await page.getByRole('button', { name: '企业单点登录' }).click();
  if (state === 'MultiTenant') await page.getByRole('button', { name: '研发租户', exact: true }).click();
  await expect(page.getByRole('heading', { name: '工作台', exact: true })).toBeVisible();
  expect(navigations.some(url => url.includes('code=mock-code') && url.includes('auth_state=mock-state'))).toBe(true);
  await expect(page).not.toHaveURL(/(?:code=|access_token=)/); await accessible(page); await noCredentials(page);
});

for (const query of ['state=CallbackFailure', 'code=invalid&auth_state=bad']) test(`SSO 回调失败有错误态和返回路径 ${query}`, async ({ page }) => {
  await page.goto(`/shell.html?component=Authentication&${query}`);
  if (query.startsWith('state=')) await page.getByRole('button', { name: '企业单点登录' }).click();
  await expect(page.getByRole('alert')).toContainText('单点登录未完成'); await accessible(page);
  await page.getByRole('button', { name: '返回登录' }).click(); await expect(page.getByLabel(/账号/)).toBeVisible(); await noCredentials(page);
});

test('401 且刷新失败只弹一个过期弹窗，明确重新登录后恢复', async ({ page }) => {
  await page.goto('/shell.html'); await page.getByRole('button', { name: '模拟会话过期' }).click();
  const dialog = page.getByRole('alertdialog', { name: '登录已过期' }); await expect(dialog).toHaveCount(1); await accessible(page);
  await page.keyboard.press('Escape'); await expect(dialog).toBeVisible();
  await dialog.getByRole('button', { name: '重新登录' }).click(); await login(page);
  await expect(dialog).not.toBeVisible(); await noCredentials(page);
});

test('菜单路由、侧栏、偏好、代管横幅及异常恢复可操作', async ({ page }) => {
  await page.goto('/shell.html?state=Managed');
  await expect(page.getByTestId('yue-app-shell-managed')).toBeVisible();
  await page.getByRole('link', { name: '应用设置', exact: true }).click(); await expect(page.getByRole('heading', { name: '应用设置', exact: true })).toBeVisible();
  await expect(page.getByRole('link', { name: '应用设置', exact: true })).toHaveAttribute('aria-current', 'page');
  await expect(page.getByRole('link', { name: '开发文档' })).toHaveAttribute('rel', 'noopener noreferrer');
  await page.getByRole('button', { name: '收起侧栏' }).click(); await expect(page.getByRole('button', { name: '展开侧栏' })).toHaveAttribute('aria-expanded', 'false');
  await page.getByRole('combobox', { name: '主题', exact: true }).click(); await accessible(page); await page.getByRole('option', { name: '暗色' }).click(); await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await page.getByRole('button', { name: '刷新页面', exact: true }).click(); await expect(page.getByTestId('yue-update-banner')).not.toBeVisible();
  await page.getByRole('button', { name: '模拟渲染失败' }).click(); await expect(page.getByRole('heading', { name: '页面暂时无法使用' })).toBeVisible(); await expect(page.locator('body')).not.toContainText('internal-stack');
  await page.getByRole('button', { name: '重试', exact: true }).click(); await expect(page.getByTestId('yue-fallback-page')).not.toBeVisible();
  await page.getByRole('button', { name: '用户菜单' }).click(); await accessible(page); await page.keyboard.press('Escape');
  await page.getByRole('combobox', { name: '语言' }).click(); await page.getByRole('option', { name: 'en-US' }).click(); await expect(page.getByRole('button', { name: 'Expand sidebar' })).toBeVisible(); await noCredentials(page);
});

test('浏览器离线事件显示横幅，恢复后保留页面', async ({ page, context }) => {
  await page.goto('/shell.html'); await expect(page.getByRole('heading', { name: '工作台', exact: true })).toBeVisible();
  await context.setOffline(true); await expect(page.getByTestId('yue-offline-banner')).toBeVisible(); await accessible(page);
  await context.setOffline(false); await expect(page.getByTestId('yue-offline-banner')).not.toBeVisible(); await expect(page.getByRole('heading', { name: '工作台', exact: true })).toBeVisible();
});

for (const theme of ['light', 'dark']) for (const [state, title] of [['Forbidden', '无权访问此页面'], ['NotFound', '页面不存在'], ['ServerError', '页面暂时无法使用'], ['Offline', '网络连接已断开']]) test(`兜底 ${state} ${theme} 无障碍和恢复`, async ({ page }) => {
  await page.goto(`/shell.html?component=FallbackPage&state=${state}&theme=${theme}`); await expect(page.getByRole('heading', { name: title, exact: true })).toBeVisible(); await accessible(page);
  await page.getByRole('button', { name: '重试' }).click(); await expect(page.getByRole('status')).toContainText('已重试');
});
