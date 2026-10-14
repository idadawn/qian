import { expect, test } from '@playwright/test';

test('断外网后加载本地编辑器、修改代码并真实运行', async ({ page, baseURL }) => {
  const origin = new URL(baseURL!).origin;
  const external: string[] = [];
  await page.route('**/*', route => {
    if (new URL(route.request().url()).origin !== origin) { external.push(route.request().url()); return route.abort(); }
    return route.continue();
  });
  const errors: string[] = [];
  page.on('pageerror', e => errors.push(e.message));
  await page.goto('components/yue/core/button.html');
  const frame = page.frameLocator('iframe');
  await frame.getByRole('button', { name: '编辑代码' }).click();
  const editor = frame.locator('.monaco-editor textarea');
  await expect(editor).toBeVisible();
  await editor.press('ControlOrMeta+A');
  await editor.pressSequentially("import {useState} from 'react'; export default function Example(){const[n,s]=useState(0); return <button onClick={()=>s(n+1)}>离线 {n}</button>}");
  await frame.getByRole('button', { name: '运行', exact: true }).click();
  await frame.getByRole('button', { name: '离线 0', exact: true }).click();
  await expect(frame.getByRole('button', { name: '离线 1', exact: true })).toBeVisible();
  expect(external).toEqual([]); expect(errors).toEqual([]);
});

test('属性表、继承回调、源码和 Markdown 同步', async ({ page, request }) => {
  for (const [component, props] of Object.entries({ Button: ['variant', 'size', 'loading', 'disabled'], Dialog: ['open', 'defaultOpen', 'onOpenChange'] })) {
    const route = `components/yue/core/${component.toLowerCase()}`;
    await page.goto(`${route}.html`);
    for (const details of await page.locator('details').all()) await details.locator('summary').click();
    for (const prop of props) await expect(page.getByRole('cell', { name: prop, exact: true }).first()).toBeVisible();
    const markdown = await (await request.get(`${route}.md`)).text();
    for (const prop of props) expect(markdown).toContain(prop);
    expect(markdown).toContain("@idadawn/yue-core");
    expect(markdown).not.toContain('<ComponentDemo');
  }
});

test('iframe 无障碍门禁能拒绝真实隐藏焦点缺陷', async ({ page }) => {
  await page.goto('components/yue/core/button.html');
  const body = page.frameLocator('iframe').locator('body');
  await body.evaluate(element => element.insertAdjacentHTML('beforeend', '<div aria-hidden="true"><button>负例：隐藏的可聚焦按钮</button></div>'));
  const { default: AxeBuilder } = await import('@axe-core/playwright');
  const violations = (await new AxeBuilder({ page }).analyze()).violations;
  expect(violations.some(v => v.id === 'aria-hidden-focus')).toBe(true);
});

test('站点主题支持键盘切换，iframe 保留交互状态', async ({ page }) => {
  await page.emulateMedia({ colorScheme: 'light' });
  await page.goto('components/yue/core/button.html');
  const frame = page.frameLocator('iframe');
  await frame.getByRole('button', { name: '保存更改', exact: true }).click();
  await expect(page.getByRole('button', { name: '切换主题', exact: true }).locator('svg:visible')).toHaveCount(1);
  await page.getByRole('button', { name: '切换主题', exact: true }).press('Enter');
  await expect(page.getByRole('button', { name: '切换主题', exact: true }).locator('svg:visible')).toHaveCount(1);
  await expect(page.locator('html')).toHaveClass(/dark/);
  await expect(frame.locator('html')).toHaveAttribute('data-theme', 'dark');
  await expect(frame.getByRole('status')).toHaveText('已保存 1 次');
  await page.reload();
  await expect(page.locator('html')).toHaveClass(/dark/);
  await expect(page.locator('iframe')).toHaveAttribute('src', /theme=dark/);
});

test('桌面快捷键文字在明暗主题均有足够对比度', async ({ page }) => {
  await page.addInitScript(() => { Object.defineProperty(navigator, 'platform', { get: () => 'Linux x86_64' }); });
  const { default: AxeBuilder } = await import('@axe-core/playwright');
  for (const theme of ['light', 'dark'] as const) {
    await page.emulateMedia({ colorScheme: theme });
    await page.goto('components/yue/core/button.html');
    await expect(page.locator('.rp-search-button__hotkey')).toBeVisible();
    const violations = (await new AxeBuilder({ page }).include('.rp-search-button').analyze()).violations;
    expect(violations).toEqual([]);
  }
});
