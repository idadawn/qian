import { useEffect, useMemo, useRef, useState, type ComponentProps, type ReactNode } from 'react';
import { Alert, Badge, Button, Input, Label, Result, Select, SelectContent, SelectItem, SelectTrigger, Spinner } from '@idadawn/yue-core';
import { DataTable, type DataColumn, type DataTableQuery } from '@idadawn/yue-data';
import { FeedbackProvider, SchemaForm, useConfirm, useDrawer } from '@idadawn/yue-form';
import { HttpResponseError, PlatformError, type PlatformClient } from '@idadawn/yue-sdk';
import type { paths } from './api.generated';
import { accountSchema, activateSchema, createSchema, detailSchema, editSchema, legalActions, lookupSchema, resetSchema, type AccountRequest, type ActivateRequest, type AdminAccount, type CreateRequest,
  type LookupRequest, type Quota, type ResetRequest, type StateMachine, type Tenant, type TenantAction, type TenantAdmin, type UpdateRequest } from './contracts';
import { i18n } from './i18n';

interface Issue { status?: number; code?: string; message: string }
interface Screen { rows: Tenant[]; total: number; machine?: StateMachine; loading: boolean; error?: Issue }
function issue(error: unknown): Issue {
  if (error instanceof PlatformError) return { status: error.status, code: error.detail.code, message: error.message };
  return { status: error instanceof HttpResponseError ? error.status : undefined, message: i18n.t('tenant.offline') };
}
// 平台 CONFLICT 是新建时租户号重复或编辑时版本冲突，用页面文案；其他 409（如 TENANCY_STATE_CONFLICT）显示服务端原文。
function conflict(error: PlatformError, creating = false): string {
  return error.detail.code === 'CONFLICT' ? i18n.t(creating ? 'tenant.duplicate' : 'tenant.conflict') : error.message;
}
function data<T>(result: { data?: T }): T { if (result.data === undefined) throw new Error('tenant.contract.response'); return result.data; }
const tone = { pending: 'neutral', trial: 'info', active: 'ok', grace: 'warn', suspended: 'error' } as const;
function apiOf(client: PlatformClient) { return client.api<paths>(); }
type Api = ReturnType<typeof apiOf>;
// 管理员替换与重置按状态机元数据确认（R52 requiresConfirmation）；元数据缺席时仍然确认。
function needsConfirmation(machine: StateMachine | undefined, name: string): boolean {
  return machine?.actions.find((item) => item.name === name)?.agent.requiresConfirmation ?? true;
}

function TenantContent({ client, embedded = false }: { client: PlatformClient; embedded?: boolean }) {
  const api = useMemo(() => apiOf(client), [client]);
  const [query, setQuery] = useState<DataTableQuery>({ pageIndex: 0, pageSize: 10, sorting: [], filters: {} });
  const [revision, setRevision] = useState(0);
  const [view, setView] = useState<Screen>({ rows: [], total: 0, loading: true });
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<{ message: string; error: boolean; status?: number } | null>(null);
  const refreshButton = useRef<HTMLButtonElement>(null);
  const drawer = useDrawer(i18n);
  const confirm = useConfirm();

  useEffect(() => {
    const controller = new AbortController();
    void Promise.all([
      api.GET('/api/tenancy/tenants', { params: { query: { skip: query.pageIndex * query.pageSize, take: query.pageSize, keyword: query.filters.keyword || undefined, status: (query.filters.status || undefined) as Tenant['status'] | undefined } }, signal: controller.signal }),
      api.GET('/api/tenancy/meta/state-machine', { signal: controller.signal }),
    ]).then(([listed, described]) => {
      if (controller.signal.aborted) return;
      const list = data(listed); const machine = data(described);
      if (!Array.isArray(list.items) || list.total === undefined || !Array.isArray(machine.transitions)) throw new Error('tenant.contract.response');
      const total = Number(list.total);
      if (!Number.isSafeInteger(total) || total < 0) throw new Error('tenant.contract.total');
      setView({ rows: list.items, total, machine, loading: false });
    }).catch((error: unknown) => { if (!controller.signal.aborted) setView({ rows: [], total: 0, loading: false, error: issue(error) }); });
    return () => controller.abort();
  }, [api, query, revision]);

  function refresh() { setView((current) => ({ ...current, loading: true, error: undefined })); setRevision((value) => value + 1); }
  function changeQuery(next: DataTableQuery) { setView((current) => ({ ...current, loading: true, error: undefined })); setQuery(next); }
  function completed(key: string) {
    drawer.close(); setNotice({ message: i18n.t(key), error: false });
    refresh(); refreshButton.current?.focus();
  }
  // 页面级错误提示只来自行内的冻结、恢复；抽屉里的错误由 TenantForm 与 SchemaForm 在抽屉内提示。
  function actionError(error: unknown) {
    if (error instanceof PlatformError && error.status === 409) setNotice({ message: conflict(error), error: true, status: 409 });
    else setNotice({ ...issue(error), error: true });
  }
  function create() {
    setNotice(null);
    drawer.open({ title: i18n.t('tenant.create'), content: <CreateTenant api={api} refresh={() => { drawer.close(); setNotice(null); refresh(); }} done={() => completed('tenant.created')} /> });
  }
  async function edit(row: Tenant) {
    setNotice(null);
    const title = i18n.t('tenant.edit');
    drawer.open({ title, content: <Spinner label={i18n.t('common.loading')} /> });
    try {
      const quota = data(await api.GET('/api/tenancy/tenants/{id}/quota', { params: { path: { id: row.id } } }));
      drawer.open({ title, content: <TenantForm refresh={() => { drawer.close(); setNotice(null); refresh(); }} key={row.id} schema={editSchema} i18n={i18n} initialValues={{ name: row.name, expectedRevision: row.revision, quotas: quota.limits }} testId="tenant-edit-form" onSubmit={async (values) => {
        await api.PUT('/api/tenancy/tenants/{id}', { params: { path: { id: row.id } }, body: values as UpdateRequest });
        completed('tenant.updated');
      }} /> });
    } catch (error) { drawer.open({ title, content: <Alert tone="error" role="alert">{issue(error).message}</Alert> }); }
  }
  async function detail(row: Tenant) {
    const title = i18n.t('tenant.detail');
    drawer.open({ title, content: <Spinner label={i18n.t('common.loading')} /> });
    try {
      // 身份组件缺席或管理员读取失败时详情照常展示，管理员一栏标为不可用。
      const [current, quota, admins] = await Promise.all([
        api.GET('/api/tenancy/tenants/{id}', { params: { path: { id: row.id } } }),
        api.GET('/api/tenancy/tenants/{id}/quota', { params: { path: { id: row.id } } }),
        api.GET('/api/tenancy/tenants/{id}/admins', { params: { path: { id: row.id } } }).then((result) => data(result).items, () => undefined),
      ]);
      const record = data(current);
      drawer.open({ title, content: <div className="grid gap-6"><Badge tone={tone[record.status]}>{i18n.t(`tenant.status.${record.status}`)}</Badge>
        <SchemaForm schema={detailSchema} i18n={i18n} initialValues={{ code: record.code, name: record.name }} mode="readonly" />
        <QuotaDetails quota={data(quota)} />
        <AdminDetails admins={admins} onReplace={() => replaceAdmin(record)} onReset={(admin) => resetPassword(record, admin)} /></div> });
    } catch (error) { drawer.open({ title, content: <Alert tone="error" role="alert">{issue(error).message}</Alert> }); }
  }
  function replaceAdmin(row: Tenant) {
    drawer.open({ title: i18n.t('tenant.adminReplace'), description: row.name, content: <ReplaceAdmin api={api} tenant={row} machine={view.machine} done={() => completed('tenant.adminChanged')} /> });
  }
  function resetPassword(row: Tenant, admin: TenantAdmin) {
    drawer.open({ title: i18n.t('tenant.adminResetPassword'), description: `${row.name} · ${admin.displayName}`, content: <TenantForm refresh={() => { drawer.close(); setNotice(null); refresh(); }} schema={resetSchema} i18n={i18n}
      testId="tenant-admin-reset-form" submitLabel={i18n.t('tenant.adminResetPassword')} onSubmit={async (values) => {
        if (needsConfirmation(view.machine, 'reset-admin-password') && !await confirm({ title: i18n.t('tenant.confirmReset'), description: i18n.t('tenant.confirmResetDescription') })) return;
        await api.POST('/api/tenancy/tenants/{id}/admins/{membershipId}:reset-password', { params: { path: { id: row.id, membershipId: admin.membershipId } }, body: values as ResetRequest });
        completed('tenant.passwordReset');
      }} /> });
  }
  async function transition(row: Tenant, action: TenantAction) {
    setNotice(null);
    if (action === 'activate') {
      drawer.open({ title: i18n.t('tenant.activateTitle'), content: <TenantForm refresh={() => { drawer.close(); setNotice(null); refresh(); }} schema={activateSchema} i18n={i18n} initialValues={{ trial: false }} onSubmit={async (values) => {
        await api.POST('/api/tenancy/tenants/{id}:activate', { params: { path: { id: row.id } }, body: values as ActivateRequest });
        completed('tenant.changed');
      }} /> });
      return;
    }
    const descriptor = view.machine?.actions.find((item) => item.name === action);
    if (descriptor?.agent.requiresConfirmation && !await confirm({ title: i18n.t('tenant.confirmSuspend'), description: i18n.t('tenant.confirmSuspendDescription') })) return;
    setBusy(true);
    try {
      if (action === 'suspend') await api.POST('/api/tenancy/tenants/{id}:suspend', { params: { path: { id: row.id } } });
      else await api.POST('/api/tenancy/tenants/{id}:resume', { params: { path: { id: row.id } } });
      setNotice({ message: i18n.t('tenant.changed'), error: false }); refresh(); refreshButton.current?.focus();
    } catch (error) { actionError(error); }
    finally { setBusy(false); }
  }
  const columns: DataColumn<Tenant>[] = [
    { field: 'name', titleKey: 'tenant.name', width: 260, render: (row) => <Button variant="ghost" className="px-0 font-semibold" onClick={() => void detail(row)}>{row.name}</Button> },
    { field: 'code', titleKey: 'tenant.code', width: 180, render: (row) => <span className="font-mono text-xs">{row.code}</span> },
    { field: 'status', titleKey: 'tenant.status', width: 140, render: (row) => <Badge tone={tone[row.status]}>{i18n.t(`tenant.status.${row.status}`)}</Badge> },
    { field: 'actions', titleKey: 'tenant.actions', width: 270, fixed: 'right', render: (row) => <div className="flex flex-wrap gap-1">
      <Button size="sm" variant="ghost" disabled={busy} onClick={() => void edit(row)}>{i18n.t('common.edit')}</Button>
      {view.machine && legalActions(view.machine, row.status).map((action) => <Button key={action} size="sm" variant="ghost" disabled={busy} className={action === 'suspend' ? 'text-[var(--destructive)]' : undefined} onClick={() => void transition(row, action)}>{i18n.t(`tenant.action.${action}`)}</Button>)}
    </div> },
  ];
  const denied = view.error?.status === 401 || view.error?.status === 403;
  const Content = embedded ? 'section' : 'main';
  return <div className={embedded ? undefined : "min-h-screen"}>
    {!embedded && <header className="border-b border-[var(--border)] bg-[var(--background)]"><div className="mx-auto flex max-w-7xl items-center justify-between px-6 py-4"><span className="font-semibold tracking-wide">{i18n.t('tenant.brand')}</span><Badge>{i18n.t('tenant.context')}</Badge></div></header>}
    <Content className={embedded ? 'mx-auto max-w-7xl' : 'mx-auto max-w-7xl px-6 py-10'}>
      <div className="mb-8 flex flex-wrap items-end justify-between gap-4"><div><h1 className="text-3xl font-semibold tracking-tight">{i18n.t('tenant.title')}</h1><p className="mt-2 text-sm text-[var(--muted-foreground)]">{i18n.t('tenant.subtitle')}</p></div>
        {!denied && <Button onClick={create} disabled={!view.machine}>{i18n.t('tenant.create')}</Button>}</div>
      {notice && <Alert role={notice.error ? 'alert' : 'status'} tone={notice.error ? 'error' : 'ok'} className="mb-4">{notice.status && <span className="me-2 font-mono">{notice.status}</span>}{notice.message}{notice.status === 409 && <Button variant="ghost" size="sm" className="ms-3" onClick={() => { drawer.close(); setNotice(null); refresh(); }}>{i18n.t('tenant.refresh')}</Button>}</Alert>}
      {denied ? <Result tone="warn" icon="alert" title={i18n.t(view.error?.status === 403 ? 'tenant.noAccess' : 'tenant.signIn')} description={<><span className="block">{i18n.t(view.error?.status === 403 ? 'tenant.noAccessHint' : 'tenant.signInHint')}</span><code className="mt-3 block">{view.error?.status} {view.error?.code}</code></>} action={<Button onClick={refresh}>{i18n.t('tenant.refresh')}</Button>} />
        : <div className="rounded-2xl border border-[var(--border)] bg-[var(--background)] p-5 shadow-sm">
          <DataTable columns={columns} data={view.rows} total={view.total} query={query} onQueryChange={changeQuery} getRowId={(row) => row.id} i18n={i18n} label={i18n.t('tenant.list')} loading={view.loading} error={view.error?.message} onRetry={refresh} toolbar={<div className="flex flex-wrap items-end gap-3">
            <div className="grid gap-1"><Label htmlFor="tenant-search">{i18n.t('tenant.search')}</Label><Input id="tenant-search" className="w-72" value={query.filters.keyword ?? ''} onChange={(event) => changeQuery({ ...query, pageIndex: 0, filters: { ...query.filters, keyword: event.target.value } })} /></div>
            <div className="grid gap-1"><Label htmlFor="tenant-status">{i18n.t('tenant.status')}</Label><Select value={query.filters.status ?? ''} onValueChange={(value) => changeQuery({ ...query, pageIndex: 0, filters: { ...query.filters, status: value ?? '' } })} items={[{ value: '', label: i18n.t('tenant.allStatus') }, ...(view.machine?.states ?? []).map((value) => ({ value, label: i18n.t(`tenant.status.${value}`) }))]}><SelectTrigger id="tenant-status" /><SelectContent><SelectItem value="">{i18n.t('tenant.allStatus')}</SelectItem>{view.machine?.states.map((value) => <SelectItem key={value} value={value}>{i18n.t(`tenant.status.${value}`)}</SelectItem>)}</SelectContent></Select></div>
            <Button ref={refreshButton} variant="outline" onClick={refresh}>{i18n.t('tenant.refresh')}</Button>
          </div>} />
        </div>}
    </Content>{drawer.holder}
  </div>;
}

// 抽屉里的错误只在抽屉内提示一处，不报页面级提示：字段错误与其他 ErrorDetail 交给 SchemaForm；
// 409 与连接失败拦在 SchemaForm 之前，换成页面文案或服务端原文，免得再叠一条表单根错误。
function TenantForm({ creating = false, refresh, onSubmit, ...props }: ComponentProps<typeof SchemaForm> & { creating?: boolean; refresh(): void }) {
  const [failure, setFailure] = useState<{ message: string; conflict: boolean }>();
  return <div className="grid gap-4"><SchemaForm {...props} onSubmit={async (values, api) => {
    setFailure(undefined);
    try { await onSubmit?.(values, api); }
    catch (error) {
      if (!(error instanceof PlatformError)) setFailure({ message: issue(error).message, conflict: false });
      else if (error.status === 409) setFailure({ message: conflict(error, creating), conflict: true });
      else throw error;
    }
  }} />
    {failure && <Alert tone="error" role="alert"><span>{failure.message}</span>
      {failure.conflict && <Button variant="outline" className="mt-3" onClick={refresh}>{i18n.t('tenant.refresh')}</Button>}</Alert>}
  </div>;
}

// 新建租户：先选定管理员，再填主档。成员 ID 在选定管理员时生成一次，同一份开户请求重复提交保持幂等。
function CreateTenant({ api, refresh, done }: { api: Api; refresh(): void; done(): void }) {
  const [chosen, setChosen] = useState<{ account: AdminAccount; membershipId: string }>();
  if (!chosen) return <AdminPicker api={api} onPick={(account) => setChosen({ account, membershipId: crypto.randomUUID() })} />;
  return <div className="grid gap-4">
    <AccountCard account={chosen.account} action={<Button variant="ghost" size="sm" onClick={() => setChosen(undefined)}>{i18n.t('tenant.adminRepick')}</Button>} />
    <TenantForm creating refresh={refresh} schema={createSchema} i18n={i18n} initialValues={{ isolation: 'row' }} testId="tenant-create-form" onSubmit={async (values) => {
      await api.POST('/api/tenancy/tenants', { body: { ...values, administrator: { membershipId: chosen.membershipId, subjectId: chosen.account.subjectId } } as CreateRequest });
      done();
    }} />
  </div>;
}

// 管理员账号经身份组件选择或新建：先按登录标识查找；没有合适的账号时新建（初始密码，首次登录强制修改）。
// 同一时刻只显示一张表单，抽屉页脚只有一个取消按钮；两张表单的 key 不同，切换时不沿用另一张的表单状态。
function AdminPicker({ api, onPick }: { api: Api; onPick(account: AdminAccount): void | Promise<void> }) {
  const [creating, setCreating] = useState(false);
  const [found, setFound] = useState<AdminAccount | null>();
  if (creating) return <div className="grid gap-4">
    <SchemaForm key="account" schema={accountSchema} i18n={i18n} initialValues={{ identifierKind: 'phone' }} testId="tenant-admin-account-form" submitLabel={i18n.t('tenant.adminCreate')} onSubmit={async (values) => {
      await onPick(data(await api.POST('/api/tenancy/admin-accounts', { body: values as AccountRequest })));
    }} />
    <Button variant="ghost" onClick={() => setCreating(false)}>{i18n.t('tenant.adminBack')}</Button>
  </div>;
  return <div className="grid gap-4">
    <SchemaForm key="lookup" schema={lookupSchema} i18n={i18n} testId="tenant-admin-lookup-form" submitLabel={i18n.t('tenant.adminLookup')} onSubmit={async (values) => {
      setFound(undefined);
      try { setFound(data(await api.POST('/api/tenancy/admin-accounts:lookup', { body: values as LookupRequest }))); }
      catch (error) { if (error instanceof PlatformError && error.status === 404) setFound(null); else throw error; }
    }} />
    {found && <AccountCard account={found} action={<Button size="sm" onClick={() => void onPick(found)} testId="tenant-admin-use">{i18n.t('tenant.adminUse')}</Button>} />}
    {found === null && <Alert tone="warn">{i18n.t('tenant.adminMissing')}</Alert>}
    <Button variant="outline" onClick={() => setCreating(true)} testId="tenant-admin-new">{i18n.t('tenant.adminCreate')}</Button>
  </div>;
}

// 替换管理员：新管理员得到 tenant.owner，原管理员撤销 owner 与管理员标志；满额、开通未完成等拒绝显示服务端原文。
function ReplaceAdmin({ api, tenant, machine, done }: { api: Api; tenant: Tenant; machine?: StateMachine; done(): void }) {
  const confirm = useConfirm();
  const [failure, setFailure] = useState<string>();
  return <div className="grid gap-4">
    <AdminPicker api={api} onPick={async (account) => {
      setFailure(undefined);
      if (needsConfirmation(machine, 'assign-admin') && !await confirm({ title: i18n.t('tenant.confirmAssign'), description: i18n.t('tenant.confirmAssignDescription') })) return;
      try {
        await api.PUT('/api/tenancy/tenants/{id}/admins', { params: { path: { id: tenant.id } }, body: { items: [{ subjectId: account.subjectId }] } });
        done();
      } catch (error) { setFailure(issue(error).message); }
    }} />
    {failure && <Alert tone="error" role="alert">{failure}</Alert>}
  </div>;
}

// 管理员只显示掩码标识；原值与密码都不出身份组件。
function AccountCard({ account, action }: { account: Pick<AdminAccount, 'displayName' | 'status' | 'identifiers'> & { account?: string }; action?: ReactNode }) {
  return <section className="grid gap-2 rounded-xl border border-[var(--border)] p-4" data-testid="tenant-admin-card">
    <div className="flex flex-wrap items-center justify-between gap-2"><span className="font-semibold">{account.displayName}</span><Badge tone={account.status === 'active' ? 'ok' : 'warn'}>{i18n.t(`tenant.member.${account.status}`)}</Badge></div>
    {account.account && <span className="font-mono text-xs">{account.account}</span>}
    <ul className="grid gap-1 text-sm text-[var(--muted-foreground)]">{account.identifiers.map((item) => <li key={item.kind + item.masked}>
      {i18n.t(`tenant.identifier.${item.kind}`)} <span className="font-mono">{item.masked}</span> · {i18n.t(item.verified ? 'tenant.verified' : 'tenant.unverified')}</li>)}</ul>
    {action && <div className="flex flex-wrap gap-2">{action}</div>}
  </section>;
}

function AdminDetails({ admins, onReplace, onReset }: { admins?: TenantAdmin[]; onReplace(): void; onReset(admin: TenantAdmin): void }) {
  return <section className="grid gap-3 rounded-xl border border-[var(--border)] p-4" data-testid="tenant-admin-details"><h3 className="font-semibold">{i18n.t('tenant.admin')}</h3>
    {admins === undefined ? <p className="text-sm text-[var(--muted-foreground)]">{i18n.t('tenant.adminUnavailable')}</p>
      : admins.length === 0 ? <p className="text-sm text-[var(--muted-foreground)]">{i18n.t('tenant.adminNone')}</p>
      : admins.map((admin) => <AccountCard key={admin.membershipId} account={admin} action={<Button size="sm" variant="outline" onClick={() => onReset(admin)} testId="tenant-admin-reset">{i18n.t('tenant.adminResetPassword')}</Button>} />)}
    {admins !== undefined && <div><Button size="sm" variant="outline" onClick={onReplace} testId="tenant-admin-replace">{i18n.t(admins.length ? 'tenant.adminReplace' : 'tenant.adminAssign')}</Button></div>}
  </section>;
}

function QuotaDetails({ quota }: { quota: Quota }) {
  return <section className="rounded-xl border border-[var(--border)] p-4"><h3 className="mb-4 font-semibold">{i18n.t('tenant.detailsQuota')}</h3><dl className="grid grid-cols-2 gap-4 text-sm">
    <dt className="text-[var(--muted-foreground)]">{i18n.t('tenant.quota')}</dt><dd className="text-end font-medium">{quota.limits.users === null || quota.limits.users === undefined ? i18n.t('tenant.unlimited') : String(quota.limits.users)}</dd>
    <dt className="text-[var(--muted-foreground)]">{i18n.t('tenant.usage')}</dt><dd className="text-end">{quota.usageAvailable && quota.usersUsed !== null && quota.usersUsed !== undefined ? String(quota.usersUsed) : i18n.t('tenant.usageUnavailable')}</dd>
  </dl></section>;
}

export function TenantPage({ client, embedded = false }: { client: PlatformClient; embedded?: boolean }) { return <FeedbackProvider i18n={i18n}><TenantContent client={client} embedded={embedded} /></FeedbackProvider>; }
