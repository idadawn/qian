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
import { button, surface, menuItem } from '../lib/styles';
import type { YueProps } from '../lib/test-id';

// 下拉菜单（Base UI Menu）。验证门项：键盘导航、焦点管理、ARIA、子菜单（docs/prd/specs/yue.md §4.4）。

export type MenuProps = MenuRootProps;
export function Menu(props: MenuProps) {
  return <BaseMenu.Root {...props} />;
}

export type MenuTriggerType = YueProps<MenuTriggerProps>;
export function MenuTrigger({ className, testId, ...props }: MenuTriggerType) {
  return (
    <BaseMenu.Trigger {...props}
      data-slot="menu-trigger"
      data-testid={testId ?? 'yue-menu-trigger'}
      className={cn(
        button, 'border-border bg-background text-foreground shadow-xs hover:bg-accent data-[popup-open]:bg-accent',
        className,
      )}
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
        <BaseMenu.Popup {...props}
          data-slot="menu-content"
          data-testid={testId ?? 'yue-menu-content'}
          className={cn(
            surface, 'min-w-40 max-h-(--available-height) w-(--anchor-width) overflow-y-auto p-1',
            className,
          )}
        />
      </BaseMenu.Positioner>
    </BaseMenu.Portal>
  );
}

export type MenuItemType = YueProps<MenuItemProps>;
export function MenuItem({ className, testId, ...props }: MenuItemType) {
  return (
    <BaseMenu.Item {...props}
      data-slot="menu-item"
      data-testid={testId ?? 'yue-menu-item'}
      className={cn(
        menuItem,
        className,
      )}
    />
  );
}

export type MenuSeparatorProps = YueProps<ComponentProps<typeof BaseMenu.Separator>>;
export function MenuSeparator({ className, testId, ...props }: MenuSeparatorProps) {
  return (
    <BaseMenu.Separator {...props}
      data-slot="menu-separator"
      data-testid={testId ?? 'yue-menu-separator'}
      className={cn('my-1 h-px bg-border', className)}
    />
  );
}

export function MenuGroup({ className, testId, ...props }: YueProps<MenuGroupProps>) {
  return <BaseMenu.Group {...props} data-slot="menu-group" data-testid={testId ?? 'yue-menu-group'} className={className} />;
}

export function MenuGroupLabel({ className, testId, ...props }: YueProps<MenuGroupLabelProps>) {
  return (
    <BaseMenu.GroupLabel {...props}
      data-slot="menu-group-label"
      data-testid={testId ?? 'yue-menu-group-label'}
      className={cn('px-2 py-1.5 text-xs text-muted-foreground', className)}
    />
  );
}

export type MenuSubProps = MenuSubmenuRootProps;
export function MenuSub(props: MenuSubProps) {
  return <BaseMenu.SubmenuRoot {...props} />;
}

export function MenuSubTrigger({ className, testId, ...props }: YueProps<MenuSubmenuTriggerProps>) {
  return (
    <BaseMenu.SubmenuTrigger {...props}
      data-slot="menu-sub-trigger"
      data-testid={testId ?? 'yue-menu-sub-trigger'}
      className={cn(
        menuItem, 'justify-between data-[popup-open]:bg-accent',
        className,
      )}
    />
  );
}
