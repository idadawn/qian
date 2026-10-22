import { Input as BaseInput } from '@base-ui/react/input';
import { Field as BaseField } from '@base-ui/react/field';
import { NumberField } from '@base-ui/react/number-field';
import { OTPField } from '@base-ui/react/otp-field';
import { useId, useState, type ComponentProps } from 'react';
import { cn } from '../lib/cn';
import { control } from '../lib/styles';
import type { YueProps } from '../lib/test-id';
import { IconButton } from './button';
import { Icon } from './icon';

export type InputProps = YueProps<ComponentProps<typeof BaseInput>>;
export function Input({ className, testId, ...props }: InputProps) { return <BaseInput {...props} data-slot="input" data-testid={testId ?? 'yue-input'} className={cn(control, 'h-9 w-full min-w-0 px-3 text-sm', className)} />; }
export type TextareaProps = YueProps<ComponentProps<typeof BaseField.Control>> & { rows?: number };
export function Textarea({ className, testId, rows = 3, ...props }: TextareaProps) { return <BaseField.Control {...props} render={<textarea rows={rows} />} data-slot="textarea" data-testid={testId ?? 'yue-textarea'} className={cn(control, 'min-h-20 w-full resize-y px-3 py-2 text-sm', className)} />; }
export type InputPasswordProps = Omit<InputProps, 'type'> & { showLabel: string; hideLabel: string };
export function InputPassword({ showLabel, hideLabel, className, testId, disabled, ...props }: InputPasswordProps) {
  const [visible, setVisible] = useState(false);
  return <div data-slot="input-password" data-testid={testId ?? 'yue-input-password'} className={cn('relative flex items-center', className)}><Input {...props} className="pr-10" disabled={disabled} type={visible ? 'text' : 'password'} /><IconButton type="button" label={visible ? hideLabel : showLabel} variant="ghost" className="absolute right-1 size-7" disabled={disabled} aria-pressed={visible} onClick={() => setVisible(value => !value)}><Icon name={visible ? 'eyeOff' : 'eye'} /></IconButton></div>;
}
export type InputNumberProps = YueProps<ComponentProps<typeof NumberField.Root>> & { label: string; decreaseLabel: string; increaseLabel: string };
export function InputNumber({ label, decreaseLabel, increaseLabel, className, testId, ...props }: InputNumberProps) {
  return <NumberField.Root {...props} data-slot="input-number" data-testid={testId ?? 'yue-input-number'} className={className}><NumberField.Group className="flex items-center"><NumberField.Decrement aria-label={decreaseLabel} className={cn(control, 'size-9 rounded-r-none')}><Icon name="minus" /></NumberField.Decrement><NumberField.Input aria-label={label} data-slot="input-number-input" data-testid="yue-input-number-input" className={cn(control, 'h-9 w-24 rounded-none px-2 text-center')} /><NumberField.Increment aria-label={increaseLabel} className={cn(control, 'size-9 rounded-l-none')}>+</NumberField.Increment></NumberField.Group></NumberField.Root>;
}
export type PinInputProps = Omit<YueProps<ComponentProps<typeof OTPField.Root>>, 'children'> & { label: string; getSlotLabel: (index: number) => string };
export function PinInput({ className, testId, length, label, getSlotLabel, id: suppliedId, ...props }: PinInputProps) {
  const generatedId = useId();
  const id = suppliedId ?? generatedId;
  if (!Number.isInteger(length) || length < 1 || length > 32) throw new Error('length 必须为 1–32 的整数。');
  return <><label htmlFor={id} className="sr-only">{label}</label><OTPField.Root {...props} id={id} length={length} data-slot="pin-input" data-testid={testId ?? 'yue-pin-input'} className={cn('flex gap-2', className)}>{Array.from({ length }, (_, index) => <OTPField.Input key={index} aria-label={index === 0 ? undefined : getSlotLabel(index)} data-slot="pin-input-slot" data-testid={`${testId ?? 'yue-pin-input'}-${index + 1}`} className={cn(control, 'size-10 text-center')} />)}</OTPField.Root></>;
}
export type LabelProps = YueProps<ComponentProps<'label'>>;
export function Label({ className, testId, ...props }: LabelProps) { return <label {...props} data-slot="label" data-testid={testId ?? 'yue-label'} className={cn('flex items-center gap-2 text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-50', className)} />; }
