import { expect, test } from '@playwright/test';

test('静态 Storybook 的文档渲染器、真实属性表与暗色用例可用', async ({ page }, testInfo) => {
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.goto('/?path=/docs/core-button--docs');
  const frame = page.frameLocator('#storybook-preview-iframe');
  await expect(frame.getByRole('heading', { name: 'Button', exact: true })).toBeVisible();
  await frame.getByRole('tab', { name: 'Button', exact: true }).click();
  await expect(frame.getByRole('cell', { name: 'variant', exact: true })).toBeVisible();
  await expect(frame.getByRole('cell', { name: 'loading', exact: true })).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath('storybook-docs.png'), fullPage: false });
  await page.goto('/?path=/story/core-button--dark');
  await expect(frame.getByRole('button', { name: '保存更改', exact: true })).toBeVisible();
  await expect(frame.locator('html')).toHaveAttribute('data-theme', 'dark');
  expect(errors).toEqual([]);
});
