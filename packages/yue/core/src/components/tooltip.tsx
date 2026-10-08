import { Tooltip as BaseTooltip } from '@base-ui/react/tooltip';
import type { TooltipPopupProps, TooltipPositionerProps, TooltipProviderProps, TooltipRootProps, TooltipTriggerProps } from '@base-ui/react/tooltip';
import { createContext, useContext, useId, useState } from 'react';
import { cn } from '../lib/cn';
import type { YueProps } from '../lib/test-id';

// 工具提示（Base UI Tooltip）。验证门项：聚焦即显示（键盘可达）、Escape 关闭、role=tooltip 与 aria-describedby 关联。
// 验证门发现：Base UI 1.8 的 Tooltip 有意不输出 role="tooltip" 与 aria-describedby（它把提示视为非必要的补充信息）。
// WAI-ARIA tooltip 模式与 PRD §07 7.15.2 的 ARIA 验收都要求这两项，所以在封装层补上：打开期间触发器带 aria-describedby 指向弹层。
// 不需要 patch 包；记录在 ADR-013 状态历史。

interface TooltipLink {
  id: string;
  open: boolean;
}
const TooltipLinkContext = createContext<TooltipLink | null>(null);

export type TooltipProviderType = TooltipProviderProps;
export function TooltipProvider({ delay = 300, ...props }: TooltipProviderType) {
  return <BaseTooltip.Provider delay={delay} {...props} />;
}

export type TooltipProps = TooltipRootProps;
export function Tooltip({ open: openProp, defaultOpen, onOpenChange, ...props }: TooltipProps) {
  const id = useId();
  const [innerOpen, setInnerOpen] = useState(defaultOpen ?? false);
  const open = openProp ?? innerOpen;
  return (
    <TooltipLinkContext value={{ id, open }}>
      <BaseTooltip.Root
        open={openProp}
        defaultOpen={defaultOpen}
        onOpenChange={(next, details) => {
          setInnerOpen(next);
          onOpenChange?.(next, details);
        }}
        {...props}
      />
    </TooltipLinkContext>
  );
}

export function TooltipTrigger({ className, testId, ...props }: YueProps<TooltipTriggerProps>) {
  const link = useContext(TooltipLinkContext);
  return (
    <BaseTooltip.Trigger
      data-slot="tooltip-trigger"
      data-testid={testId ?? 'yue-tooltip-trigger'}
      aria-describedby={link?.open ? link.id : undefined}
      className={cn('inline-flex h-9 items-center rounded-md border border-neutral-300 bg-white px-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-neutral-900', className)}
      {...props}
    />
  );
}

export interface TooltipContentProps extends YueProps<TooltipPopupProps> {
  side?: TooltipPositionerProps['side'];
  align?: TooltipPositionerProps['align'];
  sideOffset?: number;
}
export function TooltipContent({ className, side, align, sideOffset = 6, testId, ...props }: TooltipContentProps) {
  const link = useContext(TooltipLinkContext);
  return (
    <BaseTooltip.Portal>
      <BaseTooltip.Positioner side={side} align={align} sideOffset={sideOffset} className="z-50">
        <BaseTooltip.Popup
          id={link?.id}
          role="tooltip"
          data-slot="tooltip-content"
          data-testid={testId ?? 'yue-tooltip-content'}
          className={cn('rounded-md bg-neutral-900 px-2 py-1 text-xs text-white shadow-md', className)}
          {...props}
        />
      </BaseTooltip.Positioner>
    </BaseTooltip.Portal>
  );
}
