import { Popover as Base } from '@base-ui/react/popover';
import type { ComponentProps } from 'react';
import { ButtonIdentity } from '../lib/button-identity';
import { cn } from '../lib/cn';
import type { YueProps } from '../lib/test-id';
import { button, surface } from '../lib/styles';

export type PopoverProps = ComponentProps<typeof Base.Root>;
export function Popover(props: PopoverProps) { return <Base.Root {...props} />; }

export type PopoverTriggerProps = YueProps<ComponentProps<typeof Base.Trigger>>;
export function PopoverTrigger({ className, testId, ...props }: PopoverTriggerProps) {
  return <ButtonIdentity slot="popover-trigger" testId={testId ?? 'yue-popover-trigger'}><Base.Trigger {...props} data-slot="popover-trigger" data-testid={testId ?? 'yue-popover-trigger'} className={className} /></ButtonIdentity>;
}

export type PopoverTitleProps = YueProps<ComponentProps<typeof Base.Title>>;
export function PopoverTitle({ className, testId, ...props }: PopoverTitleProps) {
  return <Base.Title {...props} data-slot="popover-title" data-testid={testId ?? 'yue-popover-title'} className={cn('text-base font-semibold leading-none', className)} />;
}

export type PopoverDescriptionProps = YueProps<ComponentProps<typeof Base.Description>>;
export function PopoverDescription({ className, testId, ...props }: PopoverDescriptionProps) {
  return <Base.Description {...props} data-slot="popover-description" data-testid={testId ?? 'yue-popover-description'} className={cn('text-sm text-muted-foreground', className)} />;
}

export type PopoverCloseProps = YueProps<ComponentProps<typeof Base.Close>>;
// 经 render 交给 Button 渲染时不再叠基础按钮类：Button 自带一套，再叠会让基础类里的 border-transparent 盖掉描边变体的边框。
export function PopoverClose({ className, testId, render, ...props }: PopoverCloseProps) {
  return <ButtonIdentity slot="popover-close" testId={testId ?? 'yue-popover-close'}><Base.Close {...props} data-slot="popover-close" data-testid={testId ?? 'yue-popover-close'} render={render} className={render ? className : cn(button, className)} /></ButtonIdentity>;
}

export type PopoverContentProps = YueProps<ComponentProps<typeof Base.Popup>> & { side?: "top" | "right" | "bottom" | "left"; align?: "start" | "center" | "end"; sideOffset?: number };
export function PopoverContent({ className, testId, sideOffset = 6, side, align, ...props }: PopoverContentProps) {
  return <Base.Portal><Base.Positioner side={side} align={align} sideOffset={sideOffset} className="z-50"><Base.Popup {...props} data-slot="popover-content" data-testid={testId ?? 'yue-popover-content'} className={cn(surface, 'max-w-sm', className)} /></Base.Positioner></Base.Portal>;
}
