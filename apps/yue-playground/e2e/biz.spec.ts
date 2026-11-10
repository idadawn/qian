import { settleVisuals } from './settle';
import { expect, test, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import scenarios from '../../../packages/yue/biz/docs/scenarios.json' with { type: 'json' };

// S1-YUE-010 验收 2（靶场，R109）：OrganizeSelect 选部门、UserSelect 选人、RoleSelect 选角色、授权树勾选，
// 数据由 @yue/sdk 的 mock 中间件按 PRD 7.4.3 目录 API 与 PRD 7.5 角色 / 权限点目录返回；全程键盘，axe critical / serious = 0。
const uuid = (n: number) => `0193a000-0000-7000-8000-${String(n).padStart(12, '0')}`;

async function expectNoBlocking(page: Page, where: string) {
  await settleVisuals(page);
  const result = await new AxeBuilder({ page }).analyze();
  expect(result.violations.filter((v) => ['critical', 'serious'].includes(v.impact ?? '')), where).toEqual([]);
}

test('角色授予：全程键盘完成选部门、选人、选角色与授权树勾选，页面可访问', async ({ page }) => {
  await page.goto('/biz.html');
  const organization = page.getByRole('button', { name: /^部门/ });
  await expect(organization).toHaveAccessibleName('部门 请选择');

  // 部门：Tab 到组织选择器，Enter 打开，方向键在组织树里展开并选中「研发中心」。
  await page.keyboard.press('Tab');
  await expect(organization).toBeFocused();
  await page.keyboard.press('Enter');
  const tree = page.getByRole('tree', { name: '部门' });
  await expect(tree.getByRole('treeitem', { name: '总部' })).toBeVisible();
  await expectNoBlocking(page, '组织树打开');
  await tree.getByRole('treeitem', { name: '总部' }).focus();
  await page.keyboard.press('ArrowRight');
  await page.keyboard.press('ArrowDown');
  await expect(tree.getByRole('treeitem', { name: '研发中心' })).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(organization).toHaveAccessibleName('部门 研发中心');
  await expect(organization).toBeFocused();

  // 成员：Tab 到成员选择器，弹层里输入过滤（组织范围含子树），回车选中，Escape 关闭。
  await page.keyboard.press('Tab');
  const users = page.getByRole('combobox', { name: '成员', exact: true });
  await expect(users).toBeFocused();
  await page.keyboard.press('ArrowDown');
  const search = page.getByRole('combobox', { name: '搜索成员' });
  await expect(search).toBeFocused();
  await expect(page.getByRole('option')).toHaveText(['李四lisi', '钱七qianqi · 已锁定', '王五wangwu']);
  await expectNoBlocking(page, '成员选项打开');
  await page.keyboard.type('wang');
  await expect(page.getByRole('option')).toHaveText(['王五wangwu']);
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('Enter');
  await page.keyboard.press('Escape');
  await expect(users).toContainText('王五');
  await expect(users).toBeFocused();

  // 角色：经清空按钮 Tab 到角色选择器，输入过滤后回车选中「销售经理」。
  await page.keyboard.press('Tab');
  await expect(page.getByRole('button', { name: '清空成员' })).toBeFocused();
  await page.keyboard.press('Tab');
  const role = page.getByRole('combobox', { name: '角色', exact: true });
  await expect(role).toBeFocused();
  await page.keyboard.press('ArrowDown');
  await expect(page.getByRole('combobox', { name: '搜索角色' })).toBeFocused();
  await expect(page.getByRole('option')).toHaveText(['租户所有者内置', '租户管理员内置', '开发者内置', '使用者内置', '销售经理sales.manager']);
  await page.keyboard.type('销售');
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('Enter');
  await expect(role).toContainText('销售经理');

  // 授权树：该角色已有客户的查看与写入；Tab 进树，勾选「导出」后客户菜单变为全选；取消「查看」连带取消该菜单的按钮。
  const permissions = page.getByRole('tree', { name: '角色权限' });
  await expect(permissions.getByRole('treeitem', { name: '客户' })).toHaveAttribute('aria-checked', 'mixed');
  await page.keyboard.press('Tab');
  await page.keyboard.press('Tab');
  await expect(permissions.getByRole('treeitem', { name: '客户' })).toBeFocused();
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('ArrowDown');
  await expect(permissions.getByRole('treeitem', { name: '导出 · 高风险' })).toBeFocused();
  await page.keyboard.press('Space');
  await expect(permissions.getByRole('treeitem', { name: '客户' })).toHaveAttribute('aria-checked', 'true');
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('ArrowDown');
  await expect(permissions.getByRole('treeitem', { name: '新建' })).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(permissions.getByRole('treeitem', { name: '成员' })).toHaveAttribute('aria-checked', 'mixed');

  const state = JSON.parse(await page.getByTestId('biz-demo-grant').textContent() ?? '{}') as { organization: string; users: string[]; role: string; grants: string[] };
  expect(state.organization).toBe(uuid(2));
  expect(state.users).toEqual([uuid(203)]);
  expect(state.role).toBe('sales.manager');
  expect([...state.grants].sort()).toEqual(['qian.identity:member:create', 'qian.identity:member:read', 'qian.sample.crm:customer:export', 'qian.sample.crm:customer:read', 'qian.sample.crm:customer:write']);
  await expectNoBlocking(page, '角色授予完成');
});

test('成员加载失败时触发器旁给出可键盘到达的重试，重试后可选人', async ({ page }) => {
  await page.goto('/biz.html?kind=UserSelect&state=Error');
  const alert = page.getByRole('alert');
  await expect(alert).toContainText('加载失败');
  await expectNoBlocking(page, '成员加载失败');
  await alert.getByRole('button', { name: '重试' }).focus();
  await page.keyboard.press('Enter');
  await expect(alert).toHaveCount(0);
  await page.getByRole('combobox', { name: '成员', exact: true }).click();
  await expect(page.getByRole('option')).toHaveCount(5);
});

for (const scenario of scenarios.filter((item) => item.theme === 'dark' || item.state === 'Multiple')) test(`${scenario.component}/${scenario.state} 打开态可访问`, async ({ page }) => {
  await page.goto(`/biz.html?kind=${scenario.component}&state=${scenario.state}&theme=${scenario.theme}`);
  const label = { ApiSelect: '客户', DictSelect: '组织类型', OrganizeSelect: '部门', UserSelect: '成员', RoleSelect: '角色', IconPicker: '菜单图标' }[scenario.component];
  if (scenario.component === 'AuthorizationTree') await expect(page.getByRole('tree', { name: '角色权限' })).toBeVisible();
  else if (scenario.component === 'OrganizeSelect') { await page.getByRole('button', { name: /^部门/ }).click(); await expect(page.getByRole('tree', { name: '部门' })).toBeVisible(); }
  else { await page.getByRole('combobox', { name: label, exact: true }).click(); await expect(page.getByRole('option').first()).toBeVisible(); }
  await expectNoBlocking(page, `${scenario.component}/${scenario.state}`);
});
