import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { userEvent } from 'vitest/browser';
import axe from 'axe-core';
import { afterEach, expect, it, vi } from 'vitest';
import { useState } from 'react';
import { AppShell, Authentication, ColPage, FallbackPage, initPreferences, LeftTree, OfflineBanner, Page, PageHeader, PreferenceControls, PreferencesProvider, SessionExpiredDialog, ShellErrorBoundary, SsoCallback, TenantSwitcher, UpdateBanner } from './index';
import { makeI18n, makeProvider, session } from './test/fixture';
afterEach(() => { cleanup(); vi.restoreAllMocks(); });
async function accessible() { await waitFor(() => expect(document.querySelector('[data-starting-style], [data-ending-style]')).toBeNull()); const result = await axe.run(document.body); expect(result.violations.filter(value => ['critical', 'serious'].includes(value.impact ?? ''))).toEqual([]); }

it('密码表单复用 SchemaForm，必填焦点、密码掩码与一次提交', async () => {
  const provider = makeProvider(); const authenticated = vi.fn();
  render(<Authentication i18n={makeI18n()} provider={provider} onAuthenticated={authenticated} onRedirect={vi.fn()} />); await screen.findByLabelText(/账号/);
  await userEvent.click(screen.getByRole('button', { name: '登录' }));
  expect(document.activeElement).toBe(screen.getByLabelText(/账号/));
  await userEvent.fill(screen.getByLabelText(/账号/), 'demo'); await userEvent.fill(screen.getByLabelText(/密码/), 'secret');
  expect(screen.getByLabelText(/密码/).getAttribute('type')).toBe('password');
  await accessible(); await userEvent.click(screen.getByRole('button', { name: '登录' }));
  await waitFor(() => expect(authenticated).toHaveBeenCalledWith(session));
  expect(provider.signIn).toHaveBeenCalledTimes(1); expect(provider.signIn).toHaveBeenCalledWith({ username: 'demo', password: 'secret' }, expect.any(AbortSignal));
  expect((screen.getByLabelText(/密码/) as HTMLInputElement).value).toBe('');
});
it('密码失败不输出异常细节；多租户二次选择可键盘完成', async () => {
  const provider = makeProvider(); vi.mocked(provider.signIn).mockRejectedValueOnce(new Error('internal-secret')).mockResolvedValue({ kind: 'select-tenant', tenants: session.tenants });
  const complete = vi.fn(); render(<Authentication i18n={makeI18n()} provider={provider} onAuthenticated={complete} onRedirect={vi.fn()} />); await screen.findByLabelText(/账号/);
  await userEvent.fill(screen.getByLabelText(/账号/), 'demo'); await userEvent.fill(screen.getByLabelText(/密码/), 'bad'); await userEvent.click(screen.getByRole('button', { name: '登录' }));
  await screen.findByRole('alert'); expect(document.body.textContent).not.toContain('internal-secret');
  await userEvent.fill(screen.getByLabelText(/密码/), 'good'); await userEvent.click(screen.getByRole('button', { name: '登录' }));
  const tenant = await screen.findByRole('button', { name: '租户 B' }); tenant.focus(); await userEvent.keyboard('{Enter}');
  await waitFor(() => expect(complete).toHaveBeenCalledWith({ ...session, tenantId: 'b' }));
  await accessible();
});
it('SSO 回调成功、错误与卸载竞态', async () => {
  const provider = makeProvider(); const result = vi.fn(); const back = vi.fn();
  const view = render(<SsoCallback i18n={makeI18n()} provider={provider} callback={{ code: 'code', state: 'state' }} onResult={result} onBack={back} />);
  await waitFor(() => expect(result).toHaveBeenCalledWith({ kind: 'authenticated', session })); view.unmount();
  render(<SsoCallback i18n={makeI18n()} provider={provider} callback={{ error: 'access_denied' }} onResult={result} onBack={back} />);
  await screen.findByRole('alert'); await accessible(); await userEvent.click(screen.getByRole('button', { name: '返回登录' })); expect(back).toHaveBeenCalledOnce();
  cleanup(); let resolve!: (value: { kind: 'authenticated'; session: typeof session }) => void;
  vi.mocked(provider.completeSso!).mockImplementation(() => new Promise(done => { resolve = done; })); result.mockClear();
  const pending = render(<SsoCallback i18n={makeI18n()} provider={provider} callback={{ code: 'later', state: 'state' }} onResult={result} onBack={back} />);
  pending.unmount(); resolve({ kind: 'authenticated', session }); await Promise.resolve(); expect(result).not.toHaveBeenCalled();
});
it.each(['/auth?access_token=secret', '/auth#/callback?access_token=secret'])('SSO 跳转经注入回调且拒绝令牌 URL %s', async unsafe => {
  const provider = makeProvider(); const redirect = vi.fn();
  vi.mocked(provider.beginSso!).mockResolvedValueOnce(unsafe);
  render(<Authentication i18n={makeI18n()} provider={provider} onAuthenticated={vi.fn()} onRedirect={redirect} />); await screen.findByLabelText(/账号/);
  await userEvent.click(screen.getByRole('button', { name: '企业单点登录' })); await screen.findByRole('alert'); expect(redirect).not.toHaveBeenCalled();
  vi.mocked(provider.beginSso!).mockResolvedValue('/auth?code=c&state=s'); await userEvent.click(screen.getByRole('button', { name: '企业单点登录' }));
  await waitFor(() => expect(redirect).toHaveBeenCalledWith('/auth?code=c&state=s'));
});
it('租户切换失败保留当前值，成功更新会话且打开态无障碍通过', async () => {
  const provider = makeProvider(); vi.mocked(provider.switchTenant).mockRejectedValueOnce(new Error('unavailable'));
  function Host() { const [current, setCurrent] = useState(session); return <TenantSwitcher i18n={makeI18n()} provider={provider} session={current} onChanged={setCurrent} />; }
  render(<Host />); await userEvent.click(screen.getByRole('combobox')); await accessible(); await userEvent.click(screen.getByRole('option', { name: '租户 B' }));
  await screen.findByRole('alert'); expect(screen.getByRole('combobox').textContent).toContain('租户 A');
  await userEvent.click(screen.getByRole('combobox')); await userEvent.click(screen.getByRole('option', { name: '租户 B' }));
  await waitFor(() => expect(screen.getByRole('combobox').textContent).toContain('租户 B'));
});
it('过期弹窗只能明确重新登录，键盘可达且有名称和说明', async () => {
  const signIn = vi.fn(); render(<SessionExpiredDialog open i18n={makeI18n()} onSignIn={signIn} />);
  await screen.findByRole('alertdialog', { name: '登录已过期' }); await accessible();
  await userEvent.keyboard('{Escape}'); expect(screen.getByRole('alertdialog')).toBeTruthy();
  await userEvent.click(screen.getByRole('button', { name: '重新登录' })); expect(signIn).toHaveBeenCalledOnce();
});
it('AppShell 单侧栏、页面族、导航、偏好和用户菜单通过键盘与 axe', async () => {
  const store = initPreferences({ namespace: 'test', storage: null }); const i18n = makeI18n(); const navigate = vi.fn(); const signOut = vi.fn();
  render(<PreferencesProvider store={store}><AppShell i18n={i18n} brand="Qian" activePath="/" menu={[{ id: 'home', labelKey: 'test.home', type: 'page', target: '/' }, { id: 'docs', labelKey: 'test.docs', type: 'external', target: 'https://example.test' }]} onNavigate={navigate} onSignOut={signOut} userName="用户" managed toolbar={<PreferenceControls i18n={i18n} />}><Page header={<PageHeader title="页面标题" />}><ColPage aside={<LeftTree label="目录">资源</LeftTree>}>内容</ColPage></Page></AppShell></PreferencesProvider>);
  expect(screen.getByRole('link', { name: '工作台' }).getAttribute('aria-current')).toBe('page');
  await userEvent.click(screen.getByRole('link', { name: '工作台' })); expect(navigate).toHaveBeenCalledWith('/');
  expect(screen.getByRole('link', { name: '文档' }).getAttribute('rel')).toContain('noopener');
  await userEvent.click(screen.getByRole('button', { name: '收起侧栏' })); expect(store.getSnapshot().sidebarCollapsed).toBe(true);
  expect(screen.getByRole('button', { name: '展开侧栏' }).getAttribute('aria-expanded')).toBe('false');
  await userEvent.click(screen.getByRole('combobox', { name: '主题' })); await accessible(); await userEvent.click(screen.getByRole('option', { name: '暗色' })); expect(store.getSnapshot().theme).toBe('dark');
  await userEvent.click(screen.getByRole('button', { name: '用户菜单' })); await accessible(); await userEvent.click(screen.getByRole('menuitem', { name: '退出登录' })); expect(signOut).toHaveBeenCalledOnce();
});
it.each([403, 404, 500, 'offline'] as const)('兜底 %s 有标题、可操作出口并通过 axe', async status => {
  const retry = vi.fn(); render(<FallbackPage status={status} i18n={makeI18n()} onRetry={retry} onHome={vi.fn()} />); expect(screen.getByRole('heading', { level: 1 })).toBeTruthy(); await accessible(); await userEvent.click(screen.getByRole('button', { name: '重试' })); expect(retry).toHaveBeenCalledOnce();
});
it('离线事件、更新提示和错误边界均有可观察恢复路径', async () => {
  const i18n = makeI18n(); const reload = vi.fn();
  const online = vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false);
  render(<><OfflineBanner i18n={i18n} /><UpdateBanner i18n={i18n} available onReload={reload} /></>);
  expect(screen.getByText('网络连接已断开')).toBeTruthy(); await userEvent.click(screen.getByRole('button', { name: '刷新页面' })); expect(reload).toHaveBeenCalledOnce();
  online.mockReturnValue(true); fireEvent(window, new Event('online')); await waitFor(() => expect(screen.queryByText('网络连接已断开')).toBeNull());
  cleanup(); vi.spyOn(console, 'error').mockImplementation(() => {});
  function Broken(): never { throw new Error('private-stack'); }
  function Host() { const [broken, setBroken] = useState(true); return <ShellErrorBoundary i18n={i18n} resetKey={broken} onReset={() => setBroken(false)}>{broken ? <Broken /> : <p>恢复成功</p>}</ShellErrorBoundary>; }
  render(<Host />); expect(document.body.textContent).not.toContain('private-stack'); await accessible(); await userEvent.click(screen.getByRole('button', { name: '重试' })); await screen.findByText('恢复成功');
});

it('同一认证实例收到新 callback 时处理新回调，不被已消费状态吞掉', async () => {
  const i18n = makeI18n(); const provider = makeProvider(); const complete = vi.fn();
  vi.mocked(provider.completeSso!).mockResolvedValueOnce({ kind: 'select-tenant', tenants: session.tenants }).mockResolvedValue({ kind: 'authenticated', session });
  const view = render(<Authentication i18n={i18n} provider={provider} callback={{ code: 'first', state: 'one' }} onAuthenticated={complete} onRedirect={vi.fn()} />);
  await screen.findByRole('heading', { name: '选择租户' });
  view.rerender(<Authentication i18n={i18n} provider={provider} callback={{ code: 'second', state: 'two' }} onAuthenticated={complete} onRedirect={vi.fn()} />);
  await waitFor(() => expect(complete).toHaveBeenCalledWith(session));
  expect(provider.completeSso).toHaveBeenNthCalledWith(2, { code: 'second', state: 'two' }, expect.any(AbortSignal));
});
