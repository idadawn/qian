import { test, expect, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

type Frame = { theme: string; preference: string; brand: string; dark: boolean; scheme: string };
async function firstFrame(page: Page, stored: string | null, blocked = false): Promise<void> {
  await page.addInitScript(({ stored, blocked }) => {
    if (blocked) Object.defineProperty(window, 'localStorage', { get() { throw new DOMException('blocked', 'SecurityError'); } });
    else { window.localStorage.clear(); if (stored !== null) window.localStorage.setItem('yue-theme', stored); }
    const state = window as typeof window & { firstThemeFrame?: Frame };
    requestAnimationFrame(() => {
      const root = document.documentElement;
      state.firstThemeFrame = { theme: root.dataset.theme ?? '', preference: root.dataset.themePreference ?? '', brand: root.dataset.brand ?? '', dark: root.classList.contains('dark'), scheme: root.style.colorScheme };
    });
  }, { stored, blocked });
}
async function frame(page: Page): Promise<Frame> {
  return page.evaluate(() => { const root = document.documentElement; return { theme: root.dataset.theme ?? '', preference: root.dataset.themePreference ?? '', brand: root.dataset.brand ?? '', dark: root.classList.contains('dark'), scheme: root.style.colorScheme }; });
}

test('首帧读取偏好，明暗/品牌同步切换，系统变化不切换品牌', async ({ page }) => {
  await page.emulateMedia({ colorScheme: 'light' }); await firstFrame(page, 'dark');
  await page.goto('/tokens.html'); await expect(page.getByTestId('yue-theme-select')).toBeVisible();
  await expect.poll(() => page.evaluate(() => (window as typeof window & { firstThemeFrame?: Frame }).firstThemeFrame)).toBeTruthy();
  const initial = await page.evaluate(() => (window as typeof window & { firstThemeFrame: Frame }).firstThemeFrame);
  expect(initial).toEqual({ theme: 'dark', preference: 'dark', brand: 'default', dark: true, scheme: 'dark' });
  expect(JSON.stringify(initial, null, 2)).toMatchSnapshot('first-frame-dark.txt');
  const primary = page.getByTestId('yue-token-primary');
  const blue = await primary.evaluate(element => getComputedStyle(element).backgroundColor);
  await page.getByTestId('yue-brand-select').selectOption('teal');
  expect(await primary.evaluate(element => getComputedStyle(element).backgroundColor)).not.toBe(blue);
  expect(JSON.stringify(await frame(page), null, 2)).toMatchSnapshot('dark-teal.txt');
  await page.getByTestId('yue-theme-select').selectOption('light');
  expect(JSON.stringify(await frame(page), null, 2)).toMatchSnapshot('light-teal.txt');
  await page.getByTestId('yue-theme-select').selectOption('system'); await page.emulateMedia({ colorScheme: 'dark' });
  await expect.poll(async () => (await frame(page)).theme).toBe('dark');
  expect((await frame(page)).brand).toBe('teal');
});

test('拒绝存储时首帧仍按系统渲染，服务端品牌不被覆盖', async ({ page }) => {
  await page.emulateMedia({ colorScheme: 'dark' }); await firstFrame(page, null, true);
  await page.route('**/tokens.html', async route => { const response = await route.fetch(); const body = (await response.text()).replace('data-brand="default"', 'data-brand="teal"'); await route.fulfill({ response, body }); });
  await page.goto('/tokens.html'); await expect(page.getByTestId('yue-theme-select')).toBeVisible();
  await expect.poll(() => page.evaluate(() => (window as typeof window & { firstThemeFrame?: Frame }).firstThemeFrame)).toBeTruthy();
  const initial = await page.evaluate(() => (window as typeof window & { firstThemeFrame: Frame }).firstThemeFrame);
  expect(initial).toEqual({ theme: 'dark', preference: 'system', brand: 'teal', dark: true, scheme: 'dark' });
  expect(JSON.stringify(initial, null, 2)).toMatchSnapshot('first-frame-system-teal.txt');
  await page.getByTestId('yue-theme-select').selectOption('light'); expect((await frame(page)).theme).toBe('light');
});

for (const mode of ['light', 'dark'] as const) test(`令牌页 ${mode} 状态无严重无障碍问题`, async ({ page }) => {
  await firstFrame(page, mode); await page.goto('/tokens.html'); await expect(page.getByTestId('yue-token-lab')).toBeVisible();
  const result = await new AxeBuilder({ page }).analyze();
  expect(result.violations.filter(issue => issue.impact === 'critical' || issue.impact === 'serious')).toEqual([]);
});

for (const mode of ['light', 'dark'] as const) test(`浏览器 ${mode} 实际合成后的输入边界与系统字体契约`, async ({ page }) => {
  await firstFrame(page, mode); await page.goto('/tokens.html');
  await expect(page.getByTestId('yue-theme-select')).toBeVisible();
  const result = await page.evaluate(() => {
    const root = getComputedStyle(document.documentElement);
    const body = getComputedStyle(document.body);
    const input = getComputedStyle(document.querySelector('#theme')!).borderColor;
    const canvas = document.createElement('canvas'); canvas.width = canvas.height = 1;
    const context = canvas.getContext('2d')!;
    const luminance = (rgb: Uint8ClampedArray): number => {
      const linear = [...rgb].slice(0, 3).map(channel => { const value = channel / 255; return value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4; });
      return linear[0]! * 0.2126 + linear[1]! * 0.7152 + linear[2]! * 0.0722;
    };
    const ratios = ['background', 'card', 'popover', 'muted', 'sidebar'].map(name => {
      context.clearRect(0, 0, 1, 1);
      context.fillStyle = root.getPropertyValue(`--${name}`); context.fillRect(0, 0, 1, 1);
      const back = luminance(context.getImageData(0, 0, 1, 1).data);
      context.fillStyle = input; context.fillRect(0, 0, 1, 1);
      const front = luminance(context.getImageData(0, 0, 1, 1).data);
      return { name, ratio: (Math.max(front, back) + 0.05) / (Math.min(front, back) + 0.05) };
    });
    return { font: body.fontFamily, fontSize: body.fontSize, ratios };
  });
  expect(result.font).toContain('ui-sans-serif'); expect(result.font).toContain('PingFang SC'); expect(result.fontSize).toBe('14px');
  for (const { name, ratio } of result.ratios) expect(ratio, `${mode}/${name}`).toBeGreaterThanOrEqual(3);
});
