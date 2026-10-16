import { writeFileSync } from 'node:fs';
import { stripVTControlCharacters } from 'node:util';
import { test, expect, type Page } from '@playwright/test';
import manifest from '../../../packages/yue/core/src/manifest-catalog.json' with { type: 'json' };
import coreScenarios from '../../../packages/yue/core/docs/scenarios.json' with { type: 'json' };
import dataScenarios from '../../../packages/yue/data/docs/scenarios.json' with { type: 'json' };
import formScenarios from '../../../packages/yue/form/docs/scenarios.json' with { type: 'json' };

const names = Object.keys(manifest);
const openings: Record<string, string> = { Dialog: '打开对话框', AlertDialog: '打开确认框', Sheet: '打开侧栏', Drawer: '打开抽屉', Popover: '打开浮层', Menu: '打开菜单', DropdownMenu: '打开菜单', Toast: '显示消息', Notification: '显示消息', Accordion: '如何使用' };
async function openComponent(page: Page, name: string, state: string) {
  if (state === 'Disabled' || state === 'Error') return;
  if (state === 'Hover') { await page.getByRole('button').first().hover(); return; }
  if (openings[name]) await page.getByRole('button', { name: openings[name], exact: true }).click();
  if (name === 'Select') await page.getByRole('combobox').click();
  if (name === 'Combobox') await page.getByTestId('yue-combobox-trigger').click();
  if (name === 'ContextMenu') await page.getByTestId('yue-context-menu-trigger').press('Shift+F10');
  if (name === 'Tooltip') { await page.getByRole('button', { name: '查看提示' }).focus(); await expect(page.getByRole('tooltip')).toBeVisible(); }
  if (name === 'HoverCard') { await page.getByRole('link', { name: '查看用户' }).hover(); await expect(page.getByText('示例用户', { exact: true })).toBeVisible(); }
  if (name === 'Toast' || name === 'Notification') await expect(page.getByText(name === 'Toast' ? '保存成功' : '任务已完成', { exact: true })).toBeVisible();
}
const statesFor = (component: string, scenarios: { component: string; state: string }[]) => [...new Set(['Default', ...scenarios.filter(s => s.component === component && s.state !== 'Dark').map(s => s.state)])];
for (const name of names) for (const state of statesFor(name, coreScenarios)) for (const theme of ['light', 'dark'] as const) {
  test(`core ${name} ${state} ${theme}`, async ({ page }) => {
    await page.emulateMedia({ colorScheme: theme });
    await page.goto(`/core.html?${new URLSearchParams({ component: name, state, theme })}`);
    await openComponent(page, name, state);
    await expect(page.locator(`[data-slot="${manifest[name as keyof typeof manifest].slots[0]}"]`).first()).toBeAttached();
    await expect(page.locator('[data-starting-style],[data-ending-style]')).toHaveCount(0);
    await expect(page).toHaveScreenshot(`core-${name}-${state}-${theme}.png`, { fullPage: true });
  });
}
for (const name of ['DataTable', 'SchemaForm']) for (const state of statesFor(name, name === 'DataTable' ? dataScenarios : formScenarios)) for (const theme of ['light', 'dark'] as const) {
  test(`${name} ${state} ${theme}`, async ({ page }) => {
    await page.emulateMedia({ colorScheme: theme });
    await page.goto(`/${name === 'DataTable' ? 'data' : 'form'}.html?${new URLSearchParams({ state, theme })}`);
    if (name === 'DataTable') await expect(page.getByText(state === 'Loading' ? '加载中' : state === 'Empty' ? '暂无数据' : state === 'Error' ? '加载失败' : '共 24 条', { exact: true })).toBeVisible();
    else if (state !== 'Readonly') {
      await page.getByRole('button', { name: '新建客户', exact: true }).click();
      if (state === 'Error') await page.getByRole('button', { name: '保存', exact: true }).click();
      await expect(page.getByRole('dialog', { name: '新建客户' })).toBeVisible();
    }
    await expect(page.locator('[data-starting-style],[data-ending-style]')).toHaveCount(0);
    await expect(page).toHaveScreenshot(`${name}-${state}-${theme}.png`, { fullPage: true });
  });
}
test('基线覆盖全部 51 个 core 入口，状态清单无遗漏', () => {
  expect(names).toHaveLength(51);
  for (const name of names) expect(coreScenarios.some(s => s.component === name && s.state === 'Default')).toBe(true);
});

for (const [name, slot] of [['Dialog', 'dialog'], ['AlertDialog', 'alert-dialog'], ['Sheet', 'sheet']] as const) for (const theme of ['light', 'dark'] as const) {
  test(`像素门禁反例 ${name} ${theme} 隐藏关闭 X 必须失败`, async ({ page }, testInfo) => {
    // 更新模式只生成真实界面，不允许把故意破坏后的界面写成基线。
    test.skip(testInfo.config.updateSnapshots !== 'none', '强制刷新时不执行破坏性反例。');
    await page.emulateMedia({ colorScheme: theme });
    await page.goto(`/core.html?${new URLSearchParams({ component: name, theme })}`);
    await openComponent(page, name, 'Default');
    const screenshot = `core-${name}-Default-${theme}.png`;
    await expect(page).toHaveScreenshot(screenshot, { fullPage: true });
    await page.getByTestId(`yue-${slot}-close-button`).evaluate(button => { button.style.visibility = 'hidden'; });
    const viewport = page.viewportSize()!;
    const oldBudget = Math.floor(viewport.width * viewport.height * 0.001);
    // 同一缺陷在旧比例预算内被接受，随后必须被实际门禁的绝对上限拒绝。
    await expect(page).toHaveScreenshot(screenshot, { fullPage: true, maxDiffPixels: oldBudget, maxDiffPixelRatio: 0.001 });
    let differentPixels: number | undefined;
    try {
      await expect(page).toHaveScreenshot(screenshot, { fullPage: true, timeout: 1000 });
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      const match = stripVTControlCharacters(message).match(/(\d+) pixels \([^)]*\) are different/);
      if (!match) throw error;
      differentPixels = Number(match[1]);
    }
    expect(differentPixels, '隐藏关闭图标未被实际像素门禁拒绝。').toBeGreaterThan(0);
    expect(differentPixels).toBeLessThanOrEqual(oldBudget);
    writeFileSync(testInfo.outputPath('gate-proof.json'), JSON.stringify({ component: name, theme, differentPixels, oldBudget, strictBudget: 0 }) + '\n');
  });
}
