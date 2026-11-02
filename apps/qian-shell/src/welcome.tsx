import type { ReactNode } from 'react';
import { Alert, Badge, Button, cn, Icon } from '@idadawn/yue-core/shell';
import type { PlatformI18n } from '@idadawn/yue-sdk';
import type { ShellMenuItem } from '@idadawn/yue-shell/menu';

/** 本租户摘要（S1-TEN-021）：租户主档、到期、用户用量与管理员；接口只让租户主体读本租户。 */
export interface TenantSummary {
  code: string; name: string; status: 'pending' | 'trial' | 'active' | 'grace' | 'suspended';
  validUntil: string | null; usersLimit: number | null; usersUsed: number | null; administratorName: string | null;
}
const tone = { pending: 'neutral', trial: 'info', active: 'ok', grace: 'warn', suspended: 'error' } as const;
const pad = (value: number) => String(value).padStart(2, '0');
function day(value: string): string { const date = new Date(value); return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`; }

/** 剩余天数按自然日向上取整：到期当天还算 1 天，过了到期时刻为 0。 */
export function daysLeft(validUntil: string, now: number): number { return Math.max(0, Math.ceil((new Date(validUntil).getTime() - now) / 86_400_000)); }

/**
 * 租户管理员首次进入企业控制台（开通流程原型第 8 块）：试用提示、问候、开通引导与企业信息。
 * 引导项按真实数据判定完成与否，只链接到已经存在的页面（菜单里的第一个应用页）。
 */
export function Welcome({ i18n, summary, displayName, menu, now, onNavigate }: { i18n: PlatformI18n; summary: TenantSummary; displayName: string; menu: readonly ShellMenuItem[]; now: number; onNavigate(target: string): void }) {
  const first = menu.find(item => item.type === 'page' && item.target);
  const used = summary.usersUsed;
  const steps: { key: string; done: boolean; title: string; description: string; action?: ReactNode }[] = [
    { key: 'admin', done: true, title: i18n.t('app.welcome.stepAdmin'), description: i18n.t('app.welcome.stepAdminHelp', { name: summary.administratorName ?? displayName }) },
    { key: 'members', done: used !== null && used > 1, title: i18n.t('app.welcome.stepMembers'), description: used === null ? i18n.t('app.welcome.usageUnavailable') : i18n.t('app.welcome.stepMembersHelp', { count: used }) },
    { key: 'apps', done: false, title: i18n.t('app.welcome.stepApps'), description: i18n.t(first ? 'app.welcome.stepAppsHelp' : 'app.welcome.stepAppsNone'),
      action: first ? <Button size="sm" variant="outline" onClick={() => onNavigate(first.target!)}>{i18n.t('app.welcome.open', { name: first.label ?? first.labelKey })}</Button> : undefined },
  ];
  const remaining = summary.validUntil ? daysLeft(summary.validUntil, now) : null;
  return <div className="grid gap-4" data-testid="shell-welcome">
    {summary.status === 'trial' && summary.validUntil && <Alert tone={remaining !== null && remaining <= 7 ? 'warn' : 'info'} title={i18n.t('app.welcome.trialTitle', { count: remaining ?? 0 })} testId="shell-trial">
      {i18n.t('app.welcome.trialText', { date: day(summary.validUntil) })}</Alert>}
    {summary.status === 'grace' && <Alert tone="warn" title={i18n.t('app.welcome.graceTitle')} testId="shell-grace">{i18n.t('app.welcome.graceText', { date: summary.validUntil ? day(summary.validUntil) : '—' })}</Alert>}
    <section className="grid gap-1" aria-labelledby="shell-welcome-title">
      <h2 id="shell-welcome-title" className="text-xl font-bold">{i18n.t('app.welcome.greeting', { name: displayName })}</h2>
      <p className="text-sm text-muted-foreground">{i18n.t('app.welcome.intro', { company: summary.name })}</p>
    </section>
    <div className="grid gap-4 lg:grid-cols-[minmax(0,1.4fr)_minmax(0,1fr)]">
      <section aria-labelledby="shell-onboarding-title" className="rounded-xl border border-border bg-card p-5" data-testid="shell-onboarding">
        <h3 id="shell-onboarding-title" className="mb-3 text-[15px] font-semibold">{i18n.t('app.welcome.onboarding')}</h3>
        <ol className="grid gap-2">{steps.map(step => <li key={step.key} data-step={step.key} data-done={step.done} className="flex flex-wrap items-center gap-3 rounded-lg border border-border px-4 py-3">
          <span aria-hidden="true" className={cn('inline-flex size-7 shrink-0 items-center justify-center rounded-full', step.done ? 'bg-ok-bg text-ok' : 'bg-muted text-muted-foreground')}><Icon name={step.done ? 'check' : 'arrowRight'} /></span>
          <span className="grid min-w-0 flex-1 gap-0.5"><span className="text-sm font-semibold">{step.title}<span className="sr-only">{i18n.t(step.done ? 'app.welcome.done' : 'app.welcome.todo')}</span></span>
            <span className="text-[13px] text-muted-foreground">{step.description}</span></span>
          {step.action}
        </li>)}</ol>
      </section>
      <section aria-labelledby="shell-company-title" className="rounded-xl border border-border bg-card p-5" data-testid="shell-company">
        <div className="mb-3 flex items-center justify-between gap-3"><h3 id="shell-company-title" className="text-[15px] font-semibold">{i18n.t('app.welcome.company')}</h3>
          <Badge tone={tone[summary.status]} dot>{i18n.t(`app.welcome.status.${summary.status}`)}</Badge></div>
        <dl className="grid grid-cols-[auto_minmax(0,1fr)] gap-x-4 gap-y-2 text-sm">
          <dt className="text-muted-foreground">{i18n.t('app.welcome.name')}</dt><dd data-field="name">{summary.name}</dd>
          <dt className="text-muted-foreground">{i18n.t('app.welcome.code')}</dt><dd className="font-mono" data-field="code">{summary.code}</dd>
          <dt className="text-muted-foreground">{i18n.t('app.welcome.validUntil')}</dt><dd data-field="validUntil">{summary.validUntil ? day(summary.validUntil) : i18n.t('app.welcome.noExpiry')}</dd>
          <dt className="text-muted-foreground">{i18n.t('app.welcome.users')}</dt><dd className="tabular-nums" data-field="users">{used === null ? '—' : `${used} / ${summary.usersLimit ?? i18n.t('app.welcome.unlimited')}`}</dd>
          <dt className="text-muted-foreground">{i18n.t('app.welcome.admin')}</dt><dd data-field="admin">{summary.administratorName ?? '—'}</dd>
        </dl>
      </section>
    </div>
  </div>;
}
