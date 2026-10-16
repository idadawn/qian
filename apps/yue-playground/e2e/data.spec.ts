import { expect, test } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

test('DataTable 对 SDK mock 完成服务端分页、排序、筛选、列显隐与行选择', async ({ page }) => {
  await page.goto('/data.html');
  await expect(page.getByText('共 24 条')).toBeVisible();
  await expect(page.getByRole('cell', { name: '客户 01', exact: true })).toBeVisible();
  await page.getByRole('button', { name: '下一页' }).click();
  await expect(page.getByRole('cell', { name: '客户 06', exact: true })).toBeVisible();
  await page.getByRole('button', { name: '名称', exact: true }).click();
  await expect(page.getByText('第 1 页', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: '名称 ↑', exact: true }).click();
  await expect(page.getByRole('cell', { name: '客户 24', exact: true })).toBeVisible();
  await page.getByRole('textbox', { name: '筛选 名称' }).fill('23');
  await expect(page.getByText('共 1 条')).toBeVisible();
  await expect(page.getByRole('cell', { name: '客户 23', exact: true })).toBeVisible();
  await page.getByRole('checkbox', { name: '选择第 1 行' }).check();
  await expect(page.getByRole('checkbox', { name: '选择第 1 行' })).toBeChecked();
  await page.getByText('显示列', { exact: true }).click();
  await page.getByRole('checkbox', { name: '名称', exact: true }).uncheck();
  await expect(page.getByRole('cell', { name: '客户 23', exact: true })).not.toBeVisible();
  const result = await new AxeBuilder({ page }).analyze();
  expect(result.violations.filter((v) => ['critical', 'serious'].includes(v.impact ?? ''))).toEqual([]);
});

test('Upload 从真实文件输入完成分片与进度，页面可访问', async ({ page }) => {
  await page.goto('/data.html?kind=Upload');
  await page.getByLabel('上传文件').setInputFiles({ name: 'sample.txt', mimeType: 'text/plain', buffer: Buffer.from('abcdefgh') });
  await expect(page.getByText('file-demo', { exact: true })).toBeVisible();
  await expect(page.getByRole('progressbar')).toHaveAttribute('value', '8');
  const result = await new AxeBuilder({ page }).analyze();
  expect(result.violations.filter((v) => ['critical', 'serious'].includes(v.impact ?? ''))).toEqual([]);
});
