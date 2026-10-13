import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';
import catalog from '../../../packages/yue/core/src/manifest-catalog.json' with { type: 'json' };
const opening: Record<string, string> = { Dialog: '打开对话框', AlertDialog: '打开确认框', Sheet: '打开侧栏', Drawer: '打开抽屉', Popover: '打开浮层', Menu: '打开菜单', DropdownMenu: '打开菜单', Toast: '显示消息', Notification: '显示消息', Accordion: '如何使用' };
for (const name of Object.keys(catalog)) {
  test(`${name} 真实构建页面与弹出态通过 axe`, async ({ page }) => {
    await page.goto(`/core.html?component=${name}`);
    await expect(page.getByRole('heading', { name, exact: true })).toBeVisible();
    if (opening[name]) await page.getByRole('button', { name: opening[name], exact: true }).click();
    if (name === 'Select') await page.getByRole('combobox').click();
    if (name === 'Combobox') await page.getByTestId('yue-combobox-trigger').click();
    if (name === 'Tooltip') await page.getByRole('button', { name: '查看提示' }).focus();
    if (name === 'HoverCard') await page.getByRole('link', { name: '查看用户' }).hover();
    if (name === 'ContextMenu') { await page.getByTestId('yue-context-menu-trigger').focus(); await page.keyboard.press('Shift+F10'); }
    if (['Dialog', 'Sheet', 'Drawer', 'Popover'].includes(name)) await expect(page.getByRole('dialog')).toBeVisible();
    if (name === 'AlertDialog') await expect(page.getByRole('alertdialog')).toBeVisible();
    if (['Menu', 'DropdownMenu', 'ContextMenu'].includes(name)) await expect(page.getByRole('menu')).toBeVisible();
    if (['Select', 'Combobox'].includes(name)) await expect(page.getByRole('listbox')).toBeVisible();
    if (name === 'Tooltip') await expect(page.getByRole('tooltip')).toBeVisible();
    if (name === 'HoverCard') await expect(page.getByText('示例用户', { exact: true })).toBeVisible();
    if (name === 'Toast') await expect(page.getByText('保存成功', { exact: true })).toBeVisible();
    if (name === 'Notification') await expect(page.getByText('任务已完成', { exact: true })).toBeVisible();
    for (const theme of ['light', 'dark']) {
      await page.evaluate(value => { document.documentElement.dataset.theme = value; }, theme);
      const violations = (await new AxeBuilder({ page }).analyze()).violations.filter(item => ['critical', 'serious'].includes(item.impact ?? ''));
      expect(violations.map(item => `${item.id}: ${item.nodes.map(node => node.target).join(',')}`), `${name} ${theme}`).toEqual([]);
    }
  });
}
