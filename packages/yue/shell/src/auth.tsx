import { lazy, Suspense, useCallback, useEffect, useRef, useState } from 'react';
import { Alert, AlertDialog, AlertDialogContent, AlertDialogDescription, AlertDialogTitle, Button, cn, Select, SelectContent, SelectItem, SelectTrigger, Spinner } from '@idadawn/yue-core';
import type { PlatformI18n } from '@idadawn/yue-sdk';
import { safeNavigationUrl } from './menu';
import { useShellLocale, type ShellPartProps } from './layout';
import type { PasswordCredentials } from './password-login';
export type { PasswordCredentials } from './password-login';

const LazyPasswordLogin = lazy(() => import('./password-login').then(module => ({ default: module.PasswordLogin })));

export interface AuthTenant { id: string; name: string }
/** UI 只消费会话展示数据；令牌、选择凭据与 PKCE verifier 始终由宿主 provider 保管。 */
export interface AuthSession { subjectId: string; displayName: string; tenantId: string; tenants: readonly AuthTenant[] }
export type AuthResult = { kind: 'authenticated'; session: AuthSession } | { kind: 'select-tenant'; tenants: readonly AuthTenant[] };
export interface AuthCallback { code?: string; state?: string; error?: string }
export interface AuthProvider {
  signIn(credentials: PasswordCredentials, signal: AbortSignal): Promise<AuthResult>;
  selectTenant(tenantId: string, signal: AbortSignal): Promise<AuthSession>;
  switchTenant(tenantId: string, signal: AbortSignal): Promise<AuthSession>;
  beginSso?(signal: AbortSignal): Promise<string>;
  completeSso?(callback: AuthCallback, signal: AbortSignal): Promise<AuthResult>;
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

export interface TenantSelectionProps extends ShellPartProps { i18n: PlatformI18n; tenants: readonly AuthTenant[]; onSelect: (id: string) => void; onBack: () => void; busy?: boolean; error?: boolean }
export function TenantSelection({ i18n, tenants, onSelect, onBack, busy, error, className, testId = 'yue-tenant-selection' }: TenantSelectionProps) {
  useShellLocale(i18n);
  return <section data-slot="tenant-selection" data-testid={testId} className={cn('mx-auto w-full max-w-md space-y-4 rounded-xl border border-border bg-card p-6', className)}>
    <h1 className="text-2xl font-semibold">{i18n.t('shell.chooseTenant')}</h1><p className="text-sm text-muted-foreground">{i18n.t('shell.chooseTenantHelp')}</p>
    {error && <Alert tone="error" role="alert">{i18n.t('shell.authError')}</Alert>}
    <ul className="space-y-2">{tenants.map(tenant => <li key={tenant.id}><Button variant="outline" className="w-full justify-start" disabled={busy} onClick={() => onSelect(tenant.id)}>{tenant.name}</Button></li>)}</ul>
    <Button variant="ghost" onClick={onBack} disabled={busy}>{i18n.t('shell.backLogin')}</Button>
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

export interface AuthenticationProps extends ShellPartProps { provider: AuthProvider; i18n: PlatformI18n; callback?: AuthCallback; onAuthenticated: (session: AuthSession) => void; onRedirect: (url: string) => void; onCallbackConsumed?: () => void }
export function Authentication({ provider, i18n, callback, onAuthenticated, onRedirect, onCallbackConsumed, className, testId = 'yue-authentication' }: AuthenticationProps) {
  const [selection, setSelection] = useState<readonly AuthTenant[] | null>(null);
  const callbackKey = callback ? JSON.stringify([callback.code, callback.state, callback.error]) : null;
  const [consumedCallbackKey, setConsumedCallbackKey] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(false);
  const [redirecting, setRedirecting] = useState(false);
  const run = useOperation(provider, callbackKey);
  const result = (value: AuthResult) => {
    setBusy(false); setError(false); setConsumedCallbackKey(callbackKey); onCallbackConsumed?.();
    if (value.kind === 'authenticated') { setSelection(null); onAuthenticated(value.session); }
    else setSelection(value.tenants);
  };
  const fail = () => { setBusy(false); setError(true); };
  const back = () => { setSelection(null); setError(false); setRedirecting(false); setConsumedCallbackKey(callbackKey); onCallbackConsumed?.(); };
  return <div data-slot="authentication" data-testid={testId} className={className}>
    {callback && callbackKey !== consumedCallbackKey ? <SsoCallback i18n={i18n} provider={provider} callback={callback} onResult={result} onBack={back} /> : selection ? <TenantSelection i18n={i18n} tenants={selection} busy={busy} error={error} onBack={back} onSelect={id => {
      if (busy || !selection.some(tenant => tenant.id === id)) return;
      setBusy(true); setError(false);
      void run(signal => provider.selectTenant(id, signal), session => result({ kind: 'authenticated', session }), fail);
    }} /> : redirecting ? <p role="status">{i18n.t('shell.ssoRedirect')}</p> : <Suspense fallback={<Spinner label={i18n.t('common.loading')} />}><LazyPasswordLogin i18n={i18n} busy={busy} error={error} onSubmit={async credentials => {
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
    } : undefined} /></Suspense>}
  </div>;
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

export function SessionExpiredDialog({ open, i18n, onSignIn, testId = 'yue-session-expired' }: { open: boolean; i18n: PlatformI18n; onSignIn: () => void; testId?: string }) {
  useShellLocale(i18n);
  return <span data-slot="session-expired" data-testid={`${testId}-root`}><AlertDialog open={open}><AlertDialogContent showCloseButton={false} testId={testId}>
    <AlertDialogTitle>{i18n.t('shell.expired')}</AlertDialogTitle><AlertDialogDescription>{i18n.t('shell.expiredHelp')}</AlertDialogDescription><Button onClick={onSignIn}>{i18n.t('shell.reauthenticate')}</Button>
  </AlertDialogContent></AlertDialog></span>;
}
