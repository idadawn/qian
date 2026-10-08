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
import { cn } from '../lib/cn';
import type { YueProps } from '../lib/test-id';

// 可输入筛选的下拉（Base UI Combobox）。验证门项：输入过滤、方向键高亮、Enter 选中、Escape 关闭、空态、ARIA combobox / listbox。

export type ComboboxProps<Value, Multiple extends boolean | undefined = false> = ComboboxRootProps<Value, Multiple>;
export function Combobox<Value, Multiple extends boolean | undefined = false>(props: ComboboxProps<Value, Multiple>) {
  return <BaseCombobox.Root<Value, Multiple> {...props} />;
}

export function ComboboxInput({ className, testId, ...props }: YueProps<ComboboxInputProps>) {
  return (
    <BaseCombobox.Input
      data-slot="combobox-input"
      data-testid={testId ?? 'yue-combobox-input'}
      className={cn(
        'h-9 min-w-40 rounded-md border border-neutral-300 bg-white px-3 text-sm text-neutral-900 outline-none placeholder:text-neutral-500 focus-visible:ring-2 focus-visible:ring-neutral-900',
        className,
      )}
      {...props}
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
        <BaseCombobox.Popup
          data-slot="combobox-content"
          data-testid={testId ?? 'yue-combobox-content'}
          className={cn('min-w-40 rounded-md border border-neutral-200 bg-white p-1 text-sm text-neutral-900 shadow-md outline-none', className)}
          {...props}
        />
      </BaseCombobox.Positioner>
    </BaseCombobox.Portal>
  );
}

export function ComboboxList({ className, testId, ...props }: YueProps<ComboboxListProps>) {
  return <BaseCombobox.List data-slot="combobox-list" data-testid={testId ?? 'yue-combobox-list'} className={className} {...props} />;
}

export function ComboboxEmpty({ className, testId, ...props }: YueProps<ComboboxEmptyProps>) {
  return (
    <BaseCombobox.Empty
      data-slot="combobox-empty"
      data-testid={testId ?? 'yue-combobox-empty'}
      className={cn('px-2 py-1.5 text-neutral-500 empty:hidden', className)}
      {...props}
    />
  );
}

export function ComboboxItem({ className, testId, ...props }: YueProps<ComboboxItemProps>) {
  return (
    <BaseCombobox.Item
      data-slot="combobox-item"
      data-testid={testId ?? 'yue-combobox-item'}
      className={cn(
        'flex cursor-default select-none items-center rounded-sm px-2 py-1.5 outline-none data-[highlighted]:bg-neutral-100 data-[selected]:font-medium data-[disabled]:opacity-50',
        className,
      )}
      {...props}
    />
  );
}
