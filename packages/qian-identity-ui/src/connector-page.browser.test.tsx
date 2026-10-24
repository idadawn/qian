import { cleanup, render, screen, waitFor } from '@testing-library/react';
import { userEvent } from 'vitest/browser';
import axe from 'axe-core';
import { afterEach, beforeAll, expect, it, vi } from 'vitest';
import type { AuthResult } from '@idadawn/yue-shell';
import type { ConnectorFlow, IdentityAuthProvider } from '@qian/shell-kit/identity';
import { ConnectorPage, LoginPage } from './App';
import { i18n } from './i18n';
import { browser } from './navigation';

// 连接器落点与登录页连接器区的组件测试：provider 是替身（流程视图按身份服务 README 的契约构造），顶层跳转经 navigation.ts 的出口记录。
// 真实上游（Dex）、真实回调与真实绑定在 apps/qian-shell/e2e-auth/auth.spec.ts 里走通。
beforeAll(async () => { await i18n.changeLanguage('zh-CN'); });
afterEach(() => { cleanup(); vi.restoreAllMocks(); });

const returnUrl = '/connect/authorize?client_id=qian-shell&response_type=code&redirect_uri=https%3A%2F%2Flocalhost%3A5443%2Fauth%2Fcallback';
function flow(next: ConnectorFlow['next'], extra: Partial<ConnectorFlow> = {}): ConnectorFlow {
  return { flow: 'f1', status: next, next, connector: { id: 'dex', displayName: 'Dex 本机身份源', protocol: 'oidc' }, tenant: { code: 'acme', name: '甲方科技' }, binding: null, error: null, returnUrl: null, ...extra };
}
const external = { displayName: 'Alice', email: 'a***@dex.local', username: null };
const unverified = flow('bind', { binding: { external, allowJit: false, verified: false, account: null } });
const verified = flow('bind', { binding: { external, allowJit: false, verified: true, account: { displayName: '演示用户', account: 'demo' } } });
const done = flow('done', { status: 'completed', returnUrl });
const authenticated: AuthResult = { kind: 'authenticated', session: { subjectId: 'person', displayName: '演示用户', tenantId: 'acme', tenants: [] } };
function provider(overrides: Partial<IdentityAuthProvider>): IdentityAuthProvider {
  const base = {
    signIn: vi.fn(), selectTenant: vi.fn(), switchTenant: vi.fn(), beginSso: vi.fn(), completeSso: vi.fn(), changePassword: vi.fn(), beginSwitch: vi.fn(), me: vi.fn(),
    listConnectors: vi.fn(async () => []), beginConnector: vi.fn(), readFlow: vi.fn(async () => null), flowSignIn: vi.fn(), flowBind: vi.fn(),
  };
  return { ...base, ...overrides } as unknown as IdentityAuthProvider;
}
function leave() { return vi.spyOn(browser, 'assign').mockImplementation(() => undefined); }
async function accessible() {
  await waitFor(() => expect(document.querySelector('[data-starting-style], [data-ending-style]')).toBeNull());
  const result = await axe.run({ include: [document.body], exclude: ['[data-base-ui-focus-guard]'] });
  expect(result.violations.filter(value => ['critical', 'serious'].includes(value.impact ?? ''))).toEqual([]);
}

it('等待态按间隔重读流程，完成后只经出口跳回流程里的授权请求地址', async () => {
  const assign = leave();
  const readFlow = vi.fn().mockResolvedValueOnce(flow('wait')).mockResolvedValueOnce(flow('wait')).mockResolvedValue(done);
  render(<ConnectorPage connectorId="dex" flowId="f1" pollInterval={20} provider={provider({ readFlow })} />);
  expect((await screen.findAllByText('正在等待身份提供方返回')).length).toBeGreaterThan(0);
  await waitFor(() => expect(assign).toHaveBeenCalledWith(returnUrl));
  expect(readFlow).toHaveBeenCalledTimes(3);
  expect(assign).toHaveBeenCalledTimes(1);
});

it('确认绑定：先用账号密码验证，再绑定到已验证账号；绑定成功后再读一次流程取 return_url 才跳回', async () => {
  const assign = leave();
  const readFlow = vi.fn().mockResolvedValueOnce(unverified).mockResolvedValue(done);
  const flowSignIn = vi.fn(async () => verified);
  const flowBind = vi.fn(async (): Promise<AuthResult | null> => authenticated);
  render(<ConnectorPage connectorId="dex" flowId="f1" provider={provider({ readFlow, flowSignIn, flowBind })} />);
  const bind = await screen.findByTestId('identity-connector-bind');
  expect(bind.textContent).toContain('上游身份 Alice 尚未绑定账号');
  expect(screen.queryByRole('button', { name: /绑定到已验证账号/ })).toBeNull();
  expect(screen.queryByRole('button', { name: '创建新账号并绑定' })).toBeNull();
  await accessible();
  await userEvent.fill(screen.getByLabelText(/账号/), 'demo');
  await userEvent.fill(screen.getByLabelText(/密码/), 'secret');
  await userEvent.click(screen.getByRole('button', { name: '登录' }));
  await waitFor(() => expect(flowSignIn).toHaveBeenCalledWith('f1', { username: 'demo', password: 'secret' }, expect.any(AbortSignal)));
  const link = await screen.findByRole('button', { name: /绑定到已验证账号/ });
  expect(link.textContent).toContain('演示用户');
  expect(assign).not.toHaveBeenCalled();
  await userEvent.click(link);
  await waitFor(() => expect(flowBind).toHaveBeenCalledWith('f1', 'link', expect.any(AbortSignal)));
  await waitFor(() => expect(assign).toHaveBeenCalledWith(returnUrl));
  expect(readFlow).toHaveBeenCalledTimes(2);
});

it('放弃本次登录：流程作废后呈现错误态，「重新发起登录」回到裸登录页', async () => {
  const assign = leave();
  const readFlow = vi.fn().mockResolvedValueOnce(verified).mockResolvedValue(flow('restart', { status: 'cancelled' }));
  const flowBind = vi.fn(async (): Promise<AuthResult | null> => null);
  render(<ConnectorPage connectorId="dex" flowId="f1" provider={provider({ readFlow, flowBind })} />);
  await userEvent.click(await screen.findByRole('button', { name: '放弃本次登录' }));
  await waitFor(() => expect(flowBind).toHaveBeenCalledWith('f1', 'cancel', expect.any(AbortSignal)));
  const error = await screen.findByTestId('identity-connector-error');
  expect(error.textContent).toContain(i18n.t('shell.ssoError'));
  expect(assign).not.toHaveBeenCalled();
  await userEvent.click(screen.getByRole('button', { name: '重新发起登录' }));
  expect(assign).toHaveBeenCalledWith('/login');
});

it('没有 flow 参数、流程不存在、落点与流程的连接器不一致、上游失败都呈现错误态且不跳转', async () => {
  const assign = leave();
  render(<ConnectorPage connectorId="dex" flowId={null} provider={provider({})} />);
  await screen.findByTestId('identity-connector-error'); cleanup();
  render(<ConnectorPage connectorId="dex" flowId="f1" provider={provider({ readFlow: vi.fn(async () => null) })} />);
  await screen.findByTestId('identity-connector-error'); cleanup();
  render(<ConnectorPage connectorId="other" flowId="f1" provider={provider({ readFlow: vi.fn(async () => done) })} />);
  await screen.findByTestId('identity-connector-error'); cleanup();
  render(<ConnectorPage connectorId="dex" flowId="f1" provider={provider({ readFlow: vi.fn(async () => flow('error', { status: 'failed' })) })} />);
  const error = await screen.findByTestId('identity-connector-error');
  expect(error.textContent).toContain('上游登录失败或已失效');
  await accessible();
  expect(assign).not.toHaveBeenCalled();
});

it('登录页的连接器区：没填租户代码不发起；按填写的租户列目录并发起，上游地址只经出口跳转', async () => {
  const assign = leave();
  const listConnectors = vi.fn(async () => [{ id: 'dex', displayName: 'Dex 本机身份源', protocol: 'oidc', terminals: ['pc'] }]);
  const beginConnector = vi.fn(async () => 'https://idp.example.test/auth?state=x');
  render(<LoginPage entry={{ returnUrl, clientId: 'qian-shell' }} provider={provider({ listConnectors, beginConnector })} />);
  await userEvent.click(await screen.findByRole('button', { name: 'Dex 本机身份源' }));
  expect(beginConnector).not.toHaveBeenCalled();
  expect((await screen.findByTestId('identity-connector-tenant')).textContent).toContain('请填写租户代码');
  await userEvent.fill(screen.getByLabelText('租户代码'), 'acme');
  await waitFor(() => expect(listConnectors).toHaveBeenLastCalledWith(expect.any(AbortSignal), 'acme'));
  await userEvent.click(screen.getByRole('button', { name: 'Dex 本机身份源' }));
  await waitFor(() => expect(beginConnector).toHaveBeenCalledWith('dex', expect.any(AbortSignal), { tenant: 'acme', returnUrl }));
  await waitFor(() => expect(assign).toHaveBeenCalledWith('https://idp.example.test/auth?state=x'));
  expect(assign).toHaveBeenCalledTimes(1);
});

it('登录页的连接器区：全局目录为空也渲染租户代码；租户过滤为空时输入框不卸载并提示，填对租户后重列并可发起', async () => {
  const assign = leave();
  const corp = { id: 'corp', displayName: '甲方企业身份源', protocol: 'oidc', terminals: ['pc'] };
  const listConnectors = vi.fn(async (_signal: AbortSignal, tenant: string | null) => tenant === 'acme' ? [corp] : []);
  const beginConnector = vi.fn(async () => 'https://idp.example.test/auth?state=y');
  render(<LoginPage entry={{ returnUrl, clientId: 'qian-shell' }} provider={provider({ listConnectors, beginConnector })} />);
  await waitFor(() => expect(listConnectors).toHaveBeenCalledWith(expect.any(AbortSignal), null));
  const input = await screen.findByLabelText('租户代码');
  expect(screen.queryByTestId('identity-connectors-none')).toBeNull();
  await userEvent.fill(input, 'nobody');
  await waitFor(() => expect(listConnectors).toHaveBeenLastCalledWith(expect.any(AbortSignal), 'nobody'));
  expect((await screen.findByTestId('identity-connectors-none')).textContent).toContain('该租户没有可用的单点登录方式');
  expect(screen.getByLabelText('租户代码')).toBe(input);
  await userEvent.fill(input, 'acme');
  await userEvent.click(await screen.findByRole('button', { name: '甲方企业身份源' }));
  expect(screen.queryByTestId('identity-connectors-none')).toBeNull();
  await waitFor(() => expect(beginConnector).toHaveBeenCalledWith('corp', expect.any(AbortSignal), { tenant: 'acme', returnUrl }));
  await waitFor(() => expect(assign).toHaveBeenCalledWith('https://idp.example.test/auth?state=y'));
  await accessible();
});
