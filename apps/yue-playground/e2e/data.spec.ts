import { settleVisuals } from './settle';
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
  await settleVisuals(page);
  const result = await new AxeBuilder({ page }).analyze();
  expect(result.violations.filter((v) => ['critical', 'serious'].includes(v.impact ?? ''))).toEqual([]);
});

test('Upload 从真实文件输入完成分片与进度，页面可访问', async ({ page }) => {
  await page.goto('/data.html?kind=Upload');
  await page.getByLabel('上传文件').setInputFiles({ name: 'sample.txt', mimeType: 'text/plain', buffer: Buffer.from('abcdefgh') });
  await expect(page.getByText('file-demo', { exact: true })).toBeVisible();
  await expect(page.getByRole('progressbar')).toHaveAttribute('value', '8');
  await settleVisuals(page);
  const result = await new AxeBuilder({ page }).analyze();
  expect(result.violations.filter((v) => ['critical', 'serious'].includes(v.impact ?? ''))).toEqual([]);
});


test('页长切换与紧凑密度使用同一表格协议，选择态有一致样式', async ({ page }) => {
  await page.goto('/data.html');
  await expect(page.getByTestId('yue-data-table-row')).toHaveCount(5);
  const normal = await page.getByTestId('yue-data-table-row').first().evaluate(row => row.getBoundingClientRect().height);
  await page.getByRole('combobox', { name: '每页条数' }).click();
  await page.getByRole('option', { name: '10', exact: true }).click();
  await expect(page.getByTestId('yue-data-table-row')).toHaveCount(10);
  await expect(page.getByText('第 1 页', { exact: true })).toBeVisible();
  await page.getByRole('checkbox', { name: '选择第 1 行' }).check();
  await expect(page.getByTestId('yue-data-table-row').first()).toHaveAttribute('data-state', 'selected');
  await page.goto('/data.html?state=Compact');
  await expect(page.getByTestId('yue-data-table-row')).toHaveCount(5);
  const compact = await page.getByTestId('yue-data-table-row').first().evaluate(row => row.getBoundingClientRect().height);
  expect(normal).toBe(40); expect(compact).toBe(32);
});


for (const theme of ['light', 'dark']) test(`固定列表头 ${theme} 使用与整行一致的不透明底色`, async ({ page }) => {
  await page.goto(`/data.html?theme=${theme}`);
  const colors = await page.locator('thead').evaluate(header => {
    const fixed = [...header.querySelectorAll('th')].find(cell => getComputedStyle(cell).position === 'sticky')!;
    const background = getComputedStyle(header).backgroundColor;
    const canvas = document.createElement('canvas'); canvas.width = canvas.height = 1;
    const context = canvas.getContext('2d')!; context.fillStyle = getComputedStyle(fixed).backgroundColor; context.fillRect(0, 0, 1, 1);
    return { background, height: header.getBoundingClientRect().height, fixed: getComputedStyle(fixed).backgroundColor, alpha: context.getImageData(0, 0, 1, 1).data[3] };
  });
  expect(colors.fixed).toBe(colors.background); expect(colors.alpha).toBe(255); expect(colors.height).toBe(40);
});
