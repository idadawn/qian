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
export type SheetContentProps = YueProps<ComponentProps<typeof Base.Popup>> & {
  side?: 'left' | 'right';
  /** 是否显示右上角关闭按钮；须同时提供宿主本地化的 closeLabel。 @default true */
  showCloseButton?: boolean;
  /** 右上角关闭按钮的可访问名称；旧调用不传时保留原有结构。 */
  closeLabel?: string;
};
export function SheetContent({ className, testId, children, closeLabel, showCloseButton = true, side = 'right', ...props }: SheetContentProps) {
  const closeId = testId ? `${testId}-close` : 'yue-sheet-close-button';
  return <Base.Portal><Base.Backdrop data-slot="sheet-backdrop" data-testid="yue-sheet-backdrop" className={backdrop} />
    <Base.Popup {...props} data-slot="sheet-content" data-testid={testId ?? 'yue-sheet-content'} className={cn(surface, 'fixed inset-y-0 z-50 w-[min(28rem,90vw)] overflow-auto space-y-4 rounded-none p-6 data-[starting-style]:scale-100 data-[ending-style]:scale-100', side === 'left' ? 'left-0 data-[starting-style]:-translate-x-full data-[ending-style]:-translate-x-full' : 'right-0 data-[starting-style]:translate-x-full data-[ending-style]:translate-x-full', showCloseButton && closeLabel && '[&>:nth-last-child(2)]:mb-0', className)}>
      {children}
      {showCloseButton && closeLabel && <ButtonIdentity slot="sheet-close-button" testId={closeId}><Base.Close data-slot="sheet-close-button" data-testid={closeId} render={<IconButton label={closeLabel} variant="ghost" className="absolute right-3 top-3 size-7" />}><Icon name="close" /></Base.Close></ButtonIdentity>}
    </Base.Popup>
  </Base.Portal>;
}
export type SheetTitleProps = YueProps<ComponentProps<typeof Base.Title>>;
export function SheetTitle({ className, testId, ...props }: SheetTitleProps) { return <Base.Title {...props} data-slot="sheet-title" data-testid={testId ?? 'yue-sheet-title'} className={cn('pr-8 text-base font-semibold leading-none', className)} />; }
export type SheetDescriptionProps = YueProps<ComponentProps<typeof Base.Description>>;
export function SheetDescription({ className, testId, ...props }: SheetDescriptionProps) { return <Base.Description {...props} data-slot="sheet-description" data-testid={testId ?? 'yue-sheet-description'} className={cn('text-sm text-muted-foreground', className)} />; }
export type SheetCloseProps = YueProps<ComponentProps<typeof Base.Close>>;
export function SheetClose({ className, testId, ...props }: SheetCloseProps) { return <ButtonIdentity slot="sheet-close" testId={testId ?? 'yue-sheet-close'}><Base.Close {...props} data-slot="sheet-close" data-testid={testId ?? 'yue-sheet-close'} className={cn(button, className)} /></ButtonIdentity>; }
