import { settleVisuals } from './settle';
import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';

// Base UI 验证门的页面级证据（docs/prd/specs/yue.md §4.4）：每个原语打开后的整页 axe 扫描 + 键盘主路径。
// 组件级的细粒度键盘用例在 packages/yue/*/src/**/*.browser.test.tsx。

// Base UI 的 Tab 哨兵立即转移焦点；只排除这四类内部哨兵，保留 aria-hidden-focus 规则。
const focusGuards = '[data-base-ui-focus-guard][aria-hidden="true"][data-type]';
async function expectNoBlocking(page: Page, step: string) {
  await settleVisuals(page);
  const results = await new AxeBuilder({ page }).exclude(focusGuards).analyze();
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
  const trigger = page.getByTestId('yue-combobox-trigger');
  await trigger.click();
  const input = page.getByRole('combobox', { name: '水果筛选' });
  await input.fill('樱');
  await expect(page.getByRole('option')).toHaveText(['樱桃']);
  await expectNoBlocking(page, 'Combobox 打开');
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('Enter');
  await expect(trigger).toContainText('樱桃');
  await trigger.click();
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


test('Select 的 Tab 哨兵不接收持久焦点，焦点不会留在 aria-hidden 内容中', async ({ page }) => {
  const trigger = page.getByTestId('yue-select-trigger');
  for (const key of ['Tab', 'Shift+Tab']) {
    await trigger.focus();
    await page.keyboard.press('ArrowDown');
    await expect(page.getByRole('listbox')).toBeVisible();
    await page.keyboard.press(key);
    await expect.poll(() => page.evaluate(() => document.activeElement?.closest('[aria-hidden="true"]') === null)).toBe(true);
    await expect.poll(() => page.evaluate(() => !!document.activeElement && document.activeElement !== document.body)).toBe(true);
    await page.keyboard.press('Escape');
  }
});

test('哨兵排除不掩盖真实 aria-hidden 可聚焦内容', async ({ page }) => {
  await page.evaluate(() => {
    const hidden = document.createElement('div');
    hidden.setAttribute('aria-hidden', 'true');
    const button = document.createElement('button');
    button.textContent = '故意违规的隐藏按钮';
    hidden.append(button);
    document.body.append(hidden);
  });
  await settleVisuals(page);
  const result = await new AxeBuilder({ page }).exclude(focusGuards).analyze();
  expect(result.violations.some(item => item.id === 'aria-hidden-focus')).toBe(true);
});
