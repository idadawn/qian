import { Button as BaseButton } from '@base-ui/react/button';
import type { ComponentProps } from 'react';
import { cn } from '../lib/cn';
import { button } from '../lib/styles';
import type { YueProps } from '../lib/test-id';
import { Icon } from './icon';

export type ButtonProps = YueProps<ComponentProps<typeof BaseButton>> & {
  variant?: 'primary' | 'secondary' | 'outline' | 'ghost' | 'destructive';
  size?: 'sm' | 'md' | 'lg' | 'icon';
  loading?: boolean;
};
const variants = {
  primary: 'bg-[var(--primary)] text-[var(--primary-foreground)] hover:brightness-95',
  secondary: 'bg-[var(--secondary)] text-[var(--secondary-foreground)] hover:brightness-95',
  outline: 'border border-[var(--border)] bg-[var(--background)] text-[var(--foreground)] hover:bg-[var(--accent)]',
  ghost: 'bg-transparent text-[var(--foreground)] hover:bg-[var(--accent)]',
  destructive: 'bg-[var(--destructive)] text-[var(--destructive-foreground)] hover:brightness-95',
};
function ButtonView({ slot, variant = 'primary', size = 'md', loading = false, disabled, children, className, testId, ...props }: ButtonProps & { slot: string }) {
  return <BaseButton {...props} disabled={disabled || loading} aria-busy={loading || undefined} data-slot={slot} data-testid={testId ?? `yue-${slot}`} className={cn(button, variants[variant], { 'h-7 px-2 text-xs': size === 'sm', 'h-11 px-5': size === 'lg', 'size-9 p-0': size === 'icon' }, className)}>{loading && <Icon name="spinner" className="animate-spin motion-reduce:animate-none" />}{children}</BaseButton>;
}
export function Button(props: ButtonProps) { return <ButtonView {...props} slot="button" />; }
export type IconButtonProps = ButtonProps & { label: string };
export function IconButton({ label, ...props }: IconButtonProps) { return <ButtonView {...props} size="icon" aria-label={label} slot="icon-button" />; }
export type ButtonGroupProps = YueProps<ComponentProps<'div'>>;
export function ButtonGroup({ className, testId, ...props }: ButtonGroupProps) { return <div {...props} role="group" data-slot="button-group" data-testid={testId ?? 'yue-button-group'} className={cn('inline-flex gap-1', className)} />; }
