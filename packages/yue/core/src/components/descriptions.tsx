import { useId, type ComponentProps, type ReactNode } from 'react';
import { cn } from '../lib/cn';
import type { YueProps } from '../lib/test-id';

// 描述列表单独成模块：不和 ScrollArea、Resizable 共用一个模块，外壳首屏只引 Descriptions 时不会把 Base UI 滚动区与拖拽分栏一起打进来（qian-shell 首屏 250 KB 预算）。
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
