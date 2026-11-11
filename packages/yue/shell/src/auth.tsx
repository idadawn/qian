import { lazy, Suspense, useCallback, useEffect, useId, useRef, useState, type ReactNode } from 'react';
import { Alert, Avatar, AvatarFallback, avatarTint, Button, cn, Icon, Select, SelectContent, SelectItem, SelectTrigger, Spinner } from '@idadawn/yue-core';
import type { PlatformI18n } from '@idadawn/yue-sdk';
import { safeNavigationUrl } from './menu';
import { useShellLocale, type ShellPartProps } from './layout';
import type { PasswordCredentials, PasswordLoginProps } from './password-login';
export type { PasswordCredentials } from './password-login';

const LazyPasswordLogin = lazy(() => import('./password-login').then(module => ({ default: module.PasswordLogin })));
const LazyPasswordChange = lazy(() => import('./password-change').then(module => ({ default: module.PasswordChange })));

export interface AuthTenant {
  id: string; name: string;
  /** 租户号（企业代码），工作空间卡片上以等宽字显示。 */
  code?: string;
  /** 平台身份（进入平台控制台）。 */
  platform?: boolean;
  /** 在该企业里是管理员。 */
  admin?: boolean;
  /** 该身份下的显示名，选择页顶栏显示。 */
  displayName?: string;
}
/** UI 只消费会话展示数据；令牌、选择凭据与 PKCE verifier 始终由宿主 provider 保管。 */
export interface AuthSession { subjectId: string; displayName: string; tenantId: string; tenants: readonly AuthTenant[] }
/** `change-password`：初始密码或运营重置后必须先改密（票据由 provider 保管），改密成功才继续同一登录流程。 */
export type AuthResult = { kind: 'authenticated'; session: AuthSession } | { kind: 'select-tenant'; tenants: readonly AuthTenant[] } | { kind: 'change-password' };
export interface AuthCallback { code?: string; state?: string; error?: string }
export interface AuthProvider {
  signIn(credentials: PasswordCredentials, signal: AbortSignal): Promise<AuthResult>;
  selectTenant(tenantId: string, signal: AbortSignal): Promise<AuthSession>;
  switchTenant(tenantId: string, signal: AbortSignal): Promise<AuthSession>;
  beginSso?(signal: AbortSignal): Promise<string>;
  completeSso?(callback: AuthCallback, signal: AbortSignal): Promise<AuthResult>;
  /** 强制改密；没有实现的 provider 收到 change-password 结果时认证页按失败处理。 */
  changePassword?(newPassword: string, signal: AbortSignal): Promise<AuthResult>;
}

/** 请求竞态与卸载只取消当前视图；真正的身份校验由 provider / 服务端负责。 */
function useOperation(owner: AuthProvider, scope?: string | null) {
  const current = useRef<AbortController | null>(null);
  useEffect(() => () => current.current?.abort(), [owner, scope]);
  return useCallback(async <T,>(operation: (signal: AbortSignal) => Promise<T>, success: (value: T) => void, failed: () => void) => {
    current.current?.abort();
    const controller = new AbortController();
    current.current = controller;
    try { const value = await operation(controller.signal); if (!controller.signal.aborted) success(value); }
    catch { if (!controller.signal.aborted) failed(); }
  }, []);
}

export interface TenantSelectionProps extends ShellPartProps {
  i18n: PlatformI18n; tenants: readonly AuthTenant[]; onSelect: (id: string) => void; onBack: () => void; busy?: boolean; error?: boolean;
  /** 「申请开通新企业」的地址；不传不显示虚线入口。 */
  applyHref?: string;
  /** 不在列表下方显示「返回登录」（宿主把它放进页面顶栏时）。 @default false */
  hideBack?: boolean;
}
function initialOf(name: string): string { return Array.from(name.trim())[0]?.toUpperCase() ?? '?'; }
/** 选择工作空间：平台控制台与各企业是一张张卡片，卡片名即按钮的可访问名称，租户号与角色作为说明。 */
export function TenantSelection({ i18n, tenants, onSelect, onBack, busy, error, applyHref, hideBack = false, className, testId = 'yue-tenant-selection' }: TenantSelectionProps) {
  useShellLocale(i18n);
  const id = useId();
  const companies = tenants.filter(tenant => !tenant.platform).length;
  const help = tenants.some(tenant => tenant.platform) ? i18n.t('shell.chooseWorkspacePlatformHelp', { count: companies }) : i18n.t('shell.chooseWorkspaceHelp');
  return <section data-slot="tenant-selection" data-testid={testId} className={cn('flex w-full flex-col gap-6', className)}>
    <div className="flex flex-col gap-1.5"><h1 className="m-0 text-[26px] font-bold leading-8.5 tracking-[-0.01em]">{i18n.t('shell.chooseWorkspace')}</h1><p className="m-0 text-muted-foreground">{help}</p></div>
    {error && <Alert tone="error" role="alert" size="sm">{i18n.t('shell.authError')}</Alert>}
    <ul className="m-0 flex list-none flex-col gap-3 p-0">
      {tenants.map((tenant, index) => {
        const name = tenant.platform ? i18n.t('shell.platformConsole') : tenant.name;
        // 卡片整块可点：按钮只包企业名（可访问名称与可见标签一致），租户号与角色在按钮外、经 aria-describedby 读出；按钮的 ::after 铺满卡片承接点击。
        return <li key={tenant.id} className={cn('relative flex w-full items-center gap-4 rounded-xl border px-4.5 py-4 text-foreground transition-[border-color,box-shadow] duration-fast hover:border-primary-border-strong hover:shadow-[0_4px_12px_rgb(37_99_201/0.1)] has-[button:focus-visible]:outline-2 has-[button:focus-visible]:outline-offset-2 has-[button:focus-visible]:outline-ring has-[button:disabled]:pointer-events-none has-[button:disabled]:opacity-60 motion-reduce:transition-none', tenant.platform ? 'border-primary-border bg-primary-subtle' : 'border-border bg-card')}>
          {tenant.platform ? <span aria-hidden="true" className="inline-flex size-11 shrink-0 items-center justify-center rounded-lg bg-primary text-primary-foreground"><Icon name="shield" size={22} /></span>
            : <Avatar aria-hidden="true" shape="square" tint={avatarTint(tenant.code ?? tenant.id)} className="size-11 rounded-lg"><AvatarFallback className="text-lg font-bold">{initialOf(tenant.name)}</AvatarFallback></Avatar>}
          <span className="flex min-w-0 flex-1 flex-col">
            <button type="button" disabled={busy} onClick={() => onSelect(tenant.id)} aria-describedby={`${id}-${index}-meta`} data-slot="tenant-selection-option" data-testid={`${testId}-option`}
              className="m-0 truncate border-0 bg-transparent p-0 text-start text-[15px] font-semibold text-foreground outline-none after:absolute after:inset-0 after:rounded-xl after:content-['']">{name}</button>
            <span id={`${id}-${index}-meta`} className="truncate text-[13px] text-caption-foreground">{tenant.platform ? i18n.t('shell.platformConsoleHelp') : <>{tenant.code && <><span className="font-mono">{tenant.code}</span><span aria-hidden="true"> · </span></>}{i18n.t(tenant.admin ? 'shell.role.admin' : 'shell.role.member')}</>}</span>
          </span>
          <Icon name="chevronRight" size={18} className="text-muted-foreground rtl:rotate-180" />
        </li>;
      })}
      {applyHref && <li><a href={applyHref} data-slot="tenant-selection-apply" data-testid={`${testId}-apply`} className="flex h-14 items-center justify-center gap-2 rounded-xl border border-dashed border-input/55 text-subtle-foreground no-underline transition-colors duration-fast hover:border-primary-border-strong hover:text-foreground focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring motion-reduce:transition-none"><Icon name="plus" size={18} />{i18n.t('shell.applyWorkspace')}</a></li>}
    </ul>
    <p className="m-0 text-[13px] text-muted-foreground">{i18n.t('shell.workspaceSwitchHelp')}</p>
    {!hideBack && <div><Button variant="ghost" onClick={onBack} disabled={busy} testId={`${testId}-back`}>{i18n.t('shell.backLogin')}</Button></div>}
  </section>;
}

export interface SsoCallbackProps extends ShellPartProps { i18n: PlatformI18n; provider: AuthProvider; callback: AuthCallback; onResult: (result: AuthResult) => void; onBack: () => void }
export function SsoCallback({ i18n, provider, callback, onResult, onBack, className, testId = 'yue-sso-callback' }: SsoCallbackProps) {
  useShellLocale(i18n);
  const [failedFor, setFailedFor] = useState<{ provider: AuthProvider; code?: string; state?: string } | null>(null);
  const invalid = !!callback.error || !callback.code || !callback.state || !provider.completeSso;
  const failed = invalid || (failedFor?.provider === provider && failedFor?.code === callback.code && failedFor?.state === callback.state);
  const notify = useRef(onResult);
  useEffect(() => { notify.current = onResult; }, [onResult]);
  useEffect(() => {
    const controller = new AbortController();
    if (callback.error || !callback.code || !callback.state || !provider.completeSso) return;
    void provider.completeSso({ code: callback.code, state: callback.state }, controller.signal)
      .then(result => { if (!controller.signal.aborted) notify.current(result); })
      .catch(() => { if (!controller.signal.aborted) setFailedFor({ provider, code: callback.code, state: callback.state }); });
    return () => controller.abort();
  }, [provider, callback.code, callback.state, callback.error]);
  return <section data-slot="sso-callback" data-testid={testId} className={cn('mx-auto max-w-md space-y-4 rounded-xl border border-border bg-card p-6', className)}>
    <h1 className="text-xl font-semibold">{i18n.t(failed ? 'shell.ssoError' : 'shell.callback')}</h1>
    {failed ? <><Alert tone="error" role="alert">{i18n.t('shell.ssoError')}</Alert><Button onClick={onBack}>{i18n.t('shell.backLogin')}</Button></> : <Spinner label={i18n.t('common.loading')} />}
  </section>;
}

/** 认证流程当前的视图：宿主据此套不同的页面框架（登录页与选择工作空间页布局不同）。 */
export type AuthenticationView = 'login' | 'select-tenant' | 'change-password' | 'callback' | 'redirecting';
export interface AuthenticationProps extends ShellPartProps {
  provider: AuthProvider; i18n: PlatformI18n; callback?: AuthCallback; onAuthenticated: (session: AuthSession) => void; onRedirect: (url: string) => void; onCallbackConsumed?: () => void;
  /** 按视图包一层页面框架（如 AuthLayout、PlainLayout）；缺省时内容限制在原型列宽里居中：登录等视图 400px，选择工作空间 600px，不铺满宿主容器。 */
  frame?: (view: AuthenticationView, content: ReactNode, context: { tenants: readonly AuthTenant[] | null; back: () => void }) => ReactNode;
  /** 透传给密码登录的附加项：记住账号、忘记密码、其他登录方式与表单下方内容。 */
  loginProps?: Pick<PasswordLoginProps, 'title' | 'description' | 'rememberKey' | 'forgotHref' | 'alternatives' | 'footer'>;
  /** 透传给选择工作空间的附加项：「申请开通新企业」地址、是否隐藏列表下方的「返回登录」。 */
  selectionProps?: Pick<TenantSelectionProps, 'applyHref' | 'hideBack'>;
}
export function Authentication({ provider, i18n, callback, onAuthenticated, onRedirect, onCallbackConsumed, frame, loginProps, selectionProps, className, testId = 'yue-authentication' }: AuthenticationProps) {
  const [selection, setSelection] = useState<readonly AuthTenant[] | null>(null);
  const [changing, setChanging] = useState(false);
  const callbackKey = callback ? JSON.stringify([callback.code, callback.state, callback.error]) : null;
  const [consumedCallbackKey, setConsumedCallbackKey] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(false);
  const [redirecting, setRedirecting] = useState(false);
  const run = useOperation(provider, callbackKey);
  const result = (value: AuthResult) => {
    setBusy(false); setError(false); setConsumedCallbackKey(callbackKey); onCallbackConsumed?.();
    if (value.kind === 'authenticated') { setSelection(null); setChanging(false); onAuthenticated(value.session); }
    else if (value.kind === 'select-tenant') { setChanging(false); setSelection(value.tenants); }
    else if (provider.changePassword) { setSelection(null); setChanging(true); }
    else setError(true);
  };
  const fail = () => { setBusy(false); setError(true); };
  const back = () => { setSelection(null); setChanging(false); setError(false); setRedirecting(false); setConsumedCallbackKey(callbackKey); onCallbackConsumed?.(); };
  const view: AuthenticationView = callback && callbackKey !== consumedCallbackKey ? 'callback' : changing ? 'change-password' : selection ? 'select-tenant' : redirecting ? 'redirecting' : 'login';
  const content = view === 'callback' ? <SsoCallback i18n={i18n} provider={provider} callback={callback!} onResult={result} onBack={back} />
    : view === 'change-password' ? <Suspense fallback={<Spinner label={i18n.t('common.loading')} />}><LazyPasswordChange i18n={i18n} busy={busy} error={error} onSubmit={async newPassword => {
      if (busy) return;
      setBusy(true); setError(false);
      await run(signal => provider.changePassword!(newPassword, signal), result, fail);
    }} /></Suspense>
    : view === 'select-tenant' ? <TenantSelection {...selectionProps} i18n={i18n} tenants={selection!} busy={busy} error={error} onBack={back} onSelect={id => {
      if (busy || !selection!.some(tenant => tenant.id === id)) return;
      setBusy(true); setError(false);
      void run(signal => provider.selectTenant(id, signal), session => result({ kind: 'authenticated', session }), fail);
    }} />
    : view === 'redirecting' ? <p role="status">{i18n.t('shell.ssoRedirect')}</p>
    : <Suspense fallback={<Spinner label={i18n.t('common.loading')} />}><LazyPasswordLogin {...loginProps} i18n={i18n} busy={busy} error={error} onSubmit={async credentials => {
      if (busy) return;
      setBusy(true); setError(false);
      await run(signal => provider.signIn(credentials, signal), result, fail);
    }} onSso={provider.beginSso ? () => {
      if (busy) return;
      setBusy(true); setError(false);
      void run(async signal => {
        const url = await provider.beginSso!(signal);
        return safeNavigationUrl(url, !url.startsWith('/'));
      }, url => { setBusy(false); setRedirecting(true); try { onRedirect(url); } catch { setRedirecting(false); fail(); } }, fail);
    } : undefined} /></Suspense>;
  const framed = frame ? frame(view, content, { tenants: selection, back })
    : view === 'callback' ? content
    : <div data-slot="authentication-column" className={cn('mx-auto w-full', view === 'select-tenant' ? 'max-w-150' : 'max-w-100')}>{content}</div>;
  return <div data-slot="authentication" data-view={view} data-testid={testId} className={className}>{framed}</div>;
}

export interface TenantSwitcherProps extends ShellPartProps { i18n: PlatformI18n; provider: AuthProvider; session: AuthSession; onChanged: (session: AuthSession) => void }
export function TenantSwitcher({ i18n, provider, session, onChanged, className, testId = 'yue-tenant-switcher' }: TenantSwitcherProps) {
  useShellLocale(i18n);
  const [busy, setBusy] = useState(false);
  const [failed, setFailed] = useState(false);
  const run = useOperation(provider);
  return <div data-slot="tenant-switcher" data-testid={testId} className={cn('space-y-2', className)}><Select items={session.tenants.map(tenant => ({ value: tenant.id, label: tenant.name }))} value={session.tenantId} disabled={busy} onValueChange={id => {
    if (!id || busy || id === session.tenantId || !session.tenants.some(tenant => tenant.id === id)) return;
    setBusy(true); setFailed(false);
    void run(signal => provider.switchTenant(id, signal), next => { setBusy(false); onChanged(next); }, () => { setBusy(false); setFailed(true); });
  }}><SelectTrigger aria-label={i18n.t('shell.tenant')} testId={`${testId}-trigger`} /><SelectContent listLabel={i18n.t('shell.tenant')}>{session.tenants.map(tenant => <SelectItem key={tenant.id} value={tenant.id}>{tenant.name}</SelectItem>)}</SelectContent></Select>{failed && <p role="alert" className="text-sm text-destructive">{i18n.t('shell.switchError')}</p>}</div>;
}
