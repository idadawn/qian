import type { ReactNode } from 'react';
import { Avatar, AvatarFallback, Button, Icon, Steps, type StepStatus } from '@idadawn/yue-core';
import { AuthLayout, PlainLayout } from '@idadawn/yue-shell';
import type { Applicant } from '../contracts';
import { i18n } from '../i18n';

export function statusLabels(): Record<StepStatus, string> {
  return { done: i18n.t('signup.step.done'), current: i18n.t('signup.step.current'), failed: i18n.t('signup.step.failed'), todo: i18n.t('signup.step.todo') };
}

/**
 * 注册页：左侧品牌深色面写开通的三步，右侧是 720px 的申请表单；窄屏时侧栏折到上方。
 * 带同源登录页地址（next）时，品牌与右上角「登录后申请」回到登录页；没有时不给入口（登录页只接受应用发起的授权请求）。
 */
export function SignupFrame({ next, children }: { next?: string; children: ReactNode }) {
  return <AuthLayout variant="form" brand={i18n.t('signup.brand')} brandHref={next} headline={i18n.t('signup.heroTitle')} description={i18n.t('signup.heroText')} testId="signup-layout"
    toolbar={next ? <span className="inline-flex items-center gap-1" data-testid="signup-login-entry">{i18n.t('signup.hasAccount')}<a href={next} data-testid="signup-login-link" className="font-semibold text-primary no-underline hover:underline">{i18n.t('signup.loginToApply')}</a></span> : undefined}
    aside={<Steps orientation="vertical" tone="inverse" size="lg" label={i18n.t('signup.flow')} statusLabels={statusLabels()} testId="signup-flow" items={[
      { key: 'apply', title: i18n.t('signup.flowApply'), description: i18n.t('signup.flowApplyHint'), status: 'current' },
      { key: 'review', title: i18n.t('signup.flowReview'), description: i18n.t('signup.flowReviewHint'), status: 'todo' },
      { key: 'use', title: i18n.t('signup.flowUse'), description: i18n.t('signup.flowUseHint'), status: 'todo' },
    ]} />}>{children}</AuthLayout>;
}

/** 新号码的申请人还没有姓名：头像用图标、名字位置显示掩码号码。 */
function initialOf(applicant: Applicant): ReactNode { return applicant.displayName ? Array.from(applicant.displayName.trim())[0] : <Icon name="user" />; }

/** 进度页：60px 顶栏放品牌，右侧是申请人与「退出登录」（结束申请人会话），内容 760px 居中。 */
export function StatusFrame({ applicant, onSignOut, children }: { applicant?: Applicant; onSignOut(): void; children: ReactNode }) {
  return <PlainLayout brand={i18n.t('signup.brand')} width="md" testId="signup-status-layout" account={applicant ? <>
    <span className="inline-flex items-center gap-2" data-testid="signup-status-user">
      <Avatar aria-hidden="true" tint="primary" className="size-7.5"><AvatarFallback className="text-[13px] font-bold">{initialOf(applicant)}</AvatarFallback></Avatar>
      <span className="hidden sm:inline">{applicant.displayName ?? applicant.phone}</span>
    </span>
    <Button variant="ghost" className="h-8.5 gap-1.5 px-2.5" onClick={onSignOut} testId="signup-sign-out"><Icon name="logOut" />{i18n.t('signup.signOut')}</Button>
  </> : undefined}>{children}</PlainLayout>;
}
