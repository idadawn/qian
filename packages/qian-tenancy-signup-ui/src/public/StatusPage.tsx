import { useEffect, useId, useMemo, useState } from 'react';
import { Alert, Button, Descriptions, Empty, Icon, Result, Spinner, Steps, type IconName, type StepItem, type StepStatus } from '@idadawn/yue-core';
import { useConfirm } from '@idadawn/yue-form';
import type { PlatformClient } from '@idadawn/yue-sdk';
import { apiOf, data, day, issue, moment, unauthenticated, type Issue } from '../api';
import type { Applicant, ApplicationStatus, MyApplication } from '../contracts';
import { i18n } from '../i18n';
import { SectionHeader, statusLabels, StatusFrame } from './frame';
import { browser, safeNext, withNext } from './navigation';
import { PhoneVerification } from './verification';

interface Screen { loading: boolean; applicant?: Applicant | null; items?: MyApplication[]; error?: Issue }
type Tone = 'ok' | 'info' | 'warn' | 'error' | 'neutral';
// 申请人一侧把开通失败也显示为开通中，不暴露内部原因（规格 §3）。
const results: Record<ApplicationStatus, { tone: Tone; icon: IconName; key: string }> = {
  submitted: { tone: 'info', icon: 'clock', key: 'submitted' },
  provisioning: { tone: 'info', icon: 'settings', key: 'provisioning' },
  failed: { tone: 'info', icon: 'settings', key: 'provisioning' },
  approved: { tone: 'ok', icon: 'circleCheck', key: 'approved' },
  rejected: { tone: 'error', icon: 'circleX', key: 'rejected' },
  withdrawn: { tone: 'neutral', icon: 'undo', key: 'withdrawn' },
};
function steps(item: MyApplication): StepItem[] {
  const review: StepStatus = item.status === 'submitted' ? 'current' : item.status === 'rejected' || item.status === 'withdrawn' ? 'failed' : 'done';
  const open: StepStatus = item.status === 'approved' ? 'done' : item.status === 'provisioning' || item.status === 'failed' ? 'current' : 'todo';
  return [
    { key: 'apply', title: i18n.t('signup.flowApply'), description: moment(item.submittedAt), status: 'done' },
    { key: 'review', title: i18n.t('signup.flowReview'), description: i18n.t(`signup.reviewState.${results[item.status].key}`), status: review },
    { key: 'use', title: i18n.t('signup.flowUse'), description: item.status === 'approved' && item.completedAt ? moment(item.completedAt) : undefined, status: open },
  ];
}
function approvedText(item: MyApplication): string {
  const date = item.validUntil ? day(item.validUntil) : undefined;
  if (item.plan === 'trial') return i18n.t('signup.result.approvedTrial', { company: item.companyName, date: date ?? '—' });
  return date ? i18n.t('signup.result.approvedFormalUntil', { company: item.companyName, date }) : i18n.t('signup.result.approvedFormal', { company: item.companyName });
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
    {notice && <Alert className="mb-4" tone="error" role="alert">{notice.message}</Alert>}
    {view.loading ? <Spinner label={i18n.t('signup.loading')} />
      : view.error ? <Alert tone="error" role="alert" action={<Button variant="outline" size="sm" onClick={reload}>{i18n.t('signup.retry')}</Button>}>{view.error.message}</Alert>
      : !view.applicant ? <VerifyCard onVerified={reload} api={api} applyHref={withNext('/signup', next)} />
      : !latest ? <section className="rounded-xl border border-border bg-card shadow-sm"><h1 className="sr-only">{i18n.t('signup.statusTitle')}</h1><Empty title={i18n.t('signup.noApplication')} description={i18n.t('signup.noApplicationText')} action={<Button onClick={() => browser.assign(withNext('/signup', next))}>{i18n.t('signup.applyNow')}</Button>} /></section>
      : <ApplicationStatus item={latest} applicant={view.applicant} busy={busy} next={next} onWithdraw={() => void withdraw(latest)} />}
  </StatusFrame>;
}

function VerifyCard({ api, onVerified, applyHref }: { api: ReturnType<typeof apiOf>; onVerified(): void; applyHref: string }) {
  const id = useId();
  return <section aria-labelledby={id} className="mx-auto grid max-w-lg gap-5 rounded-xl border border-border bg-card p-5 shadow-sm sm:p-8" data-testid="signup-status-verify">
    <header className="grid gap-1"><h1 id={id} className="text-xl font-bold leading-8">{i18n.t('signup.statusTitle')}</h1><p className="text-sm text-muted-foreground">{i18n.t('signup.statusVerifyHint')}</p></header>
    <PhoneVerification api={api} onVerified={onVerified} />
    <p className="border-t border-border pt-4 text-sm text-muted-foreground">{i18n.t('signup.noApplicationYet')} <a href={applyHref} className="font-medium text-primary underline-offset-4 hover:underline">{i18n.t('signup.applyNow')}</a></p>
  </section>;
}

function ApplicationStatus({ item, applicant, busy, next, onWithdraw }: { item: MyApplication; applicant: Applicant; busy: boolean; next?: string; onWithdraw(): void }) {
  const result = results[item.status];
  const summaryId = useId();
  const description = item.status === 'approved' ? approvedText(item) : i18n.t(`signup.result.${result.key}Text`, { phone: applicant.phone, code: item.code });
  const notify = [i18n.t('signup.notifySms', { phone: item.phone }), item.email ? i18n.t('signup.notifyEmail', { email: item.email }) : null].filter(Boolean).join(i18n.t('signup.listSeparator'));
  const retry = () => browser.assign(withNext(`/signup?from=${encodeURIComponent(item.id)}`, next));
  return <article className="overflow-hidden rounded-xl border border-border bg-card shadow-sm" data-testid="signup-status" data-status={item.status}>
    <h1 className="sr-only">{i18n.t('signup.statusTitle')}</h1>
    <Result tone={result.tone} icon={result.icon} title={item.status === 'approved' ? i18n.t('signup.result.approved', { company: item.companyName }) : i18n.t(`signup.result.${result.key}`)} description={description} className="pb-6 pt-10" />
    <div className="border-t border-border px-5 py-6 sm:px-8"><Steps items={steps(item)} label={i18n.t('signup.progress')} statusLabels={statusLabels()} /></div>
    {item.status === 'rejected' && item.rejectReason && <div className="px-5 pb-6 sm:px-8"><Alert tone="error" title={i18n.t('signup.rejectReason')} testId="signup-reject-reason">{item.rejectReason}</Alert></div>}
    <section aria-labelledby={summaryId} className="grid gap-4 border-t border-border px-5 py-6 sm:px-8">
      <SectionHeader id={summaryId} title={i18n.t('signup.summary')} />
      <Descriptions items={[
        { key: 'number', label: i18n.t('signup.number'), children: <span className="font-mono">{item.number}</span> },
        { key: 'submitted', label: i18n.t('signup.submittedAt'), children: moment(item.submittedAt) },
        { key: 'company', label: i18n.t('signup.companyName'), children: item.companyName },
        { key: 'code', label: i18n.t('signup.tenantCode'), children: <span className="font-mono">{item.code}</span> },
        { key: 'notify', label: i18n.t('signup.notify'), children: notify },
      ]} />
    </section>
    <footer className="flex flex-wrap items-center justify-between gap-3 border-t border-border bg-muted/30 px-5 py-4 sm:px-8">
      <p className="text-[13px] text-muted-foreground">{i18n.t(`signup.footer.${result.key}`, { phone: applicant.phone })}</p>
      <div className="flex flex-wrap gap-2">
        {item.status === 'submitted' && <Button variant="outline" disabled={busy} onClick={onWithdraw}>{i18n.t('signup.withdraw')}</Button>}
        {(item.status === 'rejected' || item.status === 'withdrawn') && <Button onClick={retry}><Icon name="pencil" />{i18n.t('signup.resubmit')}</Button>}
        {item.status === 'approved' && next && <Button onClick={() => browser.assign(next)}>{i18n.t('signup.goLogin')}<Icon name="arrowRight" /></Button>}
      </div>
    </footer>
  </article>;
}
