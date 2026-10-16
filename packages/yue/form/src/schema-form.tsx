import { useContext, useEffect, useId, useImperativeHandle, useMemo, useRef, type BaseSyntheticEvent, type ReactNode, type Ref } from 'react';
import { Controller, useForm, useWatch, type FieldErrors, type Resolver } from 'react-hook-form';
import { Button, Checkbox, Field as CoreField, FieldDescription, FieldLabel, Input, Select, SelectContent, SelectItem, SelectTrigger, Textarea } from '@idadawn/yue-core';
import { PlatformError, pointerSegments, type ErrorDetail, type PlatformI18n } from '@idadawn/yue-sdk';
import type { FormSchema, UiNode } from '@idadawn/yue-contracts';
import { OverlayFormContext } from './overlay-form-context';
import { assertFormSchema, effectiveSchema, getValue, maskValue, validateValues, type DataSchema, type FormValues } from './schema';

export interface FormApi {
  getValues(): FormValues;
  setValue(path: string, value: unknown): void;
  reset(values?: FormValues): void;
  validate(): Promise<boolean>;
  submit(): Promise<void>;
  setErrors(detail: ErrorDetail): void;
  clearErrors(): void;
}
export interface SchemaFormProps {
  schema: FormSchema;
  i18n: PlatformI18n;
  initialValues?: FormValues;
  mode?: 'edit' | 'readonly';
  onSubmit?: (values: FormValues, api: FormApi) => void | Promise<void>;
  onError?: (error: unknown) => void;
  apiRef?: Ref<FormApi>;
  className?: string;
  testId?: string;
  submitLabel?: string;
  children?: ReactNode;
}

function addError(errors: FieldErrors<FormValues>, segments: string[], message: string): void {
  let target = errors as Record<string, unknown>;
  for (const key of segments.slice(0, -1)) {
    if (!Object.hasOwn(target, key)) target[key] = Object.create(null) as Record<string, unknown>;
    target = target[key] as Record<string, unknown>;
  }
  target[segments.at(-1) ?? 'root'] = { type: 'schema', message };
}
function fields(data: DataSchema, nodes: UiNode[], prefix = ''): { path: string; schema: DataSchema; ui: UiNode; required: boolean }[] {
  const flatten = (items: UiNode[]): UiNode[] => items.flatMap((node) => node.field ? [node] : flatten(node.children ?? []));
  const layout = flatten(nodes);
  const definitions = data.properties ?? {};
  const ordered = layout.length ? layout.filter((node) => node.field).map((node) => ({ key: node.field!, ui: node })) : Object.keys(definitions).map((key) => ({ key, ui: { field: key } as UiNode }));
  return ordered.flatMap(({ key, ui }) => {
    const schema = definitions[key];
    if (!schema) throw new Error('form.error.field');
    const path = prefix ? `${prefix}.${key}` : key;
    if (schema.type === 'object') return fields(schema, ui.children ?? [], path);
    return [{ path, schema, ui, required: data.required?.includes(key) ?? false }];
  });
}
function defaults(schema: DataSchema): FormValues {
  return Object.fromEntries(Object.entries(schema.properties ?? {}).flatMap(([key, field]) => {
    if (field.default !== undefined) return [[key, field.default]];
    if (field.type === 'object') return [[key, defaults(field)]];
    return [];
  }));
}

/** 编辑、只读与字段错误共享同一份 Schema、RHF 状态和字段目录。 */
export function SchemaForm({ schema, i18n, initialValues, mode = 'edit', onSubmit, onError, apiRef, className, testId = 'yue-schema-form', submitLabel, children }: SchemaFormProps) {
  useMemo(() => assertFormSchema(schema), [schema]);
  const id = useId();
  const overlay = useContext(OverlayFormContext);
  const register = overlay?.register;
  const hasActions = mode === 'edit' && !!onSubmit;
  useEffect(() => { if (hasActions) return register?.(); }, [register, hasActions]);
  const resolver: Resolver<FormValues> = (values) => {
    const errors: FieldErrors<FormValues> = Object.create(null) as FieldErrors<FormValues>;
    for (const error of validateValues(schema, values)) {
      const path = pointerSegments('#' + error.instancePath);
      if (error.keyword === 'required') path.push(String(error.params.missingProperty));
      addError(errors, path, i18n.t(error.keyword === 'required' ? 'common.required' : 'common.invalid'));
    }
    if (Object.keys(errors).length) return { values: {}, errors };
    return { values, errors: {} };
  };
  const form = useForm<FormValues>({ defaultValues: initialValues ?? defaults(schema.data as DataSchema), resolver });
  const values = useWatch({ control: form.control }) as FormValues;
  const effective = effectiveSchema(schema, values);
  const { errors, isSubmitting } = form.formState;
  const pendingFocus = useRef<string | null>(null);
  const { setFocus } = form;
  useEffect(() => {
    if (!isSubmitting && pendingFocus.current) {
      setFocus(pendingFocus.current);
      pendingFocus.current = null;
    }
  }, [errors, isSubmitting, setFocus]);
  const { setValue } = form;
  const assignments = JSON.stringify(effective.assignments);
  useEffect(() => {
    const changes = JSON.parse(assignments) as FormValues;
    for (const [path, value] of Object.entries(changes)) {
      if (JSON.stringify(getValue(values, path)) !== JSON.stringify(value)) setValue(path, value, { shouldValidate: true });
    }
  }, [assignments, setValue, values]);
  const api: FormApi = {
    getValues: form.getValues,
    setValue: (path, value) => form.setValue(path, value, { shouldValidate: true, shouldDirty: true }),
    reset: (next) => form.reset(next ?? initialValues ?? defaults(schema.data as DataSchema)),
    validate: () => form.trigger(),
    submit: () => submit(),
    clearErrors: () => form.clearErrors(),
    setErrors: (detail) => {
      const entries = detail.errors ?? (detail.pointer ? [{ pointer: detail.pointer, message: detail.message }] : []);
      if (!entries.length) form.setError('root', { type: 'server', message: detail.message });
      entries.forEach((error, index) => {
        let segments: string[];
        try { segments = pointerSegments(error.pointer); } catch { segments = []; }
        const path = segments.length ? segments.join('.') : 'root';
        if (index === 0 && segments.length) pendingFocus.current = path;
        form.setError(path, { type: 'server', message: error.message });
      });
    },
  };
  const submit = (event?: BaseSyntheticEvent) => form.handleSubmit(async (data) => {
    try { await onSubmit?.(data, api); }
    catch (error) {
      if (error instanceof PlatformError) api.setErrors(error.detail);
      else form.setError('root', { type: 'server', message: i18n.t('sdk.error.http') });
      onError?.(error);
    }
  })(event);
  useImperativeHandle(apiRef, () => api);
  const definitions = fields(effective.data, schema.ui.layout);

  return <form className={className ?? 'grid gap-4'} data-slot="schema-form" data-testid={testId} onSubmit={submit} noValidate>
    {definitions.map(({ path, schema: fieldSchema, ui, required }) => {
      const state = effective.states[path];
      const access = schema.auth.fields?.[path];
      if (state?.visible === false || (access && !access.includes('read'))) return null;
      const readonly = mode === 'readonly' || (access && !access.includes('write'));
      const horizontal = fieldSchema.type === 'boolean' && !readonly;
      const fieldId = `${id}-${path}`;
      const error = getValue(errors, path) as { message?: string } | undefined;
      const label = i18n.t(ui.labelKey ?? fieldSchema.title ?? path);
      const description = ui.descriptionKey ? i18n.t(ui.descriptionKey) : undefined;
      const describedBy = [error ? `${fieldId}-error` : '', description ? `${fieldId}-description` : ''].filter(Boolean).join(' ') || undefined;
      return <CoreField className={horizontal ? 'grid grid-cols-[1rem_minmax(0,1fr)] items-center gap-x-3 gap-y-2' : undefined} key={path} name={path} invalid={!!error} data-slot="schema-field" testId={`${testId}-field-${path}`}>
        <FieldLabel className={horizontal ? 'col-start-2 row-start-1 leading-5' : undefined} htmlFor={fieldId}>{label}{required ? <span aria-hidden className="text-destructive"> *</span> : null}</FieldLabel>
        <Controller name={path} control={form.control} render={({ field }) => {
          if (readonly) return <output id={fieldId} className="min-h-9 rounded-md bg-muted/40 px-3 py-2 text-sm text-foreground" data-slot="schema-field-readonly" data-testid={`${testId}-readonly-${path}`} aria-describedby={describedBy}>{maskValue(field.value, typeof ui.props?.readonlyMask === 'string' ? ui.props.readonlyMask : 'none')}</output>;
          const common = { id: fieldId, name: field.name, ref: field.ref, onBlur: field.onBlur, disabled: state?.disabled || isSubmitting, 'aria-required': required, 'aria-invalid': !!error, 'aria-describedby': describedBy };
          if (fieldSchema.type === 'boolean') return <Checkbox {...common} className="col-start-1 row-start-1" checked={!!field.value} onCheckedChange={field.onChange} aria-label={label} />;
          if (fieldSchema.enum) return <Select value={field.value ?? null} onValueChange={field.onChange} items={fieldSchema.enum.map((value) => ({ value, label: String(value) }))} disabled={common.disabled}>
            <SelectTrigger {...common} aria-label={label} /><SelectContent>{fieldSchema.enum.map((value) => <SelectItem key={String(value)} value={value}>{String(value)}</SelectItem>)}</SelectContent>
          </Select>;
          if (ui.widget === 'qian.textarea') return <Textarea {...common} value={String(field.value ?? '')} onChange={field.onChange} />;
          const numeric = fieldSchema.type === 'number' || fieldSchema.type === 'integer';
          const dateTime = fieldSchema.format === 'date-time';
          const type = numeric ? 'number' : dateTime ? 'datetime-local' : fieldSchema.format === 'date' ? 'date' : 'text';
          let display = typeof field.value === 'number' || typeof field.value === 'string' ? field.value : '';
          if (dateTime && typeof display === 'string' && display) {
            const date = new Date(display);
            if (!Number.isNaN(date.getTime())) display = new Date(date.getTime() - date.getTimezoneOffset() * 60_000).toISOString().slice(0, 16);
          }
          return <Input {...common} type={type} value={display} onChange={(event) => {
            const value = event.target.value;
            field.onChange(numeric ? value === '' ? undefined : Number(value) : dateTime ? value ? new Date(value).toISOString() : undefined : value);
          }} />;
        }} />
        {description && <FieldDescription className={horizontal ? 'col-start-2' : undefined} id={`${fieldId}-description`}>{description}</FieldDescription>}
        {error?.message && <p id={`${fieldId}-error`} role="alert" data-slot="schema-field-error" data-testid={`${testId}-error-${path}`} className={horizontal ? 'col-start-2 text-sm text-destructive' : 'text-sm text-destructive'}>{error.message}</p>}
      </CoreField>;
    })}
    {errors.root?.message && <p role="alert" className="text-sm text-destructive" data-slot="schema-form-error">{String(errors.root.message)}</p>}
    {hasActions ? <div data-slot="schema-form-actions" className="flex justify-end gap-2 border-t border-border pt-4">{overlay?.cancel}{children}<Button type="submit" disabled={isSubmitting} testId={`${testId}-submit`}>{submitLabel ?? i18n.t('common.save')}</Button></div> : children}
  </form>;
}
