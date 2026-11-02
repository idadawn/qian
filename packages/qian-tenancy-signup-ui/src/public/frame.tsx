import type { ReactNode } from 'react';
import { Button, Icon, Steps, type StepStatus } from '@idadawn/yue-core';
import type { Applicant } from '../contracts';
import { i18n } from '../i18n';

export function statusLabels(): Record<StepStatus, string> {
  return { done: i18n.t('signup.step.done'), current: i18n.t('signup.step.current'), failed: i18n.t('signup.step.failed'), todo: i18n.t('signup.step.todo') };
}

export function Brand() {
  return <span className="flex items-center gap-2.5 text-[15px] font-bold" data-testid="signup-brand">
    <span aria-hidden="true" className="inline-flex size-8 items-center justify-center rounded-lg bg-primary text-primary-foreground"><Icon name="building" /></span>{i18n.t('signup.brand')}
  </span>;
}

/** 与 SchemaForm 字段组同一版式的分段标题（序号、标题与说明），用于表单之外的分段。 */
export function SectionHeader({ id, marker, title, description }: { id?: string; marker?: number; title: string; description?: string }) {
  return <div className="flex items-start gap-3">
    {marker !== undefined && <span aria-hidden="true" className="inline-flex size-6 shrink-0 items-center justify-center rounded-full bg-info-bg text-xs font-semibold text-info">{marker}</span>}
    <div className="grid min-w-0 gap-0.5"><h3 id={id} className="text-[15px] font-semibold leading-6">{title}</h3>{description && <p className="text-[13px] text-muted-foreground">{description}</p>}</div>
  </div>;
}

/** 注册页：左侧说明开通的三步，右侧是表单卡片；窄屏上下排列。 */
export function SignupFrame({ progressHref, children }: { progressHref: string; children: ReactNode }) {
  return <div className="min-h-dvh bg-canvas text-foreground lg:grid lg:grid-cols-[minmax(320px,400px)_minmax(0,1fr)]">
    <aside className="flex flex-col gap-8 border-b border-border bg-background px-6 py-8 lg:sticky lg:top-0 lg:h-dvh lg:border-e lg:border-b-0 lg:px-10 lg:py-12">
      <Brand />
      <div className="grid gap-2"><h1 className="text-2xl font-bold leading-9 tracking-tight">{i18n.t('signup.heroTitle')}</h1><p className="text-sm leading-6 text-muted-foreground">{i18n.t('signup.heroText')}</p></div>
      <Steps orientation="vertical" label={i18n.t('signup.flow')} statusLabels={statusLabels()} items={[
        { key: 'apply', title: i18n.t('signup.flowApply'), description: i18n.t('signup.flowApplyHint'), status: 'current' },
        { key: 'review', title: i18n.t('signup.flowReview'), description: i18n.t('signup.flowReviewHint'), status: 'todo' },
        { key: 'use', title: i18n.t('signup.flowUse'), description: i18n.t('signup.flowUseHint'), status: 'todo' },
      ]} />
      <p className="text-sm text-muted-foreground lg:mt-auto">{i18n.t('signup.submittedBefore')} <a href={progressHref} className="font-medium text-primary underline-offset-4 hover:underline">{i18n.t('signup.checkProgress')}</a></p>
    </aside>
    <main className="min-w-0 px-4 py-8 sm:px-8 lg:py-12"><div className="mx-auto max-w-2xl">{children}</div></main>
  </div>;
}

/** 进度页：顶栏放品牌、申请人与退出，内容居中。 */
export function StatusFrame({ applicant, onSignOut, children }: { applicant?: Applicant; onSignOut(): void; children: ReactNode }) {
  const name = applicant ? applicant.displayName ?? applicant.phone : undefined;
  // 新号码的申请人还没有姓名，头像用图标而不是号码的首位数字。
  return <div className="min-h-dvh bg-canvas text-foreground">
    <header className="border-b border-border bg-background"><div className="mx-auto flex h-14 max-w-3xl items-center justify-between gap-4 px-4 sm:px-6"><Brand />
      {name && <div className="flex items-center gap-2" data-testid="signup-status-user">
        <span aria-hidden="true" className="inline-flex size-8 items-center justify-center rounded-full bg-info-bg text-[13px] font-bold text-info">{applicant?.displayName ? Array.from(applicant.displayName)[0] : <Icon name="user" />}</span>
        <span className="hidden text-sm font-medium sm:inline">{name}</span>
        <Button variant="ghost" size="sm" onClick={onSignOut}><Icon name="logOut" />{i18n.t('signup.signOut')}</Button>
      </div>}
    </div></header>
    <main className="mx-auto max-w-3xl px-4 py-8 sm:px-6 lg:py-12">{children}</main>
  </div>;
}
