import { Separator as BaseSeparator } from '@base-ui/react/separator';
import { ScrollArea as BaseScrollArea } from '@base-ui/react/scroll-area';
import { Group, Panel, Separator as ResizeSeparator } from 'react-resizable-panels';
import { useId, type ComponentProps, type ReactNode } from 'react';
import { cn } from '../lib/cn';
import type { YueProps } from '../lib/test-id';

export type SeparatorProps = YueProps<ComponentProps<typeof BaseSeparator>>;
export function Separator({ className, testId, ...props }: SeparatorProps) { return <BaseSeparator {...props} data-slot="separator" data-testid={testId ?? 'yue-separator'} className={cn('shrink-0 bg-[var(--border)] data-[orientation=horizontal]:h-px data-[orientation=horizontal]:w-full data-[orientation=vertical]:h-full data-[orientation=vertical]:w-px', className)} />; }
export type ScrollAreaProps = YueProps<ComponentProps<typeof BaseScrollArea.Root>> & { label: string };
export function ScrollArea({ className, testId, children, label, ...props }: ScrollAreaProps) {
  return <BaseScrollArea.Root {...props} data-slot="scroll-area" data-testid={testId ?? 'yue-scroll-area'} className={cn('relative overflow-hidden', className)}><BaseScrollArea.Viewport tabIndex={0} aria-label={label} role="region" className="size-full rounded-[inherit] outline-none focus-visible:ring-2 focus-visible:ring-[var(--ring)]"><BaseScrollArea.Content>{children}</BaseScrollArea.Content></BaseScrollArea.Viewport><BaseScrollArea.Scrollbar orientation="vertical" className="flex w-2 p-0.5"><BaseScrollArea.Thumb className="w-full rounded bg-[var(--muted-foreground)]" /></BaseScrollArea.Scrollbar><BaseScrollArea.Scrollbar orientation="horizontal" className="flex h-2 p-0.5"><BaseScrollArea.Thumb className="h-full rounded bg-[var(--muted-foreground)]" /></BaseScrollArea.Scrollbar></BaseScrollArea.Root>;
}
export type ResizableProps = YueProps<ComponentProps<typeof Group>>;
export function Resizable({ className, testId, ...props }: ResizableProps) { return <Group {...props} data-slot="resizable" data-testid={testId ?? 'yue-resizable'} className={cn('min-h-24', className)} />; }
export type ResizablePanelProps = YueProps<ComponentProps<typeof Panel>>;
export function ResizablePanel({ className, testId, ...props }: ResizablePanelProps) { return <Panel {...props} data-slot="resizable-panel" data-testid={testId ?? 'yue-resizable-panel'} className={className} />; }
export type ResizableHandleProps = YueProps<ComponentProps<typeof ResizeSeparator>> & { label: string };
export function ResizableHandle({ className, testId, label, ...props }: ResizableHandleProps) { return <ResizeSeparator {...props} aria-label={label} data-slot="resizable-handle" data-testid={testId ?? 'yue-resizable-handle'} className={cn('w-2 bg-[var(--border)] outline-none aria-[orientation=horizontal]:h-2 aria-[orientation=horizontal]:w-full focus-visible:bg-[var(--ring)]', className)} />; }
export type CardProps = YueProps<ComponentProps<'div'>>;
export function Card({ className, testId, ...props }: CardProps) { return <div {...props} data-slot="card" data-testid={testId ?? 'yue-card'} className={cn('rounded-lg border border-[var(--border)] bg-[var(--card)] text-[var(--card-foreground)] shadow-sm', className)} />; }
export function CardHeader({ className, testId, ...props }: CardProps) { return <div {...props} data-slot="card-header" data-testid={testId ?? 'yue-card-header'} className={cn('space-y-1.5 p-5', className)} />; }
export type CardTitleProps = YueProps<ComponentProps<'h3'>>;
export function CardTitle({ className, testId, ...props }: CardTitleProps) { return <h3 {...props} data-slot="card-title" data-testid={testId ?? 'yue-card-title'} className={cn('text-lg font-semibold', className)} />; }
export type CardDescriptionProps = YueProps<ComponentProps<'p'>>;
export function CardDescription({ className, testId, ...props }: CardDescriptionProps) { return <p {...props} data-slot="card-description" data-testid={testId ?? 'yue-card-description'} className={cn('text-sm text-[var(--muted-foreground)]', className)} />; }
export function CardContent({ className, testId, ...props }: CardProps) { return <div {...props} data-slot="card-content" data-testid={testId ?? 'yue-card-content'} className={cn('p-5 pt-0', className)} />; }
export function CardFooter({ className, testId, ...props }: CardProps) { return <div {...props} data-slot="card-footer" data-testid={testId ?? 'yue-card-footer'} className={cn('flex items-center gap-2 p-5 pt-0', className)} />; }
export interface DescriptionItem { key: string; label: ReactNode; children: ReactNode }
export type DescriptionsProps = YueProps<ComponentProps<'dl'>> & { items: readonly DescriptionItem[] };
export function Descriptions({ className, testId, items, ...props }: DescriptionsProps) {
  const prefix = useId();
  return <dl {...props} data-slot="descriptions" data-testid={testId ?? 'yue-descriptions'} className={cn('grid grid-cols-[max-content_1fr] gap-x-5 gap-y-2 text-sm', className)}>{items.map(item => <div key={item.key} className="contents"><dt id={`${prefix}-${item.key}`} className="text-[var(--muted-foreground)]">{item.label}</dt><dd aria-labelledby={`${prefix}-${item.key}`}>{item.children}</dd></div>)}</dl>;
}
