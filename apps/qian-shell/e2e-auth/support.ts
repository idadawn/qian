import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';

// 真实流程 E2E 的共用件：种子账号、宿主地址、CSP 违规收集、动画结束后的 axe 扫描与登录表单。
export const root = resolve(import.meta.dirname, '../../..');
export interface SeedPerson { key: string; displayName: string; username: string; password: string }
export const seed = JSON.parse(readFileSync(resolve(root, 'tools/Qian.Dev.Shell/seed/accounts.json'), 'utf8')) as { persons: SeedPerson[]; tenants: { code: string; name: string }[] };
export const person = (key: string) => seed.persons.find(item => item.key === key)!;
export const identity = (process.env.QIAN_DEV_SHELL_API_URL ?? 'http://127.0.0.1:5090').replace(/\/$/, '');
export const state = process.env.QIAN_DEV_STATE ?? resolve(root, '.qian-dev/shell');
export const loginPage = new RegExp(`^${identity.replaceAll('.', '\\.')}/login\\?ReturnUrl=`);
// Base UI 的 Tab 哨兵立即转移焦点；与靶场门禁一致，只排除这一类内部哨兵。
const focusGuards = '[data-base-ui-focus-guard]';

/** 每个文档都收集 securitypolicyviolation，用例结束时必须为零（外壳构建产物带 meta CSP；身份服务源上的页面没有这条 meta，照样收集）。 */
export function collectCspViolations() {
  const violations: string[] = [];
  test.beforeEach(async ({ page }) => {
    violations.length = 0;
    await page.exposeFunction('qianCspViolation', (detail: string) => { violations.push(detail); });
    await page.addInitScript(() => document.addEventListener('securitypolicyviolation', event => {
      (window as unknown as { qianCspViolation(detail: string): void }).qianCspViolation(`${location.origin} ${event.violatedDirective} ${event.blockedURI} ${event.sourceFile}:${event.lineNumber}`);
    }));
  });
  test.afterEach(() => { expect(violations, 'CSP 违规').toEqual([]); });
}

/** 开场 / 退场属性清除后 opacity 过渡才开始：等真实动画结束再扫描，否则慢机器上会在半透明过渡中测出 color-contrast。 */
export async function settled(page: Page) {
  await expect.poll(() => page.evaluate(() => document.querySelector('[data-starting-style], [data-ending-style]') === null)).toBe(true);
  await page.evaluate(async () => {
    let stableFrames = 0;
    while (stableFrames < 2) {
      await new Promise<void>(resolve => requestAnimationFrame(() => resolve()));
      const finite = document.getAnimations().filter(animation => animation.playState !== 'finished' && Number.isFinite(Number(animation.effect?.getComputedTiming().endTime)));
      if (finite.length) { await Promise.all(finite.map(animation => animation.finished.catch(() => undefined))); stableFrames = 0; } else stableFrames++;
    }
  });
}

export async function accessible(page: Page, step: string) {
  await settled(page);
  const results = await new AxeBuilder({ page }).exclude(focusGuards).analyze();
  expect(results.violations.filter(violation => violation.impact === 'critical' || violation.impact === 'serious').map(violation => `${violation.id}: ${violation.nodes.map(node => node.target.join(' ')).join('; ')}`), step).toEqual([]);
}

/** 身份服务同源登录页：账号框标作「手机号或邮箱」，用户名同样可以登录；缺省用种子里的演示用户。密码框旁有「显示密码」按钮，按标签精确匹配。 */
export async function fillLogin(page: Page, account: { username: string; password: string } = person('owner')) {
  await page.getByLabel('手机号或邮箱').fill(account.username);
  await page.getByLabel('密码', { exact: true }).fill(account.password);
  await expect(page.getByLabel('密码', { exact: true })).toHaveAttribute('type', 'password');
  await page.getByRole('button', { name: '登录', exact: true }).click();
}

/** 租户会话的首页只有欢迎区（S1-YUE-019 按原型）：会话、有效权限与可用页面卡片在「会话信息」页，经账号菜单进入（站内路由，不重拉 bootstrap）。 */
export async function openSession(page: Page) {
  await page.getByRole('button', { name: '用户菜单' }).click();
  await page.getByRole('menuitem', { name: '会话信息' }).click();
  await expect(page.getByRole('heading', { name: '会话信息', exact: true })).toBeVisible();
}
