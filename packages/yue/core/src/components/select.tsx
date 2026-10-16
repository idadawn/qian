import { Select as BaseSelect } from '@base-ui/react/select';
import type { SelectItemProps, SelectPopupProps, SelectPositionerProps, SelectRootProps, SelectTriggerProps } from '@base-ui/react/select';
import type { ReactNode } from 'react';
import { cn } from '../lib/cn';
import type { YueProps } from '../lib/test-id';

// 单选下拉（Base UI Select）。验证门项：键盘打开 / 选择、类型提前、受控 / 非受控、ARIA listbox。

export type SelectProps<Value, Multiple extends boolean | undefined = false> = SelectRootProps<Value, Multiple>;
export function Select<Value, Multiple extends boolean | undefined = false>(props: SelectProps<Value, Multiple>) {
  return <BaseSelect.Root<Value, Multiple> {...props} />;
}

export interface SelectTriggerType extends YueProps<SelectTriggerProps> {
  /** 没有值时显示的占位文字。 */
  placeholder?: ReactNode;
}
export function SelectTrigger({ className, children, placeholder, testId, ...props }: SelectTriggerType) {
  return (
    <BaseSelect.Trigger {...props}
      data-slot="select-trigger"
      data-testid={testId ?? 'yue-select-trigger'}
      className={cn(
        'inline-flex h-9 min-w-40 items-center justify-between gap-2 rounded-md border border-[var(--input)] bg-[var(--background)] px-3 text-sm text-[var(--foreground)] outline-none focus-visible:ring-2 focus-visible:ring-[var(--ring)] data-[placeholder]:text-[var(--muted-foreground)]',
        className,
      )}
    >
      <BaseSelect.Value data-slot="select-value" data-testid="yue-select-value" placeholder={placeholder}>
        {children}
      </BaseSelect.Value>
      <BaseSelect.Icon data-slot="select-icon" data-testid="yue-select-icon" className="text-[var(--muted-foreground)]" aria-hidden>
        ▾
      </BaseSelect.Icon>
    </BaseSelect.Trigger>
  );
}

export interface SelectContentProps extends YueProps<SelectPopupProps> {
  side?: SelectPositionerProps['side'];
  align?: SelectPositionerProps['align'];
  sideOffset?: number;
}
export function SelectContent({ className, children, side, align, sideOffset = 4, testId, ...props }: SelectContentProps) {
  return (
    <BaseSelect.Portal>
      <BaseSelect.Positioner side={side} align={align} sideOffset={sideOffset} className="z-50 outline-none">
        <BaseSelect.Popup {...props}
          data-slot="select-content"
          data-testid={testId ?? 'yue-select-content'}
          className={cn('min-w-40 rounded-md border border-[var(--border)] bg-[var(--background)] p-1 text-sm text-[var(--foreground)] shadow-md outline-none', className)}
        >
          <BaseSelect.List data-slot="select-list" data-testid="yue-select-list">{children}</BaseSelect.List>
        </BaseSelect.Popup>
      </BaseSelect.Positioner>
    </BaseSelect.Portal>
  );
}

export type SelectItemType = YueProps<SelectItemProps>;
export function SelectItem({ className, children, testId, ...props }: SelectItemType) {
  return (
    <BaseSelect.Item {...props}
      data-slot="select-item"
      data-testid={testId ?? 'yue-select-item'}
      className={cn(
        'flex cursor-default select-none items-center justify-between rounded-sm px-2 py-1.5 outline-none data-[highlighted]:bg-[var(--accent)] data-[disabled]:opacity-50',
        className,
      )}
    >
      <BaseSelect.ItemText data-slot="select-item-text" data-testid="yue-select-item-text">{children}</BaseSelect.ItemText>
      <BaseSelect.ItemIndicator data-slot="select-item-indicator" data-testid="yue-select-item-indicator" aria-hidden>
        ✓
      </BaseSelect.ItemIndicator>
    </BaseSelect.Item>
  );
}
