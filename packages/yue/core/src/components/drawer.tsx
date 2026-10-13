import { Drawer as Base } from '@base-ui/react/drawer';
import type { ComponentProps } from 'react';
import { cn } from '../lib/cn';
import type { YueProps } from '../lib/test-id';
import { button, surface } from '../lib/styles';

export type DrawerProps = ComponentProps<typeof Base.Root>;
export function Drawer(props: DrawerProps) { return <Base.Root {...props} />; }

export type DrawerTriggerProps = YueProps<ComponentProps<typeof Base.Trigger>>;
export function DrawerTrigger({ className, testId, ...props }: DrawerTriggerProps) {
  return <Base.Trigger {...props} data-slot="drawer-trigger" data-testid={testId ?? 'yue-drawer-trigger'} className={cn(button, className)} />;
}

export type DrawerTitleProps = YueProps<ComponentProps<typeof Base.Title>>;
export function DrawerTitle({ className, testId, ...props }: DrawerTitleProps) {
  return <Base.Title {...props} data-slot="drawer-title" data-testid={testId ?? 'yue-drawer-title'} className={cn('text-lg font-semibold', className)} />;
}

export type DrawerDescriptionProps = YueProps<ComponentProps<typeof Base.Description>>;
export function DrawerDescription({ className, testId, ...props }: DrawerDescriptionProps) {
  return <Base.Description {...props} data-slot="drawer-description" data-testid={testId ?? 'yue-drawer-description'} className={cn('text-sm text-[var(--muted-foreground)]', className)} />;
}

export type DrawerCloseProps = YueProps<ComponentProps<typeof Base.Close>>;
export function DrawerClose({ className, testId, ...props }: DrawerCloseProps) {
  return <Base.Close {...props} data-slot="drawer-close" data-testid={testId ?? 'yue-drawer-close'} className={cn(button, className)} />;
}

export type DrawerContentProps = YueProps<ComponentProps<typeof Base.Popup>>;
export function DrawerContent({ className, testId, ...props }: DrawerContentProps) {
  return <Base.Portal><Base.Backdrop data-slot="drawer-backdrop" data-testid="yue-drawer-backdrop" className="fixed inset-0 z-40 bg-[oklch(var(--yue-p-black)/0.4)]" /><Base.Viewport className="fixed inset-0 z-50 flex items-end"><Base.Popup {...props} data-slot="drawer-content" data-testid={testId ?? 'yue-drawer-content'} className={cn(surface, 'max-h-[85dvh] w-full overflow-auto rounded-b-none', className)} /></Base.Viewport></Base.Portal>;
}
