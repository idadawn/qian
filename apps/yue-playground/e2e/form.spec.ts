import { settleVisuals } from './settle';
import { expect, test } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

test('SchemaForm 经 SDK mock 完成新增、编辑、只读与字段错误，弹层焦点可恢复', async ({ page }) => {
  await page.goto('/form.html');
  await page.getByRole('button', { name: '新建客户', exact: true }).click();
  let dialog = page.getByRole('dialog', { name: '新建客户' });
  await dialog.getByRole('button', { name: '保存', exact: true }).click();
  await expect(dialog.getByRole('textbox', { name: /客户号/ })).toBeFocused();
  await dialog.getByRole('textbox', { name: /客户号/ }).fill('tenant1');
  await dialog.getByRole('textbox', { name: /客户名称/ }).fill('客户一');
  await dialog.getByRole('button', { name: '保存', exact: true }).click();
  await expect(dialog).not.toBeVisible();
  await expect(page.getByRole('status').filter({ hasText: '已保存：客户一' })).toBeVisible();
  await expect(page.getByRole('button', { name: '新建客户', exact: true })).toBeFocused();
  await page.getByRole('button', { name: '编辑客户', exact: true }).click();
  dialog = page.getByRole('dialog', { name: '编辑客户' });
  await expect(dialog.getByRole('textbox', { name: /客户名称/ })).toHaveValue('客户一');
  await dialog.getByRole('textbox', { name: /客户名称/ }).fill('服务端错误');
  await dialog.getByRole('button', { name: '保存', exact: true }).click();
  await expect(dialog.getByRole('alert')).toHaveText('名称已被保留');
  await expect(dialog.getByRole('textbox', { name: /客户名称/ })).toHaveAttribute('aria-invalid', 'true');
  await dialog.getByRole('textbox', { name: /客户名称/ }).fill('客户二');
  await dialog.getByRole('button', { name: '保存', exact: true }).click();
  await expect(dialog).not.toBeVisible();
  await page.getByRole('button', { name: '只读详情', exact: true }).click();
  dialog = page.getByRole('dialog', { name: '只读详情' });
  await expect(dialog.getByText('客户二', { exact: true })).toBeVisible();
  await expect(dialog.getByRole('textbox')).toHaveCount(0);
  await settleVisuals(page);
  const axe = await new AxeBuilder({ page }).analyze();
  expect(axe.violations.filter((v) => ['critical', 'serious'].includes(v.impact ?? ''))).toEqual([]);
  await page.keyboard.press('Escape');
  await expect(dialog).not.toBeVisible();
});

test('Popup 与 Confirm 复用 core 原语，键盘可操作且没有嵌套交互控件', async ({ page }) => {
  await page.goto('/form.html');
  await page.getByRole('button', { name: '浮层详情' }).click();
  await expect(page.getByRole('dialog', { name: '只读详情' })).toBeVisible();
  await settleVisuals(page);
  let result = await new AxeBuilder({ page }).analyze();
  expect(result.violations.filter((v) => ['critical', 'serious'].includes(v.impact ?? ''))).toEqual([]);
  await page.keyboard.press('Escape');
  await page.getByRole('button', { name: '确认操作', exact: true }).click();
  await expect(page.getByRole('alertdialog', { name: '确认操作' })).toBeVisible();
  await settleVisuals(page);
  result = await new AxeBuilder({ page }).analyze();
  expect(result.violations.filter((v) => ['critical', 'serious'].includes(v.impact ?? ''))).toEqual([]);
  await page.getByRole('button', { name: '取消', exact: true }).click();
  await expect(page.getByRole('alertdialog')).not.toBeVisible();
});


test('弹层表单取消与保存共用页脚，取消不提交且恢复触发器焦点', async ({ page }) => {
  await page.goto('/form.html');
  const trigger = page.getByRole('button', { name: '新建客户', exact: true });
  await trigger.click();
  const dialog = page.getByRole('dialog', { name: '新建客户' });
  const footer = dialog.locator('[data-slot="schema-form-actions"]');
  await expect(footer.getByRole('button')).toHaveText(['取消', '保存']);
  await expect(dialog.getByRole('button', { name: '关闭', exact: true })).toHaveCount(0);
  await footer.getByRole('button', { name: '取消' }).click();
  await expect(dialog).not.toBeVisible();
  await expect(trigger).toBeFocused();
  await expect(page.getByRole('status').filter({ hasText: '已保存：' })).toHaveCount(0);
});
