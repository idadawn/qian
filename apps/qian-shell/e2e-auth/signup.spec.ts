import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { expect, test, type APIRequestContext, type Browser, type Locator, type Page } from '@playwright/test';
import { accessible, collectCspViolations, fillLogin, identity, loginPage, openSession, person, seed, state } from './support';

// 租户自助开通的真实流程（S1-TEN-020；企业首次进入 S1-TEN-021）：Dev.Shell 拉起的真实身份服务、开通组件、租户组件、平台控制台与网关 BFF。
// 申请人在身份服务同源的公开页注册并申请，验证码从开发期发件箱读取（tools/Qian.Dev.Shell/DevSignupOutbox.cs，回环地址 + 状态目录里的开发令牌）；
// 平台运营在外壳的平台控制台打开「开通申请」远程页审核；获批后申请人凭手机号与密码登录，直接进入新企业，首页显示试用提示、开通引导与企业信息。
collectCspViolations();
const operator = person('operator');
const owner = person('owner');
const password = 'Signup-Horse-2026';
const suffix = () => Date.now().toString(36) + Math.floor(Math.random() * 46_656).toString(36);
const newPhone = () => '139' + String(10_000_000 + Math.floor(Math.random() * 89_999_999));
const pad = (value: number) => String(value).padStart(2, '0');
// 与外壳首页同一口径：到期日按浏览器本地日期显示（E2E 与浏览器在同一台机器、同一时区）。
const localDate = (value: string) => { const date = new Date(value); return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`; };

interface Outbox {
  codes: { code: string; sentAt: string }[];
  notifications: { kind: 'approved' | 'rejected'; applicationNumber: string; tenantCode: string | null; validUntil: string | null; reason: string | null }[];
}
async function outbox(request: APIRequestContext, phone: string): Promise<Outbox> {
  const token = readFileSync(resolve(state, 'signup-outbox.token'), 'utf8').trim();
  const response = await request.get(`${identity}/__dev/signup/outbox?phone=${phone}`, { headers: { 'X-Qian-Dev-Token': token } });
  expect(response.status(), '开发期发件箱').toBe(200);
  return await response.json() as Outbox;
}

/** 发验证码，从发件箱取比发送前多出来的那条并填入；核验在点「提交申请」时与申请一起完成（S1-YUE-019 按原型合成一步）。返回发送时刻（同一号码的重发间隔从这里算）。 */
async function verifyPhone(page: Page, phone: string): Promise<number> {
  const before = (await outbox(page.request, phone)).codes.length;
  await page.getByLabel('手机号 *').fill(phone);
  await page.getByRole('button', { name: '获取验证码', exact: true }).click();
  const sentAt = Date.now();
  let code = '';
  await expect.poll(async () => { const box = await outbox(page.request, phone); code = box.codes.length > before ? box.codes[0]!.code : ''; return code; }).not.toBe('');
  await page.getByLabel('短信验证码 *').fill(code);
  return sentAt;
}

async function fillCompany(form: Locator, company: string, code: string) {
  await form.getByLabel('企业名称 *').fill(company);
  await form.getByLabel('租户号 *').fill(code);
  await form.getByRole('radio', { name: '21–100 人' }).click();
  await form.getByRole('checkbox', { name: '我已阅读并同意服务条款和隐私政策' }).click();
}

async function fillAccount(form: Locator, name: string, secret = password) {
  await form.getByLabel('姓名 *').fill(name);
  await form.getByLabel('设置密码 *').fill(secret);
  await form.getByLabel('确认密码 *').fill(secret);
}

/** 运营在独立的浏览器上下文里登录平台控制台，打开「开通申请」，对指定企业的那条申请执行审核动作。 */
async function review(browser: Browser, company: string, act: (drawer: Locator, page: Page) => Promise<void>) {
  const context = await browser.newContext({ baseURL: test.info().project.use.baseURL, ignoreHTTPSErrors: true, locale: 'zh-CN', viewport: { width: 1440, height: 1000 } });
  try {
    const page = await context.newPage();
    await page.goto('/');
    await expect(page).toHaveURL(loginPage);
    await fillLogin(page, operator);
    await page.getByTestId('yue-app-shell-sidebar').getByRole('link', { name: '开通申请', exact: true }).click();
    const remote = page.getByTestId('qian-tenancy-signup-remote');
    await expect(remote).toBeVisible();
    const row = remote.getByRole('row').filter({ hasText: company });
    await expect(row).toHaveCount(1);
    await row.getByRole('button', { name: '审核', exact: true }).click();
    const drawer = page.getByRole('dialog');
    await expect(drawer).toBeVisible();
    await act(drawer, page);
  } finally { await context.close(); }
}

async function confirm(page: Page) {
  const dialog = page.getByRole('alertdialog');
  await dialog.getByRole('button', { name: '确认', exact: true }).click();
  await expect(dialog).toHaveCount(0);
}

async function codeAvailable(page: Page, code: string): Promise<boolean> {
  const response = await page.request.get(`${identity}/api/signup/code-availability?code=${code}`, { headers: { Accept: 'application/json' } });
  expect(response.status(), '租户号可用性').toBe(200);
  return (await response.json() as { available: boolean }).available;
}

test('主线：新号码申请 → 运营通过（试用）→ 申请人登录直接进入新企业，首页试用提示与开通引导；已有账号再申请时账号区收起，撤回后租户号释放', async ({ page, browser, baseURL }) => {
  const origin = baseURL!.replace(/\/$/, '');
  const tag = suffix(); const phone = newPhone(); const name = `李雷${tag.slice(-3)}`; const company = `星河科技${tag}`; const code = `xh${tag}`;

  // 1. 外壳 → 身份服务登录页 → 「申请开通」：公开页带同源登录页地址（next），获批后才有「进入企业控制台」。手机号、验证码、账号与企业信息在同一张表单里，提交时一起核验。
  await page.goto('/');
  await expect(page).toHaveURL(loginPage);
  await page.getByRole('link', { name: '申请开通', exact: true }).click();
  await expect(page).toHaveURL(new RegExp(`^${identity.replaceAll('.', '\\.')}/signup\\?next=%2Flogin`));
  await expect(page.getByRole('heading', { name: '注册并申请开通', exact: true })).toBeVisible();
  await accessible(page, '申请开通页');
  const firstSend = await verifyPhone(page, phone);
  const form = page.getByTestId('signup-application-form');
  await fillAccount(form, name);
  await fillCompany(form, company, code);
  await expect(page.getByTestId('signup-code-availability')).toHaveText('可以使用');
  await accessible(page, '申请表单');
  await form.getByRole('button', { name: '提交申请', exact: true }).click();
  await expect(page).toHaveURL(/\/signup\/status\?next=/);
  await expect(page.getByText('申请已提交，等待平台审核')).toBeVisible();
  await expect(page.getByRole('button', { name: '进入企业控制台' })).toHaveCount(0);
  await accessible(page, '进度页（审核中）');

  // 2. 运营在平台控制台通过：缺省试用、配额 20。
  await review(browser, company, async (drawer, operatorPage) => {
    // 开通设置表单里的租户号（企业信息里另有「期望租户号」一项，不是输入框）。
    const settings = drawer.getByTestId('application-approve-form');
    await expect(settings.getByLabel('租户号', { exact: true })).toHaveValue(code);
    await expect(settings.getByRole('radio', { name: '试用' })).toHaveAttribute('aria-checked', 'true');
    await expect(settings.getByLabel('用户配额', { exact: true })).toHaveValue('20');
    await accessible(operatorPage, '审核抽屉');
    await drawer.getByRole('button', { name: '通过并开通', exact: true }).click();
    await confirm(operatorPage);
    await expect(operatorPage.getByText(`已通过 ${company} 的申请，租户已开通`)).toBeVisible();
  });
  let approved: Outbox['notifications'][number] | undefined;
  await expect.poll(async () => (approved = (await outbox(page.request, phone)).notifications.find(item => item.kind === 'approved'))?.tenantCode).toBe(code);
  const until = approved!.validUntil!;

  // 3. 申请人进度页：已开通（试用到期日）与「进入企业控制台」→ 登录页凭手机号与密码登录，只有这一家企业，直接进入。
  await page.reload();
  await expect(page.getByText(`${company}已开通`)).toBeVisible();
  await expect(page.getByText(`已开通试用，到期日 ${localDate(until)}`, { exact: false })).toBeVisible();
  await accessible(page, '进度页（已开通）');
  await page.getByRole('button', { name: '进入企业控制台', exact: true }).click();
  await expect(page).toHaveURL(loginPage);
  await fillLogin(page, { username: phone, password });
  await expect(page).toHaveURL(origin + '/');
  await expect(page.getByTestId('shell-home')).toBeVisible();

  // 4. 企业首次进入（S1-TEN-021，S1-YUE-019 按原型改版）：试用剩余天数与到期日一致；开通引导按真实数据判定；企业信息；侧栏工作空间卡片是这家企业；
  //    会话与权限卡片在账号菜单的「会话信息」页。
  const welcome = page.getByTestId('shell-welcome');
  await expect(welcome).toBeVisible();
  await expect(welcome.getByRole('heading', { name: `你好，${name}` })).toBeVisible();
  await expect(page.getByTestId('shell-welcome-intro')).toHaveText(`${company}已开通。完成下面几步，团队就可以开始使用。`);
  await expect(page.getByTestId('shell-workspace')).toContainText(company);
  await expect(page.getByTestId('shell-workspace')).toContainText(`试用 · ${code}`);
  const days = Math.max(0, Math.ceil((Date.parse(until) - Date.now()) / 86_400_000));
  await expect(page.getByTestId('shell-trial')).toContainText(`试用中，还剩 ${days} 天`);
  await expect(page.getByTestId('shell-trial')).toContainText(`（${localDate(until)} 到期）`);
  const onboarding = page.getByTestId('shell-onboarding');
  await expect(onboarding.locator('[data-step=admin]')).toHaveAttribute('data-done', 'true');
  await expect(onboarding.locator('[data-step=admin]')).toContainText(`${name} 是企业的管理员。`);
  await expect(onboarding.locator('[data-step=members]')).toHaveAttribute('data-done', 'false');
  await expect(onboarding.locator('[data-step=members]')).toContainText('当前 1 / 20 人');
  await expect(page.getByTestId('shell-onboarding-progress')).toHaveText('已完成 1 / 3');
  const card = page.getByTestId('shell-company');
  await expect(card.locator('[data-field=code]')).toHaveText(code);
  await expect(card.locator('[data-field=validUntil]')).toHaveText(localDate(until));
  await expect(card.locator('[data-field=users]')).toHaveText('1 / 20');
  await expect(card.locator('[data-field=admin]')).toHaveText(name);
  await expect(card.getByTestId('shell-company-status')).toHaveText('试用');
  await accessible(page, '企业首次进入');
  await openSession(page);
  await expect(page.getByTestId('shell-session').locator('[data-field=displayName]')).toHaveText(name);
  await expect(page.getByTestId('shell-permissions')).toBeVisible();

  // 5. 同一号码现在有账号。提交时申请人会话已绑定到新账号，回到申请页账号区直接收起；再走一遍：换个账号（结束申请人会话）→ 照新号码那样填完整张表单再提交，
  //    核验时才认出号码已有账号：账号区收起为「以某某的身份申请」并说明刚填的姓名与密码不会使用，企业信息保留；确认后再提交一次，之后撤回，租户号释放。
  //    条款勾选在表单切换后仍保留，不再点（再点会取消勾选）。同一号码的重发间隔是开通组件的配置（Dev.Shell 设为 10 秒），这里只等到间隔满。
  await page.goto(`${identity}/signup`);
  await expect(page.getByTestId('signup-applicant')).toContainText(`以 ${name} 的身份申请`);
  await page.getByRole('button', { name: '换个账号', exact: true }).click();
  await expect(page.getByLabel('手机号 *')).toBeVisible();
  const again = page.getByTestId('signup-application-form');
  const second = `${code}b`;
  await fillAccount(again, name);
  await fillCompany(again, `${company}分公司`, second);
  await page.waitForTimeout(Math.max(0, firstSend + 11_000 - Date.now()));
  await verifyPhone(page, phone);
  await again.getByRole('button', { name: '提交申请', exact: true }).click();
  await expect(page.getByTestId('signup-applicant')).toContainText(`以 ${name} 的身份申请`);
  await expect(page.getByTestId('signup-existing-notice')).toBeVisible();
  await expect(again.getByLabel('姓名 *')).toHaveCount(0);
  await expect(again.getByLabel('设置密码 *')).toHaveCount(0);
  await expect(again.getByLabel('企业名称 *')).toHaveValue(`${company}分公司`);
  await expect(page.getByTestId('signup-code-availability')).toHaveText('可以使用');
  await again.getByRole('button', { name: '提交申请', exact: true }).click();
  await expect(page.getByText('申请已提交，等待平台审核')).toBeVisible();
  expect(await codeAvailable(page, second)).toBe(false);
  await page.getByRole('button', { name: '撤回申请', exact: true }).click();
  await confirm(page);
  await expect(page.getByText('申请已撤回')).toBeVisible();
  await expect(page.getByText(`租户号 ${second} 已释放`, { exact: false })).toBeVisible();
  expect(await codeAvailable(page, second)).toBe(true);
});

test('驳回与修改后重新提交：租户号实时提示（不合法 / 保留字 / 已占用 / 可用）、字段错误落在对应输入框、驳回原因可见、租户号释放、以原申请为底稿再交', async ({ page, browser }) => {
  const tag = suffix(); const phone = newPhone(); const name = `韩梅梅${tag.slice(-3)}`; const company = `云帆贸易${tag}`; const code = `yf${tag}`;
  const reason = '企业名称与申请说明中的单位不一致，请核对后重新提交';
  await page.goto(`${identity}/signup`);
  await verifyPhone(page, phone);
  const form = page.getByTestId('signup-application-form');
  const hint = page.getByTestId('signup-code-availability');
  const codeInput = form.getByLabel('租户号 *');
  await codeInput.fill('Bad-Code');
  await expect(hint).toHaveText('格式或长度不符合要求');
  await codeInput.fill('admin');
  await expect(hint).toHaveText('这是保留字，请换一个');
  await codeInput.fill(seed.tenants[0]!.code);
  await expect(hint).toHaveText('已被占用，请换一个');
  // 提交时宿主再核对：已占用的租户号落在「租户号」；不满足密码策略的密码落在「设置密码」。
  await fillAccount(form, name);
  await fillCompany(form, company, seed.tenants[0]!.code);
  await form.getByRole('button', { name: '提交申请', exact: true }).click();
  await expect(codeInput).toHaveAttribute('aria-invalid', 'true');
  await codeInput.fill(code);
  await expect(hint).toHaveText('可以使用');
  await fillAccount(form, name, 'short1');
  await form.getByRole('button', { name: '提交申请', exact: true }).click();
  await expect(form.getByLabel('设置密码 *')).toHaveAttribute('aria-invalid', 'true');
  await expect(codeInput).not.toHaveAttribute('aria-invalid', 'true');
  await accessible(page, '字段错误');
  await fillAccount(form, name);
  await form.getByRole('button', { name: '提交申请', exact: true }).click();
  await expect(page.getByText('申请已提交，等待平台审核')).toBeVisible();

  // 运营驳回并写明原因。
  await review(browser, company, async (drawer, operatorPage) => {
    await drawer.getByRole('button', { name: '驳回', exact: true }).click();
    await drawer.getByLabel('驳回原因', { exact: true }).fill(reason);
    await drawer.getByRole('button', { name: '确认驳回', exact: true }).click();
    await confirm(operatorPage);
    await expect(operatorPage.getByText(`已驳回 ${company} 的申请`)).toBeVisible();
  });
  let rejected: Outbox['notifications'][number] | undefined;
  await expect.poll(async () => (rejected = (await outbox(page.request, phone)).notifications.find(item => item.kind === 'rejected'))?.reason).toBe(reason);

  // 申请人看到原因，租户号已释放；「修改后重新提交」以原申请为底稿，提交后是新的申请编号。账号在第一次提交时已经建立（账号先行），
  // 重新提交时账号区收起为「以某某的身份申请」，不再要姓名与密码。
  await page.reload();
  await expect(page.getByText('申请未通过')).toBeVisible();
  await expect(page.getByTestId('signup-reject-reason')).toContainText(reason);
  await expect(page.getByText(`租户号 ${code} 已释放`, { exact: false })).toBeVisible();
  expect(await codeAvailable(page, code)).toBe(true);
  await accessible(page, '进度页（未通过）');
  await page.getByRole('button', { name: '修改后重新提交', exact: true }).click();
  await expect(page).toHaveURL(/\/signup\?from=/);
  const draft = page.getByTestId('signup-application-form');
  await expect(draft.getByLabel('企业名称 *')).toHaveValue(company);
  await expect(draft.getByLabel('租户号 *')).toHaveValue(code);
  await expect(page.getByTestId('signup-code-availability')).toHaveText('可以使用');
  await expect(page.getByTestId('signup-applicant')).toContainText(`以 ${name} 的身份申请`);
  await expect(draft.getByLabel('姓名 *')).toHaveCount(0);
  await expect(draft.getByLabel('设置密码 *')).toHaveCount(0);
  await draft.getByRole('checkbox', { name: '我已阅读并同意服务条款和隐私政策' }).click();
  await draft.getByRole('button', { name: '提交申请', exact: true }).click();
  await expect(page.getByText('申请已提交，等待平台审核')).toBeVisible();
  await expect(page.getByText(rejected!.applicationNumber)).toHaveCount(0);
});

test('正式租户：首页有欢迎区与企业信息、没有试用提示，会话与权限卡片照常', async ({ page }) => {
  await page.goto('/');
  await expect(page).toHaveURL(loginPage);
  await fillLogin(page, owner);
  await page.getByRole('button', { name: seed.tenants[0]!.name, exact: true }).click();
  await expect(page.getByTestId('shell-home')).toBeVisible();
  const card = page.getByTestId('shell-company');
  await expect(card.locator('[data-field=code]')).toHaveText(seed.tenants[0]!.code);
  await expect(card.locator('[data-field=validUntil]')).toHaveText('长期有效');
  await expect(card).toContainText('正常');
  await expect(page.getByTestId('shell-workspace')).toContainText(`正式 · ${seed.tenants[0]!.code}`);
  await expect(page.getByTestId('shell-trial')).toHaveCount(0);
  await expect(page.getByTestId('shell-onboarding').locator('[data-step=apps] button')).toHaveText('打开客户');
  await accessible(page, '正式租户首页');
  await openSession(page);
  await expect(page.getByTestId('shell-session').locator('[data-field=displayName]')).toHaveText(owner.displayName);
  await expect(page.getByTestId('shell-permissions')).toBeVisible();
});
