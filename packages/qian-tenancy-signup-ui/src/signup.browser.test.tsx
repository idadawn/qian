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
  await Promise.all(document.getAnimations().map((animation) => animation.finished));
  return (await axe.run(root)).violations.filter((item) => item.impact === 'critical' || item.impact === 'serious');
}
const posted = (host: ReturnType<typeof fakeHost>, path: string) => host.calls.filter((call) => call.method === 'POST' && call.path === path);
function signup(host: ReturnType<typeof fakeHost>) { return render(<FeedbackProvider i18n={i18n}><SignupPage client={publicClient(host.transport)} /></FeedbackProvider>); }
function status(host: ReturnType<typeof fakeHost>) { return render(<FeedbackProvider i18n={i18n}><StatusPage client={publicClient(host.transport)} /></FeedbackProvider>); }

it('新号码：核验后填账号与企业信息；租户号实时提示；确认密码不一致落在该字段；提交只带契约字段并去进度页', async () => {
  const host = fakeHost({ taken: ['taken'] });
  const assign = vi.spyOn(browser, 'assign').mockImplementation(() => {});
  signup(host);
  await userEvent.fill(await screen.findByLabelText('手机号 *'), '13800006021');
  await userEvent.click(screen.getByRole('button', { name: '获取验证码' }));
  expect(await screen.findByText(/验证码已发送到 13800006021/)).toBeTruthy();
  expect(screen.getByTestId('signup-resend').hasAttribute('disabled')).toBe(true);
  await userEvent.fill(screen.getByLabelText('短信验证码 *'), '000000');
  await userEvent.click(screen.getByRole('button', { name: '验证' }));
  expect(await screen.findByText('验证码不正确，还可以再试 4 次。')).toBeTruthy();
  await userEvent.fill(screen.getByLabelText('短信验证码 *'), '246810');
  await userEvent.click(screen.getByRole('button', { name: '验证' }));
  expect(await screen.findByText('138****6021 已验证')).toBeTruthy();
  const form = screen.getByTestId('signup-application-form');
  expect(within(form).getByRole('group', { name: '企业信息' })).toBeTruthy();
  await userEvent.fill(within(form).getByLabelText('姓名 *'), '李雷');
  await userEvent.fill(within(form).getByLabelText('设置密码 *'), 'Pass-Word-2026');
  await userEvent.fill(within(form).getByLabelText('确认密码 *'), 'Pass-Word-2027');
  await userEvent.fill(within(form).getByLabelText('企业名称 *'), '星河科技有限公司');
  const code = within(form).getByLabelText('租户号 *');
  expect(screen.queryByTestId('signup-code-availability')).toBeNull();
  await userEvent.fill(code, 'taken');
  const hint = () => screen.getByTestId('signup-code-availability');
  await vi.waitFor(() => expect(hint().getAttribute('data-state')).toBe('taken'));
  expect(hint().textContent).toBe('已被占用，请换一个');
  expect(code.getAttribute('aria-describedby')?.split(' ')).toContain(hint().parentElement!.id);
  await userEvent.fill(code, 'xinghe');
  await vi.waitFor(() => expect(hint().getAttribute('data-state')).toBe('available'));
  await userEvent.click(within(form).getByRole('radio', { name: '21–100 人' }));
  await userEvent.click(within(form).getByRole('checkbox', { name: '我已阅读并同意服务条款和隐私政策' }));
  await userEvent.click(within(form).getByRole('button', { name: '提交申请' }));
  expect(await within(form).findByText('两次输入的密码不一致。')).toBeTruthy();
  expect(within(form).getByLabelText('确认密码 *').getAttribute('aria-invalid')).toBe('true');
  expect(posted(host, '/api/signup/me/applications')).toHaveLength(0);
  await userEvent.fill(within(form).getByLabelText('确认密码 *'), 'Pass-Word-2026');
  await userEvent.click(within(form).getByRole('button', { name: '提交申请' }));
  await vi.waitFor(() => expect(assign).toHaveBeenCalledWith('/signup/status'));
  expect(posted(host, '/api/signup/me/applications')[0]?.body).toEqual({ displayName: '李雷', password: 'Pass-Word-2026', companyName: '星河科技有限公司', code: 'xinghe', size: '21-100', acceptTerms: true });
  expect(await serious()).toEqual([]);
});

it('已有号码：账号区收起为「以某某的身份申请」，不要求姓名与密码；服务端的租户号占用落在租户号字段；「换个账号」退出申请人会话', async () => {
  const host = fakeHost({ existing: '李雷', session: true, taken: ['xinghe'] });
  signup(host);
  expect(await screen.findByText('以 李雷 的身份申请')).toBeTruthy();
  const form = screen.getByTestId('signup-application-form');
  expect(within(form).queryByLabelText('姓名 *')).toBeNull();
  expect(within(form).queryByLabelText('设置密码 *')).toBeNull();
  await userEvent.fill(within(form).getByLabelText('企业名称 *'), '星河科技有限公司');
  await userEvent.fill(within(form).getByLabelText('租户号 *'), 'xinghe');
  await userEvent.click(within(form).getByRole('radio', { name: '1–20 人' }));
  await userEvent.click(within(form).getByRole('checkbox', { name: '我已阅读并同意服务条款和隐私政策' }));
  await userEvent.click(within(form).getByRole('button', { name: '提交申请' }));
  expect(await within(form).findByTestId('signup-application-form-error-code')).toBeTruthy();
  expect(within(form).getByTestId('signup-application-form-error-code').textContent).toBe('租户号已被占用，请换一个。');
  expect(within(form).getByLabelText('租户号 *').getAttribute('aria-invalid')).toBe('true');
  expect(posted(host, '/api/signup/me/applications')[0]?.body).toEqual({ companyName: '星河科技有限公司', code: 'xinghe', size: '1-20', acceptTerms: true });
  expect(await serious()).toEqual([]);
  await userEvent.click(screen.getByRole('button', { name: '换个账号' }));
  expect(await screen.findByLabelText('手机号 *')).toBeTruthy();
  expect(host.calls.some((call) => call.method === 'DELETE' && call.path === '/api/signup/session')).toBe(true);
});

it('「修改后重新提交」以被驳回的申请为底稿；已有进行中的申请时不再给表单，引导去进度页', async () => {
  const rejected = application({ status: 'rejected', rejectReason: '企业名称与说明不一致' });
  history.replaceState(null, '', `/signup?from=${rejected.id}`);
  const host = fakeHost({ session: true, applications: [rejected] });
  const view = signup(host);
  const form = await screen.findByTestId('signup-application-form');
  expect((within(form).getByLabelText('企业名称 *') as HTMLInputElement).value).toBe('星河科技有限公司');
  expect((within(form).getByLabelText('租户号 *') as HTMLInputElement).value).toBe('xinghe');
  view.unmount();
  signup(fakeHost({ session: true, applications: [application()] }));
  expect(await screen.findByText('你已有进行中的申请')).toBeTruthy();
  expect(screen.queryByTestId('signup-application-form')).toBeNull();
});

it('进度页：没有会话先核验手机号；待审核可撤回（二次确认），撤回后可修改重新提交；驳回显示原因；获批且带登录页地址时给「去登录」', async () => {
  const empty = status(fakeHost());
  expect(await screen.findByRole('heading', { level: 1, name: '申请进度' })).toBeTruthy();
  expect(screen.getByLabelText('手机号 *')).toBeTruthy();
  expect(await serious()).toEqual([]);
  empty.unmount();

  const assign = vi.spyOn(browser, 'assign').mockImplementation(() => {});
  const host = fakeHost({ session: true, applications: [application()] });
  const submitted = status(host);
  expect(await screen.findByRole('heading', { level: 2, name: '申请已提交，等待平台审核' })).toBeTruthy();
  expect(screen.getByRole('list', { name: '开通进度' }).querySelector('[aria-current="step"]')?.textContent).toContain('平台审核');
  expect(await serious()).toEqual([]);
  await userEvent.click(screen.getByRole('button', { name: '撤回申请' }));
  await userEvent.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: '确认' }));
  await vi.waitFor(() => expect(posted(host, `/api/signup/me/applications/${application().id}:withdraw`)).toHaveLength(1));
  expect(await screen.findByRole('heading', { level: 2, name: '申请已撤回' })).toBeTruthy();
  expect(posted(host, `/api/signup/me/applications/${application().id}:withdraw`)).toHaveLength(1);
  await userEvent.click(screen.getByRole('button', { name: '修改后重新提交' }));
  expect(assign).toHaveBeenCalledWith(`/signup?from=${application().id}`);
  submitted.unmount();

  const rejected = status(fakeHost({ session: true, applications: [application({ status: 'rejected', rejectReason: '企业名称与说明不一致' })] }));
  expect(await screen.findByTestId('signup-reject-reason')).toBeTruthy();
  expect(screen.getByTestId('signup-reject-reason').textContent).toContain('企业名称与说明不一致');
  rejected.unmount();

  history.replaceState(null, '', '/signup/status?next=' + encodeURIComponent('/login?client_id=qian-shell'));
  status(fakeHost({ session: true, applications: [application({ status: 'approved', plan: 'trial', validUntil: '2026-12-03T00:00:00Z', completedAt: '2026-11-03T08:00:00Z' })] }));
  expect(await screen.findByRole('heading', { level: 2, name: '星河科技有限公司已开通' })).toBeTruthy();
  expect(screen.getByText(/已开通试用，到期日 2026-12-03/)).toBeTruthy();
  await userEvent.click(screen.getByRole('button', { name: '去登录' }));
  expect(assign).toHaveBeenLastCalledWith('/login?client_id=qian-shell');
});

it('运营页：页签计数、审核抽屉的自动检查与开通设置；通过与驳回都二次确认，只发契约字段；切换英文后文案随之改变', async () => {
  const second = application({ id: '0193a1b2-0000-7000-8000-000000000003', number: 'AP-20261103-0003', companyName: '远山制造', code: 'yuanshan' });
  const host = fakeHost({ applications: [application(), second, application({ id: '0193a1b2-0000-7000-8000-000000000004', number: 'AP-20261103-0004', companyName: '旧申请', code: 'old', status: 'approved', plan: 'trial' })] });
  const client = new PlatformClient({ baseUrl: location.origin, transport: host.transport });
  const view = render(<ApplicationsPage client={client} />);
  const tab = (name: RegExp) => screen.getByRole('tab', { name });
  await vi.waitFor(() => expect(within(tab(/^全部/)).getByTestId('yue-tabs-tab-count').textContent).toBe('3'));
  expect(within(tab(/^待审核/)).getByTestId('yue-tabs-tab-count').textContent).toBe('2');
  expect(await serious()).toEqual([]);

  await userEvent.click(screen.getAllByRole('button', { name: '审核' })[0]!);
  const dialog = await screen.findByRole('dialog', { name: '星河科技有限公司' });
  const review = await within(dialog).findByTestId('application-review');
  expect(within(review).getByTestId('application-checks').textContent).toContain('发现 1 个同名企业或申请');
  const approve = within(dialog).getByTestId('application-approve-form');
  expect((within(approve).getByLabelText('租户号') as HTMLInputElement).value).toBe('xinghe');
  expect(within(approve).getByRole('radio', { name: '试用' }).getAttribute('aria-checked')).toBe('true');
  expect((within(approve).getByLabelText('用户配额') as HTMLInputElement).value).toBe('20');
  expect(await serious(dialog)).toEqual([]);
  await userEvent.click(within(dialog).getByRole('button', { name: '通过并开通' }));
  await userEvent.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: '取消' }));
  expect(posted(host, `/api/signup/applications/${application().id}:approve`)).toHaveLength(0);
  await userEvent.click(within(dialog).getByRole('button', { name: '通过并开通' }));
  await userEvent.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: '确认' }));
  expect(await screen.findByText('已通过 星河科技有限公司 的申请，租户已开通')).toBeTruthy();
  expect(posted(host, `/api/signup/applications/${application().id}:approve`)[0]?.body).toEqual({ code: 'xinghe', plan: 'trial', users: 20 });

  await userEvent.click(await screen.findByRole('button', { name: '审核' }));
  const next = await screen.findByRole('dialog', { name: '远山制造' });
  await userEvent.click(await within(next).findByTestId('application-to-reject'));
  const reject = await within(next).findByTestId('application-reject-form');
  await userEvent.click(within(next).getByRole('button', { name: '确认驳回' }));
  expect(await within(reject).findByText('此项必填')).toBeTruthy();
  await userEvent.fill(within(reject).getByLabelText('驳回原因 *'), '企业名称与申请说明中的单位不一致');
  await userEvent.click(within(next).getByRole('button', { name: '确认驳回' }));
  await userEvent.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: '确认' }));
  expect(await screen.findByText('已驳回 远山制造 的申请')).toBeTruthy();
  expect(posted(host, `/api/signup/applications/${second.id}:reject`)[0]?.body).toEqual({ reason: '企业名称与申请说明中的单位不一致' });

  view.rerender(<ApplicationsPage client={client} locale="en-US" />);
  expect(await screen.findByRole('heading', { level: 1, name: 'Applications' })).toBeTruthy();
  expect(screen.getByRole('tablist', { name: 'Application status' })).toBeTruthy();
});
