import { Field as BaseField } from '@base-ui/react/field';
import type { ComponentProps } from 'react';
import { cn } from '../lib/cn';
import type { YueProps } from '../lib/test-id';
import { control } from '../lib/styles';

export type FieldProps = YueProps<ComponentProps<typeof BaseField.Root>>;
export function Field({ className, testId, ...props }: FieldProps) {
  return <BaseField.Root {...props} data-slot="field" data-testid={testId ?? 'yue-field'} className={cn('flex flex-col gap-2', className)} />;
}

export type FieldLabelProps = YueProps<ComponentProps<typeof BaseField.Label>>;
export function FieldLabel({ className, testId, ...props }: FieldLabelProps) {
  return <BaseField.Label {...props} data-slot="field-label" data-testid={testId ?? 'yue-field-label'} className={cn('text-sm font-medium leading-none data-[disabled]:opacity-50', className)} />;
}

export type FieldDescriptionProps = YueProps<ComponentProps<typeof BaseField.Description>>;
export function FieldDescription({ className, testId, ...props }: FieldDescriptionProps) {
  return <BaseField.Description {...props} data-slot="field-description" data-testid={testId ?? 'yue-field-description'} className={cn('text-sm text-muted-foreground', className)} />;
}

export type FieldErrorProps = YueProps<ComponentProps<typeof BaseField.Error>>;
export function FieldError({ className, testId, ...props }: FieldErrorProps) {
  return <BaseField.Error {...props} data-slot="field-error" data-testid={testId ?? 'yue-field-error'} className={cn('text-sm text-destructive', className)} />;
}

export type FieldControlProps = YueProps<ComponentProps<typeof BaseField.Control>>;
export function FieldControl({ className, testId, ...props }: FieldControlProps) {
  return <BaseField.Control {...props} data-slot="field-control" data-testid={testId ?? 'yue-field-control'} className={cn(control + ' h-9 px-3', className)} />;
}

export type FieldItemProps = YueProps<ComponentProps<typeof BaseField.Item>>;
export function FieldItem({ className, testId, ...props }: FieldItemProps) {
  return <BaseField.Item {...props} data-slot="field-item" data-testid={testId ?? 'yue-field-item'} className={cn('flex items-center gap-2', className)} />;
}
