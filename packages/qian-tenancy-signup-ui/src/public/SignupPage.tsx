import { useEffect, useId, useMemo, useState } from 'react';
import { Alert, Button, cn, Icon, Spinner } from '@idadawn/yue-core';
import { SchemaForm } from '@idadawn/yue-form';
import { PlatformError, type PlatformClient } from '@idadawn/yue-sdk';
import { apiOf, data, fieldError, issue, unauthenticated, type Api, type Issue } from '../api';
import { existingAccountSchema, industries, newAccountSchema, openStatuses, type Applicant, type MyApplication, type SubmitRequest } from '../contracts';
import { i18n } from '../i18n';
import { SectionHeader, SignupFrame } from './frame';
import { browser, safeNext, withNext } from './navigation';
import { PhoneVerification } from './verification';

interface Screen { loading: boolean; applicant?: Applicant | null; open?: MyApplication; draft?: Record<string, unknown>; error?: Issue }
// 「修改后重新提交」以被驳回或撤回的那份申请为底稿；空值与页面不认识的行业代码不带进表单。
function draftOf(item: MyApplication): Record<string, unknown> {
  const industry = item.industry && (industries as readonly string[]).includes(item.industry) ? item.industry : undefined;
  return Object.fromEntries(Object.entries({ companyName: item.companyName, code: item.code, size: item.size, industry, description: item.description, email: item.email }).filter(([, value]) => value !== null && value !== undefined && value !== ''));
}
const present = (value: unknown) => value !== '' && value !== undefined && value !== null;

/** 注册并申请开通（公开页 /signup）：先核验手机号，再填账号与企业信息；已有账号的号码收起账号区（规格 §8）。 */
export function SignupPage({ client }: { client: PlatformClient }) {
  const api = useMemo(() => apiOf(client), [client]);
  const headingId = useId();
  const [view, setView] = useState<Screen>({ loading: true });
  const [revision, setRevision] = useState(0);
  const [next] = useState(() => safeNext(new URLSearchParams(location.search).get('next')));
  const [from] = useState(() => new URLSearchParams(location.search).get('from'));
  useEffect(() => {
    const controller = new AbortController();
    const signal = controller.signal;
    void (async (): Promise<Omit<Screen, 'loading'>> => {
      let applicant: Applicant | null = null;
      try { applicant = data(await api.GET('/api/signup/session', { signal })); }
      catch (error) { if (!unauthenticated(error)) throw error; }
      if (!applicant) return { applicant: null };
      const items = data(await api.GET('/api/signup/me/applications', { signal })).items;
      const base = items.find((item) => item.id === from && (item.status === 'rejected' || item.status === 'withdrawn'));
      return { applicant, open: items.find((item) => openStatuses.includes(item.status)), draft: base ? draftOf(base) : undefined };
    })().then((screen) => { if (!signal.aborted) setView({ loading: false, ...screen }); },
      (error: unknown) => { if (!signal.aborted) setView({ loading: false, error: issue(error) }); });
    return () => controller.abort();
  }, [api, from, revision]);
  const reload = () => { setView({ loading: true }); setRevision((value) => value + 1); };
  async function switchAccount() {
    try { await api.DELETE('/api/signup/session'); } catch (error) { if (!unauthenticated(error)) { setView((current) => ({ ...current, error: issue(error) })); return; } }
    reload();
  }
  const progress = withNext('/signup/status', next);
  return <SignupFrame progressHref={progress}>
    <section aria-labelledby={headingId} className="rounded-xl border border-border bg-card p-5 shadow-sm sm:p-8" data-testid="signup-card">
      <header className="mb-6 grid gap-1"><h2 id={headingId} className="text-xl font-bold leading-8">{i18n.t('signup.formTitle')}</h2><p className="text-sm text-muted-foreground">{i18n.t('signup.formHint')}</p></header>
      {view.loading ? <Spinner label={i18n.t('signup.loading')} />
        : view.error ? <Alert tone="error" role="alert" action={<Button variant="outline" size="sm" onClick={reload}>{i18n.t('signup.retry')}</Button>}>{view.error.message}</Alert>
        : <div className="grid gap-5">
          <SectionHeader marker={1} title={i18n.t('signup.accountSection')} description={i18n.t('signup.accountSectionHint')} />
          {view.applicant ? <ApplicantCard applicant={view.applicant} onSwitch={() => void switchAccount()} /> : <PhoneVerification api={api} onVerified={reload} />}
          {!view.applicant ? <p className="border-t border-border pt-5 text-sm text-muted-foreground">{i18n.t('signup.verifyFirst')}</p>
            : view.open ? <Alert tone="info" title={i18n.t('signup.openTitle')} action={<Button variant="outline" size="sm" onClick={() => browser.assign(progress)}>{i18n.t('signup.viewProgress')}</Button>}>
              {i18n.t('signup.openText', { number: view.open.number, company: view.open.companyName })}</Alert>
            : <ApplicationForm api={api} applicant={view.applicant} draft={view.draft} onOpen={reload} done={() => browser.assign(progress)} />}
        </div>}
    </section>
  </SignupFrame>;
}

function ApplicantCard({ applicant, onSwitch }: { applicant: Applicant; onSwitch(): void }) {
  const name = applicant.displayName ?? applicant.phone;
  return <div className="flex flex-wrap items-center gap-3 rounded-xl border border-border bg-muted/40 p-4" data-testid="signup-applicant">
    <span aria-hidden="true" className="inline-flex size-10 shrink-0 items-center justify-center rounded-full bg-info-bg text-sm font-bold text-info">{applicant.existingAccount ? Array.from(name)[0] : <Icon name="phone" />}</span>
    <div className="grid min-w-0 flex-1 gap-0.5">
      <span className="font-semibold">{applicant.existingAccount ? i18n.t('signup.applyAs', { name }) : i18n.t('signup.phoneVerified', { phone: applicant.phone })}</span>
      <span className="text-[13px] text-muted-foreground">{applicant.existingAccount ? i18n.t('signup.applyAsHint', { phone: applicant.phone }) : i18n.t('signup.newAccountHint')}</span>
    </div>
    <Button variant="link" className="px-0" onClick={onSwitch}>{i18n.t(applicant.existingAccount ? 'signup.switchAccount' : 'signup.switchPhone')}</Button>
  </div>;
}

/** 申请表单：数据 Schema 来自契约；确认密码只在页面核对，空的选填项不提交。字段错误（租户号占用、密码策略）按 pointer 落在对应输入框。 */
function ApplicationForm({ api, applicant, draft, onOpen, done }: { api: Api; applicant: Applicant; draft?: Record<string, unknown>; onOpen(): void; done(): void }) {
  const existing = applicant.existingAccount;
  return <SchemaForm key={existing ? 'existing' : 'new'} schema={existing ? existingAccountSchema : newAccountSchema} i18n={i18n} testId="signup-application-form" initialValues={draft} submitLabel={i18n.t('signup.submit')}
    // 空值不占位；输入后提示区随值出现并保持到清空。
    fieldExtra={(path, value) => path === 'code' && typeof value === 'string' && value.trim() ? <CodeAvailability api={api} value={value} /> : null}
    onSubmit={async (values, form) => {
      if (!existing && values.password !== values.passwordConfirm) { form.setErrors(fieldError('#/passwordConfirm', i18n.t('signup.passwordMismatch'))); return; }
      const { passwordConfirm: _confirm, ...rest } = values;
      const body = Object.fromEntries(Object.entries(rest).filter(([, value]) => present(value))) as SubmitRequest;
      try { await api.POST('/api/signup/me/applications', { body }); }
      catch (error) { if (error instanceof PlatformError && error.detail.code === 'SIGNUP_APPLICATION_OPEN') { onOpen(); return; } throw error; }
      done();
    }} />;
}

type Availability = 'checking' | 'available' | 'invalid' | 'reserved' | 'taken' | 'unknown';
const availabilityTone: Record<Availability, string> = { checking: 'text-muted-foreground', available: 'text-ok', invalid: 'text-error', reserved: 'text-error', taken: 'text-error', unknown: 'text-muted-foreground' };
/** 租户号实时提示：输入停顿后查询可用性（宿主按来源地址限流）；提交时宿主仍会再核对一次。 */
function CodeAvailability({ api, value }: { api: Api; value: unknown }) {
  const code = typeof value === 'string' ? value.trim() : '';
  const [result, setResult] = useState<{ code: string; state: Availability }>();
  useEffect(() => {
    if (!code) return;
    const controller = new AbortController();
    const timer = setTimeout(() => {
      api.GET('/api/signup/code-availability', { params: { query: { code } }, signal: controller.signal }).then((response) => {
        const answer = data(response);
        const reason = answer.reason === 'reserved' || answer.reason === 'taken' ? answer.reason : 'invalid';
        setResult({ code, state: answer.available ? 'available' : reason });
      }, () => { if (!controller.signal.aborted) setResult({ code, state: 'unknown' }); });
    }, 350);
    return () => { clearTimeout(timer); controller.abort(); };
  }, [api, code]);
  const state: Availability | undefined = !code ? undefined : result?.code === code ? result.state : 'checking';
  return <span role="status" data-testid="signup-code-availability" data-state={state} className={cn('inline-flex items-center gap-1.5', state && availabilityTone[state])}>
    {state && state !== 'checking' && state !== 'unknown' && <Icon name={state === 'available' ? 'circleCheck' : 'circleX'} />}
    {state && i18n.t(`signup.availability.${state}`)}
  </span>;
}
