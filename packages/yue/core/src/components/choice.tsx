import { Checkbox as BaseCheckbox } from '@base-ui/react/checkbox';
import { Radio as BaseRadio } from '@base-ui/react/radio';
import { RadioGroup as BaseRadioGroup } from '@base-ui/react/radio-group';
import { Switch as BaseSwitch } from '@base-ui/react/switch';
import { Toggle as BaseToggle } from '@base-ui/react/toggle';
import type { ComponentProps } from 'react';
import { cn } from '../lib/cn';
import { button, control } from '../lib/styles';
import type { YueProps } from '../lib/test-id';
import { Icon } from './icon';

export type CheckboxProps = YueProps<ComponentProps<typeof BaseCheckbox.Root>>;
export function Checkbox({ className, testId, indeterminate, ...props }: CheckboxProps) {
  return <BaseCheckbox.Root {...props} indeterminate={indeterminate} data-slot="checkbox" data-testid={testId ?? 'yue-checkbox'} className={cn(control, 'inline-flex size-5 items-center justify-center data-[checked]:bg-[var(--primary)] data-[checked]:text-[var(--primary-foreground)]', className)}><BaseCheckbox.Indicator><Icon name={indeterminate ? 'minus' : 'check'} /></BaseCheckbox.Indicator></BaseCheckbox.Root>;
}
export type RadioGroupProps = YueProps<ComponentProps<typeof BaseRadioGroup>>;
export function RadioGroup({ className, testId, ...props }: RadioGroupProps) { return <BaseRadioGroup {...props} data-slot="radio-group" data-testid={testId ?? 'yue-radio-group'} className={cn('flex flex-col gap-2', className)} />; }
export type RadioProps = YueProps<ComponentProps<typeof BaseRadio.Root>>;
export function Radio({ className, testId, ...props }: RadioProps) { return <BaseRadio.Root {...props} data-slot="radio" data-testid={testId ?? 'yue-radio'} className={cn(control, 'inline-flex size-5 items-center justify-center rounded-full', className)}><BaseRadio.Indicator className="size-2.5 rounded-full bg-[var(--primary)]" /></BaseRadio.Root>; }
export type SwitchProps = YueProps<ComponentProps<typeof BaseSwitch.Root>>;
export function Switch({ className, testId, ...props }: SwitchProps) { return <BaseSwitch.Root {...props} data-slot="switch" data-testid={testId ?? 'yue-switch'} className={cn(control, 'inline-flex h-6 w-11 items-center rounded-full bg-[var(--muted)] p-0.5 data-[checked]:bg-[var(--primary)]', className)}><BaseSwitch.Thumb className="size-4 rounded-full bg-[var(--background)] transition-transform data-[checked]:translate-x-5 rtl:data-[checked]:-translate-x-5 motion-reduce:transition-none" /></BaseSwitch.Root>; }
export type ToggleProps = YueProps<ComponentProps<typeof BaseToggle>>;
export function Toggle({ className, testId, ...props }: ToggleProps) { return <BaseToggle {...props} data-slot="toggle" data-testid={testId ?? 'yue-toggle'} className={cn(button, 'border border-[var(--border)] data-[pressed]:bg-[var(--accent)] data-[pressed]:text-[var(--accent-foreground)]', className)} />; }
export type SegmentedProps = RadioGroupProps;
export function Segmented({ className, testId, ...props }: SegmentedProps) { return <BaseRadioGroup {...props} data-slot="segmented" data-testid={testId ?? 'yue-segmented'} className={cn('inline-flex gap-1 rounded-lg bg-[var(--muted)] p-1', className)} />; }
export type SegmentedItemProps = RadioProps;
export function SegmentedItem({ className, testId, ...props }: SegmentedItemProps) { return <BaseRadio.Root {...props} data-slot="segmented-item" data-testid={testId ?? 'yue-segmented-item'} className={cn(button, 'h-8 data-[checked]:bg-[var(--background)] data-[checked]:shadow-sm', className)} />; }
