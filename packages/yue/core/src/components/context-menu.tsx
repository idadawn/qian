import { ContextMenu as Base } from '@base-ui/react/context-menu';
import type { ComponentProps } from 'react';
import { cn } from '../lib/cn';
import { menuItem, surface } from '../lib/styles';
import type { YueProps } from '../lib/test-id';

export type ContextMenuProps = ComponentProps<typeof Base.Root>;
export function ContextMenu(props: ContextMenuProps) { return <Base.Root {...props} />; }
export type ContextMenuTriggerProps = YueProps<ComponentProps<typeof Base.Trigger>>;
export function ContextMenuTrigger({ className, testId, tabIndex = 0, onKeyDown, ...props }: ContextMenuTriggerProps) { return <Base.Trigger {...props} tabIndex={tabIndex} onKeyDown={event => {
    onKeyDown?.(event);
    if (!event.defaultPrevented && (event.key === 'ContextMenu' || (event.shiftKey && event.key === 'F10'))) {
      event.preventDefault();
      const bounds = event.currentTarget.getBoundingClientRect();
      event.currentTarget.dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, cancelable: true, button: 2, clientX: bounds.left + bounds.width / 2, clientY: bounds.top + bounds.height / 2 }));
    }
  }} data-slot="context-menu-trigger" data-testid={testId ?? 'yue-context-menu-trigger'} className={cn('outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50', className)} />; }
export type ContextMenuContentProps = YueProps<ComponentProps<typeof Base.Popup>>;
export function ContextMenuContent({ className, testId, ...props }: ContextMenuContentProps) { return <Base.Portal><Base.Positioner className="z-50"><Base.Popup {...props} data-slot="context-menu-content" data-testid={testId ?? 'yue-context-menu-content'} className={cn(surface, 'min-w-40 p-1', className)} /></Base.Positioner></Base.Portal>; }
export type ContextMenuItemProps = YueProps<ComponentProps<typeof Base.Item>>;
export function ContextMenuItem({ className, testId, ...props }: ContextMenuItemProps) { return <Base.Item {...props} data-slot="context-menu-item" data-testid={testId ?? 'yue-context-menu-item'} className={cn(menuItem, className)} />; }
export type ContextMenuSeparatorProps = YueProps<ComponentProps<typeof Base.Separator>>;
export function ContextMenuSeparator({ className, testId, ...props }: ContextMenuSeparatorProps) { return <Base.Separator {...props} data-slot="context-menu-separator" data-testid={testId ?? 'yue-context-menu-separator'} className={cn('my-1 h-px bg-border', className)} />; }
