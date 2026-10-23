import { useEffect, useMemo, useState } from 'react';
import { Alert, Button, Spinner } from '@idadawn/yue-core';
import { HttpResponseError, PlatformError } from '@idadawn/yue-sdk';
import { Authentication, createIdentityAuthProvider, PasswordLogin, safeNavigationUrl, TenantSelection, type AuthTenant, type ConnectorFlow, type IdentityAuthProvider, type IdentityConnector } from '@idadawn/yue-shell';
import { authorizeEntry, loginUrl, type AuthorizeEntry } from './entry';
import { i18n } from './i18n';

/** 页面只有三个落点：/login、/login/select-tenant、/login/connector/{connectorId}；其余路径按无效入口呈现。 */
export function App() {
  const pathname = location.pathname.replace(/\/+$/, '') || '/';
  const connector = /^\/login\/connector\/([^/]+)$/.exec(pathname);
  return <main className="flex min-h-dvh items-start justify-center bg-background p-4 text-foreground sm:items-center">
    {pathname === '/login' ? <LoginPage /> : pathname === '/login/select-tenant' ? <SelectTenantPage /> : connector ? <ConnectorPage connectorId={decodeURIComponent(connector[1]!)} /> : <InvalidEntry />}
  </main>;
}

function InvalidEntry() {
  return <section role="alert" data-testid="identity-invalid-entry" className="mx-auto w-full max-w-md space-y-3 rounded-xl border border-border bg-card p-6">
    <h1 className="text-xl font-semibold">{i18n.t('identity.invalidEntry')}</h1><p className="text-sm text-muted-foreground">{i18n.t('identity.invalidEntryHelp')}</p>
  </section>;
}

function useProvider(entry: AuthorizeEntry | null): IdentityAuthProvider {
  return useMemo(() => createIdentityAuthProvider({ clientId: entry?.clientId ?? 'unknown', platformLabel: i18n.t('shell.platform') }), [entry?.clientId]);
}

function LoginPage() {
  const entry = useMemo(() => authorizeEntry(location.search, location.origin), []);
  const provider = useProvider(entry);
  const [connectors, setConnectors] = useState<IdentityConnector[]>([]);
  const [connectorError, setConnectorError] = useState(false);
  useEffect(() => {
    if (!entry) return;
    const controller = new AbortController();
    provider.listConnectors(controller.signal).then(items => { if (!controller.signal.aborted) setConnectors(items); }).catch(() => { /* 连接器目录不可用时只剩账号密码登录。 */ });
    return () => controller.abort();
  }, [provider, entry]);
  if (!entry) return <InvalidEntry />;
  const begin = (connectorId: string) => {
    setConnectorError(false);
    provider.beginConnector(connectorId, new AbortController().signal, { returnUrl: entry.returnUrl }).then(url => location.assign(url)).catch(() => setConnectorError(true));
  };
  return <div className="w-full max-w-md space-y-4">
    <Authentication provider={provider} i18n={i18n} onAuthenticated={() => location.assign(entry.returnUrl)} onRedirect={url => location.assign(url)} />
    {connectors.length > 0 && <section aria-label={i18n.t('identity.connectors')} data-testid="identity-connectors" className="space-y-2 rounded-xl border border-border bg-card p-4">
      <p className="text-sm text-muted-foreground">{i18n.t('identity.connectors')}</p>
      {connectorError && <Alert tone="error" role="alert">{i18n.t('identity.connectorError')}</Alert>}
      <div className="flex flex-wrap gap-2">{connectors.map(item => <Button key={item.id} variant="outline" onClick={() => begin(item.id)}>{item.displayName}</Button>)}</div>
    </section>}
  </div>;
}

function unauthenticated(error: unknown): boolean {
  return (error instanceof PlatformError && error.status === 401) || (error instanceof HttpResponseError && error.status === 401);
}

/** 外壳切换租户：授权端点带 prompt=select_account 且账号 Cookie 仍有效时 302 到这里；换会话后回到去掉 prompt 的授权请求签新授权码。 */
function SelectTenantPage() {
  const entry = useMemo(() => authorizeEntry(location.search, location.origin), []);
  const provider = useProvider(entry);
  const [tenants, setTenants] = useState<readonly AuthTenant[] | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(false);
  useEffect(() => {
    if (!entry) return;
    const controller = new AbortController();
    provider.beginSwitch(controller.signal).then(items => { if (!controller.signal.aborted) setTenants(items); }).catch(failure => {
      if (controller.signal.aborted) return;
      // 账号 Cookie 已失效：回登录页，登录完成后同样回到授权请求。
      if (unauthenticated(failure)) location.replace(loginUrl(entry.returnUrl)); else setError(true);
    });
    return () => controller.abort();
  }, [provider, entry]);
  if (!entry) return <InvalidEntry />;
  if (!tenants) return error
    ? <section className="mx-auto w-full max-w-md space-y-3 rounded-xl border border-border bg-card p-6"><Alert tone="error" role="alert">{i18n.t('shell.switchError')}</Alert><Button variant="ghost" onClick={() => location.assign(loginUrl(entry.returnUrl))}>{i18n.t('shell.backLogin')}</Button></section>
    : <Spinner label={i18n.t('shell.switching')} />;
  return <div className="w-full max-w-md space-y-2">
    <TenantSelection i18n={i18n} tenants={tenants} busy={busy} error={error} onBack={() => location.assign(loginUrl(entry.returnUrl))} onSelect={id => {
      if (busy || !tenants.some(tenant => tenant.id === id)) return;
      setBusy(true); setError(false);
      provider.selectTenant(id, new AbortController().signal).then(() => location.assign(entry.returnUrl)).catch(() => { setBusy(false); setError(true); });
    }} />
    <p className="text-center text-xs text-muted-foreground">{i18n.t('identity.switchHelp')}</p>
  </div>;
}

/** 连接器统一回调的界面落点：按流程的 next 驱动等待、确认绑定、完成与错误。 */
function ConnectorPage({ connectorId }: { connectorId: string }) {
  const flowId = useMemo(() => new URLSearchParams(location.search).get('flow'), []);
  const provider = useProvider(null);
  // 没有 flow 参数就直接是错误态；有则先读一次，wait 时每 1.5 秒重读。
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
      if (value?.next === 'wait') timer = setTimeout(read, 1500);
    }).catch(() => { if (!controller.signal.aborted) setFlow(null); });
    void read();
    return () => { controller.abort(); if (timer) clearTimeout(timer); };
  }, [provider, flowId]);
  const returnUrl = useMemo(() => { try { return flow?.returnUrl ? safeNavigationUrl(flow.returnUrl) : null; } catch { return null; } }, [flow]);
  const next = flow?.next;
  useEffect(() => { if (next === 'done') location.assign(returnUrl ?? '/login'); }, [next, returnUrl]);
  if (flow === undefined) return <Spinner label={i18n.t('common.loading')} />;
  const restart = <Button variant="ghost" onClick={() => location.assign(loginUrl(returnUrl))}>{i18n.t('identity.restart')}</Button>;
  if (flow === null || flow.connector.id !== connectorId || flow.next === 'error' || flow.next === 'restart') return <section className="mx-auto w-full max-w-md space-y-3 rounded-xl border border-border bg-card p-6" data-testid="identity-connector-error">
    <h1 className="text-xl font-semibold">{flow?.connector.displayName ?? connectorId}</h1><Alert tone="error" role="alert">{i18n.t(flow?.next === 'restart' ? 'shell.ssoError' : 'identity.flowError')}</Alert>{restart}
  </section>;
  if (flow.next === 'wait' || flow.next === 'done') return <section className="mx-auto w-full max-w-md space-y-3 rounded-xl border border-border bg-card p-6"><h1 className="text-xl font-semibold">{flow.connector.displayName ?? connectorId}</h1><Spinner label={i18n.t('identity.waiting')} /><p className="text-sm text-muted-foreground">{i18n.t('identity.waiting')}</p></section>;
  const binding = flow.binding;
  const finish = (next: Awaited<ReturnType<IdentityAuthProvider['flowBind']>>) => {
    if (next?.kind === 'authenticated') location.assign(returnUrl ?? '/login');
    else if (next === null) provider.readFlow(flow.flow, new AbortController().signal).then(setFlow).catch(() => setFailed(true));
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
