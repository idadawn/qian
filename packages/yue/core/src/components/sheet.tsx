import { Dialog as Base } from '@base-ui/react/dialog';
import type { ComponentProps } from 'react';
import { ButtonIdentity } from '../lib/button-identity';
import { cn } from '../lib/cn';
import { button, surface, backdrop } from '../lib/styles';
import type { YueProps } from '../lib/test-id';

export type SheetProps = ComponentProps<typeof Base.Root>;
export function Sheet(props: SheetProps) { return <Base.Root {...props} />; }
export type SheetTriggerProps = YueProps<ComponentProps<typeof Base.Trigger>>;
export function SheetTrigger({ className, testId, ...props }: SheetTriggerProps) { return <ButtonIdentity slot="sheet-trigger" testId={testId ?? 'yue-sheet-trigger'}><Base.Trigger {...props} data-slot="sheet-trigger" data-testid={testId ?? 'yue-sheet-trigger'} className={className} /></ButtonIdentity>; }
export type SheetContentProps = YueProps<ComponentProps<typeof Base.Popup>> & { side?: 'left' | 'right' };
export function SheetContent({ className, testId, side = 'right', ...props }: SheetContentProps) {
  return <Base.Portal><Base.Backdrop data-slot="sheet-backdrop" data-testid="yue-sheet-backdrop" className={backdrop} /><Base.Popup {...props} data-slot="sheet-content" data-testid={testId ?? 'yue-sheet-content'} className={cn(surface, 'fixed inset-y-0 z-50 w-[min(28rem,90vw)] overflow-auto space-y-4 rounded-none p-6 data-[starting-style]:scale-100 data-[ending-style]:scale-100', side === 'left' ? 'left-0 data-[starting-style]:-translate-x-full data-[ending-style]:-translate-x-full' : 'right-0 data-[starting-style]:translate-x-full data-[ending-style]:translate-x-full', className)} /></Base.Portal>;
}
export type SheetTitleProps = YueProps<ComponentProps<typeof Base.Title>>;
export function SheetTitle({ className, testId, ...props }: SheetTitleProps) { return <Base.Title {...props} data-slot="sheet-title" data-testid={testId ?? 'yue-sheet-title'} className={cn('text-base font-semibold leading-none', className)} />; }
export type SheetDescriptionProps = YueProps<ComponentProps<typeof Base.Description>>;
export function SheetDescription({ className, testId, ...props }: SheetDescriptionProps) { return <Base.Description {...props} data-slot="sheet-description" data-testid={testId ?? 'yue-sheet-description'} className={cn('text-sm text-muted-foreground', className)} />; }
export type SheetCloseProps = YueProps<ComponentProps<typeof Base.Close>>;
export function SheetClose({ className, testId, ...props }: SheetCloseProps) { return <ButtonIdentity slot="sheet-close" testId={testId ?? 'yue-sheet-close'}><Base.Close {...props} data-slot="sheet-close" data-testid={testId ?? 'yue-sheet-close'} className={cn(button, className)} /></ButtonIdentity>; }
