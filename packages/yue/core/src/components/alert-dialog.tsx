import { AlertDialog as Base } from '@base-ui/react/alert-dialog';
import type { ComponentProps } from 'react';
import { cn } from '../lib/cn';
import type { YueProps } from '../lib/test-id';
import { button, surface } from '../lib/styles';

export type AlertDialogProps = ComponentProps<typeof Base.Root>;
export function AlertDialog(props: AlertDialogProps) { return <Base.Root {...props} />; }

export type AlertDialogTriggerProps = YueProps<ComponentProps<typeof Base.Trigger>>;
export function AlertDialogTrigger({ className, testId, ...props }: AlertDialogTriggerProps) {
  return <Base.Trigger {...props} data-slot="alert-dialog-trigger" data-testid={testId ?? 'yue-alert-dialog-trigger'} className={cn(button, className)} />;
}

export type AlertDialogTitleProps = YueProps<ComponentProps<typeof Base.Title>>;
export function AlertDialogTitle({ className, testId, ...props }: AlertDialogTitleProps) {
  return <Base.Title {...props} data-slot="alert-dialog-title" data-testid={testId ?? 'yue-alert-dialog-title'} className={cn('text-lg font-semibold', className)} />;
}

export type AlertDialogDescriptionProps = YueProps<ComponentProps<typeof Base.Description>>;
export function AlertDialogDescription({ className, testId, ...props }: AlertDialogDescriptionProps) {
  return <Base.Description {...props} data-slot="alert-dialog-description" data-testid={testId ?? 'yue-alert-dialog-description'} className={cn('text-sm text-[var(--muted-foreground)]', className)} />;
}

export type AlertDialogCloseProps = YueProps<ComponentProps<typeof Base.Close>>;
export function AlertDialogClose({ className, testId, ...props }: AlertDialogCloseProps) {
  return <Base.Close {...props} data-slot="alert-dialog-close" data-testid={testId ?? 'yue-alert-dialog-close'} className={cn(button, className)} />;
}

export type AlertDialogContentProps = YueProps<ComponentProps<typeof Base.Popup>>;
export function AlertDialogContent({ className, testId, ...props }: AlertDialogContentProps) {
  return <Base.Portal><Base.Backdrop data-slot="alert-dialog-backdrop" data-testid="yue-alert-dialog-backdrop" className="fixed inset-0 z-40 bg-[oklch(var(--yue-p-black)/0.4)]" /><Base.Popup {...props} data-slot="alert-dialog-content" data-testid={testId ?? 'yue-alert-dialog-content'} className={cn(surface, 'fixed left-1/2 top-1/2 z-50 max-h-[85dvh] w-[min(32rem,calc(100vw-2rem))] -translate-x-1/2 -translate-y-1/2 overflow-auto', className)} /></Base.Portal>;
}
