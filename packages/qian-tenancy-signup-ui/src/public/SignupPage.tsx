import { useEffect, useId, useMemo, useRef, useState } from 'react';
import { Alert, Avatar, AvatarFallback, Button, Checkbox, cn, Icon, Spinner, type IconName } from '@idadawn/yue-core';
import { SchemaForm, type FieldAdornment, type FormApi, type FormValues } from '@idadawn/yue-form';
import { PlatformError, type PlatformClient } from '@idadawn/yue-sdk';
import { apiOf, data, fieldError, issue, unauthenticated, type Api, type Issue } from '../api';
import { accountSchema, companyFields, existingAccountSchema, industries, openStatuses, type Applicant, type MyApplication, type SubmitRequest } from '../contracts';
import { i18n } from '../i18n';
import { SignupFrame } from './frame';
import { browser, safeNext, withNext } from './navigation';
import { exchange, SendCodeButton, useCodeSender } from './verification';

interface Screen { loading: boolean; applicant?: Applicant | null; open?: MyApplication; draft?: FormValues; switched?: boolean; error?: Issue }
interface Carry { draft?: FormValues; switched?: boolean }
// 「修改后重新提交」以被驳回或撤回的那份申请为底稿；空值与页面不认识的行业代码不带进表单。
function draftOf(item: MyApplication): FormValues {
  const industry = item.industry && (industries as readonly string[]).includes(item.industry) ? item.industry : undefined;
  return Object.fromEntries(Object.entries({ companyName: item.companyName, code: item.code, size: item.size, industry, description: item.description, email: item.email }).filter(([, value]) => present(value)));
}
const present = (value: unknown) => value !== '' && value !== undefined && value !== null;
const pick = (values: FormValues | undefined, keys: readonly string[]) => Object.fromEntries(Object.entries(values ?? {}).filter(([key, value]) => keys.includes(key) && present(value)));

/**
 * 注册并申请开通（公开页 /signup，规格 §8）。没有已有账号的会话时，手机号与短信验证码就在账号区里：
 * 「获取验证码」在验证码右侧，提交时先用验证码换申请人会话，再提交申请（两个接口都不变）。
 * 核验后才知道号码是否已有账号：已有账号时收起账号区，改为「以某某的身份申请」，企业信息保留，由申请人确认后再提交。
 */
export function SignupPage({ client }: { client: PlatformClient }) {
  const api = useMemo(() => apiOf(client), [client]);
  const [view, setView] = useState<Screen>({ loading: true });
  const [revision, setRevision] = useState(0);
  const [accepted, setAccepted] = useState(false);
  const carry = useRef<Carry>({});
  const [next] = useState(() => safeNext(new URLSearchParams(location.search).get('next')));
  const [from] = useState(() => new URLSearchParams(location.search).get('from'));
  useEffect(() => {
    const controller = new AbortController();
    const signal = controller.signal;
    const carried = carry.current;
    void (async (): Promise<Omit<Screen, 'loading'>> => {
      let applicant: Applicant | null = null;
      try { applicant = data(await api.GET('/api/signup/session', { signal })); }
      catch (error) { if (!unauthenticated(error)) throw error; }
      if (!applicant) return { applicant: null, draft: carried.draft };
      const items = data(await api.GET('/api/signup/me/applications', { signal })).items;
      const base = items.find((item) => item.id === from && (item.status === 'rejected' || item.status === 'withdrawn'));
      return { applicant, open: items.find((item) => openStatuses.includes(item.status)), draft: carried.draft ?? (base ? draftOf(base) : undefined), switched: carried.switched };
    })().then((screen) => { if (!signal.aborted) { carry.current = {}; setView({ loading: false, ...screen }); } },
      (error: unknown) => { if (!signal.aborted) setView({ loading: false, error: issue(error) }); });
    return () => controller.abort();
  }, [api, from, revision]);
  const reload = (carried: Carry = {}) => { carry.current = carried; setView({ loading: true }); setRevision((value) => value + 1); };
  async function endSession() {
    try { await api.DELETE('/api/signup/session'); } catch (error) { if (!unauthenticated(error)) { setView((current) => ({ ...current, error: issue(error) })); return; } }
    reload();
  }
  const progress = withNext('/signup/status', next);
  const applicant = view.applicant;
  return <SignupFrame next={next}>
    <header className="flex flex-col gap-1.5">
      <h1 className="m-0 text-[28px] font-bold leading-9 tracking-[-0.01em]">{i18n.t('signup.formTitle')}</h1>
      <p className="m-0 text-muted-foreground">{i18n.t('signup.formHintBefore')}<span className="text-destructive">*</span>{i18n.t('signup.formHintAfter')}</p>
    </header>
    {view.loading ? <Spinner label={i18n.t('signup.loading')} />
      : view.error ? <Alert tone="error" role="alert" action={<Button variant="outline" size="sm" onClick={() => reload()} testId="signup-retry">{i18n.t('signup.retry')}</Button>}>{view.error.message}</Alert>
      : <>
        {applicant?.existingAccount && <ApplicantCard applicant={applicant} onSwitch={() => void endSession()} />}
        {view.open ? <Alert tone="info" title={i18n.t('signup.openTitle')} testId="signup-open" action={<Button variant="outline" size="sm" onClick={() => browser.assign(progress)}>{i18n.t('signup.viewProgress')}</Button>}>
            {i18n.t('signup.openText', { number: view.open.number, company: view.open.companyName })}</Alert>
          : <>
            {view.switched && applicant?.existingAccount && <Alert tone="info" testId="signup-existing-notice">{i18n.t('signup.existingNotice', { name: applicant.displayName ?? applicant.phone })}</Alert>}
            <ApplicationForm key={applicant?.existingAccount ? 'existing' : 'account'} api={api} applicant={applicant ?? null} draft={view.draft} next={next} accepted={accepted} onAccept={setAccepted}
              onExisting={(draft) => reload({ draft, switched: true })} onOpen={() => reload()} onSwitchPhone={() => void endSession()} done={() => browser.assign(progress)} />
          </>}
      </>}
  </SignupFrame>;
}

/** 已有账号的会话：主色浅底卡片「以某某的身份申请」，「换个账号」结束申请人会话。 */
function ApplicantCard({ applicant, onSwitch }: { applicant: Applicant; onSwitch(): void }) {
  const name = applicant.displayName ?? applicant.phone;
  return <div className="flex flex-wrap items-center gap-3 rounded-xl border border-primary-border bg-primary-subtle px-4 py-3.5" data-testid="signup-applicant">
    <Avatar aria-hidden="true" tint="solid" className="size-9"><AvatarFallback className="font-bold">{Array.from(name.trim())[0]}</AvatarFallback></Avatar>
    <span className="flex min-w-0 flex-1 flex-col">
      <span className="font-semibold">{i18n.t('signup.applyAs', { name })}</span>
      <span className="text-[13px] text-caption-foreground">{i18n.t('signup.applyAsHint', { phone: applicant.phone })}</span>
    </span>
    <Button variant="link" size="sm" className="h-auto px-0" onClick={onSwitch} testId="signup-switch-account">{i18n.t('signup.switchAccount')}</Button>
  </div>;
}

interface FormProps {
  api: Api; applicant: Applicant | null; draft?: FormValues; next?: string; accepted: boolean;
  onAccept(value: boolean): void; onExisting(draft: FormValues): void; onOpen(): void; onSwitchPhone(): void; done(): void;
}
/**
 * 申请表单：两个分节卡片（管理员账号、企业信息），数据 Schema 来自契约；确认密码只在页面核对，空的选填项不提交。
 * 字段错误（验证码、租户号占用、密码策略）按 pointer 落在对应输入框；同意条款与操作按钮同一行。
 */
function ApplicationForm({ api, applicant, draft, next, accepted, onAccept, onExisting, onOpen, onSwitchPhone, done }: FormProps) {
  const existing = !!applicant?.existingAccount;
  const schema = existing ? existingAccountSchema : accountSchema;
  const form = useRef<FormApi>(null);
  const sender = useCodeSender(api, form);
  // 新号码的申请人会话（核验过、还没交申请）进来时已经核验：手机号只读、不再要验证码。
  const [verified, setVerified] = useState(!!applicant);
  const [termsError, setTermsError] = useState(false);
  const [availability, setAvailability] = useState<{ code: string; state: Availability }>();
  const termsId = useId();
  const initialValues = { ...pick(draft, Object.keys(schema.data.properties)), ...(existing ? {} : { verified: !!applicant, ...(applicant ? { phone: applicant.phone } : {}) }) };
  const stateOf = (value: unknown): Availability | undefined => {
    const code = typeof value === 'string' ? value.trim() : '';
    return !code ? undefined : availability?.code === code ? availability.state : 'checking';
  };
  function adornment(path: string, value: unknown): FieldAdornment | null {
    if (path === 'code') {
      const code = typeof value === 'string' ? value.trim() : '';
      const state = stateOf(value);
      // 查询组件挂在输入框末端；可用与检查中在框内显示，不可用的原因在框下显示。
      return code ? { suffix: <><CodeWatcher api={api} code={code} onResult={setAvailability} />{(state === 'available' || state === 'checking') && <AvailabilityText state={state} />}</> } : null;
    }
    if (existing) return null;
    if (path === 'smsCode') return { addon: <SendCodeButton sender={sender} /> };
    if (path === 'phone' && verified) return {
      suffix: <span className="inline-flex items-center gap-1 text-ok" data-testid="signup-phone-verified"><Icon name="check" />{i18n.t('signup.phoneVerified')}</span>,
      addon: <Button type="button" variant="outline" onClick={onSwitchPhone} testId="signup-switch-phone">{i18n.t('signup.switchPhone')}</Button>,
    };
    return null;
  }
  function extra(path: string, value: unknown) {
    if (path === 'password') return typeof value === 'string' && value ? <PasswordStrength value={value} /> : null;
    if (path === 'code') { const state = stateOf(value); return state && state !== 'available' && state !== 'checking' ? <AvailabilityText state={state} below /> : null; }
    return null;
  }
  async function submit(values: FormValues, formApi: FormApi) {
    if (!existing && values.password !== values.passwordConfirm) { formApi.setErrors(fieldError('#/passwordConfirm', i18n.t('signup.passwordMismatch'))); return; }
    if (!accepted) { setTermsError(true); return; }
    if (!existing && !verified) {
      const person = await exchange(api, formApi, sender, values);
      if (!person) return;
      setVerified(true); formApi.setValue('verified', true);
      // 号码已有账号：契约要求不得带姓名与密码。换成已有账号的表单，只带企业信息，请申请人确认后再提交。
      if (person.existingAccount) { onExisting(pick(values, companyFields)); return; }
    }
    const { passwordConfirm: _confirm, phone: _phone, smsCode: _code, verified: _verified, ...rest } = values;
    const body = { ...Object.fromEntries(Object.entries(rest).filter(([, value]) => present(value))), acceptTerms: true } as SubmitRequest;
    try { await api.POST('/api/signup/me/applications', { body }); }
    catch (error) { if (error instanceof PlatformError && error.detail.code === 'SIGNUP_APPLICATION_OPEN') { onOpen(); return; } throw error; }
    done();
  }
  return <SchemaForm schema={schema} i18n={i18n} apiRef={form} testId="signup-application-form" initialValues={initialValues} submitLabel={i18n.t('signup.submit')}
    size="lg" layout="grid" groupVariant="card" className="flex flex-col gap-5 [&_input[name=code]]:font-mono" fieldExtra={extra} fieldAdornment={adornment} onSubmit={submit}>
    <div className="me-auto flex flex-col gap-1">
      <label className="inline-flex cursor-pointer items-center gap-2 text-subtle-foreground">
        <Checkbox checked={accepted} onCheckedChange={(value) => { onAccept(value === true); if (value === true) setTermsError(false); }} aria-labelledby={termsId}
          aria-invalid={termsError || undefined} aria-describedby={termsError ? `${termsId}-error` : undefined} testId="signup-accept-terms" />
        <span id={termsId}>{i18n.t('signup.acceptTerms')}</span>
      </label>
      {termsError && <p id={`${termsId}-error`} role="alert" className="m-0 text-xs leading-4.5 text-destructive">{i18n.t('signup.acceptTermsRequired')}</p>}
    </div>
    {next && <Button type="button" variant="outline" size="xl" onClick={() => browser.assign(next)} testId="signup-cancel">{i18n.t('signup.cancel')}</Button>}
  </SchemaForm>;
}

// 密码强度只是提示（长度与字符种类），真正的密码策略由身份服务在提交时核对。
const meters = { 1: ['bg-error-solid', 'text-error'], 2: ['bg-warn-solid', 'text-warn'], 3: ['bg-ok-solid', 'text-ok'], 4: ['bg-ok-solid', 'text-ok'] } as const;
function strength(value: string): 1 | 2 | 3 | 4 {
  if (value.length < 8) return 1;
  const kinds = [/[a-z]/, /[A-Z]/, /\d/, /[^A-Za-z0-9]/].filter((pattern) => pattern.test(value)).length;
  return Math.max(1, Math.min(4, (value.length >= 12 ? 1 : 0) + (kinds >= 2 ? 1 : 0) + (kinds >= 3 ? 1 : 0) + 1)) as 1 | 2 | 3 | 4;
}
function PasswordStrength({ value }: { value: string }) {
  const score = strength(value);
  const [bar, text] = meters[score];
  return <span className="flex items-center gap-2" data-testid="signup-password-strength" data-score={score}>
    <span aria-hidden="true" className="flex flex-1 gap-1">{[1, 2, 3, 4].map((step) => <span key={step} className={cn('h-1 flex-1 rounded-xs', step <= score ? bar : 'bg-border')} />)}</span>
    <span className={text}>{i18n.t('signup.strength', { level: i18n.t(`signup.strengths.${score}`) })}</span>
  </span>;
}

type Availability = 'checking' | 'available' | 'invalid' | 'reserved' | 'taken' | 'unknown';
const availabilityTone: Record<Availability, string> = { checking: 'text-muted-foreground', available: 'text-ok', invalid: 'text-error', reserved: 'text-error', taken: 'text-error', unknown: 'text-muted-foreground' };
const availabilityIcon: Partial<Record<Availability, IconName>> = { available: 'check', invalid: 'circleX', reserved: 'circleX', taken: 'circleX' };
/** 租户号实时提示：输入停顿后查询可用性（宿主按来源地址限流），结果交给表单显示；提交时宿主仍会再核对一次。 */
function CodeWatcher({ api, code, onResult }: { api: Api; code: string; onResult(result: { code: string; state: Availability }): void }) {
  useEffect(() => {
    const controller = new AbortController();
    const timer = setTimeout(() => {
      api.GET('/api/signup/code-availability', { params: { query: { code } }, signal: controller.signal }).then((response) => {
        if (controller.signal.aborted) return;
        const answer = data(response);
        const reason = answer.reason === 'reserved' || answer.reason === 'taken' ? answer.reason : 'invalid';
        onResult({ code, state: answer.available ? 'available' : reason });
      }, () => { if (!controller.signal.aborted) onResult({ code, state: 'unknown' }); });
    }, 350);
    return () => { clearTimeout(timer); controller.abort(); };
  }, [api, code, onResult]);
  return null;
}
function AvailabilityText({ state, below = false }: { state: Availability; below?: boolean }) {
  const icon = availabilityIcon[state];
  return <span role="status" data-testid="signup-code-availability" data-state={state} className={cn('inline-flex items-center', below ? 'gap-1.5' : 'gap-1', availabilityTone[state])}>
    {icon && <Icon name={icon} size={below ? 14 : 16} />}{i18n.t(`signup.availability.${state}`)}
  </span>;
}
