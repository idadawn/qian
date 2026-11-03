import { useEffect, useState } from 'react';
import { Alert, Button } from '@idadawn/yue-core';
import { SchemaForm } from '@idadawn/yue-form';
import { data, issue, type Api } from '../api';
import { codeSchema, verifySchema, type Applicant } from '../contracts';
import { i18n } from '../i18n';

const secondsUntil = (time: number) => Math.max(0, Math.ceil((time - Date.now()) / 1000));

/** 手机号核验：发验证码 → 输入验证码建立申请人会话（规格 §6.2）。号码是否已注册在核验通过之前不作区分。 */
export function PhoneVerification({ api, onVerified }: { api: Api; onVerified(applicant: Applicant): void }) {
  const [challenge, setChallenge] = useState<{ id: string; phone: string; resendAt: number }>();
  const [wait, setWait] = useState(0);
  const [resendError, setResendError] = useState<string>();
  useEffect(() => {
    if (!challenge) return;
    const timer = setInterval(() => setWait(secondsUntil(challenge.resendAt)), 1000);
    return () => clearInterval(timer);
  }, [challenge]);
  async function send(phone: string) {
    const sent = data(await api.POST('/api/signup/verifications', { body: { phone } }));
    const resendAt = new Date(sent.resendAfter).getTime();
    setChallenge({ id: sent.id, phone, resendAt }); setWait(secondsUntil(resendAt)); setResendError(undefined);
  }
  if (!challenge) return <SchemaForm key="phone" inlineActions schema={verifySchema} i18n={i18n} testId="signup-phone-form" submitLabel={i18n.t('signup.sendCode')} onSubmit={async (values) => send(String(values.phone))} />;
  return <div className="grid gap-3">
    <p className="text-sm text-muted-foreground" role="status">{i18n.t('signup.codeSent', { phone: challenge.phone })}{' '}
      <Button variant="link" className="h-auto px-0" onClick={() => { setChallenge(undefined); setResendError(undefined); }}>{i18n.t('signup.changePhone')}</Button></p>
    <SchemaForm key={challenge.id} inlineActions schema={codeSchema} i18n={i18n} testId="signup-code-form" submitLabel={i18n.t('signup.verify')} onSubmit={async (values) => {
      onVerified(data(await api.POST('/api/signup/session', { body: { verificationId: challenge.id, code: String(values.code) } })));
    }}>
      <Button type="button" variant="outline" disabled={wait > 0} testId="signup-resend" onClick={() => void send(challenge.phone).catch((error: unknown) => setResendError(issue(error).message))}>
        {wait > 0 ? i18n.t('signup.resendIn', { seconds: wait }) : i18n.t('signup.resend')}
      </Button>
    </SchemaForm>
    {resendError && <Alert tone="error" role="alert">{resendError}</Alert>}
  </div>;
}
