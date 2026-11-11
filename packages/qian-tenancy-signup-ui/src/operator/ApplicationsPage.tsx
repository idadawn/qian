import { useEffect, useId, useMemo, useRef, useState, useSyncExternalStore, type ReactNode } from 'react';
import { Alert, Badge, Button, cn, Descriptions, Field, FieldLabel, Icon, IconButton, Input, Select, SelectContent, SelectItem, SelectTrigger, Sheet, SheetBody, SheetContent, SheetDescription,
  SheetFooter, SheetHeader, SheetTitle, Spinner, Tabs, TabsList, TabsTab, Tag, Textarea, Toggle, type IconName } from '@idadawn/yue-core';
import { DataTable, type DataColumn, type DataTableQuery } from '@idadawn/yue-data';
import { FeedbackProvider, SchemaForm, useConfirm, type FormApi } from '@idadawn/yue-form';
import { PlatformError, type PlatformClient, type PlatformI18n } from '@idadawn/yue-sdk';
import { apiOf, data, day, issue, moment, type Api, type Issue } from '../api';
import { approveSchema, count, needsConfirmation, reasonLimit, sizes, type ApplicationCounts, type ApplicationDetail, type ApplicationRow, type ApplicationStatus,
  type ApproveRequest, type Check, type StateMachine } from '../contracts';
import { i18n } from '../i18n';

/** at：这一页数据取回的时刻，相对时间（「2 小时前」）以它为准，刷新后随之更新。 */
interface Screen { rows: ApplicationRow[]; total: number; counts?: ApplicationCounts; machine?: StateMachine; loading: boolean; at?: number; error?: Issue }
const tone = { submitted: 'warn', provisioning: 'info', approved: 'ok', failed: 'error', rejected: 'neutral', withdrawn: 'neutral' } as const;
// 页签顺序按运营关注度：待审核在前（默认），全部在最后；「开通失败」只在有失败的申请（或正停在该页签）时出现，紧跟「开通中」。
const tabs = ['submitted', 'provisioning', 'failed', 'approved', 'rejected', 'withdrawn', 'all'] as const;
const present = (value: unknown) => value !== '' && value !== undefined && value !== null;
function StatusBadge({ status }: { status: ApplicationStatus }) { return <Badge tone={tone[status]} dot testId="application-status">{i18n.t(`applications.status.${status}`)}</Badge>; }
const known = (key: string, value: string | null | undefined) => value ? i18n.instance.exists(`${key}.${value}`) ? i18n.t(`${key}.${value}`) : value : '—';
const pad = (value: number) => String(value).padStart(2, '0');
/** 列表里的提交时间：今年的只写月日时分（10-20 14:32），往年的带年份。 */
function stamp(value: string, now: number): string {
  const date = new Date(value);
  return date.getFullYear() === new Date(now).getFullYear() ? `${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}` : moment(value);
}
/** 相对时间：同一天按分钟、小时，前一天写「昨天」，更早按自然日。 */
function ago(value: string, now: number): string {
  const time = new Date(value).getTime();
  const elapsed = now - time;
  if (!Number.isFinite(time) || elapsed < 60_000) return i18n.t('applications.ago.justNow');
  const midnight = (moment: number) => { const date = new Date(moment); date.setHours(0, 0, 0, 0); return date.getTime(); };
  const days = Math.round((midnight(now) - midnight(time)) / 86_400_000);
  if (days <= 0) return elapsed < 3_600_000 ? i18n.t('applications.ago.minutes', { count: Math.floor(elapsed / 60_000) }) : i18n.t('applications.ago.hours', { count: Math.floor(elapsed / 3_600_000) });
  return days === 1 ? i18n.t('applications.ago.yesterday') : i18n.t('applications.ago.days', { count: days });
}

// 自动检查（规格 §5.2）：租户号可用、同名企业、申请人已加入的企业；级别从轻到重 pass / info / warning / error。
const levels: Record<string, { tone: 'ok' | 'info' | 'warn' | 'error'; icon: IconName; iconColor: string }> = {
  pass: { tone: 'ok', icon: 'circleCheck', iconColor: 'text-ok-icon' }, info: { tone: 'info', icon: 'info', iconColor: 'text-info-icon' },
  warning: { tone: 'warn', icon: 'alert', iconColor: 'text-warn-icon' }, error: { tone: 'error', icon: 'circleX', iconColor: 'text-error-icon' },
};
const reasonOf = (check: Check) => check.reason === 'taken' || check.reason === 'reserved' ? check.reason : 'invalid';
// 文案里的租户号用等宽字：按占位标记切开译文，把租户号放回去。
const MARK = String.fromCharCode(0xe000);
function withCode(key: string, code: string): ReactNode {
  const [before, after = ''] = i18n.t(key, { code: MARK }).split(MARK);
  return <>{before}<span className="font-mono">{code}</span>{after}</>;
}
function checkText(check: Check, code: string): ReactNode {
  const total = count(check.count);
  if (check.kind === 'code') return withCode(check.level === 'pass' ? 'applications.check.codeOk' : `applications.check.code.${reasonOf(check)}`, code);
  if (check.kind === 'duplicateName') return check.level === 'pass' ? i18n.t('applications.check.nameOk') : i18n.t('applications.check.nameDuplicate', { count: total ?? 1 });
  if (check.kind === 'existingMemberships') return total === null ? i18n.t('applications.check.membershipsUnknown') : total === 0 ? i18n.t('applications.check.membershipsNone') : i18n.t('applications.check.memberships', { count: total });
  return check.reason ?? check.kind;
}
/** 列表每行只带最需要关注的一条检查（宿主挑选）：它通过即表示全部通过；其余给一句短语。 */
function tagText(check: Check): string {
  if (check.level === 'pass') return i18n.t('applications.tag.pass');
  const total = count(check.count);
  if (check.kind === 'code') return i18n.t(`applications.tag.code.${reasonOf(check)}`);
  if (check.kind === 'duplicateName') return i18n.t('applications.tag.duplicateName');
  if (check.kind === 'existingMemberships') return total ? i18n.t('applications.tag.memberships', { count: total }) : i18n.t('applications.tag.membershipsUnknown');
  return check.reason ?? check.kind;
}
function CheckTag({ check }: { check: Check }) { return <Tag tone={(levels[check.level] ?? levels.info!).tone} dot testId="application-check">{tagText(check)}</Tag>; }

function subscribeLanguage(listener: () => void) { i18n.instance.on('languageChanged', listener); return () => { i18n.instance.off('languageChanged', listener); }; }
const currentLanguage = () => i18n.instance.language;
const locales = ['zh-CN', 'zh-TW', 'en-US'] as const;
function isLocale(value: string | undefined): value is typeof locales[number] { return (locales as readonly string[]).includes(value ?? ''); }

type Reviewing = { row: ApplicationRow; detail?: ApplicationDetail; error?: string };
const initialQuery: DataTableQuery = { pageIndex: 0, pageSize: 10, sorting: [], filters: { status: 'submitted' } };

function ApplicationsContent({ client, embedded = false }: { client: PlatformClient; embedded?: boolean }) {
  const api = useMemo(() => apiOf(client), [client]);
  const headingId = useId();
  const [query, setQuery] = useState<DataTableQuery>(initialQuery);
  const [revision, setRevision] = useState(0);
  const [view, setView] = useState<Screen>({ rows: [], total: 0, loading: true });
  const [notice, setNotice] = useState<{ message: string; error: boolean } | null>(null);
  const [busy, setBusy] = useState(false);
  const [reviewing, setReviewing] = useState<Reviewing | null>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);
  // 关闭动画结束才卸下抽屉内容；动画期间又打开了另一条申请时不卸。
  const drawerOpenRef = useRef(false);
  const showDrawer = (open: boolean) => { drawerOpenRef.current = open; setDrawerOpen(open); };
  const refreshButton = useRef<HTMLButtonElement>(null);
  // 审核完成后焦点落到刷新按钮（原来那一行可能已不在当前页签）；取消或关闭时回到打开抽屉的按钮。
  const focusAfterClose = useRef<HTMLElement | null>(null);
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
      setView({ rows: list.items, total, counts: list.counts, machine, loading: false, at: Date.now() });
    }).catch((error: unknown) => { if (!controller.signal.aborted) setView({ rows: [], total: 0, loading: false, error: issue(error) }); });
    return () => controller.abort();
  }, [api, query, revision]);

  function refresh() { setView((current) => ({ ...current, loading: true, error: undefined })); setRevision((value) => value + 1); }
  function changeQuery(next: DataTableQuery) { setView((current) => ({ ...current, loading: true, error: undefined })); setQuery(next); }
  function filter(key: 'keyword' | 'status' | 'size', value: string) { changeQuery({ ...query, pageIndex: 0, filters: { ...query.filters, [key]: value } }); }
  function completed(key: string, values?: Record<string, string>) { focusAfterClose.current = refreshButton.current; showDrawer(false); setNotice({ message: i18n.t(key, values), error: false }); refresh(); }
  async function review(row: ApplicationRow) {
    setNotice(null);
    focusAfterClose.current = null;
    setReviewing({ row }); showDrawer(true);
    const settle = (next: Reviewing) => setReviewing((current) => current?.row.id === row.id ? next : current);
    try { settle({ row, detail: data(await api.GET('/api/signup/applications/{id}', { params: { path: { id: row.id } } })) }); }
    catch (error) { settle({ row, error: issue(error).message }); }
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
  const now = view.at ?? 0;
  const columns: DataColumn<ApplicationRow>[] = [
    { field: 'companyName', titleKey: 'applications.company', width: 230, render: (row) => <span className="flex min-w-0 flex-col">
      <span className="truncate font-semibold">{row.companyName}</span><span className="truncate font-mono text-xs leading-4.5 text-muted-foreground">{row.number}</span></span> },
    { field: 'code', titleKey: 'applications.code', width: 115, render: (row) => <span className="font-mono">{row.code}</span> },
    // 申请都经过短信核验：掩码号码后的绿色对勾读作「已验证」。
    { field: 'contactName', titleKey: 'applications.applicant', width: 140, render: (row) => <span className="flex flex-col"><span>{row.contactName}</span>
      <span className="inline-flex items-center gap-1 text-xs leading-4.5 whitespace-nowrap text-muted-foreground">{row.phone}<Icon name="check" size={12} strokeWidth={2.5} label={i18n.t('applications.verified')} className="text-ok-icon" /></span></span> },
    { field: 'size', titleKey: 'applications.sizeColumn', width: 120, render: (row) => <span className="whitespace-nowrap text-subtle-foreground">{known('signup.sizes', row.size)}</span> },
    { field: 'submittedAt', titleKey: 'applications.submittedAt', width: 127, render: (row) => <span className="flex flex-col whitespace-nowrap">
      <span className="tabular-nums">{stamp(row.submittedAt, now)}</span><span className="text-xs leading-4.5 text-muted-foreground">{ago(row.submittedAt, now)}</span></span> },
    { field: 'check', titleKey: 'applications.checks', width: 207, render: (row) => row.check ? <CheckTag check={row.check} /> : <span className="text-muted-foreground">—</span> },
    { field: 'status', titleKey: 'applications.statusColumn', width: 104, render: (row) => <StatusBadge status={row.status} /> },
    { field: 'actions', titleKey: 'applications.actions', width: 110, align: 'end', fixed: 'right', render: (row) => <span className="inline-flex flex-nowrap justify-end gap-2">
      {row.status === 'failed' && <Button size="xs" variant="soft" disabled={busy} onClick={() => void retry(row)} testId="application-retry">{i18n.t('applications.retry')}</Button>}
      <Button size="xs" variant={row.status === 'submitted' ? 'soft' : 'outline'} onClick={() => void review(row)} testId="application-open">{i18n.t(row.status === 'submitted' ? 'applications.review' : 'applications.view')}</Button>
    </span> },
  ];
  const denied = view.error?.status === 401 || view.error?.status === 403;
  const status = query.filters.status || 'all';
  const visibleTabs = tabs.filter((key) => key !== 'failed' || status === 'failed' || (count(view.counts?.failed) ?? 0) > 0);
  const Content = embedded ? 'section' : 'main';
  return <div className={embedded ? undefined : 'min-h-dvh bg-canvas'}>
    <Content aria-labelledby={headingId} className={cn('min-w-0 space-y-4', embedded ? 'p-4 md:p-6' : 'mx-auto max-w-7xl px-6 py-8')}>
      <div className="flex min-w-0 flex-col gap-1"><h1 id={headingId} className="m-0 text-[22px] font-bold leading-7.5">{i18n.t('applications.title')}</h1><p className="m-0 text-muted-foreground">{i18n.t('applications.subtitle')}</p></div>
      {notice && <Alert role={notice.error ? 'alert' : 'status'} tone={notice.error ? 'error' : 'ok'}>{notice.message}</Alert>}
      {denied ? <Alert tone="warn" role="alert" title={i18n.t(view.error?.status === 403 ? 'applications.noAccess' : 'applications.signIn')} action={<Button variant="outline" size="sm" onClick={refresh} testId="applications-denied-refresh">{i18n.t('applications.refresh')}</Button>}>
        <code>{view.error?.status} {view.error?.code}</code></Alert>
        : <DataTable columns={columns} data={view.rows} total={view.total} query={query} onQueryChange={changeQuery} getRowId={(row) => row.id} i18n={i18n} label={i18n.t('applications.list')} loading={view.loading} error={view.error?.message} onRetry={refresh}
          columnToggle={false} density="comfortable" testId="applications-table"
          header={<Tabs value={status} onValueChange={(value) => filter('status', value === 'all' ? '' : String(value))}>
            <TabsList variant="line" countVariant="pill" aria-label={i18n.t('applications.tabs')} className="px-5">{visibleTabs.map((key) => <TabsTab key={key} value={key} testId={`applications-tab-${key}`} count={view.counts ? count(view.counts[key]) ?? undefined : undefined}>{i18n.t(key === 'all' ? 'applications.all' : `applications.status.${key}`)}</TabsTab>)}</TabsList>
          </Tabs>}
          toolbar={<>
            <span className="relative w-full sm:w-80"><Icon name="search" className="pointer-events-none absolute start-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
              <Input type="search" aria-label={i18n.t('applications.searchLabel')} placeholder={i18n.t('applications.search')} className="w-full ps-9" value={query.filters.keyword ?? ''} onChange={(event) => filter('keyword', event.target.value)} testId="applications-search" /></span>
            <Select value={query.filters.size || 'all'} onValueChange={(value) => filter('size', value === 'all' || value === null ? '' : String(value))}
              items={[{ value: 'all', label: i18n.t('applications.allSizes') }, ...sizes.map((value) => ({ value, label: i18n.t(`signup.sizes.${value}`) }))]}>
              <SelectTrigger aria-label={i18n.t('applications.size')} className="w-auto min-w-30 text-control-foreground" testId="applications-size" /><SelectContent>
                <SelectItem value="all" testId="applications-size-all">{i18n.t('applications.allSizes')}</SelectItem>{sizes.map((value) => <SelectItem key={value} value={value} testId={`applications-size-${value}`}>{i18n.t(`signup.sizes.${value}`)}</SelectItem>)}
              </SelectContent></Select>
            <Button variant="link" onClick={() => changeQuery({ ...query, pageIndex: 0, filters: { ...query.filters, keyword: '', size: '' } })} testId="applications-reset">{i18n.t('applications.reset')}</Button>
          </>}
          toolbarEnd={<IconButton ref={refreshButton} variant="outline" label={i18n.t('applications.refresh')} className="text-subtle-foreground" onClick={refresh} testId="applications-refresh"><Icon name="refresh" /></IconButton>} />}
    </Content>
    <Sheet open={drawerOpen} onOpenChange={(open) => { if (!open) showDrawer(false); }} onOpenChangeComplete={(open) => { if (!open && !drawerOpenRef.current) setReviewing(null); }}>
      {reviewing && <SheetContent size="md" closeLabel={i18n.t('common.close')} testId="application-drawer" finalFocus={() => focusAfterClose.current ?? true}>
        <Review key={reviewing.row.id} api={api} state={reviewing} machine={view.machine} done={completed} onClose={() => showDrawer(false)} />
      </SheetContent>}
    </Sheet>
  </div>;
}

function DetailSection({ title, hint, children, testId, className }: { title: string; hint?: string; children: ReactNode; testId?: string; className?: string }) {
  const id = useId();
  return <section aria-labelledby={id} className={cn('flex flex-col gap-3', className)} data-testid={testId}>
    <h3 id={id} className="m-0 text-sm font-semibold leading-5">{title}{hint && <span className="ms-1.5 font-normal text-muted-foreground">{hint}</span>}</h3>{children}
  </section>;
}

const presets = ['incomplete', 'mismatch', 'duplicate', 'code'] as const;
/**
 * 审核抽屉（600px）：页头是标题、状态徽标与「编号 · 提交时间」；主体分企业信息、申请人、自动检查与开通设置；页脚固定。
 * 驳回切到原因分节（常用原因一键填入，申请人可见）。通过与驳回按状态机元数据二次确认（契约标 humanOnly + confirm）。
 */
function Review({ api, state, machine, done, onClose }: { api: Api; state: Reviewing; machine?: StateMachine; done(key: string, values?: Record<string, string>): void; onClose(): void }) {
  const confirm = useConfirm();
  const { row, detail } = state;
  const status = detail?.status ?? row.status;
  const reviewing = status === 'submitted';
  const [mode, setMode] = useState<'approve' | 'reject'>('approve');
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<string>();
  const [reason, setReason] = useState('');
  const [picked, setPicked] = useState<typeof presets[number]>();
  const [reasonError, setReasonError] = useState<string>();
  const form = useRef<FormApi>(null);
  const reasonInput = useRef<HTMLTextAreaElement>(null);
  const rejectId = useId();
  const users = detail ? count(detail.defaults.users) : null;
  const days = detail ? count(detail.defaults.trialDays) : null;
  const company = detail?.company;
  // 到期日的标签与说明随开通方式变化：翻译时读表单当前的开通方式。
  const planI18n = useMemo<PlatformI18n>(() => ({ ...i18n, t: (key, values) => {
    const formal = form.current?.getValues().plan === 'formal';
    if (key === 'applications.validUntil') return i18n.t(formal ? 'applications.validUntilFormal' : 'applications.validUntilTrial');
    if (key === 'applications.validUntilHint') return formal ? i18n.t('applications.validUntilFormalHint') : days !== null ? i18n.t('applications.validUntilTrialHint', { days }) : i18n.t('applications.validUntilTrialHintDefault');
    return i18n.t(key, values);
  } }), [days]);
  async function approve() {
    const current = form.current;
    if (!current || !detail || !company || !await current.validate()) return;
    setFailure(undefined);
    const body = Object.fromEntries(Object.entries(current.getValues()).filter(([, value]) => present(value))) as ApproveRequest;
    if (needsConfirmation(machine, 'approve') && !await confirm({ title: i18n.t('applications.confirmApprove'), description: i18n.t('applications.confirmApproveText', { company: company.name, code: String(body.code ?? company.code) }) })) return;
    setBusy(true);
    try {
      const result = data(await api.POST('/api/signup/applications/{id}:approve', { params: { path: { id: detail.id } }, body }));
      done(result.status === 'failed' ? 'applications.approvedButFailed' : 'applications.approved', { company: company.name });
    } catch (error) { if (error instanceof PlatformError) current.setErrors(error.detail); else setFailure(issue(error).message); }
    finally { setBusy(false); }
  }
  async function reject() {
    if (!detail || !company) return;
    const text = reason.trim();
    const problem = text.length < reasonLimit.min ? i18n.t('common.required') : text.length > reasonLimit.max ? i18n.t('applications.reasonTooLong', { max: reasonLimit.max }) : undefined;
    if (problem) { setReasonError(problem); reasonInput.current?.focus(); return; }
    setFailure(undefined);
    if (needsConfirmation(machine, 'reject') && !await confirm({ title: i18n.t('applications.confirmReject'), description: i18n.t('applications.confirmRejectText', { company: company.name }) })) return;
    setBusy(true);
    try { await api.POST('/api/signup/applications/{id}:reject', { params: { path: { id: detail.id } }, body: { reason: text } }); done('applications.rejected', { company: company.name }); }
    catch (error) { if (error instanceof PlatformError && error.detail.pointer === '#/reason') setReasonError(error.detail.message); else setFailure(issue(error).message); }
    finally { setBusy(false); }
  }
  function choose(key: typeof presets[number]) { setPicked(key); setReason(i18n.t(`applications.reasonTemplates.${key}`)); setReasonError(undefined); }
  return <>
    <SheetHeader>
      <div className="flex min-h-8 flex-wrap items-center gap-3"><SheetTitle>{i18n.t(reviewing ? 'applications.reviewTitle' : 'applications.detailTitle')}</SheetTitle><StatusBadge status={status} /></div>
      <SheetDescription><span className="font-mono">{row.number}</span>{i18n.t('applications.submittedSuffix', { time: moment(detail?.submittedAt ?? row.submittedAt) })}</SheetDescription>
    </SheetHeader>
    <SheetBody>
      {failure && <Alert className="mb-5" tone="error" role="alert">{failure}</Alert>}
      {state.error ? <Alert tone="error" role="alert">{state.error}</Alert>
        : !detail || !company ? <Spinner label={i18n.t('applications.loading')} />
        : <div className="flex flex-col gap-6" data-testid="application-review" data-status={detail.status}>
          <DetailSection title={i18n.t('applications.companyInfo')}><Descriptions layout="stacked" items={[
            { key: 'name', label: i18n.t('signup.companyName'), children: company.name },
            { key: 'code', label: i18n.t('applications.code'), children: <span className="font-mono">{company.code}</span> },
            { key: 'industry', label: i18n.t('signup.industry'), children: known('signup.industries', company.industry) },
            { key: 'size', label: i18n.t('signup.size'), children: known('signup.sizes', company.size) },
            { key: 'description', label: i18n.t('signup.description'), children: company.description || '—', span: 'full' },
          ]} /></DetailSection>
          <DetailSection title={i18n.t('applications.applicant')} hint={i18n.t('applications.applicantHint')}><Descriptions layout="stacked" items={[
            { key: 'name', label: i18n.t('signup.displayName'), children: detail.applicant.contactName },
            { key: 'phone', label: i18n.t('signup.phone'), children: <span className="inline-flex flex-wrap items-center gap-2">{detail.applicant.phone}<Tag tone="ok" testId="application-verified">{i18n.t('applications.verified')}</Tag></span> },
            { key: 'email', label: i18n.t('signup.email'), children: detail.applicant.email || '—' },
            { key: 'account', label: i18n.t('applications.account'), children: i18n.t(detail.applicant.existingAccount ? 'applications.existingAccount' : 'applications.newAccount') },
          ]} /></DetailSection>
          {/* 宿主只对进行中的申请做自动检查；已结束的申请没有检查项，不留空区块。 */}
          {detail.checks.length > 0 && <DetailSection title={i18n.t('applications.checks')} testId="application-checks" className="gap-2.5">
            <ul className="m-0 flex list-none flex-col rounded-lg border border-border p-0">{detail.checks.map((check) => { const level = levels[check.level] ?? levels.info!; return <li key={check.kind} data-level={check.level} className="flex items-center gap-2.5 border-b border-border/70 px-3.5 py-2.5 last:border-b-0">
              <Icon name={level.icon} size={18} strokeWidth={2} className={level.iconColor} /><span>{checkText(check, company.code)}</span></li>; })}</ul>
          </DetailSection>}
          {!reviewing ? <Outcome detail={detail} />
            : mode === 'approve' ? <DetailSection title={i18n.t('applications.settings')} className="gap-3.5">
              <SchemaForm schema={approveSchema} i18n={planI18n} apiRef={form} testId="application-approve-form" layout="grid" columnWidth="sm" className="grid gap-3.5 [&_input[name=code]]:font-mono"
                initialValues={{ code: company.code, plan: 'trial', ...(users !== null ? { users } : {}) }} fieldAdornment={(path) => path === 'users' ? { unit: i18n.t('applications.usersUnit') } : null} />
            </DetailSection>
            : <section aria-labelledby={rejectId} className="flex flex-col gap-3 rounded-xl border border-error-border bg-error-bg/50 p-4" data-testid="application-reject-form">
              <h3 id={rejectId} className="m-0 text-sm font-semibold leading-5 text-error-strong">{i18n.t('applications.rejectTitle')} <span aria-hidden="true" className="text-destructive">*</span></h3>
              <div role="group" aria-label={i18n.t('applications.reasonPresets')} className="flex flex-wrap gap-2">{presets.map((key) => <Toggle key={key} pressed={picked === key} onPressedChange={() => choose(key)} testId={`application-reason-${key}`}
                className="h-7 rounded-full border-border-strong bg-background px-2.5 text-[13px] text-control-foreground hover:bg-background hover:text-foreground data-[pressed]:border-error-solid data-[pressed]:bg-error-bg data-[pressed]:text-error">{i18n.t(`applications.reasons.${key}`)}</Toggle>)}</div>
              <Field invalid={!!reasonError} className="gap-1.5">
                <FieldLabel className="sr-only">{i18n.t('applications.reason')}</FieldLabel>
                <Textarea ref={reasonInput} value={reason} onChange={(event) => { setReason(event.target.value); if (reasonError) setReasonError(undefined); }} aria-required aria-invalid={!!reasonError}
                  aria-describedby={`${rejectId}-hint${reasonError ? ` ${rejectId}-error` : ''}`} className="bg-background py-2" testId="application-reason" />
                {reasonError && <p id={`${rejectId}-error`} role="alert" className="m-0 text-xs leading-4.5 text-destructive">{reasonError}</p>}
              </Field>
              <p id={`${rejectId}-hint`} className="m-0 text-xs leading-4.5 text-error-strong">{i18n.t('applications.rejectHint', { code: company.code })}</p>
            </section>}
        </div>}
    </SheetBody>
    <SheetFooter>
      {reviewing && detail && mode === 'approve' ? <>
        <Button variant="destructive-outline" className="me-auto px-4" disabled={busy} onClick={() => { setFailure(undefined); setMode('reject'); }} testId="application-to-reject">{i18n.t('applications.reject')}</Button>
        <Button variant="outline" className="px-4" onClick={onClose} testId="application-cancel">{i18n.t('common.cancel')}</Button>
        <Button className="px-4.5" loading={busy} onClick={() => void approve()} testId="application-approve">{i18n.t('applications.approve')}</Button>
      </> : reviewing && detail ? <>
        <Button variant="outline" className="me-auto gap-1.5" disabled={busy} onClick={() => { setFailure(undefined); setMode('approve'); }} testId="application-to-approve"><Icon name="chevronLeft" />{i18n.t('applications.back')}</Button>
        <Button variant="destructive" className="px-4.5" loading={busy} onClick={() => void reject()} testId="application-reject">{i18n.t('applications.confirmRejectAction')}</Button>
      </> : <Button variant="outline" className="px-4" onClick={onClose} testId="application-close">{i18n.t('common.close')}</Button>}
    </SheetFooter>
  </>;
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
      ...(review.plan ? [{ key: 'validUntil', label: i18n.t('applications.validUntilOutcome'), children: review.validUntil ? day(review.validUntil) : i18n.t('applications.noExpiry') }] : []),
      ...(review.plan ? [{ key: 'users', label: i18n.t('applications.users'), children: count(review.users) ?? '—' }] : []),
      ...(review.rejectReason ? [{ key: 'reason', label: i18n.t('applications.reason'), children: review.rejectReason, span: 'full' as const }] : []),
      { key: 'note', label: i18n.t('applications.note'), children: review.note || '—', span: 'full' },
    ]} /></DetailSection>}
    {provisioning && <DetailSection title={i18n.t('applications.provisioning')} testId="application-provisioning">
      {provisioning.failureReason && <Alert tone="error" title={i18n.t('applications.failureReason')}><code>{provisioning.failureReason}</code></Alert>}
      <ol className="m-0 flex list-none flex-col rounded-lg border border-border p-0">{provisioning.steps.map((step) => <li key={step.step} data-status={step.status} className="flex items-center justify-between gap-3 border-b border-border/70 px-3.5 py-2.5 last:border-b-0">
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
