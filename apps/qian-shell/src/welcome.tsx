import { Alert, Badge, Button, cn, Descriptions, Icon } from '@idadawn/yue-core/shell';
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

interface Step { key: string; done: boolean; title: string; description: string; action?: { label: string; target: string } }

/**
 * 企业首页（开通流程原型第 8 块）：试用提示、问候、开通引导与企业信息；摘要还没回来时只有问候，摘要不可用时问候下换成通用说明。
 * 引导项按真实数据判定完成与否，只给已经存在的页面配按钮（菜单里的第一个应用页）；还没有页面的项（完善企业资料、搭建组织架构、两步验证）不放。
 */
export function Welcome({ i18n, summary, displayName, menu, now, onNavigate }: { i18n: PlatformI18n; summary: TenantSummary | null | undefined; displayName: string; menu: readonly ShellMenuItem[]; now: number; onNavigate(target: string): void }) {
  const greeting = <section className="flex flex-col gap-1" aria-labelledby="shell-welcome-title">
    <h1 id="shell-welcome-title" className="m-0 text-2xl font-bold leading-8">{i18n.t('app.welcome.greeting', { name: displayName })}</h1>
    {summary !== undefined && <p className="m-0 text-muted-foreground" data-testid="shell-welcome-intro">{summary ? i18n.t('app.welcome.intro', { company: summary.name }) : i18n.t('app.welcome.introGeneric')}</p>}
  </section>;
  if (!summary) return <div className="flex flex-col gap-5" data-testid="shell-welcome">{greeting}</div>;
  const first = menu.find(item => item.type === 'page' && item.target && item.target !== '/');
  const used = summary.usersUsed;
  const steps: Step[] = [
    { key: 'admin', done: true, title: i18n.t('app.welcome.stepAdmin'), description: i18n.t('app.welcome.stepAdminHelp', { name: summary.administratorName ?? displayName }) },
    { key: 'members', done: used !== null && used > 1, title: i18n.t('app.welcome.stepMembers'),
      description: used === null ? i18n.t('app.welcome.usageUnavailable') : summary.usersLimit === null ? i18n.t('app.welcome.stepMembersCount', { count: used }) : i18n.t('app.welcome.stepMembersHelp', { count: used, limit: summary.usersLimit }) },
    { key: 'apps', done: false, title: i18n.t('app.welcome.stepApps'), description: i18n.t(first ? 'app.welcome.stepAppsHelp' : 'app.welcome.stepAppsNone'),
      action: first ? { label: i18n.t('app.welcome.open', { name: first.label ?? i18n.t(first.labelKey) }), target: first.target } : undefined },
  ];
  const done = steps.filter(step => step.done).length;
  const remaining = summary.validUntil ? daysLeft(summary.validUntil, now) : null;
  return <div className="flex flex-col gap-5" data-testid="shell-welcome">
    {summary.status === 'trial' && summary.validUntil && <Alert tone={remaining !== null && remaining <= 7 ? 'warn' : 'info'} testId="shell-trial">
      {i18n.t('app.welcome.trialText', { count: remaining ?? 0, date: day(summary.validUntil) })}</Alert>}
    {summary.status === 'grace' && <Alert tone="warn" testId="shell-grace">{i18n.t('app.welcome.graceText', { date: summary.validUntil ? day(summary.validUntil) : '—' })}</Alert>}
    {greeting}
    <div className="flex flex-wrap items-start gap-5">
      <section aria-labelledby="shell-onboarding-title" className="min-w-0 flex-[2_1_520px] rounded-xl border border-border bg-card" data-testid="shell-onboarding">
        <div className="flex flex-col gap-2.5 border-b border-border/70 px-5 py-4.5">
          <div className="flex items-center justify-between gap-3"><h2 id="shell-onboarding-title" className="m-0 text-base font-semibold leading-6">{i18n.t('app.welcome.onboarding')}</h2>
            <span className="text-[13px] text-caption-foreground" data-testid="shell-onboarding-progress">{i18n.t('app.welcome.progress', { done, total: steps.length })}</span></div>
          <span aria-hidden="true" className="block h-1.5 overflow-hidden rounded-full bg-border"><span className="block h-full rounded-full bg-primary" style={{ width: `${Math.round(done / steps.length * 100)}%` }} /></span>
        </div>
        <ol className="m-0 list-none p-0">{steps.map((step, index) => <li key={step.key} data-step={step.key} data-done={step.done} className="flex items-center gap-3.5 border-b border-border/70 px-5 py-3.5 last:border-b-0">
          {step.done
            ? <span aria-hidden="true" className="inline-flex size-7 shrink-0 items-center justify-center rounded-full bg-ok-bg text-ok-icon"><Icon name="check" strokeWidth={2.25} /></span>
            : <span aria-hidden="true" className="inline-flex size-7 shrink-0 items-center justify-center rounded-full border-2 border-border-strong text-[13px] font-semibold text-caption-foreground">{index + 1}</span>}
          <span className="flex min-w-0 flex-1 flex-col">
            <span className={cn(step.done ? 'text-caption-foreground line-through' : 'font-semibold')}>{step.title}<span className="sr-only">{i18n.t(step.done ? 'app.welcome.done' : 'app.welcome.todo')}</span></span>
            <span className="text-[13px] text-muted-foreground">{step.description}</span>
          </span>
          {step.done ? <span aria-hidden="true" className="shrink-0 text-[13px] text-ok">{i18n.t('app.welcome.doneLabel')}</span>
            : step.action && <Button size="sm" variant="outline" className="shrink-0 gap-1" onClick={() => onNavigate(step.action!.target)}>{step.action.label}<Icon name="chevronRight" size={14} className="rtl:rotate-180" /></Button>}
        </li>)}</ol>
      </section>
      <section aria-labelledby="shell-company-title" className="flex min-w-0 flex-[1_1_300px] flex-col gap-3.5 rounded-xl border border-border bg-card px-5 py-4.5" data-testid="shell-company">
        <h2 id="shell-company-title" className="m-0 text-base font-semibold leading-6">{i18n.t('app.welcome.company')}</h2>
        <Descriptions layout="justified" items={[
          { key: 'code', label: i18n.t('app.welcome.code'), children: <span className="font-mono" data-field="code">{summary.code}</span> },
          { key: 'status', label: i18n.t('app.welcome.statusLabel'), children: <Badge tone={tone[summary.status]} dot testId="shell-company-status">{i18n.t(`app.welcome.status.${summary.status}`)}</Badge> },
          { key: 'validUntil', label: i18n.t('app.welcome.validUntil'), children: <span data-field="validUntil">{summary.validUntil ? day(summary.validUntil) : i18n.t('app.welcome.noExpiry')}</span> },
          { key: 'users', label: i18n.t('app.welcome.users'), children: <span className="tabular-nums" data-field="users">{used === null ? '—' : `${used} / ${summary.usersLimit ?? i18n.t('app.welcome.unlimited')}`}</span> },
          { key: 'admin', label: i18n.t('app.welcome.admin'), children: <span data-field="admin">{summary.administratorName ?? '—'}</span> },
        ]} />
      </section>
    </div>
  </div>;
}
