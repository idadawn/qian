import { ArrowUp, Check, ChevronDown, ChevronLeft, ChevronRight, CircleAlert, Eye, EyeOff, Info, LoaderCircle, Maximize, Minimize, Minus, X } from 'lucide-react';
import type { ComponentProps } from 'react';
import { cn } from '../lib/cn';
import type { YueProps } from '../lib/test-id';

const icons = { arrowUp: ArrowUp, check: Check, chevronDown: ChevronDown, chevronLeft: ChevronLeft, chevronRight: ChevronRight, alert: CircleAlert, eye: Eye, eyeOff: EyeOff, info: Info, spinner: LoaderCircle, maximize: Maximize, minimize: Minimize, minus: Minus, close: X };
export type IconName = keyof typeof icons;
export type IconProps = YueProps<Omit<ComponentProps<'svg'>, 'name'>> & {
  name: IconName; label?: string; size?: number;
  /** 透传 SVG 的 title 属性；可访问名称由 label 提供。 */
  title?: string;
};
/** 统一 Lucide 入口；无 label 时为装饰，有 label 时提供独立可访问名称。 */
export function Icon({ name, label, size = 16, className, testId, ...props }: IconProps) {
  const Glyph = icons[name];
  return <Glyph {...props} size={size} role={label ? 'img' : undefined} aria-label={label} aria-hidden={label ? undefined : true} focusable="false" data-slot="icon" data-testid={testId ?? 'yue-icon'} className={cn('shrink-0', className)} />;
}
