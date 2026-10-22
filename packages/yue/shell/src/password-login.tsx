import { Alert, Button, cn } from '@idadawn/yue-core';
import { SchemaForm, type FormSchema } from '@idadawn/yue-form';
import type { PlatformI18n } from '@idadawn/yue-sdk';
import { useShellLocale, type ShellPartProps } from './layout';

export interface PasswordCredentials { username: string; password: string }

const loginSchema: FormSchema = {
  $schema: 'https://schema.qian.dev/form/v1', version: '1.0.0', id: 'qian.shell.login',
  data: { type: 'object', additionalProperties: false, required: ['username', 'password'], properties: { username: { type: 'string', minLength: 1 }, password: { type: 'string', minLength: 1 } } },
  ui: { layout: [{ field: 'username', labelKey: 'shell.username', props: { autoComplete: 'username' } }, { field: 'password', labelKey: 'shell.password', props: { type: 'password', autoComplete: 'current-password', readonlyMask: 'full' } }], device: {} },
  rules: {}, binding: {}, auth: {},
};

export interface PasswordLoginProps extends ShellPartProps { i18n: PlatformI18n; onSubmit: (credentials: PasswordCredentials) => Promise<void>; onSso?: () => void; busy?: boolean; error?: boolean }
export function PasswordLogin({ i18n, onSubmit, onSso, busy, error, className, testId = 'yue-password-login' }: PasswordLoginProps) {
  useShellLocale(i18n);
  return <section data-slot="password-login" data-testid={testId} className={cn('mx-auto w-full max-w-md space-y-6 rounded-xl border border-border bg-card p-6 shadow-sm', className)}>
    <div className="space-y-2"><h1 className="text-2xl font-semibold">{i18n.t('shell.login')}</h1><p className="text-sm text-muted-foreground">{i18n.t('shell.loginHelp')}</p></div>
    {error && <Alert tone="error" role="alert">{i18n.t('shell.authError')}</Alert>}
    <SchemaForm schema={loginSchema} i18n={i18n} testId={`${testId}-form`} submitLabel={i18n.t('shell.login')} onSubmit={async (values, api) => { try { await onSubmit({ username: String(values.username), password: String(values.password) }); } finally { api.reset({ ...api.getValues(), password: '' }); } }} />
    {onSso && <Button className="w-full" variant="outline" disabled={busy} onClick={onSso}>{i18n.t('shell.sso')}</Button>}
  </section>;
}
