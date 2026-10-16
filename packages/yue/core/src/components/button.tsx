import { Button as BaseButton } from '@base-ui/react/button';
import { useContext, type ComponentProps } from 'react';
import { cn } from '../lib/cn';
import { ButtonIdentityContext } from '../lib/button-identity';
import { button } from '../lib/styles';
import type { YueProps } from '../lib/test-id';
import { Icon } from './icon';

export type ButtonProps = YueProps<ComponentProps<typeof BaseButton>> & {
  /** 按钮强调程度。 @default 'primary' */
  variant?: 'primary' | 'secondary' | 'outline' | 'ghost' | 'destructive';
  /** 按钮尺寸。 @default 'md' */
  size?: 'sm' | 'md' | 'lg' | 'icon';
  /** 加载中时禁用操作并显示忙碌状态。 @default false */
  loading?: boolean;
};
const variants = {
  primary: 'bg-primary text-primary-foreground shadow-xs hover:bg-primary/90',
  secondary: 'bg-secondary text-secondary-foreground shadow-xs hover:bg-secondary/80',
  outline: 'border-border bg-background text-foreground shadow-xs hover:bg-accent hover:text-accent-foreground',
  ghost: 'bg-transparent text-foreground hover:bg-accent hover:text-accent-foreground',
  destructive: 'bg-destructive text-destructive-foreground shadow-xs hover:bg-destructive/90',
};
function ButtonView({ slot, variant = 'primary', size = 'md', loading = false, disabled, children, className, testId, ...props }: ButtonProps & { slot: string }) {
  const identity = useContext(ButtonIdentityContext);
  return <BaseButton {...props} disabled={disabled || loading} aria-busy={loading || undefined} data-slot={identity?.slot ?? slot} data-testid={identity?.testId ?? testId ?? `yue-${slot}`} className={cn(button, variants[variant], { 'h-8 px-2.5': size === 'sm', 'h-10 px-4': size === 'lg', 'size-9 p-0': size === 'icon' }, className)}>{loading && <Icon name="spinner" className="animate-spin motion-reduce:animate-none" />}{children}</BaseButton>;
}
export function Button(props: ButtonProps) { return <ButtonView {...props} slot="button" />; }
export type IconButtonProps = ButtonProps & { label: string };
export function IconButton({ label, ...props }: IconButtonProps) { return <ButtonView {...props} size="icon" aria-label={label} slot="icon-button" />; }
export type ButtonGroupProps = YueProps<ComponentProps<'div'>>;
export function ButtonGroup({ className, testId, ...props }: ButtonGroupProps) { return <div {...props} role="group" data-slot="button-group" data-testid={testId ?? 'yue-button-group'} className={cn('inline-flex w-fit items-stretch [&>button]:rounded-none [&>button:first-child]:rounded-l-md [&>button:last-child]:rounded-r-md [&>button+button]:-ml-px [&>button:focus-visible]:z-10', className)} />; }
