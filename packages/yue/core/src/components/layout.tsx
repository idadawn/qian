import { Separator as BaseSeparator } from '@base-ui/react/separator';
import { ScrollArea as BaseScrollArea } from '@base-ui/react/scroll-area';
import { Group, Panel, Separator as ResizeSeparator } from 'react-resizable-panels';
import { useId, type ComponentProps, type ReactNode } from 'react';
import { cn } from '../lib/cn';
import type { YueProps } from '../lib/test-id';

export type SeparatorProps = YueProps<ComponentProps<typeof BaseSeparator>>;
export function Separator({ className, testId, ...props }: SeparatorProps) { return <BaseSeparator {...props} data-slot="separator" data-testid={testId ?? 'yue-separator'} className={cn('shrink-0 bg-border data-[orientation=horizontal]:h-px data-[orientation=horizontal]:w-full data-[orientation=vertical]:h-full data-[orientation=vertical]:w-px', className)} />; }
export type ScrollAreaProps = YueProps<ComponentProps<typeof BaseScrollArea.Root>> & { label: string };
export function ScrollArea({ className, testId, children, label, ...props }: ScrollAreaProps) {
  return <BaseScrollArea.Root {...props} data-slot="scroll-area" data-testid={testId ?? 'yue-scroll-area'} className={cn('relative overflow-hidden', className)}><BaseScrollArea.Viewport tabIndex={0} aria-label={label} role="region" className="size-full rounded-[inherit] outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50"><BaseScrollArea.Content>{children}</BaseScrollArea.Content></BaseScrollArea.Viewport><BaseScrollArea.Scrollbar orientation="vertical" className="flex w-2 p-0.5"><BaseScrollArea.Thumb className="w-full rounded bg-muted-foreground" /></BaseScrollArea.Scrollbar><BaseScrollArea.Scrollbar orientation="horizontal" className="flex h-2 p-0.5"><BaseScrollArea.Thumb className="h-full rounded bg-muted-foreground" /></BaseScrollArea.Scrollbar></BaseScrollArea.Root>;
}
export type ResizableProps = YueProps<ComponentProps<typeof Group>>;
export function Resizable({ className, testId, ...props }: ResizableProps) { return <Group {...props} data-slot="resizable" data-testid={testId ?? 'yue-resizable'} className={cn('min-h-24', className)} />; }
export type ResizablePanelProps = YueProps<ComponentProps<typeof Panel>>;
export function ResizablePanel({ className, testId, ...props }: ResizablePanelProps) { return <Panel {...props} data-slot="resizable-panel" data-testid={testId ?? 'yue-resizable-panel'} className={className} />; }
export type ResizableHandleProps = YueProps<ComponentProps<typeof ResizeSeparator>> & { label: string };
export function ResizableHandle({ className, testId, label, ...props }: ResizableHandleProps) { return <ResizeSeparator {...props} aria-label={label} data-slot="resizable-handle" data-testid={testId ?? 'yue-resizable-handle'} className={cn('w-1 bg-input outline-none aria-[orientation=horizontal]:h-1 aria-[orientation=horizontal]:w-full focus-visible:bg-ring', className)} />; }
export type CardProps = YueProps<ComponentProps<'div'>>;
export function Card({ className, testId, ...props }: CardProps) { return <div {...props} data-slot="card" data-testid={testId ?? 'yue-card'} className={cn('rounded-xl border border-border bg-card text-card-foreground', className)} />; }
export function CardHeader({ className, testId, ...props }: CardProps) { return <div {...props} data-slot="card-header" data-testid={testId ?? 'yue-card-header'} className={cn('flex flex-col gap-2 p-6', className)} />; }
export type CardTitleProps = YueProps<ComponentProps<'h3'>>;
export function CardTitle({ className, testId, ...props }: CardTitleProps) { return <h3 {...props} data-slot="card-title" data-testid={testId ?? 'yue-card-title'} className={cn('text-base font-semibold leading-none', className)} />; }
export type CardDescriptionProps = YueProps<ComponentProps<'p'>>;
export function CardDescription({ className, testId, ...props }: CardDescriptionProps) { return <p {...props} data-slot="card-description" data-testid={testId ?? 'yue-card-description'} className={cn('text-sm text-muted-foreground', className)} />; }
export function CardContent({ className, testId, ...props }: CardProps) { return <div {...props} data-slot="card-content" data-testid={testId ?? 'yue-card-content'} className={cn('p-6 pt-0', className)} />; }
export function CardFooter({ className, testId, ...props }: CardProps) { return <div {...props} data-slot="card-footer" data-testid={testId ?? 'yue-card-footer'} className={cn('flex items-center gap-2 p-6 pt-0', className)} />; }
export interface DescriptionItem {
  key: string; label: ReactNode; children: ReactNode;
  /** 独占一整行（stacked 与 grid 布局），用于申请说明一类的长文本。 */
  span?: 'full';
}
export type DescriptionsProps = YueProps<ComponentProps<'dl'>> & {
  items: readonly DescriptionItem[];
  /** inline：标签与值同行两列；stacked：小号标签在上、值在下，按可用宽度自动分栏；grid：定宽标签在左、值在右，按可用宽度自动分栏（摘要框）；justified：每行标签靠左、值靠右（侧栏信息卡）。 @default 'inline' */
  layout?: 'inline' | 'stacked' | 'grid' | 'justified';
};
export function Descriptions({ className, testId, items, layout = 'inline', ...props }: DescriptionsProps) {
  const prefix = useId();
  const id = (item: DescriptionItem) => `${prefix}-${item.key}`;
  const full = (item: DescriptionItem) => item.span === 'full' && 'col-span-full';
  if (layout === 'stacked') return <dl {...props} data-slot="descriptions" data-layout="stacked" data-testid={testId ?? 'yue-descriptions'} className={cn('m-0 grid grid-cols-[repeat(auto-fit,minmax(min(15rem,100%),1fr))] gap-x-6 gap-y-3 text-sm', className)}>
    {items.map(item => <div key={item.key} className={cn('flex min-w-0 flex-col', full(item))}><dt id={id(item)} className="text-xs leading-4.5 text-muted-foreground">{item.label}</dt><dd aria-labelledby={id(item)} className="m-0 min-w-0 break-words">{item.children}</dd></div>)}</dl>;
  if (layout === 'grid') return <dl {...props} data-slot="descriptions" data-layout="grid" data-testid={testId ?? 'yue-descriptions'} className={cn('m-0 grid grid-cols-[repeat(auto-fit,minmax(min(18.75rem,100%),1fr))] gap-x-8 gap-y-3.5 text-sm', className)}>
    {items.map(item => <div key={item.key} className={cn('flex min-w-0 gap-4', full(item))}><dt id={id(item)} className="w-18 shrink-0 text-muted-foreground">{item.label}</dt><dd aria-labelledby={id(item)} className="m-0 min-w-0 break-words">{item.children}</dd></div>)}</dl>;
  if (layout === 'justified') return <dl {...props} data-slot="descriptions" data-layout="justified" data-testid={testId ?? 'yue-descriptions'} className={cn('m-0 flex flex-col gap-2.5 text-sm', className)}>
    {items.map(item => <div key={item.key} className="flex min-w-0 items-center justify-between gap-3"><dt id={id(item)} className="shrink-0 text-muted-foreground">{item.label}</dt><dd aria-labelledby={id(item)} className="m-0 min-w-0 text-end break-words">{item.children}</dd></div>)}</dl>;
  return <dl {...props} data-slot="descriptions" data-testid={testId ?? 'yue-descriptions'} className={cn('grid grid-cols-[max-content_1fr] gap-x-6 gap-y-3 text-sm', className)}>{items.map(item => <div key={item.key} className="contents"><dt id={id(item)} className="text-muted-foreground">{item.label}</dt><dd aria-labelledby={id(item)}>{item.children}</dd></div>)}</dl>;
}
