import { useState } from 'react';
import { Alert, cn } from '@idadawn/yue-core';
import { SchemaForm, type FormSchema } from '@idadawn/yue-form';
import type { PlatformI18n } from '@idadawn/yue-sdk';
import { useShellLocale, type ShellPartProps } from './layout';

const changeSchema: FormSchema = {
  $schema: 'https://schema.qian.dev/form/v1', version: '1.0.0', id: 'qian.shell.password-change',
  data: { type: 'object', additionalProperties: false, required: ['newPassword', 'confirmPassword'], properties: { newPassword: { type: 'string', minLength: 1 }, confirmPassword: { type: 'string', minLength: 1 } } },
  ui: { layout: [{ field: 'newPassword', labelKey: 'shell.newPassword', props: { type: 'password', autoComplete: 'new-password', readonlyMask: 'full' } }, { field: 'confirmPassword', labelKey: 'shell.confirmPassword', props: { type: 'password', autoComplete: 'new-password', readonlyMask: 'full' } }], device: {} },
  rules: {}, binding: {}, auth: {},
};

export interface PasswordChangeProps extends ShellPartProps { i18n: PlatformI18n; onSubmit: (newPassword: string) => Promise<void>; onBack?: () => void; busy?: boolean; error?: boolean }
/** 强制改密（初始密码或运营重置）：两次输入一致才提交；提交后清空两个字段，密码只存在于本次提交。 */
export function PasswordChange({ i18n, onSubmit, busy, error, className, testId = 'yue-password-change' }: PasswordChangeProps) {
  useShellLocale(i18n);
  const [mismatch, setMismatch] = useState(false);
  return <section data-slot="password-change" data-testid={testId} className={cn('mx-auto w-full max-w-md space-y-6 rounded-xl border border-border bg-card p-6 shadow-sm', className)}>
    <div className="space-y-2"><h1 className="text-2xl font-semibold">{i18n.t('shell.changePassword')}</h1><p className="text-sm text-muted-foreground">{i18n.t('shell.changePasswordHelp')}</p></div>
    {(error || mismatch) && <Alert tone="error" role="alert">{i18n.t(mismatch ? 'shell.passwordMismatch' : 'shell.changePasswordError')}</Alert>}
    <SchemaForm schema={changeSchema} i18n={i18n} testId={`${testId}-form`} submitLabel={i18n.t('shell.changePassword')} onSubmit={async (values, api) => {
      const next = String(values.newPassword); const confirm = String(values.confirmPassword);
      if (next !== confirm) { setMismatch(true); api.reset({ newPassword: '', confirmPassword: '' }); return; }
      setMismatch(false);
      try { if (!busy) await onSubmit(next); } finally { api.reset({ newPassword: '', confirmPassword: '' }); }
    }} />
  </section>;
}
