import { Tabs as BaseTabs } from '@base-ui/react/tabs';
import type { TabsListProps, TabsPanelProps, TabsRootProps, TabsTabProps } from '@base-ui/react/tabs';
import type { ReactNode } from 'react';
import { cn } from '../lib/cn';
import type { YueProps } from '../lib/test-id';

// 标签页（Base UI Tabs）。验证门项：方向键在 tab 间漫游（roving tabindex）、Home / End、横竖向、RTL、受控 / 非受控。

export function Tabs({ className, testId, ...props }: YueProps<TabsRootProps>) {
  return <BaseTabs.Root {...props} data-slot="tabs" data-testid={testId ?? 'yue-tabs'} className={cn('flex flex-col gap-2 data-[orientation=vertical]:flex-row', className)} />;
}

export type TabsListVariant = 'pill' | 'line';

/**
 * 默认 activateOnFocus：方向键移动焦点即切换标签（WAI-ARIA 自动激活）；Base UI 的默认值是手动激活，需要时传 activateOnFocus={false}。
 * variant="line" 是列表页顶部的下划线页签：铺满容器宽度、底边一条分隔线，当前项下划主色。
 */
export function TabsList({ className, children, activateOnFocus = true, variant = 'pill', testId, ...props }: YueProps<TabsListProps> & { /** 外观。 @default 'pill' */ variant?: TabsListVariant }) {
  return (
    <BaseTabs.List
      activateOnFocus={activateOnFocus}
      data-slot="tabs-list"
      data-variant={variant}
      data-testid={testId ?? 'yue-tabs-list'}
      className={cn('group/tabs relative data-[orientation=vertical]:flex-col',
        variant === 'line' ? 'flex w-full flex-wrap items-end gap-x-6 gap-y-1 border-b border-border text-muted-foreground' : 'inline-flex w-fit items-center gap-1 rounded-lg bg-muted p-1 text-muted-foreground', className)}
      {...props}
    >
      {children}
      <BaseTabs.Indicator data-slot="tabs-indicator" data-testid="yue-tabs-indicator" className="pointer-events-none absolute inset-0 rounded-lg opacity-0" />
    </BaseTabs.List>
  );
}

export function TabsTab({ className, testId, count, children, ...props }: YueProps<TabsTabProps> & { /** 跟在标签文字后的计数，例如列表各状态的条数。 */ count?: ReactNode }) {
  return (
    <BaseTabs.Tab {...props}
      data-slot="tabs-tab"
      data-testid={testId ?? 'yue-tabs-tab'}
      className={cn(
        'inline-flex h-8 items-center justify-center gap-1.5 rounded-md border border-transparent px-3 text-sm font-medium text-muted-foreground outline-none transition-all duration-normal hover:text-foreground focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 data-[active]:bg-background data-[active]:text-foreground data-[active]:shadow-xs data-[disabled]:pointer-events-none data-[disabled]:opacity-50 motion-reduce:transition-none',
        'group-data-[variant=line]/tabs:-mb-px group-data-[variant=line]/tabs:h-11.5 group-data-[variant=line]/tabs:rounded-none group-data-[variant=line]/tabs:border-0 group-data-[variant=line]/tabs:border-b-2 group-data-[variant=line]/tabs:px-0 group-data-[variant=line]/tabs:font-normal',
        'group-data-[variant=line]/tabs:data-[active]:border-primary group-data-[variant=line]/tabs:data-[active]:bg-transparent group-data-[variant=line]/tabs:data-[active]:font-semibold group-data-[variant=line]/tabs:data-[active]:shadow-none group-data-[variant=line]/tabs:focus-visible:ring-inset',
        className,
      )}
    >
      {children}
      {count !== undefined && count !== null && <span data-slot="tabs-count" data-testid={`${testId ?? 'yue-tabs-tab'}-count`} className="text-xs font-normal tabular-nums text-muted-foreground group-data-[variant=line]/tabs:in-data-[active]:text-primary">{count}</span>}
    </BaseTabs.Tab>
  );
}

export function TabsPanel({ className, testId, ...props }: YueProps<TabsPanelProps>) {
  return <BaseTabs.Panel {...props} data-slot="tabs-panel" data-testid={testId ?? 'yue-tabs-panel'} className={cn('flex-1 text-sm outline-none', className)} />;
}
