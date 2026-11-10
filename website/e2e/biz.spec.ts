import { settleVisuals } from '../../apps/yue-playground/e2e/settle';
import { expect, test } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import scenarios from '../../packages/yue/biz/docs/scenarios.json' with { type: 'json' };

// 业务选择器的文档场景：每个场景实际打开选择器（或展开授权树）并选中一项，再跑 axe（critical / serious = 0）。
const labels: Record<string, string> = { ApiSelect: '客户', DictSelect: '组织类型', UserSelect: '成员', RoleSelect: '角色', IconPicker: '菜单图标' };
for (const scenario of scenarios) test(`${scenario.component}/${scenario.state} 业务选择器文档场景`, async ({ page }) => {
  await page.goto(`yue-demo/index.html?component=${scenario.component}&state=${scenario.state}&theme=${scenario.theme}`);
  const value = page.getByTestId('biz-demo-value');
  if (scenario.component === 'AuthorizationTree') {
    const tree = page.getByRole('tree', { name: '角色权限' });
    await tree.getByRole('treeitem', { name: '写入' }).click();
    await expect(tree.getByRole('treeitem', { name: '客户' })).toHaveAttribute('aria-checked', 'mixed');
    await expect(value).toContainText('qian.sample.crm:customer:write');
  } else if (scenario.component === 'OrganizeSelect') {
    await page.getByRole('button', { name: /^部门/ }).click();
    await page.getByRole('tree', { name: '部门' }).getByRole('treeitem', { name: '总部' }).click();
    if (scenario.state === 'Multiple') await expect(value).toHaveText('0193a000-0000-7000-8000-000000000001');
    else await expect(page.getByRole('button', { name: '部门 总部' })).toBeVisible();
  } else {
    if (scenario.state === 'Error') {
      await expect(page.getByRole('alert')).toContainText('加载失败');
      await page.getByRole('button', { name: '重试' }).click();
    }
    await page.getByRole('combobox', { name: labels[scenario.component], exact: true }).click();
    const option = page.getByRole('option').first();
    await expect(option).toBeVisible();
    await option.click();
    await expect(value).not.toHaveText('');
  }
  await settleVisuals(page);
  const result = await new AxeBuilder({ page }).analyze();
  expect(result.violations.filter((v) => ['critical', 'serious'].includes(v.impact ?? ''))).toEqual([]);
});
