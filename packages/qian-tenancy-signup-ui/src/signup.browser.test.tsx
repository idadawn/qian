import { cleanup, render, screen, within } from '@testing-library/react';
import axe from 'axe-core';
import { afterEach, expect, it, vi } from 'vitest';
import { userEvent } from 'vitest/browser';
import { FeedbackProvider } from '@idadawn/yue-form';
import { PlatformClient } from '@idadawn/yue-sdk';
import { publicClient } from './api';
import { i18n } from './i18n';
import { ApplicationsPage } from './operator/ApplicationsPage';
import { browser } from './public/navigation';
import { SignupPage } from './public/SignupPage';
import { StatusPage } from './public/StatusPage';
import { application, fakeHost } from './test/fake-host';

afterEach(async () => { cleanup(); vi.restoreAllMocks(); history.replaceState(null, '', '/'); await i18n.changeLanguage('zh-CN'); });
async function serious(root: Element = document.body) {
  // 等动画结束再跑 axe。过渡被打断时 animation.finished 以 AbortError 拒绝（Chromium 的消息是「The user aborted a request.」，没有调用栈），
  // 被取消的动画不算失败；不接住它，CI 上偶发的打断会让用例整条失败（S1-YUE-019 验收缺陷 8）。
  await Promise.all(document.getAnimations().map((animation) => animation.finished.catch(() => {})));
  return (await axe.run(root)).violations.filter((item) => item.impact === 'critical' || item.impact === 'serious');
}
const posted = (host: ReturnType<typeof fakeHost>, path: string) => host.calls.filter((call) => call.method === 'POST' && call.path === path);
function signup(host: ReturnType<typeof fakeHost>) { return render(<FeedbackProvider i18n={i18n}><SignupPage client={publicClient(host.transport)} /></FeedbackProvider>); }
function status(host: ReturnType<typeof fakeHost>) { return render(<FeedbackProvider i18n={i18n}><StatusPage client={publicClient(host.transport)} /></FeedbackProvider>); }
const invalid = (element: HTMLElement) => element.getAttribute('aria-invalid') === 'true';
const pad = (value: number) => String(value).padStart(2, '0');
/** 步骤条上的时间按浏览器本地时区显示月日时分。 */
function shortTime(value: string) { const date = new Date(value); return `${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`; }
/** 新号码的账号区与企业信息（验证码、条款另填）。 */
async function fillNewApplication(form: HTMLElement, code = 'xinghe') {
  await userEvent.fill(within(form).getByLabelText('姓名 *'), '李雷');
  await userEvent.fill(within(form).getByLabelText('设置密码 *'), 'Pass-Word-2026');
  await userEvent.fill(within(form).getByLabelText('确认密码 *'), 'Pass-Word-2026');
  await userEvent.fill(within(form).getByLabelText('企业名称 *'), '星河科技有限公司');
  await userEvent.fill(within(form).getByLabelText('租户号 *'), code);
  await userEvent.click(within(form).getByRole('radio', { name: '21–100 人' }));
}
async function sendCode(form: HTMLElement, phone = '13800006021') {
  await userEvent.fill(within(form).getByLabelText('手机号 *'), phone);
  await userEvent.click(within(form).getByRole('button', { name: '获取验证码' }));
  await vi.waitFor(() => expect(screen.getByTestId('signup-send-code-status').textContent).toBe(`验证码已发送到 ${phone}。`));
}

it('axe 前等待动画：被打断的过渡不算失败（打断时 animation.finished 以 AbortError 拒绝）', async () => {
  const box = document.createElement('div');
  box.textContent = '过渡'; box.style.cssText = 'width:10px;transition:width 2s';
  document.body.append(box);
  try {
    box.getBoundingClientRect(); box.style.width = '200px';
    await new Promise<void>((resolve) => requestAnimationFrame(() => resolve()));
    expect(document.getAnimations().length).toBeGreaterThan(0);
    const pending = serious(box);
    box.style.transition = 'none'; box.style.width = '50px';
    await expect(pending).resolves.toEqual([]);
  } finally { box.remove(); }
});

it('新号码：手机号与验证码在账号区，发码后倒计时；提交时先用验证码换会话再提交申请；租户号实时提示与密码强度；字段错误落在对应输入框', async () => {
  history.replaceState(null, '', '/signup?next=' + encodeURIComponent('/login?client_id=qian-shell'));
  const host = fakeHost({ taken: ['taken'] });
  const assign = vi.spyOn(browser, 'assign').mockImplementation(() => {});
  signup(host);
  const form = await screen.findByTestId('signup-application-form');
  expect(within(form).getByRole('group', { name: '管理员账号' })).toBeTruthy();
  expect(within(form).getByRole('group', { name: '企业信息' })).toBeTruthy();
  expect(screen.getByTestId('signup-login-entry').querySelector('a')?.getAttribute('href')).toBe('/login?client_id=qian-shell');
  // 没填手机号先发码：提示落在手机号上。
  await userEvent.click(within(form).getByRole('button', { name: '获取验证码' }));
  await vi.waitFor(() => expect(invalid(within(form).getByLabelText('手机号 *'))).toBe(true));
  expect(posted(host, '/api/signup/verifications')).toHaveLength(0);
  await sendCode(form);
  expect(posted(host, '/api/signup/verifications')[0]?.body).toEqual({ phone: '13800006021' });
  expect(screen.getByTestId('signup-send-code').hasAttribute('disabled')).toBe(true);
  expect(screen.getByTestId('signup-send-code').textContent).toMatch(/^重新发送 \d+s$/);

  await fillNewApplication(form, 'taken');
  const hint = () => screen.getByTestId('signup-code-availability');
  await vi.waitFor(() => expect(hint().getAttribute('data-state')).toBe('taken'));
  expect(hint().textContent).toBe('已被占用，请换一个');
  const code = within(form).getByLabelText('租户号 *');
  expect(code.getAttribute('aria-describedby')?.split(' ')).toContain(hint().parentElement!.id);
  await userEvent.fill(code, 'xinghe');
  await vi.waitFor(() => expect(hint().getAttribute('data-state')).toBe('available'));
  expect(hint().textContent).toBe('可以使用');
  expect(code.getAttribute('aria-describedby')?.split(' ')).toContain(hint().parentElement!.id);
  expect(screen.getByTestId('signup-password-strength').textContent).toBe('强度：强');

  // 本地核对在核验之前：两次密码不一致、未同意条款都不消耗验证码。
  await userEvent.fill(within(form).getByLabelText('确认密码 *'), 'Pass-Word-2027');
  await userEvent.fill(within(form).getByLabelText('短信验证码 *'), '000000');
  await userEvent.click(within(form).getByRole('button', { name: '提交申请' }));
  expect(await within(form).findByText('两次输入的密码不一致。')).toBeTruthy();
  expect(invalid(within(form).getByLabelText('确认密码 *'))).toBe(true);
  await userEvent.fill(within(form).getByLabelText('确认密码 *'), 'Pass-Word-2026');
  await userEvent.click(within(form).getByRole('button', { name: '提交申请' }));
  expect(await screen.findByText('请先阅读并同意服务条款和隐私政策。')).toBeTruthy();
  expect(posted(host, '/api/signup/session')).toHaveLength(0);

  await userEvent.click(screen.getByRole('checkbox', { name: '我已阅读并同意服务条款和隐私政策' }));
  await userEvent.click(within(form).getByRole('button', { name: '提交申请' }));
  expect(await within(form).findByText('验证码不正确，还可以再试 4 次。')).toBeTruthy();
  expect(invalid(within(form).getByLabelText('短信验证码 *'))).toBe(true);
  expect(posted(host, '/api/signup/me/applications')).toHaveLength(0);
  expect(await serious()).toEqual([]);

  await userEvent.fill(within(form).getByLabelText('短信验证码 *'), '246810');
  await userEvent.click(within(form).getByRole('button', { name: '提交申请' }));
  await vi.waitFor(() => expect(assign).toHaveBeenCalledWith('/signup/status?next=%2Flogin%3Fclient_id%3Dqian-shell'));
  expect(posted(host, '/api/signup/session').map((call) => call.body)).toEqual([
    { verificationId: '0193a1b2-0000-7000-8000-0000000000c1', code: '000000' }, { verificationId: '0193a1b2-0000-7000-8000-0000000000c1', code: '246810' }]);
  expect(posted(host, '/api/signup/me/applications')[0]?.body).toEqual({ displayName: '李雷', password: 'Pass-Word-2026', companyName: '星河科技有限公司', code: 'xinghe', size: '21-100', acceptTerms: true });
});

it('核验通过但申请被退回：手机号只读并标「已验证」、验证码收起，改正后再提交不再核验', async () => {
  const host = fakeHost({ taken: ['xinghe'] });
  const assign = vi.spyOn(browser, 'assign').mockImplementation(() => {});
  signup(host);
  const form = await screen.findByTestId('signup-application-form');
  await sendCode(form);
  await fillNewApplication(form);
  await userEvent.fill(within(form).getByLabelText('短信验证码 *'), '246810');
  await userEvent.click(screen.getByRole('checkbox', { name: '我已阅读并同意服务条款和隐私政策' }));
  await userEvent.click(within(form).getByRole('button', { name: '提交申请' }));
  expect(await within(form).findByTestId('signup-application-form-error-code')).toBeTruthy();
  expect(within(form).getByTestId('signup-application-form-error-code').textContent).toBe('租户号已被占用，请换一个。');
  expect(within(form).getByTestId('signup-phone-verified').textContent).toBe('已验证');
  expect((within(form).getByLabelText('手机号 *') as HTMLInputElement).disabled).toBe(true);
  expect(within(form).queryByLabelText('短信验证码 *')).toBeNull();
  expect(await serious()).toEqual([]);
  await userEvent.fill(within(form).getByLabelText('租户号 *'), 'xinghe2');
  await userEvent.click(within(form).getByRole('button', { name: '提交申请' }));
  await vi.waitFor(() => expect(assign).toHaveBeenCalledWith('/signup/status'));
  expect(posted(host, '/api/signup/session')).toHaveLength(1);
  expect(posted(host, '/api/signup/me/applications').map((call) => (call.body as { code: string }).code)).toEqual(['xinghe', 'xinghe2']);
});

it('号码已有账号：核验后收起账号区，改为「以某某的身份申请」，企业信息保留，确认后再提交且不带姓名与密码', async () => {
  const host = fakeHost({ existing: '李雷' });
  const assign = vi.spyOn(browser, 'assign').mockImplementation(() => {});
  signup(host);
  const form = await screen.findByTestId('signup-application-form');
  await sendCode(form);
  await fillNewApplication(form);
  await userEvent.fill(within(form).getByLabelText('短信验证码 *'), '246810');
  await userEvent.click(screen.getByRole('checkbox', { name: '我已阅读并同意服务条款和隐私政策' }));
  await userEvent.click(within(form).getByRole('button', { name: '提交申请' }));
  expect(await screen.findByText('以 李雷 的身份申请')).toBeTruthy();
  expect(screen.getByTestId('signup-existing-notice').textContent).toContain('刚填写的姓名和密码不会使用');
  const again = await screen.findByTestId('signup-application-form');
  expect(within(again).queryByLabelText('姓名 *')).toBeNull();
  expect(within(again).queryByLabelText('设置密码 *')).toBeNull();
  expect((within(again).getByLabelText('企业名称 *') as HTMLInputElement).value).toBe('星河科技有限公司');
  expect((within(again).getByLabelText('租户号 *') as HTMLInputElement).value).toBe('xinghe');
  expect(within(again).getByRole('radio', { name: '21–100 人' }).getAttribute('aria-checked')).toBe('true');
  expect(posted(host, '/api/signup/me/applications')).toHaveLength(0);
  await userEvent.click(within(again).getByRole('button', { name: '提交申请' }));
  await vi.waitFor(() => expect(assign).toHaveBeenCalledWith('/signup/status'));
  expect(posted(host, '/api/signup/me/applications')[0]?.body).toEqual({ companyName: '星河科技有限公司', code: 'xinghe', size: '21-100', acceptTerms: true });
});

it('已有账号的会话：账号区整节不显示，企业信息为第 1 节；服务端的租户号占用落在租户号字段；「换个账号」结束申请人会话', async () => {
  const host = fakeHost({ existing: '李雷', session: true, taken: ['xinghe'] });
  signup(host);
  expect(await screen.findByText('以 李雷 的身份申请')).toBeTruthy();
  const form = screen.getByTestId('signup-application-form');
  expect(within(form).queryByLabelText('姓名 *')).toBeNull();
  expect(within(form).queryByLabelText('手机号 *')).toBeNull();
  expect(within(form).queryByRole('group', { name: '管理员账号' })).toBeNull();
  expect(within(form).getByTestId('signup-application-form-group-0-marker').textContent).toBe('1');
  await userEvent.fill(within(form).getByLabelText('企业名称 *'), '星河科技有限公司');
  await userEvent.fill(within(form).getByLabelText('租户号 *'), 'xinghe');
  await userEvent.click(within(form).getByRole('radio', { name: '1–20 人' }));
  await userEvent.click(screen.getByRole('checkbox', { name: '我已阅读并同意服务条款和隐私政策' }));
  await userEvent.click(within(form).getByRole('button', { name: '提交申请' }));
  expect(await within(form).findByTestId('signup-application-form-error-code')).toBeTruthy();
  expect(within(form).getByTestId('signup-application-form-error-code').textContent).toBe('租户号已被占用，请换一个。');
  expect(invalid(within(form).getByLabelText('租户号 *'))).toBe(true);
  expect(posted(host, '/api/signup/me/applications')[0]?.body).toEqual({ companyName: '星河科技有限公司', code: 'xinghe', size: '1-20', acceptTerms: true });
  expect(await serious()).toEqual([]);
  await userEvent.click(screen.getByRole('button', { name: '换个账号' }));
  expect(await screen.findByLabelText('手机号 *')).toBeTruthy();
  expect(host.calls.some((call) => call.method === 'DELETE' && call.path === '/api/signup/session')).toBe(true);
});

it('「修改后重新提交」以被驳回的申请为底稿；已有进行中的申请时不再给表单，引导去进度页', async () => {
  const rejected = application({ status: 'rejected', rejectReason: '企业名称与说明不一致' });
  history.replaceState(null, '', `/signup?from=${rejected.id}`);
  const host = fakeHost({ session: true, existing: '李雷', applications: [rejected] });
  const view = signup(host);
  const form = await screen.findByTestId('signup-application-form');
  expect((within(form).getByLabelText('企业名称 *') as HTMLInputElement).value).toBe('星河科技有限公司');
  expect((within(form).getByLabelText('租户号 *') as HTMLInputElement).value).toBe('xinghe');
  view.unmount();
  signup(fakeHost({ session: true, existing: '李雷', applications: [application()] }));
  expect(await screen.findByText('你已有进行中的申请')).toBeTruthy();
  expect(screen.queryByTestId('signup-application-form')).toBeNull();
});

it('进度页：没有会话先核验手机号；待审核四步、可撤回（二次确认），撤回后可修改重新提交；驳回显示原因；获批且带登录页地址时给「进入企业控制台」', async () => {
  const empty = status(fakeHost());
  expect(await screen.findByRole('heading', { level: 1, name: '申请进度' })).toBeTruthy();
  const verify = screen.getByTestId('signup-verify-form');
  expect(within(verify).getByLabelText('短信验证码 *')).toBeTruthy();
  expect(await serious()).toEqual([]);
  await sendCode(verify);
  await userEvent.fill(within(verify).getByLabelText('短信验证码 *'), '246810');
  await userEvent.click(within(verify).getByRole('button', { name: '查看进度' }));
  expect(await screen.findByText('还没有开通申请')).toBeTruthy();
  empty.unmount();

  const assign = vi.spyOn(browser, 'assign').mockImplementation(() => {});
  const host = fakeHost({ session: true, existing: '李雷', applications: [application()] });
  const submitted = status(host);
  expect(await screen.findByRole('heading', { level: 2, name: '申请已提交，等待平台审核' })).toBeTruthy();
  const progress = screen.getByRole('list', { name: '开通进度' });
  expect(within(progress).getAllByRole('listitem').map((item) => item.textContent)).toEqual([`提交申请（已完成）${shortTime(application().submittedAt)}`, '2平台审核（进行中）进行中', '3开通企业（未开始）自动完成', '4登录使用（未开始）短信通知']);
  expect(progress.querySelector('[aria-current="step"]')?.textContent).toContain('平台审核');
  expect(screen.getByTestId('signup-status-user').textContent).toContain('李雷');
  expect(screen.getByTestId('signup-status-footnote').textContent).toBe('审核期间需要修改资料，请先撤回再重新提交。');
  expect(await serious()).toEqual([]);
  await userEvent.click(screen.getByRole('button', { name: '撤回申请' }));
  await userEvent.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: '确认' }));
  await vi.waitFor(() => expect(posted(host, `/api/signup/me/applications/${application().id}:withdraw`)).toHaveLength(1));
  expect(await screen.findByRole('heading', { level: 2, name: '申请已撤回' })).toBeTruthy();
  await userEvent.click(screen.getByRole('button', { name: '修改后重新提交' }));
  expect(assign).toHaveBeenCalledWith(`/signup?from=${application().id}`);
  submitted.unmount();

  const rejected = status(fakeHost({ session: true, existing: '李雷', applications: [application({ status: 'rejected', rejectReason: '企业名称与说明不一致', completedAt: '2026-11-04T01:15:00Z' })] }));
  expect(await screen.findByTestId('signup-reject-reason')).toBeTruthy();
  expect(screen.getByTestId('signup-reject-reason').textContent).toContain('企业名称与说明不一致');
  expect(screen.getByRole('list', { name: '开通进度' }).querySelector('[data-status=failed]')?.textContent).toContain('驳回');
  rejected.unmount();

  history.replaceState(null, '', '/signup/status?next=' + encodeURIComponent('/login?client_id=qian-shell'));
  status(fakeHost({ session: true, existing: '李雷', applications: [application({ status: 'approved', plan: 'trial', validUntil: '2026-12-03T00:00:00Z', completedAt: '2026-11-03T08:00:00Z' })] }));
  expect(await screen.findByRole('heading', { level: 2, name: '星河科技有限公司已开通' })).toBeTruthy();
  expect(screen.getByText(/已开通试用，到期日 2026-12-03/)).toBeTruthy();
  await userEvent.click(screen.getByRole('button', { name: '进入企业控制台' }));
  expect(assign).toHaveBeenLastCalledWith('/login?client_id=qian-shell');
});

it('运营页：默认「待审核」页签与计数；审核抽屉的自动检查与开通设置（到期日随开通方式改名）；通过与驳回都二次确认，只发契约字段；切换英文后文案随之改变', async () => {
  const second = application({ id: '0193a1b2-0000-7000-8000-000000000003', number: 'AP-20261103-0003', companyName: '远山制造', code: 'yuanshan' });
  const host = fakeHost({ applications: [application(), second, application({ id: '0193a1b2-0000-7000-8000-000000000004', number: 'AP-20261103-0004', companyName: '旧申请', code: 'old', status: 'approved', plan: 'trial' })] });
  const client = new PlatformClient({ baseUrl: location.origin, transport: host.transport });
  const view = render(<ApplicationsPage client={client} />);
  const tab = (name: RegExp) => screen.getByRole('tab', { name });
  await vi.waitFor(() => expect(within(tab(/^全部/)).getByTestId('applications-tab-all-count').textContent).toBe('3'));
  expect(screen.getAllByRole('tab').map((item) => item.textContent?.replace(/\d+$/, ''))).toEqual(['待审核', '开通中', '已通过', '已驳回', '已撤回', '全部']);
  expect(tab(/^待审核/).getAttribute('aria-selected')).toBe('true');
  expect(within(tab(/^待审核/)).getByTestId('applications-tab-submitted-count').textContent).toBe('2');
  expect(screen.queryByText('旧申请')).toBeNull();
  expect(screen.getAllByTestId('application-check').map((item) => item.textContent)).toEqual(['检查通过', '检查通过']);
  expect(screen.getAllByRole('img', { name: '已验证' })).toHaveLength(2);
  expect(screen.queryByRole('button', { name: '显示列' })).toBeNull();
  expect(await serious()).toEqual([]);

  await userEvent.click(screen.getAllByRole('button', { name: '审核' })[0]!);
  const dialog = await screen.findByRole('dialog', { name: '审核开通申请' });
  const review = await within(dialog).findByTestId('application-review');
  expect(within(review).getByTestId('application-checks').textContent).toContain('发现 1 个同名企业或申请');
  const approve = within(dialog).getByTestId('application-approve-form');
  expect((within(approve).getByLabelText('租户号') as HTMLInputElement).value).toBe('xinghe');
  expect(within(approve).getByRole('radio', { name: '试用' }).getAttribute('aria-checked')).toBe('true');
  expect(within(approve).getByLabelText('试用到期')).toBeTruthy();
  expect((within(approve).getByLabelText('用户配额') as HTMLInputElement).value).toBe('20');
  await userEvent.click(within(approve).getByRole('radio', { name: '正式' }));
  expect(await within(approve).findByLabelText('有效期至')).toBeTruthy();
  await userEvent.click(within(approve).getByRole('radio', { name: '试用' }));
  expect(await within(approve).findByLabelText('试用到期')).toBeTruthy();
  expect(await serious(dialog)).toEqual([]);
  await userEvent.click(within(dialog).getByRole('button', { name: '通过并开通' }));
  await userEvent.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: '取消' }));
  expect(posted(host, `/api/signup/applications/${application().id}:approve`)).toHaveLength(0);
  await userEvent.click(within(dialog).getByRole('button', { name: '通过并开通' }));
  await userEvent.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: '确认' }));
  expect(await screen.findByText('已通过 星河科技有限公司 的申请，租户已开通')).toBeTruthy();
  expect(posted(host, `/api/signup/applications/${application().id}:approve`)[0]?.body).toEqual({ code: 'xinghe', plan: 'trial', users: 20 });

  await userEvent.click(await screen.findByRole('button', { name: '审核' }));
  const next = await screen.findByRole('dialog', { name: '审核开通申请' });
  await within(next).findByTestId('application-review');
  await userEvent.click(within(next).getByRole('button', { name: '驳回' }));
  const reject = await within(next).findByTestId('application-reject-form');
  await userEvent.click(within(next).getByRole('button', { name: '确认驳回' }));
  expect(await within(reject).findByText('此项必填')).toBeTruthy();
  await userEvent.click(within(reject).getByRole('button', { name: '企业名称与说明不一致' }));
  expect(within(reject).getByRole('button', { name: '企业名称与说明不一致' }).getAttribute('aria-pressed')).toBe('true');
  expect((within(reject).getByLabelText('驳回原因') as HTMLTextAreaElement).value).toBe('企业名称与申请说明中的单位不一致，请填写营业执照上的全称后重新提交。');
  await userEvent.fill(within(reject).getByLabelText('驳回原因'), '企业名称与申请说明中的单位不一致');
  expect(await serious(next)).toEqual([]);
  await userEvent.click(within(next).getByRole('button', { name: '确认驳回' }));
  await userEvent.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: '确认' }));
  expect(await screen.findByText('已驳回 远山制造 的申请')).toBeTruthy();
  expect(posted(host, `/api/signup/applications/${second.id}:reject`)[0]?.body).toEqual({ reason: '企业名称与申请说明中的单位不一致' });

  // 已处理的申请只读：「查看」打开详情，页脚只有关闭。
  await userEvent.click(tab(/^全部/));
  await userEvent.click((await screen.findAllByRole('button', { name: '查看' }))[0]!);
  const detail = await screen.findByRole('dialog', { name: '开通申请详情' });
  await within(detail).findByTestId('application-review');
  expect(within(detail).queryByRole('button', { name: '通过并开通' })).toBeNull();
  await userEvent.click(within(detail).getByTestId('application-close'));

  view.rerender(<ApplicationsPage client={client} locale="en-US" />);
  expect(await screen.findByRole('heading', { level: 1, name: 'Applications' })).toBeTruthy();
  expect(screen.getByRole('tablist', { name: 'Application status' })).toBeTruthy();
});

it('运营页：有开通失败的申请时出现「开通失败」页签，行内可重试', async () => {
  const failed = application({ id: '0193a1b2-0000-7000-8000-000000000005', number: 'AP-20261103-0005', companyName: '失败企业', code: 'broken', status: 'failed' });
  const host = fakeHost({ applications: [application(), failed] });
  render(<ApplicationsPage client={new PlatformClient({ baseUrl: location.origin, transport: host.transport })} />);
  const tab = await screen.findByRole('tab', { name: /^开通失败/ });
  expect(screen.getAllByRole('tab').map((item) => item.textContent?.replace(/\d+$/, ''))).toEqual(['待审核', '开通中', '开通失败', '已通过', '已驳回', '已撤回', '全部']);
  await userEvent.click(tab);
  await userEvent.click(await screen.findByRole('button', { name: '重试' }));
  expect(await screen.findByText('失败企业 已开通')).toBeTruthy();
  expect(posted(host, `/api/signup/applications/${failed.id}:retry`)).toHaveLength(1);
});
