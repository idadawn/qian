import { PreviewCard as Base } from '@base-ui/react/preview-card';
import type { ComponentProps } from 'react';
import { cn } from '../lib/cn';
import type { YueProps } from '../lib/test-id';
import { button, surface } from '../lib/styles';

export type HoverCardProps = ComponentProps<typeof Base.Root>;
export function HoverCard(props: HoverCardProps) { return <Base.Root {...props} />; }

export type HoverCardTriggerProps = YueProps<ComponentProps<typeof Base.Trigger>>;
export function HoverCardTrigger({ className, testId, ...props }: HoverCardTriggerProps) {
  return <Base.Trigger {...props} data-slot="hover-card-trigger" data-testid={testId ?? 'yue-hover-card-trigger'} className={cn(button, className)} />;
}

export type HoverCardContentProps = YueProps<ComponentProps<typeof Base.Popup>> & { side?: "top" | "right" | "bottom" | "left"; align?: "start" | "center" | "end"; sideOffset?: number };
export function HoverCardContent({ className, testId, sideOffset = 6, side, align, ...props }: HoverCardContentProps) {
  return <Base.Portal><Base.Positioner side={side} align={align} sideOffset={sideOffset} className="z-50"><Base.Popup {...props} data-slot="hover-card-content" data-testid={testId ?? 'yue-hover-card-content'} className={cn(surface, 'max-w-sm', className)} /></Base.Positioner></Base.Portal>;
}
