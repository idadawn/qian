import { Drawer as Base } from '@base-ui/react/drawer';
import type { ComponentProps } from 'react';
import { ButtonIdentity } from '../lib/button-identity';
import { cn } from '../lib/cn';
import type { YueProps } from '../lib/test-id';
import { button, surface, backdrop } from '../lib/styles';

export type DrawerProps = ComponentProps<typeof Base.Root>;
export function Drawer(props: DrawerProps) { return <Base.Root {...props} />; }

export type DrawerTriggerProps = YueProps<ComponentProps<typeof Base.Trigger>>;
export function DrawerTrigger({ className, testId, ...props }: DrawerTriggerProps) {
  return <ButtonIdentity slot="drawer-trigger" testId={testId ?? 'yue-drawer-trigger'}><Base.Trigger {...props} data-slot="drawer-trigger" data-testid={testId ?? 'yue-drawer-trigger'} className={className} /></ButtonIdentity>;
}

export type DrawerTitleProps = YueProps<ComponentProps<typeof Base.Title>>;
export function DrawerTitle({ className, testId, ...props }: DrawerTitleProps) {
  return <Base.Title {...props} data-slot="drawer-title" data-testid={testId ?? 'yue-drawer-title'} className={cn('text-base font-semibold leading-none', className)} />;
}

export type DrawerDescriptionProps = YueProps<ComponentProps<typeof Base.Description>>;
export function DrawerDescription({ className, testId, ...props }: DrawerDescriptionProps) {
  return <Base.Description {...props} data-slot="drawer-description" data-testid={testId ?? 'yue-drawer-description'} className={cn('text-sm text-muted-foreground', className)} />;
}

export type DrawerCloseProps = YueProps<ComponentProps<typeof Base.Close>>;
export function DrawerClose({ className, testId, ...props }: DrawerCloseProps) {
  return <ButtonIdentity slot="drawer-close" testId={testId ?? 'yue-drawer-close'}><Base.Close {...props} data-slot="drawer-close" data-testid={testId ?? 'yue-drawer-close'} className={cn(button, className)} /></ButtonIdentity>;
}

export type DrawerContentProps = YueProps<ComponentProps<typeof Base.Popup>>;
export function DrawerContent({ className, testId, ...props }: DrawerContentProps) {
  return <Base.Portal><Base.Backdrop data-slot="drawer-backdrop" data-testid="yue-drawer-backdrop" className={backdrop} /><Base.Viewport className="fixed inset-0 z-50 flex items-end"><Base.Popup {...props} data-slot="drawer-content" data-testid={testId ?? 'yue-drawer-content'} className={cn(surface, 'max-h-[85dvh] w-full overflow-auto space-y-4 rounded-t-xl rounded-b-none p-6 data-[starting-style]:translate-y-full data-[ending-style]:translate-y-full data-[starting-style]:scale-100 data-[ending-style]:scale-100', className)} /></Base.Viewport></Base.Portal>;
}
