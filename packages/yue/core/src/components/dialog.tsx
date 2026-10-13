import { Dialog as Base } from '@base-ui/react/dialog';
import type { ComponentProps } from 'react';
import { cn } from '../lib/cn';
import type { YueProps } from '../lib/test-id';
import { button, surface } from '../lib/styles';

export type DialogProps = ComponentProps<typeof Base.Root>;
export function Dialog(props: DialogProps) { return <Base.Root {...props} />; }

export type DialogTriggerProps = YueProps<ComponentProps<typeof Base.Trigger>>;
export function DialogTrigger({ className, testId, ...props }: DialogTriggerProps) {
  return <Base.Trigger {...props} data-slot="dialog-trigger" data-testid={testId ?? 'yue-dialog-trigger'} className={cn(button, className)} />;
}

export type DialogTitleProps = YueProps<ComponentProps<typeof Base.Title>>;
export function DialogTitle({ className, testId, ...props }: DialogTitleProps) {
  return <Base.Title {...props} data-slot="dialog-title" data-testid={testId ?? 'yue-dialog-title'} className={cn('text-lg font-semibold', className)} />;
}

export type DialogDescriptionProps = YueProps<ComponentProps<typeof Base.Description>>;
export function DialogDescription({ className, testId, ...props }: DialogDescriptionProps) {
  return <Base.Description {...props} data-slot="dialog-description" data-testid={testId ?? 'yue-dialog-description'} className={cn('text-sm text-[var(--muted-foreground)]', className)} />;
}

export type DialogCloseProps = YueProps<ComponentProps<typeof Base.Close>>;
export function DialogClose({ className, testId, ...props }: DialogCloseProps) {
  return <Base.Close {...props} data-slot="dialog-close" data-testid={testId ?? 'yue-dialog-close'} className={cn(button, className)} />;
}

export type DialogContentProps = YueProps<ComponentProps<typeof Base.Popup>>;
export function DialogContent({ className, testId, ...props }: DialogContentProps) {
  return <Base.Portal><Base.Backdrop data-slot="dialog-backdrop" data-testid="yue-dialog-backdrop" className="fixed inset-0 z-40 bg-[oklch(var(--yue-p-black)/0.4)]" /><Base.Popup {...props} data-slot="dialog-content" data-testid={testId ?? 'yue-dialog-content'} className={cn(surface, 'fixed left-1/2 top-1/2 z-50 max-h-[85dvh] w-[min(32rem,calc(100vw-2rem))] -translate-x-1/2 -translate-y-1/2 overflow-auto', className)} /></Base.Portal>;
}
