import { Menu as BaseMenu } from '@base-ui/react/menu';
import type {
  MenuGroupLabelProps,
  MenuGroupProps,
  MenuItemProps,
  MenuPopupProps,
  MenuPositionerProps,
  MenuRootProps,
  MenuSubmenuRootProps,
  MenuSubmenuTriggerProps,
  MenuTriggerProps,
} from '@base-ui/react/menu';
import type { ComponentProps } from 'react';
import { cn } from '../lib/cn';
import type { YueProps } from '../lib/test-id';

// 下拉菜单（Base UI Menu）。验证门项：键盘导航、焦点管理、ARIA、子菜单（docs/prd/specs/yue.md §4.4）。

export type MenuProps = MenuRootProps;
export function Menu(props: MenuProps) {
  return <BaseMenu.Root {...props} />;
}

export type MenuTriggerType = YueProps<MenuTriggerProps>;
export function MenuTrigger({ className, testId, ...props }: MenuTriggerType) {
  return (
    <BaseMenu.Trigger
      data-slot="menu-trigger"
      data-testid={testId ?? 'yue-menu-trigger'}
      className={cn(
        'inline-flex h-9 items-center gap-2 rounded-md border border-neutral-300 bg-white px-3 text-sm text-neutral-900 outline-none hover:bg-neutral-100 focus-visible:ring-2 focus-visible:ring-neutral-900 data-[popup-open]:bg-neutral-100',
        className,
      )}
      {...props}
    />
  );
}

export interface MenuContentProps extends YueProps<MenuPopupProps> {
  side?: MenuPositionerProps['side'];
  align?: MenuPositionerProps['align'];
  sideOffset?: number;
  alignOffset?: number;
}
/** Portal + Positioner + Popup 三件套；子菜单也用它（Base UI 在嵌套上下文里自动选侧边）。 */
export function MenuContent({ className, side, align, sideOffset = 4, alignOffset, testId, ...props }: MenuContentProps) {
  return (
    <BaseMenu.Portal>
      <BaseMenu.Positioner side={side} align={align} sideOffset={sideOffset} alignOffset={alignOffset} className="z-50 outline-none">
        <BaseMenu.Popup
          data-slot="menu-content"
          data-testid={testId ?? 'yue-menu-content'}
          className={cn(
            'min-w-40 rounded-md border border-neutral-200 bg-white p-1 text-sm text-neutral-900 shadow-md outline-none',
            className,
          )}
          {...props}
        />
      </BaseMenu.Positioner>
    </BaseMenu.Portal>
  );
}

export type MenuItemType = YueProps<MenuItemProps>;
export function MenuItem({ className, testId, ...props }: MenuItemType) {
  return (
    <BaseMenu.Item
      data-slot="menu-item"
      data-testid={testId ?? 'yue-menu-item'}
      className={cn(
        'flex cursor-default select-none items-center rounded-sm px-2 py-1.5 outline-none data-[highlighted]:bg-neutral-100 data-[disabled]:opacity-50',
        className,
      )}
      {...props}
    />
  );
}

export type MenuSeparatorProps = YueProps<ComponentProps<typeof BaseMenu.Separator>>;
export function MenuSeparator({ className, testId, ...props }: MenuSeparatorProps) {
  return (
    <BaseMenu.Separator
      data-slot="menu-separator"
      data-testid={testId ?? 'yue-menu-separator'}
      className={cn('my-1 h-px bg-neutral-200', className)}
      {...props}
    />
  );
}

export function MenuGroup({ className, testId, ...props }: YueProps<MenuGroupProps>) {
  return <BaseMenu.Group data-slot="menu-group" data-testid={testId ?? 'yue-menu-group'} className={className} {...props} />;
}

export function MenuGroupLabel({ className, testId, ...props }: YueProps<MenuGroupLabelProps>) {
  return (
    <BaseMenu.GroupLabel
      data-slot="menu-group-label"
      data-testid={testId ?? 'yue-menu-group-label'}
      className={cn('px-2 py-1.5 text-xs text-neutral-500', className)}
      {...props}
    />
  );
}

export type MenuSubProps = MenuSubmenuRootProps;
export function MenuSub(props: MenuSubProps) {
  return <BaseMenu.SubmenuRoot {...props} />;
}

export function MenuSubTrigger({ className, testId, ...props }: YueProps<MenuSubmenuTriggerProps>) {
  return (
    <BaseMenu.SubmenuTrigger
      data-slot="menu-sub-trigger"
      data-testid={testId ?? 'yue-menu-sub-trigger'}
      className={cn(
        'flex cursor-default select-none items-center justify-between rounded-sm px-2 py-1.5 outline-none data-[highlighted]:bg-neutral-100 data-[popup-open]:bg-neutral-100',
        className,
      )}
      {...props}
    />
  );
}
