import { expect, test } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import scenarios from '../../packages/yue/form/docs/scenarios.json';

for (const scenario of scenarios) test(`SchemaForm/${scenario.state} 文档场景`, async ({ page }) => {
  await page.goto(`/yue-demo/index.html?component=SchemaForm&state=${scenario.state}&theme=${scenario.theme}`);
  if (scenario.state === 'Readonly') await expect(page.getByText('••••5678')).toBeVisible();
  else {
    await page.getByRole('button', { name: '新建客户', exact: true }).click();
    const dialog = page.getByRole('dialog', { name: '新建客户' });
    await dialog.getByRole('textbox', { name: /客户号/ }).fill('demo1');
    await dialog.getByRole('textbox', { name: /客户名称/ }).fill('文档客户');
    await dialog.getByRole('button', { name: '保存', exact: true }).click();
    if (scenario.state === 'Error') await expect(dialog.getByRole('alert')).toHaveText('名称已被保留');
    else await expect(dialog).not.toBeVisible();
  }
  const result = await new AxeBuilder({ page }).analyze();
  expect(result.violations.filter((v) => ['critical', 'serious'].includes(v.impact ?? ''))).toEqual([]);
});
