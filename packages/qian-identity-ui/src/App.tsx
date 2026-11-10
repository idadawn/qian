import { useEffect, useId, useMemo, useState, type ReactNode } from 'react';
import { Alert, Avatar, AvatarFallback, Button, Field, FieldError, FieldLabel, Icon, Input, Spinner } from '@idadawn/yue-core';
import { HttpResponseError, PlatformError } from '@idadawn/yue-sdk';
import { Authentication, AuthLayout, PasswordLogin, PlainLayout, PreferenceControls, PreferencesProvider, safeNavigationUrl, TenantSelection, type AuthTenant } from '@idadawn/yue-shell';
import { createIdentityAuthProvider, type ConnectorFlow, type IdentityAuthProvider, type IdentityConnector } from '@qian/shell-kit/identity';
import { authorizeEntry, loginUrl, type AuthorizeEntry } from './entry';
import { i18n } from './i18n';
import { browser } from './navigation';
import { preferences } from './preferences';

/** 记住账号存在本机的键；只存账号，不存密码。 */
const REMEMBER_KEY = 'qian.identity.remember';

/** 页面只有三个落点：/login、/login/select-tenant、/login/connector/{connectorId}；其余路径按无效入口呈现。 */
export function App() {
  const pathname = location.pathname.replace(/\/+$/, '') || '/';
  const connector = /^\/login\/connector\/([^/]+)$/.exec(pathname);
  if (pathname === '/login') return <LoginPage />;
  if (pathname === '/login/select-tenant') return <SelectTenantPage />;
  return <SignInFrame>{connector ? <ConnectorPage connectorId={decodeURIComponent(connector[1]!)} flowId={new URLSearchParams(location.search).get('flow')} /> : <InvalidEntry />}</SignInFrame>;
}

/** 登录一类页面的框架：左侧品牌深色面，右上语言与主题。 */
function SignInFrame({ children }: { children: ReactNode }) {
  return <PreferencesProvider store={preferences}><AuthLayout brand={i18n.t('identity.brand')} headline={i18n.t('identity.headline')} description={i18n.t('identity.tagline')}
    toolbar={<PreferenceControls i18n={i18n} localeStyle="label" />} footer={<span>{i18n.t('identity.copyright')}</span>}>{children}</AuthLayout></PreferencesProvider>;
}

/** 选择工作空间的框架：顶栏右侧是当前账号与「返回登录」。 */
function WorkspaceFrame({ tenants, onBack, children }: { tenants: readonly AuthTenant[]; onBack: () => void; children: ReactNode }) {
  const name = tenants.find(tenant => tenant.displayName)?.displayName;
  return <PlainLayout brand={i18n.t('identity.brand')} account={<>
    {name && <span className="inline-flex items-center gap-2" data-testid="identity-account"><Avatar aria-hidden="true" tint="primary" className="size-7.5"><AvatarFallback className="text-[13px] font-bold">{Array.from(name.trim())[0]}</AvatarFallback></Avatar><span>{name}</span></span>}
    <Button variant="ghost" className="h-8.5 gap-1.5 px-2.5 font-normal" onClick={onBack} testId="identity-back-login"><Icon name="logOut" />{i18n.t('shell.backLogin')}</Button>
  </>}>{children}</PlainLayout>;
}

function InvalidEntry() {
  return <section role="alert" data-testid="identity-invalid-entry" className="flex flex-col gap-1.5">
    <h1 className="m-0 text-[28px] font-bold leading-9 tracking-[-0.01em]">{i18n.t('identity.invalidEntry')}</h1><p className="m-0 text-muted-foreground">{i18n.t('identity.invalidEntryHelp')}</p>
  </section>;
}

function useProvider(entry: AuthorizeEntry | null): IdentityAuthProvider {
  return useMemo(() => createIdentityAuthProvider({ clientId: entry?.clientId ?? 'unknown', platformLabel: i18n.t('shell.platform') }), [entry?.clientId]);
}

/**
 * 自助开通组件是否装在本宿主（S1-TEN-020，规格 §8）：申请人会话端点答 200 或 401 才显示开通入口。
 * `next` 带上当前登录地址，申请获批后进度页的「去登录」回到同一个授权请求。
 */
function useSignup(): { apply: string; progress: string } | null {
  const [available, setAvailable] = useState(false);
  useEffect(() => {
    const controller = new AbortController();
    fetch('/api/signup/session', { credentials: 'same-origin', redirect: 'error', signal: controller.signal })
      .then(response => { void response.body?.cancel(); if (!controller.signal.aborted) setAvailable(response.status === 200 || response.status === 401); }, () => { /* 没有开通组件时不显示入口。 */ });
    return () => controller.abort();
  }, []);
  if (!available) return null;
  const next = encodeURIComponent(location.pathname + location.search);
  return { apply: `/signup?next=${next}`, progress: `/signup/status?next=${next}` };
}

/** 登录页下方的开通入口；没有登录成员身份的账号（NO_AVAILABLE_TENANT）从「查看申请进度」进入进度页。 */
function SignupLinks({ signup }: { signup: { apply: string; progress: string } }) {
  return <p className="m-0 text-center text-subtle-foreground" data-testid="identity-signup-links">
    {i18n.t('identity.noCompany')}<a className="font-semibold text-primary no-underline hover:underline" href={signup.apply}>{i18n.t('identity.applyCompany')}</a>
    <span aria-hidden="true" className="text-muted-foreground"> · </span><a className="text-primary no-underline hover:underline" href={signup.progress}>{i18n.t('identity.signupProgress')}</a>
  </p>;
}

/**
 * 「其他登录方式」：连接器登录的绑定键是 租户 × 连接器 × 上游主体，发起前必须知道租户。「单点登录」展开面板：租户代码在上，目录先按全局列出；
 * 填了租户代码就按该租户重列（租户项遮蔽全局同名项），输入框不随目录变空而卸载。没填租户代码就点连接器只提示并聚焦租户代码，不发起。
 */
function Connectors({ entry, provider }: { entry: AuthorizeEntry; provider: IdentityAuthProvider }) {
  const panelId = useId();
  const [open, setOpen] = useState(false);
  const [tenant, setTenant] = useState('');
  const [tenantMissing, setTenantMissing] = useState(false);
  const [connectors, setConnectors] = useState<IdentityConnector[]>([]);
  // 最近一次目录结果对应的租户代码：填了租户、该租户的目录也已回来却为空时才提示「没有可用的单点登录方式」，防抖途中不提示。
  const [listedFor, setListedFor] = useState<string | null>(null);
  const [connectorError, setConnectorError] = useState(false);
  const tenantCode = tenant.trim();
  useEffect(() => {
    const controller = new AbortController();
    const timer = setTimeout(() => {
      provider.listConnectors(controller.signal, tenantCode || null).then(items => { if (!controller.signal.aborted) { setConnectors(items); setListedFor(tenantCode); } }).catch(() => { /* 连接器目录不可用时只剩账号密码登录。 */ });
    }, tenantCode ? 300 : 0);
    return () => { clearTimeout(timer); controller.abort(); };
  }, [provider, tenantCode]);
  const begin = (connectorId: string) => {
    setConnectorError(false);
    if (!tenantCode) { setTenantMissing(true); document.getElementById(`${panelId}-tenant`)?.focus(); return; }
    provider.beginConnector(connectorId, new AbortController().signal, { tenant: tenantCode, returnUrl: entry.returnUrl }).then(url => browser.assign(url)).catch(() => setConnectorError(true));
  };
  return <div className="flex flex-col gap-3">
    <Button variant="outline" size="xl" className="w-full [&_svg]:size-4.5" aria-expanded={open} aria-controls={panelId} onClick={() => setOpen(value => !value)} testId="identity-sso"><Icon name="key" />{i18n.t('shell.sso')}</Button>
    {open && <section id={panelId} aria-label={i18n.t('identity.connectors')} data-testid="identity-connectors" className="flex flex-col gap-3 rounded-lg border border-border bg-muted/50 p-4">
      {connectorError && <Alert tone="error" role="alert" size="sm">{i18n.t('identity.connectorError')}</Alert>}
      <Field invalid={tenantMissing} testId="identity-connector-tenant">
        <FieldLabel htmlFor={`${panelId}-tenant`}>{i18n.t('identity.tenantCode')}</FieldLabel>
        <Input id={`${panelId}-tenant`} name="tenant" autoComplete="organization" className="bg-background font-mono" value={tenant} onChange={event => { setTenant(event.currentTarget.value); if (event.currentTarget.value.trim()) setTenantMissing(false); }} aria-describedby={tenantMissing ? undefined : `${panelId}-tenant-help`} />
        {tenantMissing ? <FieldError match>{i18n.t('identity.tenantRequired')}</FieldError> : <p id={`${panelId}-tenant-help`} className="m-0 text-xs leading-4.5 text-muted-foreground">{i18n.t('identity.tenantCodeHelp')}</p>}
      </Field>
      {connectors.length > 0
        ? <div className="flex flex-wrap gap-2">{connectors.map(item => <Button key={item.id} variant="outline" size="lg" className="flex-[1_1_160px] bg-background" onClick={() => begin(item.id)}>{item.displayName}</Button>)}</div>
        : tenantCode !== '' && listedFor === tenantCode && <p role="status" className="m-0 text-[13px] text-muted-foreground" data-testid="identity-connectors-none">{i18n.t('identity.connectorsNone')}</p>}
    </section>}
  </div>;
}

/** 登录页；`entry` / `provider` 可注入（组件测试），缺省取自地址栏与同源账号接口。 */
export function LoginPage({ entry: injectedEntry, provider: injectedProvider }: { entry?: AuthorizeEntry | null; provider?: IdentityAuthProvider } = {}) {
  const entry = useMemo(() => injectedEntry === undefined ? authorizeEntry(location.search, location.origin) : injectedEntry, [injectedEntry]);
  const created = useProvider(entry);
  const provider = injectedProvider ?? created;
  const signup = useSignup();
  if (!entry) return <SignInFrame><InvalidEntry /></SignInFrame>;
  return <Authentication provider={provider} i18n={i18n} onAuthenticated={() => browser.assign(entry.returnUrl)} onRedirect={url => browser.assign(url)} selectionProps={{ applyHref: signup?.apply, hideBack: true }}
    loginProps={{ description: i18n.t('identity.loginHelp'), rememberKey: REMEMBER_KEY, alternatives: <Connectors entry={entry} provider={provider} />, footer: signup ? <SignupLinks signup={signup} /> : undefined }}
    frame={(view, content, { tenants, back }) => view === 'select-tenant' && tenants ? <WorkspaceFrame tenants={tenants} onBack={back}>{content}</WorkspaceFrame> : <SignInFrame>{content}</SignInFrame>} />;
}

function unauthenticated(error: unknown): boolean {
  return (error instanceof PlatformError && error.status === 401) || (error instanceof HttpResponseError && error.status === 401);
}

/** 外壳切换租户：授权端点带 prompt=select_account 且账号 Cookie 仍有效时 302 到这里；换会话后回到去掉 prompt 的授权请求签新授权码。 */
function SelectTenantPage() {
  const entry = useMemo(() => authorizeEntry(location.search, location.origin), []);
  const provider = useProvider(entry);
  const signup = useSignup();
  const [tenants, setTenants] = useState<readonly AuthTenant[] | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(false);
  useEffect(() => {
    if (!entry) return;
    const controller = new AbortController();
    provider.beginSwitch(controller.signal).then(items => { if (!controller.signal.aborted) setTenants(items); }).catch(failure => {
      if (controller.signal.aborted) return;
      // 账号 Cookie 已失效：回登录页，登录完成后同样回到授权请求。
      if (unauthenticated(failure)) browser.replace(loginUrl(entry.returnUrl)); else setError(true);
    });
    return () => controller.abort();
  }, [provider, entry]);
  if (!entry) return <SignInFrame><InvalidEntry /></SignInFrame>;
  const back = () => browser.assign(loginUrl(entry.returnUrl));
  if (!tenants) return <WorkspaceFrame tenants={[]} onBack={back}>{error
    ? <div className="flex flex-col gap-3"><Alert tone="error" role="alert">{i18n.t('shell.switchError')}</Alert><div><Button variant="outline" onClick={back}>{i18n.t('shell.backLogin')}</Button></div></div>
    : <Spinner label={i18n.t('shell.switching')} />}</WorkspaceFrame>;
  return <WorkspaceFrame tenants={tenants} onBack={back}>
    <TenantSelection i18n={i18n} tenants={tenants} busy={busy} error={error} hideBack applyHref={signup?.apply} onBack={back} onSelect={id => {
      if (busy || !tenants.some(tenant => tenant.id === id)) return;
      setBusy(true); setError(false);
      provider.selectTenant(id, new AbortController().signal).then(() => browser.assign(entry.returnUrl)).catch(() => { setBusy(false); setError(true); });
    }} />
  </WorkspaceFrame>;
}

export interface ConnectorPageProps {
  connectorId: string;
  /** 落点地址里的 `flow` 参数；没有就是错误态。 */
  flowId: string | null;
  /** 组件测试注入；缺省对接同源账号接口。 */
  provider?: IdentityAuthProvider;
  /** 等待态重读流程的间隔（毫秒），缺省 1.5 秒。 */
  pollInterval?: number;
}

/**
 * 连接器统一回调的界面落点：按流程的 next 驱动等待（轮询）、确认绑定（未验证先用账号密码验证，再绑定到已验证账号 / JIT 建号 / 放弃）、
 * 完成（回流程里的 return_url，即授权请求地址）与错误（重新发起登录）。绑定成功的响应不带去处，所以再读一次流程拿到 return_url 后才离开。
 */
export function ConnectorPage({ connectorId, flowId, provider: injectedProvider, pollInterval = 1500 }: ConnectorPageProps) {
  const created = useProvider(null);
  const provider = injectedProvider ?? created;
  // 没有 flow 参数就直接是错误态；有则先读一次，wait 时按间隔重读。
  const [flow, setFlow] = useState<ConnectorFlow | null | undefined>(flowId ? undefined : null);
  const [busy, setBusy] = useState(false);
  const [failed, setFailed] = useState(false);
  useEffect(() => {
    if (!flowId) return;
    const controller = new AbortController();
    let timer: ReturnType<typeof setTimeout> | undefined;
    const read = () => provider.readFlow(flowId, controller.signal).then(value => {
      if (controller.signal.aborted) return;
      setFlow(value);
      if (value?.next === 'wait') timer = setTimeout(read, pollInterval);
    }).catch(() => { if (!controller.signal.aborted) setFlow(null); });
    void read();
    return () => { controller.abort(); if (timer) clearTimeout(timer); };
  }, [provider, flowId, pollInterval]);
  const returnUrl = useMemo(() => { try { return flow?.returnUrl ? safeNavigationUrl(flow.returnUrl) : null; } catch { return null; } }, [flow]);
  const next = flow?.next; const matched = flow?.connector.id === connectorId;
  useEffect(() => { if (next === 'done' && matched) browser.assign(returnUrl ?? '/login'); }, [next, matched, returnUrl]);
  if (flow === undefined) return <Spinner label={i18n.t('common.loading')} />;
  const restart = <Button variant="ghost" onClick={() => browser.assign(loginUrl(returnUrl))}>{i18n.t('identity.restart')}</Button>;
  if (flow === null || flow.connector.id !== connectorId || flow.next === 'error' || flow.next === 'restart') return <section className="mx-auto w-full max-w-md space-y-3 rounded-xl border border-border bg-card p-6" data-testid="identity-connector-error">
    <h1 className="text-xl font-semibold">{flow?.connector.displayName ?? connectorId}</h1><Alert tone="error" role="alert">{i18n.t(flow?.next === 'restart' ? 'shell.ssoError' : 'identity.flowError')}</Alert>{restart}
  </section>;
  if (flow.next === 'wait' || flow.next === 'done') return <section className="mx-auto w-full max-w-md space-y-3 rounded-xl border border-border bg-card p-6"><h1 className="text-xl font-semibold">{flow.connector.displayName ?? connectorId}</h1><Spinner label={i18n.t('identity.waiting')} /><p className="text-sm text-muted-foreground">{i18n.t('identity.waiting')}</p></section>;
  const binding = flow.binding;
  // 绑定建立了账号会话（authenticated）或放弃了流程（null）之后都再读一次流程：完成态带 return_url，由上面的效果跳回授权请求；放弃即 restart。
  const finish = (next: Awaited<ReturnType<IdentityAuthProvider['flowBind']>>) => {
    if (next?.kind === 'authenticated' || next === null) provider.readFlow(flow.flow, new AbortController().signal).then(setFlow).catch(() => setFailed(true));
    else setFailed(true);
  };
  const bind = (mode: 'link' | 'create' | 'cancel') => {
    if (busy) return;
    setBusy(true); setFailed(false);
    provider.flowBind(flow.flow, mode, new AbortController().signal).then(finish).catch(() => setFailed(true)).finally(() => setBusy(false));
  };
  return <section className="mx-auto w-full max-w-md space-y-4 rounded-xl border border-border bg-card p-6" data-testid="identity-connector-bind">
    <h1 className="text-xl font-semibold">{i18n.t('identity.bindTitle')}</h1>
    <p className="text-sm text-muted-foreground">{i18n.t('identity.bindHelp', { name: binding?.external.displayName ?? binding?.external.email ?? binding?.external.username ?? flow.connector.displayName ?? connectorId })}</p>
    {failed && <Alert tone="error" role="alert">{i18n.t('identity.flowError')}</Alert>}
    {binding && !binding.verified && !binding.account ? <>
      <p className="text-sm text-muted-foreground">{i18n.t('identity.verifyHelp')}</p>
      <PasswordLogin i18n={i18n} busy={busy} error={failed} onSubmit={async credentials => {
        if (busy) return;
        setBusy(true); setFailed(false);
        try { setFlow(await provider.flowSignIn(flow.flow, credentials, new AbortController().signal)); } catch { setFailed(true); } finally { setBusy(false); }
      }} />
      {binding.allowJit && <Button className="w-full" variant="outline" disabled={busy} onClick={() => bind('create')}>{i18n.t('identity.bindCreate')}</Button>}
    </> : <div className="flex flex-col gap-2">
      {binding?.account && <Button disabled={busy} onClick={() => bind('link')}>{i18n.t('identity.bindLink')}{binding.account.displayName ? `：${binding.account.displayName}` : ''}</Button>}
      {binding?.allowJit && <Button variant="outline" disabled={busy} onClick={() => bind('create')}>{i18n.t('identity.bindCreate')}</Button>}
    </div>}
    <Button variant="ghost" disabled={busy} onClick={() => bind('cancel')}>{i18n.t('identity.bindCancel')}</Button>
  </section>;
}
