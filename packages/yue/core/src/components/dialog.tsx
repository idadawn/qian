import { Dialog as Base } from '@base-ui/react/dialog';
import type { ComponentProps } from 'react';
import { IconButton } from './button';
import { Icon } from './icon';
import { ButtonIdentity } from '../lib/button-identity';
import { cn } from '../lib/cn';
import type { YueProps } from '../lib/test-id';
import { button, surface, backdrop } from '../lib/styles';

export type DialogProps = ComponentProps<typeof Base.Root>;
export function Dialog(props: DialogProps) { return <Base.Root {...props} />; }

export type DialogTriggerProps = YueProps<ComponentProps<typeof Base.Trigger>>;
export function DialogTrigger({ className, testId, ...props }: DialogTriggerProps) {
  return <ButtonIdentity slot="dialog-trigger" testId={testId ?? 'yue-dialog-trigger'}><Base.Trigger {...props} data-slot="dialog-trigger" data-testid={testId ?? 'yue-dialog-trigger'} className={className} /></ButtonIdentity>;
}

export type DialogTitleProps = YueProps<ComponentProps<typeof Base.Title>>;
export function DialogTitle({ className, testId, ...props }: DialogTitleProps) {
  return <Base.Title {...props} data-slot="dialog-title" data-testid={testId ?? 'yue-dialog-title'} className={cn('pr-8 text-base font-semibold leading-none', className)} />;
}

export type DialogDescriptionProps = YueProps<ComponentProps<typeof Base.Description>>;
export function DialogDescription({ className, testId, ...props }: DialogDescriptionProps) {
  return <Base.Description {...props} data-slot="dialog-description" data-testid={testId ?? 'yue-dialog-description'} className={cn('text-sm text-muted-foreground', className)} />;
}

export type DialogCloseProps = YueProps<ComponentProps<typeof Base.Close>>;
export function DialogClose({ className, testId, ...props }: DialogCloseProps) {
  return <ButtonIdentity slot="dialog-close" testId={testId ?? 'yue-dialog-close'}><Base.Close {...props} data-slot="dialog-close" data-testid={testId ?? 'yue-dialog-close'} className={cn(button, className)} /></ButtonIdentity>;
}

export type DialogContentProps = YueProps<ComponentProps<typeof Base.Popup>> & {
  /** 是否显示右上角关闭按钮；须同时提供宿主本地化的 closeLabel。 @default true */
  showCloseButton?: boolean;
  /** 右上角关闭按钮的可访问名称；旧调用不传时保留原有结构。 */
  closeLabel?: string;
};
export function DialogContent({ className, testId, children, closeLabel, showCloseButton = true, ...props }: DialogContentProps) {
  const closeId = testId ? `${testId}-close` : 'yue-dialog-close-button';
  return <Base.Portal><Base.Backdrop data-slot="dialog-backdrop" data-testid="yue-dialog-backdrop" className={backdrop} />
    <Base.Popup {...props} data-slot="dialog-content" data-testid={testId ?? 'yue-dialog-content'} className={cn(surface, 'fixed left-1/2 top-1/2 z-50 max-h-[85dvh] w-[min(32rem,calc(100vw-2rem))] -translate-x-1/2 -translate-y-1/2 overflow-auto space-y-4 p-6', showCloseButton && closeLabel && '[&>:nth-last-child(2)]:mb-0', className)}>
      {children}
      {showCloseButton && closeLabel && <ButtonIdentity slot="dialog-close-button" testId={closeId}><Base.Close data-slot="dialog-close-button" data-testid={closeId} render={<IconButton label={closeLabel} variant="ghost" className="absolute right-3 top-3 size-7" />}><Icon name="close" /></Base.Close></ButtonIdentity>}
    </Base.Popup>
  </Base.Portal>;
}
