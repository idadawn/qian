import { settleVisuals } from '../../apps/yue-playground/e2e/settle';
import { expect, test } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import scenarios from '../../packages/yue/data/docs/scenarios.json' with { type: 'json' };

for (const scenario of scenarios) test(`${scenario.component}/${scenario.state} 数据组件文档场景`, async ({ page }) => {
  await page.goto(`yue-demo/index.html?component=${scenario.component}&state=${scenario.state}&theme=${scenario.theme}`);
  if (scenario.component === 'DataTable') {
    if (scenario.state === 'Loading') await expect(page.getByTestId('yue-data-table-loading')).toBeVisible();
    else if (scenario.state === 'Empty') await expect(page.getByTestId('yue-data-table-empty')).toBeVisible();
    else if (scenario.state === 'Error') await expect(page.getByTestId('yue-data-table-error')).toBeVisible();
    else if (scenario.state === 'Tools') {
      // 工具行右端的刷新按钮重新取数；「显示列」之后接 toolbarEnd，comfortable 行距让单元格上下各 14px。
      await expect(page.getByText('共 24 条')).toBeVisible();
      await page.getByRole('button', { name: '刷新' }).click();
      await expect(page.getByRole('cell', { name: '客户 01', exact: true })).toBeVisible();
      await expect(page.getByRole('cell', { name: '客户 01', exact: true })).toHaveCSS('padding-top', '14px');
      await page.getByText('显示列', { exact: true }).click();
      await page.getByRole('checkbox', { name: '名称', exact: true }).uncheck();
      await expect(page.getByRole('cell', { name: '客户 01', exact: true })).not.toBeVisible();
    }
    else await expect(page.getByText('共 24 条')).toBeVisible();
  } else if (scenario.component === 'TreeSelect') await page.getByRole('button', { name: '组织选择' }).click();
  else if (scenario.component === 'Cascader') { await page.getByRole('button', { name: '层级选择' }).click(); await page.getByRole('treeitem', { name: '总部' }).click(); }
  else if (scenario.component === 'ImagePreview') await page.getByRole('button', { name: '图片预览' }).click();
  else if (scenario.component === 'Upload') {
    await page.getByLabel('上传文件').setInputFiles({ name: 'sample.txt', mimeType: 'text/plain', buffer: Buffer.from('abcdefgh') });
    if (scenario.state === 'Error') { await expect(page.getByRole('alert')).toBeVisible(); await page.getByRole('button', { name: '继续上传' }).click(); }
    await expect(page.getByText('file-demo', { exact: true })).toBeVisible();
  }
  await settleVisuals(page);
  const result = await new AxeBuilder({ page }).analyze();
  expect(result.violations.filter((v) => ['critical', 'serious'].includes(v.impact ?? ''))).toEqual([]);
});
