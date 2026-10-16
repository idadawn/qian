import { AlertDialog as Base } from '@base-ui/react/alert-dialog';
import type { ComponentProps } from 'react';
import { IconButton } from './button';
import { Icon } from './icon';
import { ButtonIdentity } from '../lib/button-identity';
import { cn } from '../lib/cn';
import type { YueProps } from '../lib/test-id';
import { button, surface, backdrop } from '../lib/styles';

export type AlertDialogProps = ComponentProps<typeof Base.Root>;
export function AlertDialog(props: AlertDialogProps) { return <Base.Root {...props} />; }

export type AlertDialogTriggerProps = YueProps<ComponentProps<typeof Base.Trigger>>;
export function AlertDialogTrigger({ className, testId, ...props }: AlertDialogTriggerProps) {
  return <ButtonIdentity slot="alert-dialog-trigger" testId={testId ?? 'yue-alert-dialog-trigger'}><Base.Trigger {...props} data-slot="alert-dialog-trigger" data-testid={testId ?? 'yue-alert-dialog-trigger'} className={className} /></ButtonIdentity>;
}

export type AlertDialogTitleProps = YueProps<ComponentProps<typeof Base.Title>>;
export function AlertDialogTitle({ className, testId, ...props }: AlertDialogTitleProps) {
  return <Base.Title {...props} data-slot="alert-dialog-title" data-testid={testId ?? 'yue-alert-dialog-title'} className={cn('pr-8 text-base font-semibold leading-none', className)} />;
}

export type AlertDialogDescriptionProps = YueProps<ComponentProps<typeof Base.Description>>;
export function AlertDialogDescription({ className, testId, ...props }: AlertDialogDescriptionProps) {
  return <Base.Description {...props} data-slot="alert-dialog-description" data-testid={testId ?? 'yue-alert-dialog-description'} className={cn('text-sm text-muted-foreground', className)} />;
}

export type AlertDialogCloseProps = YueProps<ComponentProps<typeof Base.Close>>;
export function AlertDialogClose({ className, testId, ...props }: AlertDialogCloseProps) {
  return <ButtonIdentity slot="alert-dialog-close" testId={testId ?? 'yue-alert-dialog-close'}><Base.Close {...props} data-slot="alert-dialog-close" data-testid={testId ?? 'yue-alert-dialog-close'} className={cn(button, className)} /></ButtonIdentity>;
}

export type AlertDialogContentProps = YueProps<ComponentProps<typeof Base.Popup>> & {
  /** 是否显示右上角关闭按钮；须同时提供宿主本地化的 closeLabel。 @default true */
  showCloseButton?: boolean;
  /** 右上角关闭按钮的可访问名称；旧调用不传时保留原有结构。 */
  closeLabel?: string;
};
export function AlertDialogContent({ className, testId, children, closeLabel, showCloseButton = true, ...props }: AlertDialogContentProps) {
  const closeId = testId ? `${testId}-close` : 'yue-alert-dialog-close-button';
  return <Base.Portal><Base.Backdrop data-slot="alert-dialog-backdrop" data-testid="yue-alert-dialog-backdrop" className={backdrop} />
    <Base.Popup {...props} data-slot="alert-dialog-content" data-testid={testId ?? 'yue-alert-dialog-content'} className={cn(surface, 'fixed left-1/2 top-1/2 z-50 max-h-[85dvh] w-[min(32rem,calc(100vw-2rem))] -translate-x-1/2 -translate-y-1/2 overflow-auto space-y-4 p-6', showCloseButton && closeLabel && '[&>:nth-last-child(2)]:mb-0', className)}>
      {children}
      {showCloseButton && closeLabel && <ButtonIdentity slot="alert-dialog-close-button" testId={closeId}><Base.Close data-slot="alert-dialog-close-button" data-testid={closeId} render={<IconButton label={closeLabel} variant="ghost" className="absolute right-3 top-3 size-7" />}><Icon name="close" /></Base.Close></ButtonIdentity>}
    </Base.Popup>
  </Base.Portal>;
}
