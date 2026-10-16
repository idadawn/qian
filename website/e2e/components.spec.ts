import { settleVisuals } from '../../apps/yue-playground/e2e/settle';
import AxeBuilder from '@axe-core/playwright';
import { test, expect } from '@playwright/test';
import scenarios from '../../packages/yue/core/docs/scenarios.json' with { type: 'json' };
import { exercise } from './interactions';
for (const scenario of scenarios) {
  test(`${scenario.id} 真实 Rspress 交互与无障碍`, async ({ page }) => {
    const errors: string[] = [];
    page.on('pageerror', e => errors.push(e.message));
    await page.emulateMedia({ colorScheme: scenario.theme === 'dark' ? 'dark' : 'light' });
    await page.goto(`components/yue/${scenario.component === 'Tokens' ? 'tokens' : `core/${scenario.component.toLowerCase()}`}.html`);
    if (scenario.theme === 'dark') await expect(page.locator('html')).toHaveClass(/dark/);
    else await expect(page.locator('html')).not.toHaveClass(/dark/);
    const frame = page.frameLocator(`iframe[title="${scenario.component} 交互预览"]`);
    await frame.getByLabel('示例状态').selectOption(scenario.state);
    await frame.getByLabel('示例主题').selectOption(scenario.theme);
    const checkA11y = async () => {
      await settleVisuals(page);
      const violations = (await new AxeBuilder({ page }).analyze()).violations.filter(v => ['critical', 'serious'].includes(v.impact ?? ''));
      expect(violations.map(v => `${v.id}: ${v.nodes.map(n => n.target).join(', ')}`)).toEqual([]);
    };
    await exercise(scenario.component, frame, scenario.state, checkA11y);
    await checkA11y();
    expect(errors).toEqual([]);
  });
}
test('代表性组件正式页面交互 smoke', async ({ page }, testInfo) => {
  for (const name of ['Button', 'Input', 'Select', 'Dialog', 'Tabs', 'Toast']) {
    await page.goto(`components/yue/core/${name.toLowerCase()}.html`);
    await exercise(name, page.frameLocator(`iframe[title="${name} 交互预览"]`));
  }
  await page.goto('components/yue/core/dialog.html');
  await page.frameLocator('iframe').getByRole('button', { name: '打开对话框' }).click();
  await page.screenshot({ path: testInfo.outputPath('rspress-dialog.png') });
});
