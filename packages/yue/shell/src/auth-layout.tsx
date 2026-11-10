import type { ReactNode } from 'react';
import { cn } from '@idadawn/yue-core/shell';
import type { ShellPartProps } from './layout';

/** 品牌标识：圆角方块加三道横线。inverse 用在品牌深色面上（白底、深色横线）。 */
export function BrandMark({ size = 28, inverse = false, className }: { size?: number; inverse?: boolean; className?: string }) {
  return <svg viewBox="0 0 36 36" width={size} height={size} className={cn('shrink-0', className)} aria-hidden="true" focusable="false">
    <rect width="36" height="36" rx="9" className={inverse ? 'fill-inverse-foreground' : 'fill-primary'} />
    {[10, 16.5, 23].map(y => <rect key={y} x="9" y={y} width="18" height="3" rx="1.5" className={inverse ? 'fill-inverse' : 'fill-primary-foreground'} />)}
  </svg>;
}

export interface AuthLayoutProps extends ShellPartProps {
  /** 品牌名，如「Qian 乾」。 */
  brand: ReactNode;
  /** 品牌链接（申请页回到登录页）；不传时品牌不可点。 */
  brandHref?: string;
  /** 侧栏标题。 */
  headline: ReactNode;
  /** 侧栏标题下的一段说明。 */
  description?: ReactNode;
  /** 侧栏标题之后的附加内容，例如申请流程的步骤条。 */
  aside?: ReactNode;
  /** 侧栏底部，例如版权与政策链接。 */
  footer?: ReactNode;
  /** 主区右上角，例如语言与主题按钮、「已有账号？登录」。 */
  toolbar?: ReactNode;
  /** sign-in：侧栏 440px 起、内容 400px 宽并垂直居中（登录）；form：侧栏 360px 起、内容 720px 宽（申请表单）。 @default 'sign-in' */
  variant?: 'sign-in' | 'form';
  /** 主区内容下方、贴着页面底部的一行，例如「登录即表示你同意……」。 */
  legal?: ReactNode;
}

/** 认证与申请页布局：左侧品牌深色面（点阵底纹），右侧内容区；窄屏时侧栏折到上方。 */
export function AuthLayout({ brand, brandHref, headline, description, aside, footer, toolbar, variant = 'sign-in', legal, children, className, testId = 'yue-auth-layout' }: AuthLayoutProps) {
  const signIn = variant === 'sign-in';
  const brandRow = <><BrandMark size={36} inverse /><span className="text-lg font-bold tracking-[0.02em]">{brand}</span></>;
  return <div data-slot="auth-layout" data-variant={variant} data-testid={testId} className={cn('flex min-h-dvh flex-wrap text-foreground', signIn ? 'bg-background' : 'bg-canvas', className)}>
    <aside data-slot="auth-layout-aside" data-testid={`${testId}-aside`} className={cn('relative flex min-h-80 max-w-full flex-col overflow-hidden bg-inverse bg-[radial-gradient(rgb(255_255_255/0.09)_1px,transparent_1px)] bg-[size:22px_22px] py-10 text-inverse-foreground',
      signIn ? 'flex-[1_1_440px] justify-between gap-12 px-6 sm:px-14' : 'flex-[1_1_360px] gap-14 px-6 sm:px-12')}>
      {signIn && <div aria-hidden="true" className="absolute -end-18 bottom-24 flex flex-col gap-7.5 opacity-12">{[0, 1, 2].map(index => <span key={index} className="block h-8.5 w-105 rounded-[17px] bg-inverse-foreground" />)}</div>}
      {brandHref ? <a href={brandHref} data-slot="auth-layout-brand" className="relative flex w-fit items-center gap-3 text-inverse-foreground no-underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-inverse-foreground">{brandRow}</a>
        : <div data-slot="auth-layout-brand" className="relative flex items-center gap-3">{brandRow}</div>}
      <div className={cn('relative flex flex-col', signIn ? 'max-w-115 gap-4' : 'gap-3')}>
        <h2 className={cn('m-0 font-bold text-balance', signIn ? 'text-[34px] leading-11 tracking-[-0.01em]' : 'text-[30px] leading-10')}>{headline}</h2>
        {description && <p className={cn('m-0 text-inverse-muted-foreground', signIn ? 'text-base leading-6.5' : 'text-[15px] leading-6')}>{description}</p>}
      </div>
      {aside && <div className="relative">{aside}</div>}
      {footer && <div className={cn('relative flex flex-wrap items-center gap-4 text-[13px] text-inverse-muted-foreground [&_a]:text-inverse-muted-foreground [&_a]:no-underline [&_a:hover]:text-inverse-foreground', !signIn && 'mt-auto')}>{footer}</div>}
    </aside>
    <main data-slot="auth-layout-main" data-testid={`${testId}-main`} className={cn('flex min-w-0 flex-col gap-6 px-4 pt-6 sm:px-8', signIn ? 'flex-[999_1_560px] pb-10' : 'flex-[999_1_640px] pb-12')}>
      {toolbar && <div data-slot="auth-layout-toolbar" className="flex flex-wrap items-center justify-end gap-2 text-subtle-foreground">{toolbar}</div>}
      {signIn
        ? <div className="flex flex-1 items-center justify-center"><div className="flex w-full max-w-100 flex-col gap-6">{children}</div></div>
        : <div className="mx-auto flex w-full max-w-180 flex-col gap-5">{children}</div>}
      {legal && <p className="m-0 text-center text-xs leading-4.5 text-muted-foreground">{legal}</p>}
    </main>
  </div>;
}

export interface PlainLayoutProps extends ShellPartProps {
  brand: ReactNode;
  /** 顶栏右侧，例如头像、姓名与「退出登录」。 */
  account?: ReactNode;
  /** 内容宽度：sm 600px（选择工作空间）、md 760px（申请进度）。 @default 'sm' */
  width?: 'sm' | 'md';
}

/** 无侧栏的简单布局：60px 顶栏放品牌与账号，主区在画布底色上居中。 */
export function PlainLayout({ brand, account, width = 'sm', children, className, testId = 'yue-plain-layout' }: PlainLayoutProps) {
  return <div data-slot="plain-layout" data-testid={testId} className={cn('flex min-h-dvh flex-col bg-canvas text-foreground', className)}>
    <header data-slot="plain-layout-header" className="flex h-15 shrink-0 items-center justify-between gap-4 border-b border-border bg-background px-4 sm:px-6">
      <span className="flex min-w-0 items-center gap-2.5"><BrandMark size={30} /><span className="truncate text-base font-bold">{brand}</span></span>
      {account && <span className="flex shrink-0 items-center gap-3">{account}</span>}
    </header>
    <main data-slot="plain-layout-main" data-testid={`${testId}-main`} className={cn('flex flex-1 justify-center px-4 sm:px-6', width === 'sm' ? 'py-14' : 'py-12')}>
      <div className={cn('flex w-full flex-col', width === 'sm' ? 'max-w-150 gap-6' : 'max-w-190 gap-4')}>{children}</div>
    </main>
  </div>;
}
