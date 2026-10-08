import { Tabs as BaseTabs } from '@base-ui/react/tabs';
import type { TabsListProps, TabsPanelProps, TabsRootProps, TabsTabProps } from '@base-ui/react/tabs';
import { cn } from '../lib/cn';
import type { YueProps } from '../lib/test-id';

// 标签页（Base UI Tabs）。验证门项：方向键在 tab 间漫游（roving tabindex）、Home / End、横竖向、RTL、受控 / 非受控。

export function Tabs({ className, testId, ...props }: YueProps<TabsRootProps>) {
  return <BaseTabs.Root data-slot="tabs" data-testid={testId ?? 'yue-tabs'} className={cn('flex flex-col gap-2 data-[orientation=vertical]:flex-row', className)} {...props} />;
}

/** 默认 activateOnFocus：方向键移动焦点即切换标签（WAI-ARIA 自动激活）；Base UI 的默认值是手动激活，需要时传 activateOnFocus={false}。 */
export function TabsList({ className, children, activateOnFocus = true, testId, ...props }: YueProps<TabsListProps>) {
  return (
    <BaseTabs.List
      activateOnFocus={activateOnFocus}
      data-slot="tabs-list"
      data-testid={testId ?? 'yue-tabs-list'}
      className={cn('relative flex gap-1 border-b border-neutral-200 data-[orientation=vertical]:flex-col data-[orientation=vertical]:border-b-0 data-[orientation=vertical]:border-r', className)}
      {...props}
    >
      {children}
      <BaseTabs.Indicator data-slot="tabs-indicator" className="absolute bottom-0 left-[var(--active-tab-left)] h-0.5 w-[var(--active-tab-width)] bg-neutral-900 transition-all" />
    </BaseTabs.List>
  );
}

export function TabsTab({ className, testId, ...props }: YueProps<TabsTabProps>) {
  return (
    <BaseTabs.Tab
      data-slot="tabs-tab"
      data-testid={testId ?? 'yue-tabs-tab'}
      className={cn(
        'px-3 py-2 text-sm text-neutral-600 outline-none hover:text-neutral-900 focus-visible:ring-2 focus-visible:ring-neutral-900 data-[active]:text-neutral-900 data-[disabled]:opacity-50',
        className,
      )}
      {...props}
    />
  );
}

export function TabsPanel({ className, testId, ...props }: YueProps<TabsPanelProps>) {
  return <BaseTabs.Panel data-slot="tabs-panel" data-testid={testId ?? 'yue-tabs-panel'} className={cn('p-2 text-sm outline-none', className)} {...props} />;
}
