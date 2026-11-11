import { useEffect, useId, useMemo, useState, type ReactNode } from 'react';
import { Alert, Button, Descriptions, Icon, Result, Spinner, Steps, type IconName, type StepItem, type StepStatus } from '@idadawn/yue-core';
import { useConfirm } from '@idadawn/yue-form';
import type { PlatformClient } from '@idadawn/yue-sdk';
import { apiOf, data, day, issue, moment, unauthenticated, type Api, type Issue } from '../api';
import type { Applicant, ApplicationStatus, MyApplication } from '../contracts';
import { i18n } from '../i18n';
import { statusLabels, StatusFrame } from './frame';
import { browser, safeNext, withNext } from './navigation';
import { PhoneVerification } from './verification';

interface Screen { loading: boolean; applicant?: Applicant | null; items?: MyApplication[]; error?: Issue }
type Tone = 'ok' | 'info' | 'warn' | 'error' | 'neutral';
// 申请人一侧把开通失败也显示为开通中，不暴露内部原因（规格 §3）。
const results: Record<ApplicationStatus, { tone: Tone; icon: IconName; key: string }> = {
  submitted: { tone: 'warn', icon: 'clock', key: 'submitted' },
  provisioning: { tone: 'info', icon: 'settings', key: 'provisioning' },
  failed: { tone: 'info', icon: 'settings', key: 'provisioning' },
  approved: { tone: 'ok', icon: 'circleCheck', key: 'approved' },
  rejected: { tone: 'error', icon: 'circleX', key: 'rejected' },
  withdrawn: { tone: 'neutral', icon: 'undo', key: 'withdrawn' },
};
const pad = (value: number) => String(value).padStart(2, '0');
/** 步骤条上的时间只到月日时分，例如 10-20 14:32。 */
function short(value: string): string { const date = new Date(value); return `${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`; }
const hint = (key: string, values?: Record<string, string>) => i18n.t(`signup.stepHint.${key}`, values);
/** 开通四步：提交申请 → 平台审核 → 开通企业 → 登录使用；说明随状态变化。 */
function steps(item: MyApplication): StepItem[] {
  const step = (key: string, status: StepStatus, description: string): StepItem => ({ key, title: i18n.t(`signup.steps.${key}`), description, status });
  const submitted = step('submit', 'done', short(item.submittedAt));
  const ended = item.completedAt ? short(item.completedAt) : undefined;
  switch (item.status) {
    case 'submitted': return [submitted, step('review', 'current', hint('reviewing')), step('open', 'todo', hint('auto')), step('login', 'todo', hint('sms'))];
    case 'rejected': return [submitted, step('review', 'failed', ended ? hint('rejectedAt', { time: ended }) : hint('rejected')), step('open', 'todo', hint('none')), step('login', 'todo', hint('none'))];
    case 'withdrawn': return [submitted, step('review', 'todo', ended ? hint('withdrawnAt', { time: ended }) : hint('withdrawn')), step('open', 'todo', hint('none')), step('login', 'todo', hint('none'))];
    case 'approved': return [submitted, step('review', 'done', hint('approved')), step('open', 'done', ended ?? hint('approved')), step('login', 'current', hint('now'))];
    default: return [submitted, step('review', 'done', hint('approved')), step('open', 'current', hint('provisioning')), step('login', 'todo', hint('sms'))];
  }
}
function approvedText(item: MyApplication): string {
  const date = item.validUntil ? day(item.validUntil) : undefined;
  if (item.plan === 'trial') return i18n.t('signup.result.approvedTrial', { date: date ?? '—' });
  return date ? i18n.t('signup.result.approvedFormalUntil', { date }) : i18n.t('signup.result.approvedFormal');
}

/** 申请进度（公开页 /signup/status）：凭手机号 + 验证码建立的申请人会话查看本人最新一份申请（规格 §6.2）。 */
export function StatusPage({ client }: { client: PlatformClient }) {
  const api = useMemo(() => apiOf(client), [client]);
  const confirm = useConfirm();
  const [view, setView] = useState<Screen>({ loading: true });
  const [revision, setRevision] = useState(0);
  const [notice, setNotice] = useState<Issue>();
  const [busy, setBusy] = useState(false);
  const [next] = useState(() => safeNext(new URLSearchParams(location.search).get('next')));
  useEffect(() => {
    const controller = new AbortController();
    const signal = controller.signal;
    void (async (): Promise<Omit<Screen, 'loading'>> => {
      let applicant: Applicant | null = null;
      try { applicant = data(await api.GET('/api/signup/session', { signal })); }
      catch (error) { if (!unauthenticated(error)) throw error; }
      if (!applicant) return { applicant: null };
      return { applicant, items: data(await api.GET('/api/signup/me/applications', { signal })).items };
    })().then((screen) => { if (!signal.aborted) setView({ loading: false, ...screen }); },
      (error: unknown) => { if (!signal.aborted) setView({ loading: false, error: issue(error) }); });
    return () => controller.abort();
  }, [api, revision]);
  const reload = () => { setNotice(undefined); setView({ loading: true }); setRevision((value) => value + 1); };
  async function signOut() {
    try { await api.DELETE('/api/signup/session'); } catch (error) { if (!unauthenticated(error)) { setNotice(issue(error)); return; } }
    reload();
  }
  async function withdraw(item: MyApplication) {
    if (!await confirm({ title: i18n.t('signup.confirmWithdraw'), description: i18n.t('signup.confirmWithdrawText', { number: item.number }) })) return;
    setBusy(true);
    // 申请人的写请求只收 application/json（同源核对之外的防跨站约束）；撤回没有请求体，显式带上媒体类型。
    try { await api.POST('/api/signup/me/applications/{id}:withdraw', { params: { path: { id: item.id } }, headers: { 'Content-Type': 'application/json' } }); reload(); }
    catch (error) { setNotice(issue(error)); }
    finally { setBusy(false); }
  }
  const latest = view.items?.[0];
  return <StatusFrame applicant={view.applicant ?? undefined} onSignOut={() => void signOut()}>
    {notice && <Alert tone="error" role="alert">{notice.message}</Alert>}
    {view.loading ? <Spinner className="self-center" label={i18n.t('signup.loading')} />
      : view.error ? <Alert tone="error" role="alert" action={<Button variant="outline" size="sm" onClick={reload} testId="signup-status-retry">{i18n.t('signup.retry')}</Button>}>{view.error.message}</Alert>
      : !view.applicant ? <VerifyCard api={api} onVerified={reload} applyHref={withNext('/signup', next)} />
      : !latest ? <StatusCard testId="signup-status-empty"><h1 className="sr-only">{i18n.t('signup.statusTitle')}</h1>
        <Result tone="neutral" icon="inbox" title={i18n.t('signup.noApplication')} description={i18n.t('signup.noApplicationText')} className="gap-3 p-0"
          action={<Button size="lg" onClick={() => browser.assign(withNext('/signup', next))} testId="signup-apply-now">{i18n.t('signup.applyNow')}</Button>} /></StatusCard>
      : <ApplicationStatus item={latest} applicant={view.applicant} busy={busy} next={next} onWithdraw={() => void withdraw(latest)} />}
  </StatusFrame>;
}

/** 进度页的卡片：圆角 16、内边距 40/40/32、轻阴影，块间距 32。 */
function StatusCard({ testId, status, children }: { testId: string; status?: string; children: ReactNode }) {
  return <section className="flex flex-col gap-8 rounded-2xl border border-border bg-card px-6 pt-10 pb-8 shadow-xs sm:px-10" data-testid={testId} data-status={status}>{children}</section>;
}

function VerifyCard({ api, onVerified, applyHref }: { api: Api; onVerified(): void; applyHref: string }) {
  const id = useId();
  return <>
    <StatusCard testId="signup-status-verify">
      <header className="flex flex-col items-center gap-3 text-center">
        <span aria-hidden="true" className="inline-flex size-16 items-center justify-center rounded-full bg-primary-soft text-primary"><Icon name="phone" size={30} /></span>
        <h1 id={id} className="m-0 text-2xl font-bold">{i18n.t('signup.statusTitle')}</h1>
        <p className="m-0 max-w-130 text-caption-foreground">{i18n.t('signup.statusVerifyHint')}</p>
      </header>
      <div className="mx-auto w-full max-w-120"><PhoneVerification api={api} onVerified={onVerified} /></div>
    </StatusCard>
    <p className="m-0 text-center text-[13px] text-muted-foreground">{i18n.t('signup.noApplicationYet')} <a href={applyHref} data-testid="signup-apply-link" className="font-medium text-primary no-underline hover:underline">{i18n.t('signup.applyNow')}</a></p>
  </>;
}

function ApplicationStatus({ item, applicant, busy, next, onWithdraw }: { item: MyApplication; applicant: Applicant; busy: boolean; next?: string; onWithdraw(): void }) {
  const result = results[item.status];
  const description = item.status === 'approved' ? approvedText(item) : i18n.t(`signup.result.${result.key}Text`, { phone: applicant.phone, code: item.code });
  const notify = [i18n.t('signup.notifySms', { phone: item.phone }), item.email ? i18n.t('signup.notifyEmail', { email: item.email }) : null].filter(Boolean).join(i18n.t('signup.listSeparator'));
  const retry = () => browser.assign(withNext(`/signup?from=${encodeURIComponent(item.id)}`, next));
  const actions = [
    item.status === 'submitted' && <Button key="withdraw" variant="outline" size="lg" disabled={busy} onClick={onWithdraw} testId="signup-withdraw">{i18n.t('signup.withdraw')}</Button>,
    (item.status === 'rejected' || item.status === 'withdrawn') && <Button key="resubmit" size="lg" onClick={retry} testId="signup-resubmit">{i18n.t('signup.resubmit')}</Button>,
    // 获批后要先用手机号与密码登录才能进入企业控制台：带着同源登录页地址时直接去登录。
    item.status === 'approved' && next && <Button key="enter" size="lg" onClick={() => browser.assign(next)} testId="signup-enter-console">{i18n.t('signup.enterConsole')}<Icon name="arrowRight" /></Button>,
  ].filter(Boolean);
  return <>
    <StatusCard testId="signup-status" status={item.status}>
      <h1 className="sr-only">{i18n.t('signup.statusTitle')}</h1>
      <Result tone={result.tone} icon={result.icon} title={item.status === 'approved' ? i18n.t('signup.result.approved', { company: item.companyName }) : i18n.t(`signup.result.${result.key}`)}
        description={description} className="gap-3 p-0" testId="signup-result" />
      <Steps items={steps(item)} label={i18n.t('signup.progress')} statusLabels={statusLabels()} testId="signup-steps" />
      {item.status === 'rejected' && item.rejectReason && <Alert tone="error" title={i18n.t('signup.rejectReason')} testId="signup-reject-reason">{item.rejectReason}</Alert>}
      <div className="rounded-xl border border-border/70 bg-muted/50 px-6 py-5">
        <Descriptions layout="grid" aria-label={i18n.t('signup.summary')} testId="signup-summary" items={[
          { key: 'number', label: i18n.t('signup.number'), children: <span className="font-mono">{item.number}</span> },
          { key: 'submitted', label: i18n.t('signup.submittedAt'), children: moment(item.submittedAt) },
          { key: 'company', label: i18n.t('signup.companyName'), children: item.companyName },
          { key: 'code', label: i18n.t('signup.tenantCode'), children: <span className="font-mono">{item.code}</span> },
          { key: 'notify', label: i18n.t('signup.notify'), children: notify },
        ]} />
      </div>
      {actions.length > 0 && <div className="flex flex-wrap justify-center gap-3">{actions}</div>}
    </StatusCard>
    <p className="m-0 text-center text-[13px] text-muted-foreground" data-testid="signup-status-footnote">{i18n.t(`signup.footer.${result.key}`, { phone: applicant.phone })}</p>
  </>;
}
