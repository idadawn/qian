import { formSchema, listSchema, widgetSchema, type FormSchema } from '@idadawn/yue-contracts';
import { compileSchema, type SchemaError, type SchemaValidator } from './json-schema';

export type { SchemaError } from './json-schema';
export type FormValues = Record<string, unknown>;
export interface DataSchema { type?: string | string[]; properties?: Record<string, DataSchema>; required?: string[]; title?: string; description?: string; enum?: unknown[]; default?: unknown; format?: string; [key: string]: unknown }
export interface FieldState { required: boolean; disabled: boolean; visible: boolean }
// 元数据 Schema 与表单数据 Schema 走同一个解释型校验器（json-schema.ts，不在运行期生成代码），元数据的校验器首次使用时编译。
const metadata: Record<'form' | 'list' | 'widget', unknown> = { form: formSchema, list: listSchema, widget: widgetSchema };
const validators: Partial<Record<keyof typeof metadata, SchemaValidator>> = {};

export function validateMetadata(kind: keyof typeof metadata, value: unknown): { valid: boolean; errors: SchemaError[] } {
  const errors = (validators[kind] ??= compileSchema(metadata[kind]))(value);
  return { valid: errors.length === 0, errors };
}

export function getValue(values: FormValues, path: string): unknown {
  return path.split('.').reduce<unknown>((value, key) => value && typeof value === 'object' ? (value as FormValues)[key] : undefined, values);
}

function schemaAt(schema: DataSchema, path: string): { parent: DataSchema; key: string; field: DataSchema } | undefined {
  const keys = path.split('.');
  const key = keys.pop()!;
  let parent = schema;
  for (const segment of keys) { if (!parent.properties?.[segment]) return; parent = parent.properties[segment]; }
  const field = parent.properties?.[key];
  return field && { parent, key, field };
}

/** S1 联动是可序列化的等值条件；不执行表达式脚本。 */
export function effectiveSchema(schema: FormSchema, values: FormValues): { data: DataSchema; states: Record<string, Partial<FieldState>>; assignments: Record<string, unknown> } {
  const data = structuredClone(schema.data) as DataSchema;
  const states: Record<string, Partial<FieldState>> = {};
  const assignments: Record<string, unknown> = {};
  for (const dependency of schema.rules.dependencies ?? []) {
    if (JSON.stringify(getValue(values, dependency.source)) !== JSON.stringify(dependency.equals)) continue;
    const target = schemaAt(data, dependency.target);
    if (!target) throw new Error('form.error.dependencyTarget');
    const state = states[dependency.target] ??= {};
    switch (dependency.effect) {
      case 'required': {
        state.required = !!dependency.value;
        const required = new Set(target.parent.required ?? []);
        if (dependency.value) required.add(target.key); else required.delete(target.key);
        target.parent.required = [...required];
        break;
      }
      case 'disabled': state.disabled = !!dependency.value; break;
      case 'visible': state.visible = !!dependency.value; break;
      case 'value': assignments[dependency.target] = dependency.value; break;
    }
  }
  return { data, states, assignments };
}

function checkExtensions(value: unknown): void {
  if (!value || typeof value !== 'object') return;
  for (const [key, entry] of Object.entries(value)) {
    // 取值为 undefined 的成员当作没写，与 compileSchema 同一口径。
    if (entry === undefined) continue;
    if (key.startsWith('x-') && !['x-sensitivity', 'x-ref', 'x-widget-hint'].includes(key)) throw new Error('form.error.extension');
    checkExtensions(entry);
  }
}

export function assertFormSchema(schema: FormSchema): void {
  if (!validateMetadata('form', schema).valid) throw new Error('form.error.schema');
  checkExtensions(schema.data);
  // 数据 Schema 渲染前先编译一次：解释型子集外的关键字、格式与引用在这里显式拒绝，不等到提交时才暴露，也不静默放过。
  compileSchema(schema.data);
  // 元数据可储存后续版本的声明，本期渲染时显式拒绝未实现的执行能力。
  if (schema.rules.actions?.length || Object.keys(schema.rules.expressions ?? {}).length) throw new Error('form.error.unsupportedRules');
}

export function validateValues(schema: FormSchema, values: FormValues): SchemaError[] {
  return compileSchema(effectiveSchema(schema, values).data)(values);
}

export const ruleCatalog = {
  validation: [
    { id: 'required', parameters: { type: 'object', additionalProperties: false }, keyword: 'required' },
    { id: 'length', parameters: { type: 'object', properties: { min: { type: 'integer', minimum: 0 }, max: { type: 'integer', minimum: 0 } }, additionalProperties: false }, keywords: ['minLength', 'maxLength'] },
    { id: 'pattern', parameters: { type: 'object', required: ['pattern'], properties: { pattern: { type: 'string' } }, additionalProperties: false }, keyword: 'pattern' },
  ],
  masking: [
    { id: 'none', parameters: { type: 'object', additionalProperties: false } },
    { id: 'full', parameters: { type: 'object', additionalProperties: false } },
    { id: 'last4', parameters: { type: 'object', additionalProperties: false } },
  ],
} as const;
export function exportRuleCatalog(): typeof ruleCatalog { return structuredClone(ruleCatalog); }
export function maskValue(value: unknown, mask: string = 'none'): string {
  const text = value === null || value === undefined ? '' : String(value);
  if (mask === 'none') return text;
  if (mask === 'full') return text ? '••••' : '';
  if (mask === 'last4') return text.length <= 4 ? '•'.repeat(text.length) : '••••' + text.slice(-4);
  throw new Error('form.error.mask');
}
