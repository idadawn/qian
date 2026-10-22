import { expect, test } from '@playwright/test';
import scenarios from '../../../packages/yue/shell/docs/scenarios.json' with { type: 'json' };

for (const scenario of scenarios) for (const theme of ['light', 'dark'] as const) test(`shell ${scenario.id} ${theme}`, async ({ page }) => {
  await page.emulateMedia({ colorScheme: theme });
  await page.goto(`/shell.html?${new URLSearchParams({ component: scenario.component, state: scenario.state, theme })}`);
  if (scenario.state === 'MultiTenant') {
    await page.getByLabel(/账号/).fill('demo'); await page.getByLabel(/密码/).fill('demo-password'); await page.getByRole('button', { name: '登录', exact: true }).click();
    await expect(page.getByRole('heading', { name: '选择租户', exact: true })).toBeVisible();
  } else if (scenario.state === 'CallbackFailure') {
    await page.getByRole('button', { name: '企业单点登录' }).click(); await expect(page.getByRole('alert')).toContainText('单点登录未完成');
  } else await expect(page.locator('[data-slot="app-shell"], [data-slot="authentication"], [data-slot="fallback-page"]').first()).toBeVisible();
  await expect(page.locator('[data-starting-style], [data-ending-style]')).toHaveCount(0);
  await expect(page).toHaveScreenshot(`shell-${scenario.id}-${theme}.png`, { fullPage: true });
});

for (const theme of ['light', 'dark'] as const) test(`shell SessionExpired ${theme}`, async ({ page }) => {
  await page.goto(`/shell.html?theme=${theme}`); await page.getByRole('button', { name: '模拟会话过期' }).click();
  await expect(page.getByRole('alertdialog', { name: '登录已过期' })).toBeVisible(); await expect(page.locator('[data-starting-style], [data-ending-style]')).toHaveCount(0);
  await expect(page).toHaveScreenshot(`shell-SessionExpired-${theme}.png`, { fullPage: true });
});
