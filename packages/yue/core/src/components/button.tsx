import { Button as BaseButton } from '@base-ui/react/button';
import { useContext, type ComponentProps } from 'react';
import { cn } from '../lib/cn';
import { ButtonIdentityContext } from '../lib/button-identity';
import { button } from '../lib/styles';
import type { YueProps } from '../lib/test-id';
import { Icon } from './icon';

export type ButtonProps = YueProps<ComponentProps<typeof BaseButton>> & {
  /** 按钮强调程度：soft 是主色浅底按钮，用于表格行内的主操作（如「审核」）；destructive-outline 是描边的危险操作（如抽屉页脚的「驳回」）；link 是主色文字按钮，用于「重置」一类的次要动作。 @default 'primary' */
  variant?: 'primary' | 'secondary' | 'outline' | 'ghost' | 'destructive' | 'destructive-outline' | 'soft' | 'link';
  /** 按钮尺寸：xs 30px、sm 32px、md 36px、lg 40px、xl 44px（认证与申请表单的主按钮）；icon 为 36px 方形。 @default 'md' */
  size?: 'xs' | 'sm' | 'md' | 'lg' | 'xl' | 'icon';
  /** 加载中时禁用操作并显示忙碌状态。 @default false */
  loading?: boolean;
};
const variants = {
  primary: 'bg-primary font-semibold text-primary-foreground shadow-xs hover:bg-primary/90',
  secondary: 'bg-secondary font-medium text-secondary-foreground hover:bg-secondary/80',
  outline: 'border-border-strong bg-background text-control-foreground hover:bg-accent',
  ghost: 'bg-transparent text-subtle-foreground hover:bg-accent hover:text-accent-foreground',
  destructive: 'bg-destructive font-semibold text-destructive-foreground shadow-xs hover:bg-destructive/90',
  'destructive-outline': 'border-destructive/35 bg-background font-medium text-error hover:bg-error-bg',
  soft: 'border-primary-border-strong bg-primary-subtle font-semibold text-primary-soft-foreground hover:bg-primary-soft',
  link: 'bg-transparent px-2.5 text-primary hover:bg-primary/8',
};
const sizes = { xs: 'h-7.5 px-3 text-[13px]', sm: 'h-8 px-3 text-[13px]', md: '', lg: 'h-10 px-4.5', xl: 'h-11 px-5', icon: 'size-9 p-0' };
function ButtonView({ slot, variant = 'primary', size = 'md', loading = false, disabled, children, className, testId, ...props }: ButtonProps & { slot: string }) {
  const identity = useContext(ButtonIdentityContext);
  return <BaseButton {...props} disabled={disabled || loading} aria-busy={loading || undefined} data-slot={identity?.slot ?? slot} data-testid={identity?.testId ?? testId ?? `yue-${slot}`} className={cn(button, variants[variant], sizes[size], className)}>{loading && <Icon name="spinner" className="animate-spin motion-reduce:animate-none" />}{children}</BaseButton>;
}
export function Button(props: ButtonProps) { return <ButtonView {...props} slot="button" />; }
export type IconButtonProps = ButtonProps & { label: string };
export function IconButton({ label, ...props }: IconButtonProps) { return <ButtonView {...props} size="icon" aria-label={label} slot="icon-button" />; }
export type ButtonGroupProps = YueProps<ComponentProps<'div'>>;
export function ButtonGroup({ className, testId, ...props }: ButtonGroupProps) { return <div {...props} role="group" data-slot="button-group" data-testid={testId ?? 'yue-button-group'} className={cn('inline-flex w-fit items-stretch [&>button]:rounded-none [&>button:first-child]:rounded-l-md [&>button:last-child]:rounded-r-md [&>button+button]:-ml-px [&>button:focus-visible]:z-10', className)} />; }
