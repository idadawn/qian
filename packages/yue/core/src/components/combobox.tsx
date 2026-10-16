import { Combobox as BaseCombobox } from '@base-ui/react/combobox';
import type {
  ComboboxEmptyProps,
  ComboboxInputProps,
  ComboboxItemProps,
  ComboboxListProps,
  ComboboxPopupProps,
  ComboboxPositionerProps,
  ComboboxRootProps,
} from '@base-ui/react/combobox';
import type { ComponentProps } from 'react';
import { cn } from '../lib/cn';
import { control } from '../lib/styles';
import type { YueProps } from '../lib/test-id';

// 可输入筛选的下拉（Base UI Combobox）。验证门项：输入过滤、方向键高亮、Enter 选中、Escape 关闭、空态、ARIA combobox / listbox。

export type ComboboxProps<Value, Multiple extends boolean | undefined = false> = ComboboxRootProps<Value, Multiple>;
export function Combobox<Value, Multiple extends boolean | undefined = false>(props: ComboboxProps<Value, Multiple>) {
  return <BaseCombobox.Root<Value, Multiple> {...props} />;
}

export function ComboboxInput({ className, testId, ...props }: YueProps<ComboboxInputProps>) {
  return (
    <BaseCombobox.Input {...props}
      data-slot="combobox-input"
      data-testid={testId ?? 'yue-combobox-input'}
      className={cn(
        'h-9 min-w-40 rounded-md border border-[var(--input)] bg-[var(--background)] px-3 text-sm text-[var(--foreground)] outline-none placeholder:text-[var(--muted-foreground)] focus-visible:ring-2 focus-visible:ring-[var(--ring)]',
        className,
      )}
    />
  );
}

export interface ComboboxContentProps extends YueProps<ComboboxPopupProps> {
  side?: ComboboxPositionerProps['side'];
  align?: ComboboxPositionerProps['align'];
  sideOffset?: number;
}
export function ComboboxContent({ className, side, align, sideOffset = 4, testId, ...props }: ComboboxContentProps) {
  return (
    <BaseCombobox.Portal>
      <BaseCombobox.Positioner side={side} align={align} sideOffset={sideOffset} className="z-50 outline-none">
        <BaseCombobox.Popup {...props}
          data-slot="combobox-content"
          data-testid={testId ?? 'yue-combobox-content'}
          className={cn('min-w-40 rounded-md border border-[var(--border)] bg-[var(--background)] p-1 text-sm text-[var(--foreground)] shadow-md outline-none', className)}
        />
      </BaseCombobox.Positioner>
    </BaseCombobox.Portal>
  );
}

export function ComboboxList({ className, testId, ...props }: YueProps<ComboboxListProps>) {
  return <BaseCombobox.List {...props} data-slot="combobox-list" data-testid={testId ?? 'yue-combobox-list'} className={className} />;
}

export function ComboboxEmpty({ className, testId, ...props }: YueProps<ComboboxEmptyProps>) {
  return (
    <BaseCombobox.Empty {...props}
      data-slot="combobox-empty"
      data-testid={testId ?? 'yue-combobox-empty'}
      className={cn('px-2 py-1.5 text-[var(--muted-foreground)] empty:hidden', className)}
    />
  );
}

export function ComboboxItem({ className, testId, ...props }: YueProps<ComboboxItemProps>) {
  return (
    <BaseCombobox.Item {...props}
      data-slot="combobox-item"
      data-testid={testId ?? 'yue-combobox-item'}
      className={cn(
        'flex cursor-default select-none items-center rounded-sm px-2 py-1.5 outline-none data-[highlighted]:bg-[var(--accent)] data-[selected]:font-medium data-[disabled]:opacity-50',
        className,
      )}
    />
  );
}

/** 推荐的可搜索选择器入口：输入框放在 Content 内，避免弹层外输入形态隐藏页面其余焦点目标。 */
export type ComboboxTriggerProps = YueProps<ComponentProps<typeof BaseCombobox.Trigger>>;
export function ComboboxTrigger({ className, testId, ...props }: ComboboxTriggerProps) {
  return <BaseCombobox.Trigger {...props} data-slot="combobox-trigger" data-testid={testId ?? 'yue-combobox-trigger'} className={cn(control, 'inline-flex h-9 min-w-40 items-center justify-between px-3 text-sm', className)} />;
}
export type ComboboxValueProps = ComponentProps<typeof BaseCombobox.Value>;
export function ComboboxValue(props: ComboboxValueProps) {
  return <span data-slot="combobox-value" data-testid="yue-combobox-value"><BaseCombobox.Value {...props} /></span>;
}
