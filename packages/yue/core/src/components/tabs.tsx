import { Tabs as BaseTabs } from '@base-ui/react/tabs';
import type { TabsListProps, TabsPanelProps, TabsRootProps, TabsTabProps } from '@base-ui/react/tabs';
import { cn } from '../lib/cn';
import type { YueProps } from '../lib/test-id';

// 标签页（Base UI Tabs）。验证门项：方向键在 tab 间漫游（roving tabindex）、Home / End、横竖向、RTL、受控 / 非受控。

export function Tabs({ className, testId, ...props }: YueProps<TabsRootProps>) {
  return <BaseTabs.Root {...props} data-slot="tabs" data-testid={testId ?? 'yue-tabs'} className={cn('flex flex-col gap-2 data-[orientation=vertical]:flex-row', className)} />;
}

/** 默认 activateOnFocus：方向键移动焦点即切换标签（WAI-ARIA 自动激活）；Base UI 的默认值是手动激活，需要时传 activateOnFocus={false}。 */
export function TabsList({ className, children, activateOnFocus = true, testId, ...props }: YueProps<TabsListProps>) {
  return (
    <BaseTabs.List
      activateOnFocus={activateOnFocus}
      data-slot="tabs-list"
      data-testid={testId ?? 'yue-tabs-list'}
      className={cn('relative inline-flex w-fit items-center gap-1 rounded-lg bg-muted p-1 text-muted-foreground data-[orientation=vertical]:flex-col', className)}
      {...props}
    >
      {children}
      <BaseTabs.Indicator data-slot="tabs-indicator" data-testid="yue-tabs-indicator" className="pointer-events-none absolute inset-0 rounded-lg opacity-0" />
    </BaseTabs.List>
  );
}

export function TabsTab({ className, testId, ...props }: YueProps<TabsTabProps>) {
  return (
    <BaseTabs.Tab {...props}
      data-slot="tabs-tab"
      data-testid={testId ?? 'yue-tabs-tab'}
      className={cn(
        'inline-flex h-8 items-center justify-center rounded-md border border-transparent px-3 text-sm font-medium text-muted-foreground outline-none transition-all duration-normal hover:text-foreground focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 data-[active]:bg-background data-[active]:text-foreground data-[active]:shadow-xs data-[disabled]:pointer-events-none data-[disabled]:opacity-50 motion-reduce:transition-none',
        className,
      )}
    />
  );
}

export function TabsPanel({ className, testId, ...props }: YueProps<TabsPanelProps>) {
  return <BaseTabs.Panel {...props} data-slot="tabs-panel" data-testid={testId ?? 'yue-tabs-panel'} className={cn('flex-1 text-sm outline-none', className)} />;
}
