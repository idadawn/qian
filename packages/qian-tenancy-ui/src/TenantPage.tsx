import { useEffect, useId, useMemo, useRef, useState, useSyncExternalStore, type ComponentProps, type ReactNode } from 'react';
import { Alert, Avatar, AvatarFallback, avatarTint, Badge, Button, cn, Descriptions, Icon, IconButton, Input, Menu, MenuContent, MenuItem, MenuSeparator, MenuTrigger, Result, Spinner, Tabs, TabsList, TabsTab } from '@idadawn/yue-core';
import { DataTable, type DataColumn, type DataTableQuery } from '@idadawn/yue-data';
import { FeedbackProvider, SchemaForm, useConfirm, useDrawer } from '@idadawn/yue-form';
import { HttpResponseError, PlatformError, type PlatformClient } from '@idadawn/yue-sdk';
import type { paths } from './api.generated';
import { accountSchema, activateSchema, createSchema, editSchema, legalActions, lookupSchema, resetSchema, type AccountRequest, type ActivateRequest, type AdminAccount, type CreateRequest,
  type LookupRequest, type Quota, type ResetRequest, type StateMachine, type Tenant, type TenantAction, type TenantAdmin, type TenantCounts, type UpdateRequest } from './contracts';
import { i18n } from './i18n';

interface Issue { status?: number; code?: string; message: string }
// now 是这一页数据到达的时刻：到期提醒按它判断，渲染保持纯函数。
interface Screen { rows: Tenant[]; total: number; counts?: TenantCounts; machine?: StateMachine; loading: boolean; error?: Issue; now?: number }
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
// 页签顺序按运营关注度：全部、正常、试用、待激活、宽限期、已冻结。
const tabs = ['all', 'active', 'trial', 'pending', 'grace', 'suspended'] as const;
function apiOf(client: PlatformClient) { return client.api<paths>(); }
type Api = ReturnType<typeof apiOf>;
// 管理员替换与重置按状态机元数据确认（R52 requiresConfirmation）；元数据缺席时仍然确认。
function needsConfirmation(machine: StateMachine | undefined, name: string): boolean {
  return machine?.actions.find((item) => item.name === name)?.agent.requiresConfirmation ?? true;
}
// 契约把整数写成「字符串或整数」；页面只接受安全整数，其余按缺席处理。
function count(value: unknown): number | null {
  const parsed = typeof value === 'string' ? Number(value) : value;
  return typeof parsed === 'number' && Number.isSafeInteger(parsed) && parsed >= 0 ? parsed : null;
}
const pad = (value: number) => String(value).padStart(2, '0');
function day(value: string): string { const date = new Date(value); return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`; }
function moment(value: string): string { const date = new Date(value); return `${day(value)} ${pad(date.getHours())}:${pad(date.getMinutes())}`; }
function StatusBadge({ status }: { status: Tenant['status'] }) { return <Badge tone={tone[status]} dot>{i18n.t(`tenant.status.${status}`)}</Badge>; }
// 有效期：待激活且没有截止时间显示破折号；其余没有截止时间为长期；宽限期标出原截止日；宽限期与七天内到期用警示色。
function Validity({ row, now }: { row: Pick<Tenant, 'status' | 'validUntil'>; now?: number }) {
  if (!row.validUntil && row.status === 'pending') return <span className="text-subtle-foreground"><span aria-hidden="true">—</span><span className="sr-only">{i18n.t('tenant.validityPending')}</span></span>;
  if (!row.validUntil) return <span className="whitespace-nowrap text-subtle-foreground">{i18n.t('tenant.noExpiry')}</span>;
  const date = day(row.validUntil);
  const soon = now !== undefined && new Date(row.validUntil).getTime() - now < 7 * 24 * 3600 * 1000;
  const key = row.status === 'grace' ? 'tenant.graceSince' : row.status === 'trial' ? 'tenant.trialUntil' : 'tenant.until';
  return <span className={cn('whitespace-nowrap', row.status === 'grace' || soon ? 'text-warn' : 'text-subtle-foreground')}>{i18n.t(key, { date })}</span>;
}
// 用户用量：已用 / 上限加 4px 进度条（不足 2% 也露出一截，九成以上用警示实色）；用量缺席（身份组件不在）显示破折号与读屏说明，不报 0。
function Usage({ used, limit }: { used: number | null; limit: number | null }) {
  if (used === null) return <span className="text-muted-foreground" title={i18n.t('tenant.usageUnavailable')}>—<span className="sr-only">{i18n.t('tenant.usageUnavailable')}</span></span>;
  const ratio = limit ? Math.min(1, used / limit) : null;
  return <span className="flex w-24 flex-col gap-1">
    <span className="text-[13px] leading-5.5 whitespace-nowrap">{`${used} / ${limit ?? i18n.t('tenant.unlimitedShort')}`}</span>
    {ratio !== null && <span aria-hidden="true" data-slot="tenant-usage-bar" className="h-1 overflow-hidden rounded-xs bg-border"><span className={cn('block h-full', ratio >= 0.9 ? 'bg-warn-solid' : 'bg-primary')} style={{ width: `${Math.max(2, Math.round(ratio * 100))}%` }} /></span>}
  </span>;
}
function subscribeLanguage(listener: () => void) { i18n.instance.on('languageChanged', listener); return () => { i18n.instance.off('languageChanged', listener); }; }
const currentLanguage = () => i18n.instance.language;
const locales = ['zh-CN', 'zh-TW', 'en-US'] as const;
function isLocale(value: string | undefined): value is typeof locales[number] { return (locales as readonly string[]).includes(value ?? ''); }

function TenantContent({ client, embedded = false }: { client: PlatformClient; embedded?: boolean }) {
  const api = useMemo(() => apiOf(client), [client]);
  const headingId = useId();
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
      setView({ rows: list.items, total, counts: list.counts, machine, loading: false, now: Date.now() });
    }).catch((error: unknown) => { if (!controller.signal.aborted) setView({ rows: [], total: 0, loading: false, error: issue(error) }); });
    return () => controller.abort();
  }, [api, query, revision]);

  function refresh() { setView((current) => ({ ...current, loading: true, error: undefined })); setRevision((value) => value + 1); }
  function changeQuery(next: DataTableQuery) { setView((current) => ({ ...current, loading: true, error: undefined })); setQuery(next); }
  function filter(key: 'keyword' | 'status', value: string) { changeQuery({ ...query, pageIndex: 0, filters: { ...query.filters, [key]: value } }); }
  function completed(key: string) {
    drawer.close(); setNotice({ message: i18n.t(key), error: false });
    refresh(); refreshButton.current?.focus();
  }
  const reload = () => { drawer.close(); setNotice(null); refresh(); };
  // 页面级错误提示只来自行内的冻结、恢复；抽屉里的错误由 TenantForm 与 SchemaForm 在抽屉内提示。
  function actionError(error: unknown) {
    if (error instanceof PlatformError && error.status === 409) setNotice({ message: conflict(error), error: true, status: 409 });
    else setNotice({ ...issue(error), error: true });
  }
  function create() {
    setNotice(null);
    drawer.open({ title: i18n.t('tenant.create'), description: i18n.t('tenant.createHint'), content: <CreateTenant api={api} refresh={reload} done={() => completed('tenant.created')} /> });
  }
  async function edit(row: Tenant) {
    setNotice(null);
    const title = i18n.t('tenant.edit');
    drawer.open({ title, description: row.code, content: <Spinner label={i18n.t('common.loading')} /> });
    try {
      const quota = data(await api.GET('/api/tenancy/tenants/{id}/quota', { params: { path: { id: row.id } } }));
      drawer.open({ title, description: row.code, content: <TenantForm refresh={reload} key={row.id} schema={editSchema} i18n={i18n} initialValues={{ name: row.name, expectedRevision: row.revision, quotas: quota.limits }} testId="tenant-edit-form" onSubmit={async (values) => {
        await api.PUT('/api/tenancy/tenants/{id}', { params: { path: { id: row.id } }, body: values as UpdateRequest });
        completed('tenant.updated');
      }} /> });
    } catch (error) { drawer.open({ title, content: <Alert tone="error" role="alert">{issue(error).message}</Alert> }); }
  }
  async function detail(row: Tenant) {
    drawer.open({ title: row.name, description: row.code, size: 'md', extra: <StatusBadge status={row.status} />, content: <Spinner label={i18n.t('common.loading')} /> });
    try {
      // 身份组件缺席或管理员读取失败时详情照常展示，管理员一栏标为不可用。
      const [current, quota, admins] = await Promise.all([
        api.GET('/api/tenancy/tenants/{id}', { params: { path: { id: row.id } } }),
        api.GET('/api/tenancy/tenants/{id}/quota', { params: { path: { id: row.id } } }),
        api.GET('/api/tenancy/tenants/{id}/admins', { params: { path: { id: row.id } } }).then((result) => data(result).items, () => undefined),
      ]);
      const record = data(current);
      drawer.open({ title: record.name, description: record.code, size: 'md', extra: <StatusBadge status={record.status} />, content: <div className="grid gap-6" data-testid="tenant-detail">
        <DetailSection title={i18n.t('tenant.detailBasic')}><Descriptions layout="stacked" items={[
          { key: 'code', label: i18n.t('tenant.code'), children: <span className="font-mono">{record.code}</span> },
          { key: 'name', label: i18n.t('tenant.name'), children: record.name },
          { key: 'validity', label: i18n.t('tenant.validity'), children: <Validity row={{ status: record.status, validUntil: row.validUntil }} now={view.now} /> },
          { key: 'created', label: i18n.t('tenant.createdAt'), children: row.createdAt ? moment(row.createdAt) : '—' },
        ]} /></DetailSection>
        <QuotaDetails quota={data(quota)} />
        <AdminDetails admins={admins} onReplace={() => replaceAdmin(record)} onReset={(admin) => resetPassword(record, admin)} />
      </div> });
    } catch (error) { drawer.open({ title: row.name, content: <Alert tone="error" role="alert">{issue(error).message}</Alert> }); }
  }
  function replaceAdmin(row: Pick<Tenant, 'id' | 'name'>) {
    setNotice(null);
    drawer.open({ title: i18n.t('tenant.adminReplace'), description: row.name, content: <ReplaceAdmin api={api} tenant={row} machine={view.machine} done={() => completed('tenant.adminChanged')} /> });
  }
  function resetPassword(row: Pick<Tenant, 'id' | 'name'>, admin: TenantAdmin) {
    drawer.open({ title: i18n.t('tenant.adminResetPassword'), description: `${row.name} · ${admin.displayName}`, content: <TenantForm refresh={reload} schema={resetSchema} i18n={i18n}
      testId="tenant-admin-reset-form" submitLabel={i18n.t('tenant.adminResetPassword')} onSubmit={async (values) => {
        if (needsConfirmation(view.machine, 'reset-admin-password') && !await confirm({ title: i18n.t('tenant.confirmReset'), description: i18n.t('tenant.confirmResetDescription') })) return;
        await api.POST('/api/tenancy/tenants/{id}/admins/{membershipId}:reset-password', { params: { path: { id: row.id, membershipId: admin.membershipId } }, body: values as ResetRequest });
        completed('tenant.passwordReset');
      }} /> });
  }
  // 行内「更多」里的重置密码：列表行没有成员 ID，先读这个租户的管理员；只有一位时直接给重置表单，多位时逐个列出，与详情抽屉一致。
  async function resetFromRow(row: Tenant) {
    setNotice(null);
    const title = i18n.t('tenant.adminResetPassword');
    drawer.open({ title, description: row.name, content: <Spinner label={i18n.t('common.loading')} /> });
    try {
      const admins = data(await api.GET('/api/tenancy/tenants/{id}/admins', { params: { path: { id: row.id } } })).items;
      if (admins.length === 1) { resetPassword(row, admins[0]!); return; }
      drawer.open({ title, description: row.name, content: admins.length === 0 ? <p className="text-sm text-muted-foreground">{i18n.t('tenant.adminNone')}</p>
        : <div className="grid gap-3">{admins.map((admin) => <AccountCard key={admin.membershipId} account={admin} action={<Button size="sm" variant="outline" onClick={() => resetPassword(row, admin)} testId="tenant-admin-reset">{i18n.t('tenant.adminResetPassword')}</Button>} />)}</div> });
    } catch (error) { drawer.open({ title, description: row.name, content: <Alert tone="error" role="alert">{issue(error).message}</Alert> }); }
  }
  async function transition(row: Tenant, action: TenantAction) {
    setNotice(null);
    if (action === 'activate') {
      drawer.open({ title: i18n.t('tenant.activateTitle'), description: row.name, content: <TenantForm refresh={reload} schema={activateSchema} i18n={i18n} initialValues={{ trial: false }} onSubmit={async (values) => {
        await api.POST('/api/tenancy/tenants/{id}:activate', { params: { path: { id: row.id } }, body: values as ActivateRequest });
        completed('tenant.changed');
      }} /> });
      return;
    }
    const descriptor = view.machine?.actions.find((item) => item.name === action);
    if (descriptor?.agent.requiresConfirmation && !await confirm({ title: i18n.t('tenant.confirmSuspend'), description: i18n.t('tenant.confirmSuspendDescription'), tone: 'danger', confirmLabel: i18n.t('tenant.action.suspend') })) return;
    setBusy(true);
    try {
      if (action === 'suspend') await api.POST('/api/tenancy/tenants/{id}:suspend', { params: { path: { id: row.id } } });
      else await api.POST('/api/tenancy/tenants/{id}:resume', { params: { path: { id: row.id } } });
      setNotice({ message: i18n.t('tenant.changed'), error: false }); refresh(); refreshButton.current?.focus();
    } catch (error) { actionError(error); }
    finally { setBusy(false); }
  }
  // 列宽取原型画板（内容区 1160px）里各列的实际宽度，等比缩到合计 900 作为各列最小宽：表格更宽时多出的宽度按列宽比例分配，
  // 在原型宽度下与画板一致；名称、管理员偏长时其余列还能让出余量，最小宽合计超过表格宽度才横向滚动。
  const columns: DataColumn<Tenant>[] = [
    { field: 'name', titleKey: 'tenant.tenant', width: 146, render: (row) => <span className="flex min-w-0 items-center gap-3">
      <Avatar aria-hidden="true" shape="square" tint={row.status === 'suspended' ? 'gray' : avatarTint(row.code)} className="size-8.5"><AvatarFallback className="font-bold">{Array.from(row.name)[0]}</AvatarFallback></Avatar>
      {/* 名称与租户号最宽 200px，更长时省略，免得一个长名称把整张表撑出横向滚动。 */}
      <span className="flex min-w-0 max-w-50 flex-col leading-5">
        <Button variant="link" className="h-auto max-w-full self-start rounded-xs border-0 p-0 leading-5 font-semibold text-foreground hover:bg-transparent hover:text-primary" onClick={() => void detail(row)} testId="tenant-row-name"><span className="truncate">{row.name}</span></Button>
        <span className="truncate font-mono text-xs leading-5 text-muted-foreground" title={row.code}>{row.code}</span>
      </span>
    </span> },
    { field: 'status', titleKey: 'tenant.status', width: 102, render: (row) => <StatusBadge status={row.status} /> },
    { field: 'validUntil', titleKey: 'tenant.validity', width: 161, render: (row) => <Validity row={row} now={view.now} /> },
    { field: 'usersUsed', titleKey: 'tenant.users', width: 133, render: (row) => <Usage used={count(row.usersUsed)} limit={count(row.usersLimit)} /> },
    { field: 'administratorName', titleKey: 'tenant.admin', width: 77, render: (row) => row.administratorName ? <span className="block max-w-30 truncate" title={row.administratorName}>{row.administratorName}</span> : <span className="text-muted-foreground">—</span> },
    { field: 'createdAt', titleKey: 'tenant.createdAt', width: 118, render: (row) => <span className="whitespace-nowrap text-caption-foreground">{row.createdAt ? day(row.createdAt) : '—'}</span> },
    { field: 'actions', titleKey: 'tenant.actions', width: 163, fixed: 'right', align: 'end', render: (row) => <RowActions row={row} busy={busy} actions={view.machine ? legalActions(view.machine, row.status) : []}
      onDetail={() => void detail(row)} onEdit={() => void edit(row)} onTransition={(action) => void transition(row, action)} onReplaceAdmin={() => replaceAdmin(row)} onResetPassword={() => void resetFromRow(row)} /> },
  ];
  const denied = view.error?.status === 401 || view.error?.status === 403;
  const status = query.filters.status || 'all';
  const Content = embedded ? 'section' : 'main';
  return <div className={embedded ? undefined : 'min-h-dvh bg-canvas'}>
    {!embedded && <header className="h-14 border-b border-border bg-background"><div className="mx-auto flex h-full max-w-7xl items-center justify-between px-6"><span className="font-bold">{i18n.t('tenant.brand')}</span><Badge>{i18n.t('tenant.context')}</Badge></div></header>}
    <Content aria-labelledby={headingId} className={cn('min-w-0 space-y-4', embedded ? 'p-4 md:p-6' : 'mx-auto max-w-7xl p-6')}>
      <div className="flex flex-wrap items-end justify-between gap-4"><div className="flex min-w-0 flex-col gap-1"><h1 id={headingId} className="text-[22px] leading-[30px] font-bold">{i18n.t('tenant.title')}</h1><p className="leading-5.5 text-muted-foreground">{i18n.t('tenant.subtitle')}</p></div>
        {!denied && <Button onClick={create} disabled={!view.machine} testId="tenant-create"><Icon name="plus" strokeWidth={2} />{i18n.t('tenant.create')}</Button>}</div>
      {notice && <Alert role={notice.error ? 'alert' : 'status'} tone={notice.error ? 'error' : 'ok'}
        action={notice.status === 409 ? <Button variant="outline" size="sm" onClick={reload}>{i18n.t('tenant.refresh')}</Button> : undefined}>{notice.status && <span className="me-2 font-mono">{notice.status}</span>}{notice.message}</Alert>}
      {denied ? <Result tone="warn" icon="alert" title={i18n.t(view.error?.status === 403 ? 'tenant.noAccess' : 'tenant.signIn')} description={<><span className="block">{i18n.t(view.error?.status === 403 ? 'tenant.noAccessHint' : 'tenant.signInHint')}</span><code className="mt-3 block">{view.error?.status} {view.error?.code}</code></>} action={<Button onClick={refresh}>{i18n.t('tenant.refresh')}</Button>} />
        : <DataTable columns={columns} data={view.rows} total={view.total} query={query} onQueryChange={changeQuery} getRowId={(row) => row.id} i18n={i18n} label={i18n.t('tenant.list')} loading={view.loading} error={view.error?.message} onRetry={refresh} density="standard"
          header={<Tabs value={status} onValueChange={(value) => filter('status', value === 'all' ? '' : String(value))}>
            <TabsList variant="line" countVariant="plain" aria-label={i18n.t('tenant.tabs')} className="px-5">{tabs.map((key) => <TabsTab key={key} value={key} count={view.counts ? count(view.counts[key]) ?? undefined : undefined}>{i18n.t(key === 'all' ? 'tenant.all' : `tenant.status.${key}`)}</TabsTab>)}</TabsList>
          </Tabs>}
          toolbar={<>
            <span className="relative min-w-50 basis-80"><Icon name="search" className="pointer-events-none absolute start-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
              <Input type="search" aria-label={i18n.t('tenant.search')} placeholder={i18n.t('tenant.searchPlaceholder')} className="ps-9" value={query.filters.keyword ?? ''} onChange={(event) => filter('keyword', event.target.value)} testId="tenant-search" /></span>
            <Button variant="link" onClick={() => { if (query.filters.keyword) filter('keyword', ''); }} testId="tenant-search-reset">{i18n.t('tenant.reset')}</Button>
          </>}
          toolbarEnd={<IconButton ref={refreshButton} variant="outline" label={i18n.t('tenant.refresh')} className="text-subtle-foreground" onClick={refresh} testId="tenant-refresh"><Icon name="refresh" /></IconButton>} />}
    </Content>{drawer.holder}
  </div>;
}

// 行内操作：「详情」「编辑」两个文字按钮，其余收进「更多」菜单——状态机给出的合法迁移与管理员维护；冻结是破坏性操作，放在分隔线后。
// 菜单项先把焦点交回「更多」再执行：随后打开的抽屉或确认框关闭时，焦点回到这一行。
function RowActions({ row, actions, busy, onDetail, onEdit, onTransition, onReplaceAdmin, onResetPassword }: {
  row: Tenant; actions: TenantAction[]; busy: boolean;
  onDetail(): void; onEdit(): void; onTransition(action: TenantAction): void; onReplaceAdmin(): void; onResetPassword(): void;
}) {
  const trigger = useRef<HTMLButtonElement>(null);
  const run = (task: () => void) => () => { trigger.current?.focus(); task(); };
  return <span className="inline-flex items-center gap-1 whitespace-nowrap">
    <Button size="xs" variant="link" className="rounded-sm px-2" onClick={onDetail} testId="tenant-row-detail">{i18n.t('tenant.detailAction')}</Button>
    <Button size="xs" variant="link" className="rounded-sm px-2" disabled={busy} onClick={onEdit} testId="tenant-row-edit">{i18n.t('common.edit')}</Button>
    <Menu>
      <MenuTrigger ref={trigger} aria-label={i18n.t('tenant.moreActions', { name: row.name })} testId="tenant-row-more"
        className="size-7 rounded-sm border-transparent bg-transparent px-0 text-caption-foreground shadow-none hover:bg-accent hover:text-foreground data-[popup-open]:bg-accent"><Icon name="more" strokeWidth={2} /></MenuTrigger>
      <MenuContent align="end" className="w-auto min-w-36" testId="tenant-row-menu">
        {actions.filter((action) => action !== 'suspend').map((action) => <MenuItem key={action} disabled={busy} onClick={run(() => onTransition(action))} testId={`tenant-action-${action}`}>{i18n.t(`tenant.action.${action}`)}</MenuItem>)}
        <MenuItem disabled={busy} onClick={run(onReplaceAdmin)} testId="tenant-action-admin">{i18n.t(row.administratorName ? 'tenant.adminReplace' : 'tenant.adminAssign')}</MenuItem>
        {row.administratorName && <MenuItem disabled={busy} onClick={run(onResetPassword)} testId="tenant-action-reset-password">{i18n.t('tenant.adminResetMenu')}</MenuItem>}
        {actions.includes('suspend') && <><MenuSeparator /><MenuItem disabled={busy} onClick={run(() => onTransition('suspend'))} testId="tenant-action-suspend" variant="destructive">{i18n.t('tenant.action.suspend')}</MenuItem></>}
      </MenuContent>
    </Menu>
  </span>;
}

function DetailSection({ title, children, testId }: { title: string; children: ReactNode; testId?: string }) {
  const id = useId();
  return <section aria-labelledby={id} className="grid gap-3" data-testid={testId}><h3 id={id} className="text-sm font-semibold">{title}</h3>{children}</section>;
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
    {failure && <Alert tone="error" role="alert" action={failure.conflict ? <Button variant="outline" size="sm" onClick={refresh}>{i18n.t('tenant.refresh')}</Button> : undefined}>{failure.message}</Alert>}
  </div>;
}

// 新建租户：先选定管理员，再填主档。成员 ID 在选定管理员时生成一次，同一份开户请求重复提交保持幂等。
function CreateTenant({ api, refresh, done }: { api: Api; refresh(): void; done(): void }) {
  const [chosen, setChosen] = useState<{ account: AdminAccount; membershipId: string }>();
  if (!chosen) return <AdminPicker api={api} onPick={(account) => setChosen({ account, membershipId: crypto.randomUUID() })} />;
  return <div className="grid gap-5">
    <DetailSection title={i18n.t('tenant.admin')}><AccountCard account={chosen.account} action={<Button variant="link" size="sm" className="h-7 px-2" onClick={() => setChosen(undefined)}>{i18n.t('tenant.adminRepick')}</Button>} /></DetailSection>
    <TenantForm creating refresh={refresh} schema={createSchema} i18n={i18n} initialValues={{ isolation: 'row' }} testId="tenant-create-form" onSubmit={async (values) => {
      await api.POST('/api/tenancy/tenants', { body: { ...values, administrator: { membershipId: chosen.membershipId, subjectId: chosen.account.subjectId } } as CreateRequest });
      done();
    }} />
  </div>;
}

// 管理员账号经身份组件选择或新建：先按登录标识查找；没有合适的账号时新建（初始密码，首次登录强制修改）。
// 这两张是抽屉里的中间步骤，按钮留在表单内（inlineActions），页脚只给最终提交用；两张表单的 key 不同，切换时不沿用另一张的表单状态。
function AdminPicker({ api, onPick }: { api: Api; onPick(account: AdminAccount): void | Promise<void> }) {
  const [creating, setCreating] = useState(false);
  const [found, setFound] = useState<AdminAccount | null>();
  if (creating) return <div className="grid gap-4">
    <p className="text-sm text-muted-foreground">{i18n.t('tenant.adminCreateHint')}</p>
    <SchemaForm key="account" inlineActions schema={accountSchema} i18n={i18n} initialValues={{ identifierKind: 'phone' }} testId="tenant-admin-account-form" submitLabel={i18n.t('tenant.adminCreate')} onSubmit={async (values) => {
      await onPick(data(await api.POST('/api/tenancy/admin-accounts', { body: values as AccountRequest })));
    }} />
    <Button variant="link" className="justify-self-start px-0" onClick={() => setCreating(false)}>{i18n.t('tenant.adminBack')}</Button>
  </div>;
  return <div className="grid gap-4">
    <p className="text-sm text-muted-foreground">{i18n.t('tenant.adminPickHint')}</p>
    <SchemaForm key="lookup" inlineActions schema={lookupSchema} i18n={i18n} testId="tenant-admin-lookup-form" submitLabel={i18n.t('tenant.adminLookup')} onSubmit={async (values) => {
      setFound(undefined);
      try { setFound(data(await api.POST('/api/tenancy/admin-accounts:lookup', { body: values as LookupRequest }))); }
      catch (error) { if (error instanceof PlatformError && error.status === 404) setFound(null); else throw error; }
    }} />
    {found && <AccountCard account={found} action={<Button size="sm" onClick={() => void onPick(found)} testId="tenant-admin-use">{i18n.t('tenant.adminUse')}</Button>} />}
    {found === null && <Alert tone="warn">{i18n.t('tenant.adminMissing')}</Alert>}
    <Button variant="outline" className="justify-self-start" onClick={() => setCreating(true)} testId="tenant-admin-new"><Icon name="plus" />{i18n.t('tenant.adminCreate')}</Button>
  </div>;
}

// 替换管理员：新管理员得到 tenant.owner，原管理员撤销 owner 与管理员标志；满额、开通未完成等拒绝显示服务端原文。
function ReplaceAdmin({ api, tenant, machine, done }: { api: Api; tenant: Pick<Tenant, 'id'>; machine?: StateMachine; done(): void }) {
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
  return <section className="flex flex-wrap items-start gap-3 rounded-xl border border-border bg-card p-4" data-testid="tenant-admin-card">
    <Avatar aria-hidden="true" tint="primary" className="size-9"><AvatarFallback className="font-bold">{Array.from(account.displayName)[0]}</AvatarFallback></Avatar>
    <div className="grid min-w-0 flex-1 gap-1">
      <div className="flex flex-wrap items-center gap-2"><span className="font-semibold">{account.displayName}</span><Badge tone={account.status === 'active' ? 'ok' : 'warn'} dot>{i18n.t(`tenant.member.${account.status}`)}</Badge></div>
      {account.account && <span className="font-mono text-xs">{account.account}</span>}
      <ul className="grid gap-0.5 text-[13px] text-muted-foreground">{account.identifiers.map((item) => <li key={item.kind + item.masked}>
        {i18n.t(`tenant.identifier.${item.kind}`)} <span className="font-mono">{item.masked}</span> · {i18n.t(item.verified ? 'tenant.verified' : 'tenant.unverified')}</li>)}</ul>
    </div>
    {action && <div className="flex flex-wrap gap-2">{action}</div>}
  </section>;
}

function AdminDetails({ admins, onReplace, onReset }: { admins?: TenantAdmin[]; onReplace(): void; onReset(admin: TenantAdmin): void }) {
  return <DetailSection title={i18n.t('tenant.admin')} testId="tenant-admin-details">
    {admins === undefined ? <p className="text-sm text-muted-foreground">{i18n.t('tenant.adminUnavailable')}</p>
      : admins.length === 0 ? <p className="text-sm text-muted-foreground">{i18n.t('tenant.adminNone')}</p>
      : admins.map((admin) => <AccountCard key={admin.membershipId} account={admin} action={<Button size="sm" variant="outline" onClick={() => onReset(admin)} testId="tenant-admin-reset">{i18n.t('tenant.adminResetPassword')}</Button>} />)}
    {admins !== undefined && <div><Button size="sm" variant="outline" onClick={onReplace} testId="tenant-admin-replace">{i18n.t(admins.length ? 'tenant.adminReplace' : 'tenant.adminAssign')}</Button></div>}
  </DetailSection>;
}

// 上限与用量分两格显示原值；两者都有时再画一条用量比例，九成以上用警示实色。
function QuotaDetails({ quota }: { quota: Quota }) {
  const limit = count(quota.limits.users);
  const used = quota.usageAvailable ? count(quota.usersUsed) : null;
  return <DetailSection title={i18n.t('tenant.detailsQuota')}>
    <Descriptions layout="stacked" items={[
      { key: 'limit', label: i18n.t('tenant.quota'), children: limit === null ? i18n.t('tenant.unlimited') : String(limit) },
      { key: 'used', label: i18n.t('tenant.usage'), children: used === null ? i18n.t('tenant.usageUnavailable') : String(used) },
    ]} />
    {limit !== null && limit > 0 && used !== null && <span aria-hidden="true" className="h-1.5 overflow-hidden rounded-full bg-border"><span className={cn('block h-full', used / limit >= 0.9 ? 'bg-warn-solid' : 'bg-primary')} style={{ width: `${Math.max(2, Math.min(100, Math.round(used / limit * 100)))}%` }} /></span>}
  </DetailSection>;
}

/** `locale` 由外壳上下文给出（remote 入口传入）；语言变化时整页按新语言重渲染。 */
export function TenantPage({ client, embedded = false, locale }: { client: PlatformClient; embedded?: boolean; locale?: string }) {
  useSyncExternalStore(subscribeLanguage, currentLanguage, currentLanguage);
  useEffect(() => { if (isLocale(locale) && locale !== i18n.instance.language) void i18n.changeLanguage(locale); }, [locale]);
  return <FeedbackProvider i18n={i18n}><TenantContent client={client} embedded={embedded} /></FeedbackProvider>;
}
