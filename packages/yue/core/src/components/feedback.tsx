import { Progress as BaseProgress } from '@base-ui/react/progress';
import type { Tone } from '@idadawn/yue-tokens';
import { useId, type ComponentProps, type ReactNode } from 'react';
import { cn } from '../lib/cn';
import type { YueProps } from '../lib/test-id';
import { Icon, type IconName } from './icon';
import { IconButton } from './button';

const tones: Record<Tone, string> = {
  ok: 'bg-ok-bg text-ok',
  warn: 'bg-warn-bg text-warn',
  error: 'bg-error-bg text-error',
  info: 'bg-info-bg text-info',
  neutral: 'bg-neutral-bg text-neutral',
};
export type BadgeProps = YueProps<ComponentProps<'span'>> & { tone?: Tone };
export function Badge({ tone = 'neutral', className, testId, ...props }: BadgeProps) { return <span {...props} data-slot="badge" data-testid={testId ?? 'yue-badge'} className={cn('inline-flex w-fit shrink-0 items-center justify-center gap-1 rounded-full border border-transparent px-2 py-0.5 text-xs font-medium whitespace-nowrap transition-colors duration-fast', tones[tone], className)} />; }
export type TagProps = BadgeProps & { onRemove?: () => void; removeLabel?: string };
export function Tag({ tone = 'neutral', children, onRemove, removeLabel, className, testId, ...props }: TagProps) {
  if (onRemove && !removeLabel) throw new Error('可移除标签需要 removeLabel。');
  return <span {...props} data-slot="tag" data-testid={testId ?? 'yue-tag'} className={cn('inline-flex w-fit items-center gap-1 rounded-md border border-transparent px-2 py-0.5 text-xs font-medium', tones[tone], className)}>{children}{onRemove && <IconButton label={removeLabel!} variant="ghost" className="size-5" onClick={onRemove}><Icon name="close" size={12} /></IconButton>}</span>;
}
export type SkeletonProps = YueProps<ComponentProps<'div'>>;
export function Skeleton({ className, testId, ...props }: SkeletonProps) { return <div {...props} aria-hidden="true" data-slot="skeleton" data-testid={testId ?? 'yue-skeleton'} className={cn('h-4 animate-pulse rounded-md bg-muted motion-reduce:animate-none', className)} />; }
export type SpinnerProps = YueProps<ComponentProps<'span'>> & { label: string };
export function Spinner({ label, className, testId, ...props }: SpinnerProps) { return <span {...props} role="status" aria-label={label} data-slot="spinner" data-testid={testId ?? 'yue-spinner'} className={cn('inline-flex', className)}><Icon name="spinner" className="animate-spin motion-reduce:animate-none" /></span>; }
export type ProgressProps = YueProps<ComponentProps<typeof BaseProgress.Root>> & { label: string };
export function Progress({ label, className, testId, ...props }: ProgressProps) { return <BaseProgress.Root {...props} data-slot="progress" data-testid={testId ?? 'yue-progress'} className={cn('flex min-w-32 flex-col gap-1', className)}><BaseProgress.Label className="text-sm">{label}</BaseProgress.Label><BaseProgress.Track className="h-2 overflow-hidden rounded-full bg-primary/20"><BaseProgress.Indicator className="h-full rounded-full bg-primary transition-[width] data-[status=indeterminate]:w-1/3 data-[status=indeterminate]:animate-pulse motion-reduce:animate-none" /></BaseProgress.Track></BaseProgress.Root>; }
export type AlertProps = YueProps<ComponentProps<'div'>> & { tone?: Tone };
export function Alert({ tone = 'info', role = 'status', className, testId, ...props }: AlertProps) { return <div {...props} role={role} data-slot="alert" data-testid={testId ?? 'yue-alert'} className={cn('relative grid w-full gap-1 rounded-lg border border-current/20 px-4 py-3 text-sm', tones[tone], className)} />; }
export interface EmptyProps { title: ReactNode; description?: ReactNode; action?: ReactNode; icon?: IconName; testId?: string; className?: string }
export function Empty({ title, description, action, icon = 'info', className, testId }: EmptyProps) {
  return <div role="status" data-slot="empty" data-testid={testId ?? 'yue-empty'} className={cn('flex flex-col items-center justify-center gap-4 rounded-lg border border-dashed border-border p-8 text-center', className)}><Icon name={icon} size={32} /><p className="font-medium">{title}</p>{description && <p className="text-sm text-muted-foreground">{description}</p>}{action}</div>;
}
export interface ResultProps extends EmptyProps { tone?: Tone }
export function Result({ title, description, action, icon = 'check', tone = 'ok', className, testId }: ResultProps) {
  const id = useId();
  return <section aria-labelledby={id} data-slot="result" data-testid={testId ?? 'yue-result'} className={cn('flex flex-col items-center justify-center gap-4 rounded-lg border border-current/20 p-8 text-center', tones[tone], className)}><Icon name={icon} size={36} /><h2 id={id} className="text-xl font-semibold">{title}</h2>{description && <p>{description}</p>}{action}</section>;
}
