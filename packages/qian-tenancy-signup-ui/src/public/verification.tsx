import { useEffect, useRef, useState, type RefObject } from 'react';
import { Button } from '@idadawn/yue-core';
import { SchemaForm, type FormApi } from '@idadawn/yue-form';
import { PlatformError, type ErrorDetail } from '@idadawn/yue-sdk';
import { data, fieldError, issue, type Api } from '../api';
import { verifySchema, type Applicant } from '../contracts';
import { i18n } from '../i18n';

const secondsUntil = (time: number) => Math.max(0, Math.ceil((time - Date.now()) / 1000));
export interface Challenge { id: string; phone: string; resendAt: number }

/**
 * 短信验证码的发送与重发倒计时（重发间隔由宿主的 resendAfter 给出，规格 §4 配置）。号码是否已注册在核验通过之前不作区分。
 * 手机号取自同一张表单；没填时提示必填，发送失败（号码不合法、超限、发件端口不可用）也落在手机号字段。
 */
export function useCodeSender(api: Api, form: RefObject<FormApi | null>) {
  const [challenge, setChallenge] = useState<Challenge>();
  const [wait, setWait] = useState(0);
  const [sending, setSending] = useState(false);
  useEffect(() => {
    if (!challenge) return;
    const timer = setInterval(() => setWait(secondsUntil(challenge.resendAt)), 1000);
    return () => clearInterval(timer);
  }, [challenge]);
  async function send() {
    const phone = String(form.current?.getValues().phone ?? '').trim();
    if (!phone) { form.current?.setErrors(fieldError('#/phone', i18n.t('common.required'))); return; }
    setSending(true);
    try {
      const sent = data(await api.POST('/api/signup/verifications', { body: { phone } }));
      const resendAt = new Date(sent.resendAfter).getTime();
      setChallenge({ id: sent.id, phone, resendAt }); setWait(secondsUntil(resendAt));
    } catch (error) {
      form.current?.setErrors(error instanceof PlatformError ? retarget(error.detail, '#/phone') : fieldError('#/phone', issue(error).message));
    } finally { setSending(false); }
  }
  return { challenge, wait, sending, send };
}
export type CodeSender = ReturnType<typeof useCodeSender>;

/** 服务端错误落到页面字段上：核验接口的 #/code 是短信验证码（页面字段 smsCode）；没有 pointer 的错误落在给定字段。 */
export function retarget(detail: ErrorDetail, fallback: string): ErrorDetail {
  const map = (pointer: string | undefined) => !pointer ? fallback : pointer === '#/code' ? '#/smsCode' : pointer;
  const [first, ...rest] = detail.errors ?? [{ code: detail.code, message: detail.message, pointer: detail.pointer ?? '' }];
  const errors: NonNullable<ErrorDetail['errors']> = [{ ...first, pointer: map(first.pointer) }, ...rest.map((item) => ({ ...item, pointer: map(item.pointer) }))];
  return { ...detail, pointer: errors[0].pointer, errors };
}

/** 验证码输入框右侧的发送按钮：倒计时期间不可点；发送结果写进隐藏的状态区，屏幕阅读器能听到。 */
export function SendCodeButton({ sender }: { sender: CodeSender }) {
  const counting = !!sender.challenge && sender.wait > 0;
  return <>
    <Button type="button" variant="outline" disabled={counting} loading={sender.sending} testId="signup-send-code" onClick={() => void sender.send()}>
      {!sender.challenge ? i18n.t('signup.sendCode') : counting ? i18n.t('signup.resendIn', { seconds: sender.wait }) : i18n.t('signup.resend')}
    </Button>
    <span role="status" className="sr-only" data-testid="signup-send-code-status">{sender.challenge ? i18n.t('signup.codeSent', { phone: sender.challenge.phone }) : ''}</span>
  </>;
}

/** 用验证码换申请人会话（规格 §6.2）。没发过码、换了号码、验证码错误或过期、号码不可用时，错误落在对应字段并返回 undefined。 */
export async function exchange(api: Api, form: FormApi, sender: CodeSender, values: Record<string, unknown>): Promise<Applicant | undefined> {
  const challenge = sender.challenge;
  if (!challenge || challenge.phone !== String(values.phone ?? '').trim()) { form.setErrors(fieldError('#/smsCode', i18n.t('signup.codeFirst'))); return undefined; }
  try { return data(await api.POST('/api/signup/session', { body: { verificationId: challenge.id, code: String(values.smsCode ?? '').trim() } })); }
  catch (error) {
    if (!(error instanceof PlatformError)) throw error;
    form.setErrors(retarget(error.detail, error.detail.code === 'SIGNUP_ACCOUNT_UNAVAILABLE' ? '#/phone' : '#/smsCode'));
    return undefined;
  }
}

/** 进度页的核验：手机号与验证码同一张表单，「获取验证码」在验证码右侧；核验通过即建立申请人会话。 */
export function PhoneVerification({ api, onVerified }: { api: Api; onVerified(applicant: Applicant): void }) {
  const form = useRef<FormApi>(null);
  const sender = useCodeSender(api, form);
  return <SchemaForm schema={verifySchema} i18n={i18n} apiRef={form} size="lg" groupVariant="card" testId="signup-verify-form" submitLabel={i18n.t('signup.viewStatus')}
    fieldAdornment={(path) => path === 'smsCode' ? { addon: <SendCodeButton sender={sender} /> } : null}
    onSubmit={async (values, formApi) => { const applicant = await exchange(api, formApi, sender, values); if (applicant) onVerified(applicant); }} />;
}
