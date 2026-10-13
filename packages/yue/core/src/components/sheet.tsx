import { Dialog as Base } from '@base-ui/react/dialog';
import type { ComponentProps } from 'react';
import { cn } from '../lib/cn';
import { button, surface } from '../lib/styles';
import type { YueProps } from '../lib/test-id';

export type SheetProps = ComponentProps<typeof Base.Root>;
export function Sheet(props: SheetProps) { return <Base.Root {...props} />; }
export type SheetTriggerProps = YueProps<ComponentProps<typeof Base.Trigger>>;
export function SheetTrigger({ className, testId, ...props }: SheetTriggerProps) { return <Base.Trigger {...props} data-slot="sheet-trigger" data-testid={testId ?? 'yue-sheet-trigger'} className={cn(button, className)} />; }
export type SheetContentProps = YueProps<ComponentProps<typeof Base.Popup>> & { side?: 'left' | 'right' };
export function SheetContent({ className, testId, side = 'right', ...props }: SheetContentProps) {
  return <Base.Portal><Base.Backdrop data-slot="sheet-backdrop" data-testid="yue-sheet-backdrop" className="fixed inset-0 z-40 bg-[oklch(var(--yue-p-black)/0.4)]" /><Base.Popup {...props} data-slot="sheet-content" data-testid={testId ?? 'yue-sheet-content'} className={cn(surface, 'fixed inset-y-0 z-50 w-[min(28rem,90vw)] overflow-auto rounded-none', side === 'left' ? 'left-0' : 'right-0', className)} /></Base.Portal>;
}
export type SheetTitleProps = YueProps<ComponentProps<typeof Base.Title>>;
export function SheetTitle({ className, testId, ...props }: SheetTitleProps) { return <Base.Title {...props} data-slot="sheet-title" data-testid={testId ?? 'yue-sheet-title'} className={cn('text-lg font-semibold', className)} />; }
export type SheetDescriptionProps = YueProps<ComponentProps<typeof Base.Description>>;
export function SheetDescription({ className, testId, ...props }: SheetDescriptionProps) { return <Base.Description {...props} data-slot="sheet-description" data-testid={testId ?? 'yue-sheet-description'} className={cn('text-sm text-[var(--muted-foreground)]', className)} />; }
export type SheetCloseProps = YueProps<ComponentProps<typeof Base.Close>>;
export function SheetClose({ className, testId, ...props }: SheetCloseProps) { return <Base.Close {...props} data-slot="sheet-close" data-testid={testId ?? 'yue-sheet-close'} className={cn(button, className)} />; }
