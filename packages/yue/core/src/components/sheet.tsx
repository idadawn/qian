import { Dialog as Base } from '@base-ui/react/dialog';
import type { ComponentProps } from 'react';
import { IconButton } from './button';
import { Icon } from './icon';
import { ButtonIdentity } from '../lib/button-identity';
import { cn } from '../lib/cn';
import { button, surface, backdrop } from '../lib/styles';
import type { YueProps } from '../lib/test-id';

export type SheetProps = ComponentProps<typeof Base.Root>;
export function Sheet(props: SheetProps) { return <Base.Root {...props} />; }
export type SheetTriggerProps = YueProps<ComponentProps<typeof Base.Trigger>>;
export function SheetTrigger({ className, testId, ...props }: SheetTriggerProps) { return <ButtonIdentity slot="sheet-trigger" testId={testId ?? 'yue-sheet-trigger'}><Base.Trigger {...props} data-slot="sheet-trigger" data-testid={testId ?? 'yue-sheet-trigger'} className={className} /></ButtonIdentity>; }
const widths = { sm: 'w-[min(28rem,100vw)]', md: 'w-[min(37.5rem,100vw)]', lg: 'w-[min(48rem,100vw)]' } as const;
export type SheetContentProps = YueProps<ComponentProps<typeof Base.Popup>> & {
  side?: 'left' | 'right';
  /** 宽度档：sm 448px、md 600px、lg 768px，窄屏时占满。 @default 'sm' */
  size?: keyof typeof widths;
  /** 是否显示右上角关闭按钮；须同时提供宿主本地化的 closeLabel。 @default true */
  showCloseButton?: boolean;
  /** 右上角关闭按钮的可访问名称；旧调用不传时保留原有结构。 */
  closeLabel?: string;
};
/** 侧滑面板：内容按 SheetHeader / SheetBody / SheetFooter 排列，主体单独滚动，页头与页脚固定。 */
export function SheetContent({ className, testId, children, closeLabel, showCloseButton = true, side = 'right', size = 'sm', ...props }: SheetContentProps) {
  const closeId = testId ? `${testId}-close` : 'yue-sheet-close-button';
  return <Base.Portal><Base.Backdrop data-slot="sheet-backdrop" data-testid="yue-sheet-backdrop" className={backdrop} />
    <Base.Popup {...props} data-slot="sheet-content" data-testid={testId ?? 'yue-sheet-content'} className={cn(surface, 'fixed inset-y-0 z-50 flex flex-col gap-0 rounded-none border-y-0 p-0 shadow-xl data-[starting-style]:scale-100 data-[ending-style]:scale-100', widths[size], side === 'left' ? 'left-0 border-s-0 data-[starting-style]:-translate-x-full data-[ending-style]:-translate-x-full' : 'right-0 border-e-0 data-[starting-style]:translate-x-full data-[ending-style]:translate-x-full', className)}>
      {children}
      {showCloseButton && closeLabel && <ButtonIdentity slot="sheet-close-button" testId={closeId}><Base.Close data-slot="sheet-close-button" data-testid={closeId} render={<IconButton label={closeLabel} variant="ghost" className="absolute end-6 top-5 size-8" />}><Icon name="close" size={18} /></Base.Close></ButtonIdentity>}
    </Base.Popup>
  </Base.Portal>;
}
export type SheetSectionProps = YueProps<ComponentProps<'div'>>;
/** 页头：标题、说明与状态徽标；底边分隔，右侧为关闭按钮留出位置。 */
export function SheetHeader({ className, testId, ...props }: SheetSectionProps) { return <div {...props} data-slot="sheet-header" data-testid={testId ?? 'yue-sheet-header'} className={cn('flex shrink-0 flex-col gap-1 border-b border-border px-6 pt-5 pb-4 pe-14', className)} />; }
/** 主体：唯一的滚动区；抽屉内的输入类控件统一为 38px。 */
export function SheetBody({ className, testId, ...props }: SheetSectionProps) { return <div {...props} data-slot="sheet-body" data-testid={testId ?? 'yue-sheet-body'} className={cn('min-h-0 flex-1 overflow-y-auto px-6 py-5 [--yue-input-height:2.375rem]', className)} />; }
/** 页脚：主操作靠右，内容滚动时保持可见；页脚按钮统一为 38px。 */
export function SheetFooter({ className, testId, ...props }: SheetSectionProps) { return <div {...props} data-slot="sheet-footer" data-testid={testId ?? 'yue-sheet-footer'} className={cn('flex shrink-0 flex-wrap items-center justify-end gap-3 border-t border-border bg-background px-6 py-3.5 empty:hidden [--yue-button-height:2.375rem]', className)} />; }
export type SheetTitleProps = YueProps<ComponentProps<typeof Base.Title>>;
export function SheetTitle({ className, testId, ...props }: SheetTitleProps) { return <Base.Title {...props} data-slot="sheet-title" data-testid={testId ?? 'yue-sheet-title'} className={cn('text-lg font-bold leading-6.5', className)} />; }
export type SheetDescriptionProps = YueProps<ComponentProps<typeof Base.Description>>;
export function SheetDescription({ className, testId, ...props }: SheetDescriptionProps) { return <Base.Description {...props} data-slot="sheet-description" data-testid={testId ?? 'yue-sheet-description'} className={cn('text-[13px] text-muted-foreground', className)} />; }
export type SheetCloseProps = YueProps<ComponentProps<typeof Base.Close>>;
export function SheetClose({ className, testId, ...props }: SheetCloseProps) { return <ButtonIdentity slot="sheet-close" testId={testId ?? 'yue-sheet-close'}><Base.Close {...props} data-slot="sheet-close" data-testid={testId ?? 'yue-sheet-close'} className={cn(button, className)} /></ButtonIdentity>; }
