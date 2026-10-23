import { useEffect, useMemo, useRef, useState, type ComponentProps } from 'react';
import { Alert, Badge, Button, Input, Label, Result, Select, SelectContent, SelectItem, SelectTrigger, Spinner } from '@idadawn/yue-core';
import { DataTable, type DataColumn, type DataTableQuery } from '@idadawn/yue-data';
import { FeedbackProvider, SchemaForm, useConfirm, useDrawer } from '@idadawn/yue-form';
import { HttpResponseError, PlatformError, type PlatformClient } from '@idadawn/yue-sdk';
import type { paths } from './api.generated';
import { activateSchema, createSchema, detailSchema, editSchema, legalActions, type ActivateRequest, type CreateRequest, type Quota, type StateMachine, type Tenant, type TenantAction, type UpdateRequest } from './contracts';
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

function TenantContent({ client, embedded = false }: { client: PlatformClient; embedded?: boolean }) {
  const api = useMemo(() => client.api<paths>(), [client]);
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
    drawer.open({ title: i18n.t('tenant.create'), content: <TenantForm creating refresh={() => { drawer.close(); setNotice(null); refresh(); }} schema={createSchema} i18n={i18n} initialValues={{ isolation: 'row' }} testId="tenant-create-form" onSubmit={async (values) => {
      await api.POST('/api/tenancy/tenants', { body: values as CreateRequest });
      completed('tenant.created');
    }} /> });
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
      const [current, quota] = await Promise.all([
        api.GET('/api/tenancy/tenants/{id}', { params: { path: { id: row.id } } }),
        api.GET('/api/tenancy/tenants/{id}/quota', { params: { path: { id: row.id } } }),
      ]);
      const record = data(current);
      drawer.open({ title, content: <div className="grid gap-6"><Badge tone={tone[record.status]}>{i18n.t(`tenant.status.${record.status}`)}</Badge>
        <SchemaForm schema={detailSchema} i18n={i18n} initialValues={{ code: record.code, name: record.name }} mode="readonly" />
        <QuotaDetails quota={data(quota)} /></div> });
    } catch (error) { drawer.open({ title, content: <Alert tone="error" role="alert">{issue(error).message}</Alert> }); }
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

function QuotaDetails({ quota }: { quota: Quota }) {
  return <section className="rounded-xl border border-[var(--border)] p-4"><h3 className="mb-4 font-semibold">{i18n.t('tenant.detailsQuota')}</h3><dl className="grid grid-cols-2 gap-4 text-sm">
    <dt className="text-[var(--muted-foreground)]">{i18n.t('tenant.quota')}</dt><dd className="text-end font-medium">{quota.limits.users === null || quota.limits.users === undefined ? i18n.t('tenant.unlimited') : String(quota.limits.users)}</dd>
    <dt className="text-[var(--muted-foreground)]">{i18n.t('tenant.usage')}</dt><dd className="text-end">{quota.usageAvailable && quota.usersUsed !== null && quota.usersUsed !== undefined ? String(quota.usersUsed) : i18n.t('tenant.usageUnavailable')}</dd>
  </dl></section>;
}

export function TenantPage({ client, embedded = false }: { client: PlatformClient; embedded?: boolean }) { return <FeedbackProvider i18n={i18n}><TenantContent client={client} embedded={embedded} /></FeedbackProvider>; }
