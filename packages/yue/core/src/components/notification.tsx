import { Toast as BaseToast } from '@base-ui/react/toast';
import type { ComponentProps } from 'react';
import { cn } from '../lib/cn';
import { button, surface } from '../lib/styles';
import type { YueProps } from '../lib/test-id';
import { Icon } from './icon';

/** Toast 与 Notification 共用 Base UI 的队列、焦点恢复与可访问播报。 */
export const ToastProvider = BaseToast.Provider;
export const useToast = BaseToast.useToastManager;
export const createToastManager = BaseToast.createToastManager;
export type ToastProps = YueProps<ComponentProps<typeof BaseToast.Root>>;
export function Toast({ className, testId, ...props }: ToastProps) { return <BaseToast.Root {...props} data-slot="toast" data-testid={testId ?? 'yue-toast'} className={cn(surface, 'relative w-80 max-w-[calc(100vw-2rem)] data-[limited]:hidden', className)} />; }
export function Notification({ className, testId, ...props }: ToastProps) { return <BaseToast.Root {...props} data-slot="notification" data-testid={testId ?? 'yue-notification'} className={cn(surface, 'relative w-96 max-w-[calc(100vw-2rem)] data-[limited]:hidden', className)} />; }
export type ToastTitleProps = YueProps<ComponentProps<typeof BaseToast.Title>>;
export function ToastTitle({ className, testId, ...props }: ToastTitleProps) { return <BaseToast.Title {...props} data-slot="toast-title" data-testid={testId ?? 'yue-toast-title'} className={cn('pr-6 font-medium', className)} />; }
export type ToastDescriptionProps = YueProps<ComponentProps<typeof BaseToast.Description>>;
export function ToastDescription({ className, testId, ...props }: ToastDescriptionProps) { return <BaseToast.Description {...props} data-slot="toast-description" data-testid={testId ?? 'yue-toast-description'} className={cn('mt-1 text-sm text-[var(--muted-foreground)]', className)} />; }
export type ToastCloseProps = YueProps<ComponentProps<typeof BaseToast.Close>> & { label: string };
export function ToastClose({ label, className, testId, ...props }: ToastCloseProps) { return <BaseToast.Close {...props} aria-label={label} data-slot="toast-close" data-testid={testId ?? 'yue-toast-close'} className={cn(button, 'absolute right-2 top-2 size-6 p-0', className)}><Icon name="close" /></BaseToast.Close>; }
export type ToastActionProps = YueProps<ComponentProps<typeof BaseToast.Action>>;
export function ToastAction({ className, testId, ...props }: ToastActionProps) { return <BaseToast.Action {...props} data-slot="toast-action" data-testid={testId ?? 'yue-toast-action'} className={cn(button, 'mt-2 bg-[var(--secondary)] text-[var(--secondary-foreground)]', className)} />; }
export type ToastViewportProps = YueProps<ComponentProps<typeof BaseToast.Viewport>>;
export function ToastViewport({ className, testId, ...props }: ToastViewportProps) { return <BaseToast.Portal><BaseToast.Viewport {...props} data-slot="toast-viewport" data-testid={testId ?? 'yue-toast-viewport'} className={cn('fixed bottom-4 right-4 z-50 flex flex-col gap-2 outline-none', className)} /></BaseToast.Portal>; }
export interface ToastListProps { closeLabel: string }
/** 统一消息展示出口；type=notification 时使用持久通知外观，timeout 由同一 manager 管理。 */
export function ToastList({ closeLabel }: ToastListProps) {
  const { toasts } = useToast();
  return <ToastViewport>{toasts.map(toast => {
    const Root = toast.type === 'notification' ? Notification : Toast;
    return <Root key={toast.id} toast={toast}><BaseToast.Content><ToastTitle>{toast.title}</ToastTitle>{toast.description && <ToastDescription>{toast.description}</ToastDescription>}{toast.actionProps && <ToastAction {...toast.actionProps} />}</BaseToast.Content><ToastClose label={closeLabel} /></Root>;
  })}</ToastViewport>;
}
