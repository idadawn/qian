import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';

// Base UI 验证门的页面级证据（docs/prd/specs/yue.md §4.4）：每个原语打开后的整页 axe 扫描 + 键盘主路径。
// 组件级的细粒度键盘用例在 packages/yue/*/src/**/*.browser.test.tsx。

// 验证门发现（ADR-013 状态历史）：Combobox 的输入框在弹层外时，Base UI 1.8 把弹层的焦点管理设为 modal，
// 打开期间页面其余部分被标 aria-hidden（不是 inert），axe 的 aria-hidden-focus 把这记为 serious；弹层在焦点移出时即关闭，
// 键盘用户不会停留在被隐藏的内容里。该规则只在 Combobox 打开态豁免，其它状态与规则一律 critical / serious = 0。
async function expectNoBlocking(page: Page, step: string, disableRules: string[] = []) {
  const results = await new AxeBuilder({ page }).disableRules(disableRules).analyze();
  const blocking = results.violations.filter((v) => v.impact === 'critical' || v.impact === 'serious');
  expect(blocking.map((v) => `${v.impact} ${v.id}: ${v.help} → ${v.nodes.map((n) => n.target.join(' ')).join('; ')}`), step).toEqual([]);
}

test.beforeEach(async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('yue · Base UI 验证门');
});

test('整页（关闭态）无 critical / serious', async ({ page }) => {
  await expectNoBlocking(page, '关闭态');
});

test('Menu：键盘打开、进子菜单、选中', async ({ page }) => {
  await page.getByRole('button', { name: '操作' }).focus();
  await page.keyboard.press('Enter');
  const menu = page.getByRole('menu').first();
  await expect(menu).toBeVisible();
  await expect(menu.getByRole('menuitem').first()).toBeFocused();
  await expectNoBlocking(page, 'Menu 打开');
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('ArrowRight');
  await expect(page.getByRole('menu')).toHaveCount(2);
  await expect(page.getByRole('menuitem', { name: '导出' })).toBeFocused();
  await expectNoBlocking(page, 'Menu 子菜单');
  await page.keyboard.press('Enter');
  await expect(page.getByTestId('gate-last-action')).toHaveText('最近动作：导出');
  await expect(page.getByRole('menu')).toHaveCount(0);
  await expect(page.getByRole('button', { name: '操作' })).toBeFocused();
});

test('Select：键盘打开、选中、回写', async ({ page }) => {
  const trigger = page.getByTestId('yue-select-trigger');
  await trigger.focus();
  await page.keyboard.press('ArrowDown');
  await expect(page.getByRole('listbox')).toBeVisible();
  await expectNoBlocking(page, 'Select 打开');
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('Enter');
  await expect(page.getByRole('listbox')).toHaveCount(0);
  await expect(page.getByTestId('gate-select-value')).not.toHaveText('当前值：无');
  await expect(trigger).toBeFocused();
});

test('Combobox：输入过滤、选中、空态', async ({ page }) => {
  const input = page.getByRole('combobox', { name: '水果筛选' });
  await input.fill('樱');
  await expect(page.getByRole('option')).toHaveText(['樱桃']);
  await expectNoBlocking(page, 'Combobox 打开', ['aria-hidden-focus']);
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('Enter');
  await expect(input).toHaveValue('樱桃');
  await input.fill('zzz');
  await expect(page.getByText('没有匹配项')).toBeVisible();
  await page.keyboard.press('Escape');
});

test('Tabs：方向键漫游并激活，RTL 反向', async ({ page }) => {
  await page.getByRole('tab', { name: '基本' }).focus();
  await page.keyboard.press('ArrowRight');
  await expect(page.getByRole('tab', { name: '高级' })).toBeFocused();
  await expect(page.getByRole('tab', { name: '高级' })).toHaveAttribute('aria-selected', 'true');
  await expect(page.getByRole('tabpanel')).toHaveText('高级设置内容');
  await page.getByTestId('gate-rtl').check();
  await expect(page.locator('main')).toHaveAttribute('dir', 'rtl');
  await page.getByRole('tab', { name: '高级' }).focus();
  await page.keyboard.press('ArrowRight');
  await expect(page.getByRole('tab', { name: '基本' })).toBeFocused();
  await expectNoBlocking(page, 'Tabs RTL');
});

test('Tooltip：Tab 聚焦即显示并关联 aria-describedby', async ({ page }) => {
  const trigger = page.getByRole('button', { name: '保存' });
  await page.getByRole('tabpanel').focus();
  await page.keyboard.press('Tab');
  await expect(trigger).toBeFocused();
  const tooltip = page.getByRole('tooltip');
  await expect(tooltip).toBeVisible();
  await expect(trigger).toHaveAttribute('aria-describedby', await tooltip.getAttribute('id') ?? '');
  await expectNoBlocking(page, 'Tooltip 打开');
  await page.keyboard.press('Escape');
  await expect(tooltip).toHaveCount(0);
});

test('Tree：方向键展开进入、Enter 选中', async ({ page }) => {
  await page.getByRole('treeitem', { name: '总部' }).focus();
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('ArrowRight');
  await expect(page.getByRole('treeitem', { name: '研发部' })).toHaveAttribute('aria-expanded', 'true');
  await page.keyboard.press('ArrowRight');
  await expect(page.getByRole('treeitem', { name: '前端组' })).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(page.getByTestId('gate-tree-selected')).toHaveText('选中：fe');
  await expectNoBlocking(page, 'Tree 展开');
});
