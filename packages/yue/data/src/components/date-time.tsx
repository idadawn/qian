import { useId } from 'react';
import { Input, Label } from '@idadawn/yue-core';
import type { PlatformI18n } from '@idadawn/yue-sdk';
export interface DateTimeProps {
  value?: string;
  onChange?: (value: string) => void;
  label: string;
  min?: string;
  max?: string;
  disabled?: boolean;
  className?: string;
  testId?: string;
}
function DateTimeInput({ kind, value = '', onChange, label, min, max, disabled, className, testId, invalid, describedBy }: DateTimeProps & { kind: 'date' | 'time'; invalid?: boolean; describedBy?: string }) {
  const id = useId();
  return <div className={className} data-slot={`${kind}-picker`} data-testid={testId ?? `yue-${kind}-picker`}><Label htmlFor={id}>{label}</Label><Input id={id} type={kind} value={value} min={min} max={max} disabled={disabled} aria-invalid={invalid} aria-describedby={describedBy} onChange={(event) => onChange?.(event.target.value)} /></div>;
}
export function DatePicker(props: DateTimeProps) { return <DateTimeInput {...props} kind="date" />; }
export function TimePicker(props: DateTimeProps) { return <DateTimeInput {...props} kind="time" />; }
export interface DateTimeRangeProps extends Omit<DateTimeProps, 'value' | 'onChange'> {
  value?: readonly [string, string];
  onChange?: (value: [string, string]) => void;
  i18n: PlatformI18n;
}
function DateTimeRange({ kind, value = ['', ''], onChange, label, i18n, min, max, disabled, className, testId }: DateTimeRangeProps & { kind: 'date' | 'time' }) {
  const invalid = !!value[0] && !!value[1] && value[1] < value[0];
  const errorId = useId();
  return <fieldset className={className ?? 'grid gap-2'} data-slot={`${kind}-range`} data-testid={testId ?? `yue-${kind}-range`} aria-describedby={invalid ? errorId : undefined}>
    <legend>{label}</legend><div className="flex gap-3"><DateTimeInput kind={kind} value={value[0]} onChange={(start) => onChange?.([start, value[1]])} label={i18n.t('data.start')} min={min} max={max} disabled={disabled} />
      <DateTimeInput kind={kind} value={value[1]} onChange={(end) => onChange?.([value[0], end])} label={i18n.t('data.end')} min={value[0] || min} max={max} disabled={disabled} invalid={invalid} describedBy={invalid ? errorId : undefined} /></div>
    {invalid && <p role="alert" id={errorId}>{i18n.t('data.rangeInvalid')}</p>}
  </fieldset>;
}
export function DateRange(props: DateTimeRangeProps) { return <DateTimeRange {...props} kind="date" />; }
export function TimeRange(props: DateTimeRangeProps) { return <DateTimeRange {...props} kind="time" />; }
