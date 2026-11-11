import { useId, useState, type FormEvent, type ReactNode } from 'react';
import { Alert, Button, Checkbox, cn, Field, FieldError, FieldLabel, Icon, Input, InputPassword } from '@idadawn/yue-core';
import type { PlatformI18n } from '@idadawn/yue-sdk';
import { useShellLocale, type ShellPartProps } from './layout';

export interface PasswordCredentials { username: string; password: string }

export interface PasswordLoginProps extends ShellPartProps {
  i18n: PlatformI18n;
  onSubmit: (credentials: PasswordCredentials) => Promise<void>;
  onSso?: () => void;
  busy?: boolean;
  error?: boolean;
  /** 标题；缺省为「登录」。 */
  title?: string;
  /** 标题下的说明；缺省取 shell.loginHelp。 */
  description?: string;
  /** 启用「记住账号」：勾选后把账号（不含密码）存进本机 localStorage 的这个键，下次自动填入。 */
  rememberKey?: string;
  /** 「忘记密码？」的地址；不传不显示。 */
  forgotHref?: string;
  /** 「其他登录方式」分隔线下的附加入口，例如企业连接器。 */
  alternatives?: ReactNode;
  /** 表单下方的附加内容，例如「还没有企业账号？申请开通」。 */
  footer?: ReactNode;
}

function readRemembered(key?: string): string {
  if (!key || typeof window === 'undefined') return '';
  try { return window.localStorage.getItem(key) ?? ''; } catch { return ''; }
}
function writeRemembered(key: string, value: string | null) {
  try { if (value) window.localStorage.setItem(key, value); else window.localStorage.removeItem(key); } catch { /* 存储被禁用时不记住。 */ }
}

/** 账号密码登录：44px 控件、记住账号与「其他登录方式」；提交后清空密码，账号只在勾选「记住账号」时存在本机。 */
export function PasswordLogin({ i18n, onSubmit, onSso, busy, error, title, description, rememberKey, forgotHref, alternatives, footer, className, testId = 'yue-password-login' }: PasswordLoginProps) {
  useShellLocale(i18n);
  const id = useId();
  // 记住的账号在首次渲染时读入（认证页是纯客户端渲染；服务端读不到本机存储时为空）。
  const [username, setUsername] = useState(() => readRemembered(rememberKey));
  const [password, setPassword] = useState('');
  const [remember, setRemember] = useState(() => readRemembered(rememberKey) !== '');
  const [missing, setMissing] = useState<{ username: boolean; password: boolean }>({ username: false, password: false });
  const [submitting, setSubmitting] = useState(false);
  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const next = { username: !username.trim(), password: !password };
    setMissing(next);
    if (next.username || next.password || submitting || busy) {
      if (next.username) document.getElementById(`${id}-username`)?.focus(); else if (next.password) document.getElementById(`${id}-password`)?.focus();
      return;
    }
    if (rememberKey) writeRemembered(rememberKey, remember ? username.trim() : null);
    setSubmitting(true);
    try { await onSubmit({ username: username.trim(), password }); }
    finally { setSubmitting(false); setPassword(''); }
  };
  const required = i18n.t('common.required');
  return <section data-slot="password-login" data-testid={testId} className={cn('flex w-full flex-col gap-6', className)}>
    <div className="flex flex-col gap-1.5">
      <h1 className="m-0 text-[28px] font-bold leading-9 tracking-[-0.01em]">{title ?? i18n.t('shell.login')}</h1>
      <p className="m-0 text-muted-foreground">{description ?? i18n.t('shell.loginHelp')}</p>
    </div>
    {error && <Alert tone="error" role="alert" size="sm" testId={`${testId}-error`}>{i18n.t('shell.authError')}</Alert>}
    <form noValidate onSubmit={event => void submit(event)} data-slot="password-login-form" data-testid={`${testId}-form`} className="m-0 flex flex-col gap-4.5 [--yue-input-height:2.75rem]">
      <Field invalid={missing.username} testId={`${testId}-username`}>
        <FieldLabel htmlFor={`${id}-username`}>{i18n.t('shell.username')}</FieldLabel>
        <Input id={`${id}-username`} name="username" autoComplete="username" value={username} aria-required onChange={event => { setUsername(event.currentTarget.value); if (event.currentTarget.value.trim()) setMissing(value => ({ ...value, username: false })); }} />
        {missing.username && <FieldError match>{required}</FieldError>}
      </Field>
      <Field invalid={missing.password} testId={`${testId}-password`}>
        <FieldLabel htmlFor={`${id}-password`}>{i18n.t('shell.password')}</FieldLabel>
        <InputPassword id={`${id}-password`} name="password" autoComplete="current-password" value={password} aria-required showLabel={i18n.t('shell.showPassword')} hideLabel={i18n.t('shell.hidePassword')} onChange={event => { setPassword(event.currentTarget.value); if (event.currentTarget.value) setMissing(value => ({ ...value, password: false })); }} />
        {missing.password && <FieldError match>{required}</FieldError>}
      </Field>
      {(rememberKey || forgotHref) && <div className="flex items-center justify-between gap-3">
        {rememberKey ? <label className="inline-flex cursor-pointer items-center gap-2 text-subtle-foreground"><Checkbox name="remember" checked={remember} onCheckedChange={value => setRemember(value === true)} testId={`${testId}-remember`} />{i18n.t('shell.remember')}</label> : <span />}
        {forgotHref && <a href={forgotHref} className="text-primary no-underline hover:text-primary/90 hover:underline">{i18n.t('shell.forgot')}</a>}
      </div>}
      <Button type="submit" size="xl" className="w-full" loading={submitting} disabled={busy} testId={`${testId}-form-submit`}>{i18n.t('shell.login')}</Button>
    </form>
    {(onSso || alternatives) && <div className="flex flex-col gap-3" data-slot="password-login-alternatives">
      <div className="flex items-center gap-3 text-[13px] text-muted-foreground"><span aria-hidden="true" className="h-px flex-1 bg-border" /><span>{i18n.t('shell.otherMethods')}</span><span aria-hidden="true" className="h-px flex-1 bg-border" /></div>
      {onSso && <div className="flex flex-wrap gap-3"><Button variant="outline" size="xl" className="flex-[1_1_160px] [&_svg]:size-4.5" disabled={busy} onClick={onSso} testId={`${testId}-sso`}><Icon name="key" />{i18n.t('shell.sso')}</Button></div>}
      {alternatives}
    </div>}
    {footer}
  </section>;
}
