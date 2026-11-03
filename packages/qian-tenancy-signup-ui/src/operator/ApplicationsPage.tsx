import { useEffect, useId, useMemo, useRef, useState, useSyncExternalStore, type ReactNode } from 'react';
import { Alert, Badge, Button, cn, Descriptions, Icon, IconButton, Input, Select, SelectContent, SelectItem, SelectTrigger, Spinner, Tabs, TabsList, TabsTab, type IconName } from '@idadawn/yue-core';
import { DataTable, type DataColumn, type DataTableQuery } from '@idadawn/yue-data';
import { FeedbackProvider, SchemaForm, useConfirm, useDrawer } from '@idadawn/yue-form';
import type { PlatformClient } from '@idadawn/yue-sdk';
import { apiOf, data, day, issue, moment, type Api, type Issue } from '../api';
import { approveSchema, count, needsConfirmation, rejectSchema, sizes, type ApplicationCounts, type ApplicationDetail, type ApplicationRow, type ApplicationStatus,
  type ApproveRequest, type Check, type RejectRequest, type StateMachine } from '../contracts';
import { i18n } from '../i18n';

interface Screen { rows: ApplicationRow[]; total: number; counts?: ApplicationCounts; machine?: StateMachine; loading: boolean; error?: Issue }
const tone = { submitted: 'warn', provisioning: 'info', approved: 'ok', failed: 'error', rejected: 'neutral', withdrawn: 'neutral' } as const;
// 页签顺序按运营关注度：待审核在前，终态在后。
const tabs = ['all', 'submitted', 'provisioning', 'failed', 'approved', 'rejected', 'withdrawn'] as const;
const present = (value: unknown) => value !== '' && value !== undefined && value !== null;
function StatusBadge({ status }: { status: ApplicationStatus }) { return <Badge tone={tone[status]} dot>{i18n.t(`applications.status.${status}`)}</Badge>; }
const known = (key: string, value: string | null | undefined) => value ? i18n.instance.exists(`${key}.${value}`) ? i18n.t(`${key}.${value}`) : value : '—';

// 自动检查（规格 §5.2）：租户号可用、同名企业、申请人已加入的企业；级别从轻到重 pass / info / warning / error。
const levels: Record<string, { tone: 'ok' | 'info' | 'warn' | 'error'; icon: IconName; text: string }> = {
  pass: { tone: 'ok', icon: 'circleCheck', text: 'text-ok' }, info: { tone: 'info', icon: 'info', text: 'text-info' },
  warning: { tone: 'warn', icon: 'alert', text: 'text-warn' }, error: { tone: 'error', icon: 'circleX', text: 'text-error' },
};
function checkText(check: Check, code?: string): string {
  const total = count(check.count);
  if (check.kind === 'code') return check.level === 'pass' ? i18n.t('applications.check.codeOk', { code: code ?? '' }) : i18n.t(`applications.check.code.${check.reason === 'taken' || check.reason === 'reserved' ? check.reason : 'invalid'}`, { code: code ?? '' });
  if (check.kind === 'duplicateName') return check.level === 'pass' ? i18n.t('applications.check.nameOk') : i18n.t('applications.check.nameDuplicate', { count: total ?? 1 });
  if (check.kind === 'existingMemberships') return total === null ? i18n.t('applications.check.membershipsUnknown') : total === 0 ? i18n.t('applications.check.membershipsNone') : i18n.t('applications.check.memberships', { count: total });
  return check.reason ?? check.kind;
}
// 列表每行只带最需要关注的一条检查（宿主挑选）；它通过即表示全部通过。
function CheckBadge({ check, code }: { check: Check; code: string }) {
  const level = levels[check.level] ?? levels.info!;
  return <Badge tone={level.tone} dot>{check.level === 'pass' ? i18n.t('applications.check.allPassed') : checkText(check, code)}</Badge>;
}

function subscribeLanguage(listener: () => void) { i18n.instance.on('languageChanged', listener); return () => { i18n.instance.off('languageChanged', listener); }; }
const currentLanguage = () => i18n.instance.language;
const locales = ['zh-CN', 'zh-TW', 'en-US'] as const;
function isLocale(value: string | undefined): value is typeof locales[number] { return (locales as readonly string[]).includes(value ?? ''); }

function ApplicationsContent({ client, embedded = false }: { client: PlatformClient; embedded?: boolean }) {
  const api = useMemo(() => apiOf(client), [client]);
  const headingId = useId();
  const [query, setQuery] = useState<DataTableQuery>({ pageIndex: 0, pageSize: 10, sorting: [], filters: {} });
  const [revision, setRevision] = useState(0);
  const [view, setView] = useState<Screen>({ rows: [], total: 0, loading: true });
  const [notice, setNotice] = useState<{ message: string; error: boolean } | null>(null);
  const [busy, setBusy] = useState(false);
  const refreshButton = useRef<HTMLButtonElement>(null);
  const drawer = useDrawer(i18n);
  const confirm = useConfirm();

  useEffect(() => {
    const controller = new AbortController();
    const filters = query.filters;
    void Promise.all([
      api.GET('/api/signup/applications', { params: { query: { skip: query.pageIndex * query.pageSize, take: query.pageSize, keyword: filters.keyword || undefined, status: filters.status || undefined, size: filters.size || undefined } }, signal: controller.signal }),
      api.GET('/api/signup/meta/state-machine', { signal: controller.signal }),
    ]).then(([listed, described]) => {
      if (controller.signal.aborted) return;
      const list = data(listed); const machine = data(described);
      const total = count(list.total);
      if (!Array.isArray(list.items) || total === null || !Array.isArray(machine.actions)) throw new Error('signup.contract.response');
      setView({ rows: list.items, total, counts: list.counts, machine, loading: false });
    }).catch((error: unknown) => { if (!controller.signal.aborted) setView({ rows: [], total: 0, loading: false, error: issue(error) }); });
    return () => controller.abort();
  }, [api, query, revision]);

  function refresh() { setView((current) => ({ ...current, loading: true, error: undefined })); setRevision((value) => value + 1); }
  function changeQuery(next: DataTableQuery) { setView((current) => ({ ...current, loading: true, error: undefined })); setQuery(next); }
  function filter(key: 'keyword' | 'status' | 'size', value: string) { changeQuery({ ...query, pageIndex: 0, filters: { ...query.filters, [key]: value } }); }
  function completed(key: string, values?: Record<string, string>) { drawer.close(); setNotice({ message: i18n.t(key, values), error: false }); refresh(); refreshButton.current?.focus(); }
  async function review(row: Pick<ApplicationRow, 'id' | 'companyName' | 'number' | 'status'>) {
    setNotice(null);
    drawer.open({ title: row.companyName, description: row.number, size: 'lg', extra: <StatusBadge status={row.status} />, content: <Spinner label={i18n.t('applications.loading')} /> });
    try {
      const detail = data(await api.GET('/api/signup/applications/{id}', { params: { path: { id: row.id } } }));
      drawer.open({ title: detail.company.name, description: i18n.t('applications.submittedLine', { number: detail.number, time: moment(detail.submittedAt) }), size: 'lg', extra: <StatusBadge status={detail.status} />,
        content: <Review api={api} detail={detail} machine={view.machine} done={completed} /> });
    } catch (error) { drawer.open({ title: row.companyName, content: <Alert tone="error" role="alert">{issue(error).message}</Alert> }); }
  }
  async function retry(row: ApplicationRow) {
    setNotice(null);
    if (needsConfirmation(view.machine, 'retry') && !await confirm({ title: i18n.t('applications.confirmRetry'), description: i18n.t('applications.confirmRetryText', { company: row.companyName }) })) return;
    setBusy(true);
    try {
      const result = data(await api.POST('/api/signup/applications/{id}:retry', { params: { path: { id: row.id } } }));
      setNotice({ message: i18n.t(result.status === 'approved' ? 'applications.retried' : 'applications.retryFailed', { company: row.companyName }), error: result.status !== 'approved' });
      refresh(); refreshButton.current?.focus();
    } catch (error) { setNotice({ message: issue(error).message, error: true }); }
    finally { setBusy(false); }
  }
  const columns: DataColumn<ApplicationRow>[] = [
    { field: 'companyName', titleKey: 'applications.company', width: 240, render: (row) => <span className="flex min-w-0 flex-col">
      <Button variant="link" className="h-auto justify-start px-0 font-semibold text-foreground hover:bg-transparent hover:text-primary" onClick={() => void review(row)}>{row.companyName}</Button>
      <span className="truncate font-mono text-xs text-muted-foreground">{row.number}</span></span> },
    { field: 'code', titleKey: 'applications.code', width: 120, render: (row) => <span className="font-mono">{row.code}</span> },
    { field: 'contactName', titleKey: 'applications.applicant', width: 150, render: (row) => <span className="flex flex-col"><span>{row.contactName}</span><span className="font-mono text-xs text-muted-foreground">{row.phone}</span></span> },
    { field: 'size', titleKey: 'applications.size', width: 110, render: (row) => <span className="whitespace-nowrap">{known('signup.sizes', row.size)}</span> },
    { field: 'submittedAt', titleKey: 'applications.submittedAt', width: 150, render: (row) => <span className="whitespace-nowrap text-muted-foreground tabular-nums">{moment(row.submittedAt)}</span> },
    { field: 'check', titleKey: 'applications.checks', width: 170, render: (row) => row.check ? <CheckBadge check={row.check} code={row.code} /> : <span className="text-muted-foreground">—</span> },
    { field: 'status', titleKey: 'applications.statusColumn', width: 110, render: (row) => <StatusBadge status={row.status} /> },
    // 操作链接不换行，首个链接的文字与表头「操作」左对齐（链接按钮自带 px-2）。
    { field: 'actions', titleKey: 'applications.actions', width: 150, fixed: 'right', render: (row) => <div className="-ms-2 flex flex-nowrap gap-1 whitespace-nowrap">
      <Button size="sm" variant="link" className="h-7 px-2" onClick={() => void review(row)}>{i18n.t(row.status === 'submitted' ? 'applications.review' : 'applications.detail')}</Button>
      {row.status === 'failed' && <Button size="sm" variant="link" className="h-7 px-2" disabled={busy} onClick={() => void retry(row)}>{i18n.t('applications.retry')}</Button>}
    </div> },
  ];
  const denied = view.error?.status === 401 || view.error?.status === 403;
  const status = query.filters.status || 'all';
  const Content = embedded ? 'section' : 'main';
  return <div className={embedded ? undefined : 'min-h-dvh bg-canvas'}>
    <Content aria-labelledby={headingId} className={cn('min-w-0 space-y-4', embedded ? 'p-4 md:p-6' : 'mx-auto max-w-7xl px-6 py-8')}>
      <div className="min-w-0 space-y-1"><h1 id={headingId} className="text-[22px] font-bold leading-[30px] tracking-tight">{i18n.t('applications.title')}</h1><p className="text-sm text-muted-foreground">{i18n.t('applications.subtitle')}</p></div>
      {notice && <Alert role={notice.error ? 'alert' : 'status'} tone={notice.error ? 'error' : 'ok'}>{notice.message}</Alert>}
      {denied ? <Alert tone="warn" role="alert" title={i18n.t(view.error?.status === 403 ? 'applications.noAccess' : 'applications.signIn')} action={<Button variant="outline" size="sm" onClick={refresh}>{i18n.t('applications.refresh')}</Button>}>
        <code>{view.error?.status} {view.error?.code}</code></Alert>
        : <DataTable columns={columns} data={view.rows} total={view.total} query={query} onQueryChange={changeQuery} getRowId={(row) => row.id} i18n={i18n} label={i18n.t('applications.list')} loading={view.loading} error={view.error?.message} onRetry={refresh}
          header={<Tabs value={status} onValueChange={(value) => filter('status', value === 'all' ? '' : String(value))}>
            <TabsList variant="line" aria-label={i18n.t('applications.tabs')} className="px-5">{tabs.map((key) => <TabsTab key={key} value={key} count={view.counts ? count(view.counts[key]) ?? undefined : undefined}>{i18n.t(key === 'all' ? 'applications.all' : `applications.status.${key}`)}</TabsTab>)}</TabsList>
          </Tabs>}
          toolbar={<>
            <span className="relative w-full sm:w-72"><Icon name="search" className="pointer-events-none absolute start-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
              <Input aria-label={i18n.t('applications.search')} placeholder={i18n.t('applications.search')} className="w-full ps-9" value={query.filters.keyword ?? ''} onChange={(event) => filter('keyword', event.target.value)} /></span>
            <Select value={query.filters.size || 'all'} onValueChange={(value) => filter('size', value === 'all' || value === null ? '' : String(value))}
              items={[{ value: 'all', label: i18n.t('applications.allSizes') }, ...sizes.map((value) => ({ value, label: i18n.t(`signup.sizes.${value}`) }))]}>
              <SelectTrigger aria-label={i18n.t('applications.size')} className="w-40" /><SelectContent>
                <SelectItem value="all">{i18n.t('applications.allSizes')}</SelectItem>{sizes.map((value) => <SelectItem key={value} value={value}>{i18n.t(`signup.sizes.${value}`)}</SelectItem>)}
              </SelectContent></Select>
            {(query.filters.keyword || query.filters.size) ? <Button variant="link" onClick={() => changeQuery({ ...query, pageIndex: 0, filters: { ...query.filters, keyword: '', size: '' } })}>{i18n.t('applications.reset')}</Button> : null}
            <IconButton ref={refreshButton} variant="outline" label={i18n.t('applications.refresh')} className="ms-auto" onClick={refresh}><Icon name="refresh" /></IconButton>
          </>} />}
    </Content>{drawer.holder}
  </div>;
}

function DetailSection({ title, hint, children, testId }: { title: string; hint?: string; children: ReactNode; testId?: string }) {
  const id = useId();
  return <section aria-labelledby={id} className="grid gap-3" data-testid={testId}><h3 id={id} className="text-sm font-semibold">{title}{hint && <span className="ms-1.5 font-normal text-muted-foreground">{hint}</span>}</h3>{children}</section>;
}

/** 审核抽屉：企业信息、申请人、自动检查与开通设置；驳回切换到原因表单。通过与驳回按状态机元数据二次确认（契约标 humanOnly + confirm）。 */
function Review({ api, detail, machine, done }: { api: Api; detail: ApplicationDetail; machine?: StateMachine; done(key: string, values?: Record<string, string>): void }) {
  const confirm = useConfirm();
  const [mode, setMode] = useState<'approve' | 'reject'>('approve');
  const users = count(detail.defaults.users);
  const days = count(detail.defaults.trialDays);
  const company = detail.company;
  return <div className="grid gap-6" data-testid="application-review" data-status={detail.status}>
    <DetailSection title={i18n.t('applications.companyInfo')}><Descriptions layout="stacked" items={[
      { key: 'name', label: i18n.t('signup.companyName'), children: company.name },
      { key: 'code', label: i18n.t('applications.code'), children: <span className="font-mono">{company.code}</span> },
      { key: 'industry', label: i18n.t('signup.industry'), children: known('signup.industries', company.industry) },
      { key: 'size', label: i18n.t('signup.size'), children: known('signup.sizes', company.size) },
      { key: 'description', label: i18n.t('signup.description'), children: company.description || '—' },
    ]} /></DetailSection>
    <DetailSection title={i18n.t('applications.applicant')} hint={i18n.t('applications.applicantHint')}><Descriptions layout="stacked" items={[
      { key: 'name', label: i18n.t('signup.displayName'), children: detail.applicant.contactName },
      { key: 'phone', label: i18n.t('signup.phone'), children: <span className="inline-flex items-center gap-2"><span className="font-mono">{detail.applicant.phone}</span><Badge tone="ok" dot>{i18n.t('applications.verified')}</Badge></span> },
      { key: 'email', label: i18n.t('signup.email'), children: detail.applicant.email || '—' },
      { key: 'account', label: i18n.t('applications.account'), children: i18n.t(detail.applicant.existingAccount ? 'applications.existingAccount' : 'applications.newAccount') },
    ]} /></DetailSection>
    {/* 宿主只对进行中的申请做自动检查；已结束的申请没有检查项，不留空区块。 */}
    {detail.checks.length > 0 && <DetailSection title={i18n.t('applications.checks')} testId="application-checks">
      <ul className="grid gap-2">{detail.checks.map((check) => { const level = levels[check.level] ?? levels.info!; return <li key={check.kind} data-level={check.level} className="flex items-start gap-2.5 rounded-lg border border-border px-3 py-2.5 text-sm">
        <span className={cn('mt-0.5 inline-flex', level.text)}><Icon name={level.icon} /></span><span>{checkText(check, company.code)}</span></li>; })}</ul>
    </DetailSection>}
    {detail.status !== 'submitted' ? <Outcome detail={detail} />
      : mode === 'approve' ? <DetailSection title={i18n.t('applications.settings')} hint={days !== null && users !== null ? i18n.t('applications.defaults', { days: String(days), users: String(users) }) : undefined}>
        <SchemaForm key="approve" schema={approveSchema} i18n={i18n} testId="application-approve-form" submitLabel={i18n.t('applications.approve')} initialValues={{ code: company.code, plan: 'trial', ...(users !== null ? { users } : {}) }}
          onSubmit={async (values) => {
            const body = Object.fromEntries(Object.entries(values).filter(([, value]) => present(value))) as ApproveRequest;
            if (needsConfirmation(machine, 'approve') && !await confirm({ title: i18n.t('applications.confirmApprove'), description: i18n.t('applications.confirmApproveText', { company: company.name, code: String(body.code ?? company.code) }) })) return;
            const result = data(await api.POST('/api/signup/applications/{id}:approve', { params: { path: { id: detail.id } }, body }));
            done(result.status === 'failed' ? 'applications.approvedButFailed' : 'applications.approved', { company: company.name });
          }}>
          <Button type="button" variant="outline" className="text-destructive hover:bg-destructive/10" onClick={() => setMode('reject')} testId="application-to-reject">{i18n.t('applications.reject')}</Button>
        </SchemaForm></DetailSection>
      : <DetailSection title={i18n.t('applications.rejectTitle')} hint={i18n.t('applications.rejectHint', { code: company.code })}>
        <SchemaForm key="reject" schema={rejectSchema} i18n={i18n} testId="application-reject-form" submitLabel={i18n.t('applications.confirmRejectAction')} onSubmit={async (values) => {
          const body = Object.fromEntries(Object.entries(values).filter(([, value]) => present(value))) as RejectRequest;
          if (needsConfirmation(machine, 'reject') && !await confirm({ title: i18n.t('applications.confirmReject'), description: i18n.t('applications.confirmRejectText', { company: company.name }) })) return;
          await api.POST('/api/signup/applications/{id}:reject', { params: { path: { id: detail.id } }, body });
          done('applications.rejected', { company: company.name });
        }}>
          <Button type="button" variant="outline" onClick={() => setMode('approve')} testId="application-to-approve">{i18n.t('applications.back')}</Button>
        </SchemaForm></DetailSection>}
  </div>;
}

/** 已审核的申请：审核结果与开通步骤只读展示；开通失败时显示原因码（只给运营看）。 */
function Outcome({ detail }: { detail: ApplicationDetail }) {
  const review = detail.review;
  const provisioning = detail.provisioning;
  return <>
    {review && <DetailSection title={i18n.t('applications.outcome')} testId="application-outcome"><Descriptions layout="stacked" items={[
      { key: 'reviewer', label: i18n.t('applications.reviewer'), children: review.reviewer },
      { key: 'reviewedAt', label: i18n.t('applications.reviewedAt'), children: moment(review.reviewedAt) },
      ...(review.plan ? [{ key: 'plan', label: i18n.t('applications.plan'), children: i18n.t(`applications.plans.${review.plan}`) }] : []),
      ...(review.plan ? [{ key: 'validUntil', label: i18n.t('applications.validUntil'), children: review.validUntil ? day(review.validUntil) : i18n.t('applications.noExpiry') }] : []),
      ...(review.plan ? [{ key: 'users', label: i18n.t('applications.users'), children: count(review.users) ?? '—' }] : []),
      ...(review.rejectReason ? [{ key: 'reason', label: i18n.t('applications.reason'), children: review.rejectReason }] : []),
      { key: 'note', label: i18n.t('applications.note'), children: review.note || '—' },
    ]} /></DetailSection>}
    {provisioning && <DetailSection title={i18n.t('applications.provisioning')} testId="application-provisioning">
      {provisioning.failureReason && <Alert tone="error" title={i18n.t('applications.failureReason')}><code>{provisioning.failureReason}</code></Alert>}
      <ol className="grid gap-2">{provisioning.steps.map((step) => <li key={step.step} data-status={step.status} className="flex items-center justify-between gap-3 rounded-lg border border-border px-3 py-2 text-sm">
        <span className="font-mono">{step.step}</span><span className="text-muted-foreground">{step.status}{step.reason ? ` · ${step.reason}` : ''}</span></li>)}</ol>
    </DetailSection>}
  </>;
}

/** `locale` 由外壳上下文给出（remote 入口传入）；语言变化时整页按新语言重渲染。 */
export function ApplicationsPage({ client, embedded = false, locale }: { client: PlatformClient; embedded?: boolean; locale?: string }) {
  useSyncExternalStore(subscribeLanguage, currentLanguage, currentLanguage);
  useEffect(() => { if (isLocale(locale) && locale !== i18n.instance.language) void i18n.changeLanguage(locale); }, [locale]);
  return <FeedbackProvider i18n={i18n}><ApplicationsContent client={client} embedded={embedded} /></FeedbackProvider>;
}
