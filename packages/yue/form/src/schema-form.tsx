import { useContext, useEffect, useId, useImperativeHandle, useMemo, useRef, type BaseSyntheticEvent, type ReactNode, type Ref } from 'react';
import { createPortal } from 'react-dom';
import { Controller, useForm, useWatch, type FieldErrors, type Resolver } from 'react-hook-form';
import { Button, Checkbox, Field as CoreField, FieldDescription, FieldLabel, Input, Segmented, SegmentedItem, Select, SelectContent, SelectItem, SelectTrigger, Textarea } from '@idadawn/yue-core';
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
  /** 抽屉内的子步骤表单（如「查找账号」）：操作按钮紧跟表单、不带抽屉级的「取消」，也不占抽屉页脚。 @default false */
  inlineActions?: boolean;
  /** 字段说明下方的附加内容（如租户号的实时可用性提示），按字段路径与当前值给出；内容经 aria-describedby 关联到输入框。 */
  fieldExtra?: (path: string, value: unknown) => ReactNode;
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
function hasType(schema: DataSchema, type: string): boolean {
  return Array.isArray(schema.type) ? schema.type.includes(type) : schema.type === type;
}
interface FieldDefinition { path: string; schema: DataSchema; ui: UiNode; required: boolean; group?: UiNode }
// 不带 field 的布局节点是分组：带 labelKey 时渲染成有标题的字段组，否则只是透明容器。
function fields(data: DataSchema, nodes: UiNode[], prefix = '', group?: UiNode): FieldDefinition[] {
  const flatten = (items: UiNode[], owner?: UiNode): { node: UiNode; group?: UiNode }[] => items.flatMap((node) => node.field ? [{ node, group: owner }] : flatten(node.children ?? [], node.labelKey ? node : owner));
  const layout = flatten(nodes, group);
  const definitions = data.properties ?? {};
  const ordered = layout.length ? layout.map(({ node, group: owner }) => ({ key: node.field!, ui: node, group: owner })) : Object.keys(definitions).map((key) => ({ key, ui: { field: key } as UiNode, group }));
  return ordered.flatMap(({ key, ui, group: owner }) => {
    const schema = definitions[key];
    if (!schema) throw new Error('form.error.field');
    const path = prefix ? `${prefix}.${key}` : key;
    if (hasType(schema, 'object')) return fields(schema, ui.children ?? [], path, owner);
    return [{ path, schema, ui, required: data.required?.includes(key) ?? false, group: owner }];
  });
}
/** 相邻且同组的字段合成一段，按布局顺序渲染。 */
function blocks(definitions: FieldDefinition[]): { group?: UiNode; items: FieldDefinition[] }[] {
  const result: { group?: UiNode; items: FieldDefinition[] }[] = [];
  for (const item of definitions) {
    const last = result.at(-1);
    if (last && last.group === item.group) last.items.push(item); else result.push({ group: item.group, items: [item] });
  }
  return result;
}
function defaults(schema: DataSchema): FormValues {
  return Object.fromEntries(Object.entries(schema.properties ?? {}).flatMap(([key, field]) => {
    if (field.default !== undefined) return [[key, field.default]];
    if (hasType(field, 'object')) return [[key, defaults(field)]];
    return [];
  }));
}

/** 有标题的字段组：fieldset 以组标题命名，可带说明与序号（props.marker）；相邻两组之间用分隔线隔开。 */
function FieldGroup({ id, node, i18n, testId, children }: { id: string; node: UiNode; i18n: PlatformI18n; testId: string; children: ReactNode }) {
  const marker = typeof node.props?.marker === 'string' || typeof node.props?.marker === 'number' ? node.props.marker : undefined;
  const description = node.descriptionKey ? i18n.t(node.descriptionKey) : undefined;
  return <fieldset aria-labelledby={`${id}-title`} aria-describedby={description ? `${id}-description` : undefined} data-slot="schema-group" data-testid={testId}
    className="m-0 grid min-w-0 gap-4 border-0 border-t border-border p-0 pt-5 first:border-t-0 first:pt-0">
    <div className="flex items-start gap-3" data-slot="schema-group-header" data-testid={`${testId}-header`}>
      {marker !== undefined && <span aria-hidden="true" data-slot="schema-group-marker" data-testid={`${testId}-marker`} className="inline-flex size-6 shrink-0 items-center justify-center rounded-full bg-info-bg text-xs font-semibold text-info">{marker}</span>}
      <div className="grid min-w-0 gap-0.5"><h3 id={`${id}-title`} className="text-[15px] font-semibold leading-6">{i18n.t(node.labelKey!)}</h3>
        {description && <p id={`${id}-description`} className="text-[13px] text-muted-foreground">{description}</p>}</div>
    </div>
    {children}
  </fieldset>;
}

/** 编辑、只读与字段错误共享同一份 Schema、RHF 状态和字段目录。 */
export function SchemaForm({ schema, i18n, initialValues, mode = 'edit', onSubmit, onError, apiRef, className, testId = 'yue-schema-form', submitLabel, inlineActions = false, fieldExtra, children }: SchemaFormProps) {
  useMemo(() => assertFormSchema(schema), [schema]);
  const id = useId();
  const overlay = useContext(OverlayFormContext);
  const register = overlay?.register;
  const hasActions = mode === 'edit' && !!onSubmit;
  // 子步骤表单（inlineActions）不占抽屉页脚：不登记，页脚照常显示「关闭」或主表单的操作。
  useEffect(() => { if (hasActions && !inlineActions) return register?.(); }, [register, hasActions, inlineActions]);
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

  // 抽屉提供页脚容器时，操作按钮渲染进页脚并经 form 属性关联本表单（回车提交照常生效）；否则留在表单末尾。
  const footer = inlineActions ? null : overlay?.footer ?? null;
  const formId = `${id}-form`;
  const actions = hasActions ? <div data-slot="schema-form-actions" className={footer ? 'contents' : inlineActions ? 'flex flex-wrap gap-2' : 'flex justify-end gap-2 border-t border-border pt-4'}>{inlineActions ? null : overlay?.cancel}{children}<Button type="submit" form={formId} disabled={isSubmitting} testId={`${testId}-submit`}>{submitLabel ?? i18n.t('common.save')}</Button></div> : children;
  const renderField = ({ path, schema: fieldSchema, ui, required }: FieldDefinition) => {
      const state = effective.states[path];
      const access = schema.auth.fields?.[path];
      if (state?.visible === false || (access && !access.includes('read'))) return null;
      const readonly = mode === 'readonly' || (access && !access.includes('write'));
      const password = hasType(fieldSchema, 'string') && !hasType(fieldSchema, 'number') && !hasType(fieldSchema, 'integer')
        && !['date', 'date-time'].includes(String(fieldSchema.format)) && ui.props?.type === 'password';
      const horizontal = hasType(fieldSchema, 'boolean') && !readonly;
      const fieldId = `${id}-${path}`;
      const error = getValue(errors, path) as { message?: string } | undefined;
      const label = i18n.t(ui.labelKey ?? fieldSchema.title ?? path);
      // 枚举选项的显示文字：UI 元数据给出语言包前缀（props.optionLabelKey）时按「前缀.值」取文案，否则显示原值。
      const optionLabel = (value: unknown) => typeof ui.props?.optionLabelKey === 'string' ? i18n.t(`${ui.props.optionLabelKey}.${String(value)}`) : String(value);
      const description = ui.descriptionKey ? i18n.t(ui.descriptionKey) : undefined;
      const extra = fieldExtra?.(path, getValue(values, path));
      const hasExtra = extra !== undefined && extra !== null && extra !== false;
      const describedBy = [error ? `${fieldId}-error` : '', description ? `${fieldId}-description` : '', hasExtra ? `${fieldId}-extra` : ''].filter(Boolean).join(' ') || undefined;
      return <CoreField className={horizontal ? 'grid grid-cols-[1rem_minmax(0,1fr)] items-center gap-x-3 gap-y-2' : undefined} key={path} name={path} invalid={!!error} data-slot="schema-field" testId={`${testId}-field-${path}`}>
        <FieldLabel className={horizontal ? 'col-start-2 row-start-1 leading-5' : undefined} htmlFor={fieldId}>{label}{required ? <span aria-hidden className="text-destructive"> *</span> : null}</FieldLabel>
        <Controller name={path} control={form.control} render={({ field }) => {
          if (readonly) return <output id={fieldId} className="min-h-9 rounded-md bg-muted/40 px-3 py-2 text-sm text-foreground" data-slot="schema-field-readonly" data-testid={`${testId}-readonly-${path}`} aria-describedby={describedBy}>{maskValue(field.value, password ? 'full' : typeof ui.props?.readonlyMask === 'string' ? ui.props.readonlyMask : 'none')}</output>;
          const common = { id: fieldId, name: field.name, ref: field.ref, onBlur: field.onBlur, disabled: state?.disabled || isSubmitting, 'aria-required': required, 'aria-invalid': !!error, 'aria-describedby': describedBy };
          if (hasType(fieldSchema, 'boolean')) return <Checkbox {...common} className="col-start-1 row-start-1" checked={!!field.value} onCheckedChange={field.onChange} aria-label={label} />;
          if (fieldSchema.enum && ui.widget === 'qian.segmented') return <Segmented id={fieldId} value={field.value ?? null} onValueChange={field.onChange} disabled={common.disabled} aria-label={label} aria-describedby={describedBy} aria-invalid={!!error} className="flex w-full">
            {/* Field 会把字段标签挂到组内每个单选上；每项改由自己的文字命名，字段标签留给整组。 */}
            {fieldSchema.enum.filter((value) => value !== null).map((value, index) => <SegmentedItem key={String(value)} value={value} aria-labelledby={`${fieldId}-option-${index}`} className="flex-1"><span id={`${fieldId}-option-${index}`}>{optionLabel(value)}</span></SegmentedItem>)}</Segmented>;
          if (fieldSchema.enum) return <Select value={field.value ?? null} onValueChange={field.onChange} items={fieldSchema.enum.map((value) => ({ value, label: optionLabel(value) }))} disabled={common.disabled}>
            <SelectTrigger {...common} aria-label={label} /><SelectContent>{fieldSchema.enum.map((value) => <SelectItem key={String(value)} value={value}>{optionLabel(value)}</SelectItem>)}</SelectContent>
          </Select>;
          if (ui.widget === 'qian.textarea') return <Textarea {...common} value={String(field.value ?? '')} onChange={field.onChange} />;
          const numeric = hasType(fieldSchema, 'number') || hasType(fieldSchema, 'integer');
          const dateTime = fieldSchema.format === 'date-time';
          const type = numeric ? 'number' : dateTime ? 'datetime-local' : fieldSchema.format === 'date' ? 'date' : password ? 'password' : 'text';
          // UI 元数据不透传任意 DOM 属性；这里只开放认证与注册表单需要的浏览器自动填充语义。
          const autoComplete = (type === 'text' || type === 'password') && typeof ui.props?.autoComplete === 'string'
            && ['on', 'off', 'username', 'current-password', 'new-password', 'name', 'email', 'tel', 'organization', 'one-time-code'].includes(ui.props.autoComplete) ? ui.props.autoComplete : undefined;
          let display = typeof field.value === 'number' || typeof field.value === 'string' ? field.value : '';
          if (dateTime && typeof display === 'string' && display) {
            const date = new Date(display);
            if (!Number.isNaN(date.getTime())) display = new Date(date.getTime() - date.getTimezoneOffset() * 60_000).toISOString().slice(0, 16);
          }
          return <Input {...common} type={type} autoComplete={autoComplete} value={display} onChange={(event) => {
            const value = event.target.value;
            field.onChange(numeric ? value === '' ? undefined : Number(value) : dateTime ? value ? new Date(value).toISOString() : undefined : value);
          }} />;
        }} />
        {description && <FieldDescription className={horizontal ? 'col-start-2' : undefined} id={`${fieldId}-description`}>{description}</FieldDescription>}
        {hasExtra && <div id={`${fieldId}-extra`} data-slot="schema-field-extra" data-testid={`${testId}-extra-${path}`} className={horizontal ? 'col-start-2 text-sm' : 'text-sm'}>{extra}</div>}
        {error?.message && <p id={`${fieldId}-error`} role="alert" data-slot="schema-field-error" data-testid={`${testId}-error-${path}`} className={horizontal ? 'col-start-2 text-sm text-destructive' : 'text-sm text-destructive'}>{error.message}</p>}
      </CoreField>;
  };
  return <form id={formId} className={className ?? 'grid gap-4'} data-slot="schema-form" data-testid={testId} onSubmit={submit} noValidate>
    {blocks(definitions).map((block, index) => block.group ? <FieldGroup key={index} id={`${id}-group-${index}`} node={block.group} i18n={i18n} testId={`${testId}-group-${index}`}>{block.items.map(renderField)}</FieldGroup> : block.items.map(renderField))}
    {errors.root?.message && <p role="alert" className="text-sm text-destructive" data-slot="schema-form-error">{String(errors.root.message)}</p>}
    {hasActions && footer ? createPortal(actions, footer) : actions}
  </form>;
}
