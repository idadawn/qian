import { Progress as BaseProgress } from '@base-ui/react/progress';
import type { Tone } from '@idadawn/yue-tokens';
import { useId, type ComponentProps, type ReactNode } from 'react';
import { cn } from '../lib/cn';
import type { YueProps } from '../lib/test-id';
import { Icon, type IconName } from './icon';
import { IconButton } from './button';

// 状态色：底（-bg）、边框（-border）、文字（tone 本名）、实色（-solid，圆点与进度填充）、图标（-icon，状态图标字形；warn 比圆点深一档）；提示条的文字用深一档的 -strong。
const tones: Record<Tone, string> = {
  ok: 'bg-ok-bg text-ok',
  warn: 'bg-warn-bg text-warn',
  error: 'bg-error-bg text-error',
  info: 'bg-info-bg text-info',
  neutral: 'bg-neutral-bg text-neutral',
};
const borders: Record<Tone, string> = { ok: 'border-ok-border', warn: 'border-warn-border', error: 'border-error-border', info: 'border-info-border', neutral: 'border-neutral-border' };
const solids: Record<Tone, string> = { ok: 'bg-ok-solid', warn: 'bg-warn-solid', error: 'bg-error-solid', info: 'bg-info-solid', neutral: 'bg-neutral-solid' };
const strongs: Record<Tone, string> = { ok: 'text-ok-strong', warn: 'text-warn-strong', error: 'text-error-strong', info: 'text-info-strong', neutral: 'text-neutral-strong' };
const texts: Record<Tone, string> = { ok: 'text-ok', warn: 'text-warn', error: 'text-error', info: 'text-info', neutral: 'text-neutral' };
const icons: Record<Tone, string> = { ok: 'text-ok-icon', warn: 'text-warn-icon', error: 'text-error-icon', info: 'text-info-icon', neutral: 'text-neutral-icon' };
const toneIcons: Record<Tone, IconName> = { ok: 'circleCheck', warn: 'alert', error: 'alert', info: 'info', neutral: 'info' };
export type BadgeProps = YueProps<ComponentProps<'span'>> & {
  tone?: Tone;
  /** 在文字前显示同色圆点，用于状态列。 @default false */
  dot?: boolean;
};
/** 状态徽标：胶囊形，底色、边框、文字与圆点取同一状态色的四档。 */
export function Badge({ tone = 'neutral', dot = false, className, testId, children, ...props }: BadgeProps) {
  return <span {...props} data-slot="badge" data-testid={testId ?? 'yue-badge'} className={cn('inline-flex h-5.5 w-fit shrink-0 items-center justify-center gap-1.5 rounded-full border px-2 text-xs font-semibold whitespace-nowrap transition-colors duration-fast', tones[tone], borders[tone], className)}>
    {dot && <span aria-hidden="true" data-slot="badge-dot" data-testid={`${testId ?? 'yue-badge'}-dot`} className={cn('size-1.5 shrink-0 rounded-full', solids[tone])} />}{children}</span>;
}
export type TagProps = BadgeProps & { onRemove?: () => void; removeLabel?: string };
/** 标签：小圆角、无边框，用于分类与检查结果；可带状态圆点或移除按钮。 */
export function Tag({ tone = 'neutral', dot = false, children, onRemove, removeLabel, className, testId, ...props }: TagProps) {
  if (onRemove && !removeLabel) throw new Error('可移除标签需要 removeLabel。');
  return <span {...props} data-slot="tag" data-testid={testId ?? 'yue-tag'} className={cn('inline-flex w-fit items-center gap-1.5 rounded-sm border border-transparent px-2 py-0.5 text-xs leading-4.5 font-medium whitespace-nowrap', tones[tone], className)}>
    {dot && <span aria-hidden="true" data-slot="tag-dot" data-testid={`${testId ?? 'yue-tag'}-dot`} className={cn('size-1.5 shrink-0 rounded-full', solids[tone])} />}{children}{onRemove && <IconButton label={removeLabel!} variant="ghost" className="size-5" onClick={onRemove}><Icon name="close" size={12} /></IconButton>}</span>;
}
export type SkeletonProps = YueProps<ComponentProps<'div'>>;
export function Skeleton({ className, testId, ...props }: SkeletonProps) { return <div {...props} aria-hidden="true" data-slot="skeleton" data-testid={testId ?? 'yue-skeleton'} className={cn('h-4 animate-pulse rounded-md bg-muted motion-reduce:animate-none', className)} />; }
export type SpinnerProps = YueProps<ComponentProps<'span'>> & { label: string };
export function Spinner({ label, className, testId, ...props }: SpinnerProps) { return <span {...props} role="status" aria-label={label} data-slot="spinner" data-testid={testId ?? 'yue-spinner'} className={cn('inline-flex', className)}><Icon name="spinner" className="animate-spin motion-reduce:animate-none" /></span>; }
export type ProgressProps = YueProps<ComponentProps<typeof BaseProgress.Root>> & { label: string };
export function Progress({ label, className, testId, ...props }: ProgressProps) { return <BaseProgress.Root {...props} data-slot="progress" data-testid={testId ?? 'yue-progress'} className={cn('flex min-w-32 flex-col gap-1', className)}><BaseProgress.Label className="text-sm">{label}</BaseProgress.Label><BaseProgress.Track className="h-2 overflow-hidden rounded-full bg-primary/20"><BaseProgress.Indicator className="h-full rounded-full bg-primary transition-[width] data-[status=indeterminate]:w-1/3 data-[status=indeterminate]:animate-pulse motion-reduce:animate-none" /></BaseProgress.Track></BaseProgress.Root>; }
export type AlertProps = Omit<YueProps<ComponentProps<'div'>>, 'title'> & {
  tone?: Tone;
  /** 加粗的标题行；有标题时正文在其下方。 */
  title?: ReactNode;
  /** 是否在左侧显示按语气选择的图标。 @default true */
  icon?: boolean;
  /** 右侧操作，例如「重试」「刷新页面」按钮；窄屏时换到正文下方。 */
  action?: ReactNode;
  /** 尺寸：sm 用在表单内（13px、紧凑内边距），md 用在页面与卡片。 @default 'md' */
  size?: 'sm' | 'md';
};
/** 提示条：状态底色与边框，图标跟随文字色。md 文字取深一档（strong），带标题时是块级消息（上下 14px、图标与文字间距 10px、标题与正文间距 2px），不带标题时是一行横幅（上下 12px、间距 12px）；sm 用在表单内，文字取状态文字色。 */
export function Alert({ tone = 'info', role = 'status', title, icon = true, action, size = 'md', className, testId, children, ...props }: AlertProps) {
  return <div {...props} role={role} data-slot="alert" data-size={size} data-testid={testId ?? 'yue-alert'} className={cn('relative flex w-full flex-wrap items-start border', size === 'sm' ? 'gap-x-2.5 gap-y-1.5 rounded-md px-3 py-2.5 text-[13px] leading-5' : title ? 'gap-x-2.5 gap-y-2.5 rounded-lg px-4 py-3.5 text-sm' : 'gap-x-3 gap-y-2.5 rounded-lg px-4 py-3 text-sm', tones[tone], borders[tone], size === 'sm' ? texts[tone] : strongs[tone], className)}>
    {icon && <Icon name={toneIcons[tone]} size={size === 'sm' ? 16 : 18} className="mt-0.5 shrink-0" />}
    <div className="min-w-0 flex-1 basis-48">{title && <p data-slot="alert-title" data-testid={`${testId ?? 'yue-alert'}-title`} className="mb-0.5 font-semibold">{title}</p>}{children}</div>
    {action && <div data-slot="alert-action" data-testid={`${testId ?? 'yue-alert'}-action`} className="flex shrink-0 flex-wrap items-center gap-2 self-center">{action}</div>}
  </div>;
}
export interface EmptyProps { title: ReactNode; description?: ReactNode; action?: ReactNode; icon?: IconName; testId?: string; className?: string }
export function Empty({ title, description, action, icon = 'info', className, testId }: EmptyProps) {
  return <div role="status" data-slot="empty" data-testid={testId ?? 'yue-empty'} className={cn('flex flex-col items-center justify-center gap-4 rounded-lg border border-dashed border-border p-8 text-center', className)}><Icon name={icon} size={32} /><p className="font-medium">{title}</p>{description && <p className="text-sm text-muted-foreground">{description}</p>}{action}</div>;
}
export interface ResultProps extends EmptyProps { tone?: Tone }
export function Result({ title, description, action, icon = 'check', tone = 'ok', className, testId }: ResultProps) {
  const id = useId();
  return <section aria-labelledby={id} data-slot="result" data-testid={testId ?? 'yue-result'} className={cn('flex flex-col items-center justify-center gap-3 px-6 py-10 text-center', className)}>
    <span data-slot="result-icon" data-testid={`${testId ?? 'yue-result'}-icon`} className={cn('inline-flex size-16 items-center justify-center rounded-full', tones[tone], icons[tone])}><Icon name={icon} size={30} /></span>
    <h2 id={id} className="text-2xl font-bold">{title}</h2>{description && <div className="max-w-130 text-caption-foreground">{description}</div>}{action && <div className="mt-3 flex flex-wrap justify-center gap-3">{action}</div>}
  </section>;
}
