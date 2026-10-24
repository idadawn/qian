import { readdirSync, readFileSync } from 'node:fs';
import { Ajv2020 } from 'ajv/dist/2020';
import addFormats from 'ajv-formats';
import { describe, expect, it } from 'vitest';
import { formSchema, listSchema, widgetSchema, type FormSchema } from '@idadawn/yue-contracts';
import { compileSchema } from './json-schema';
import { assertFormSchema, validateMetadata, validateValues } from './schema';
import { credentialSchema, schema } from './test/fixture';

// 对照组：此前 SchemaForm 在运行期用的 Ajv 配置（allErrors、strict: false、ajv-formats full）。Ajv 只留在测试里。
const ajv = new Ajv2020({ allErrors: true, strict: false });
addFormats(ajv);
function same(schema: unknown, values: readonly unknown[]) {
  const reference = ajv.compile(schema as object);
  const validate = compileSchema(schema);
  for (const value of values) {
    reference(value);
    expect(validate(value), JSON.stringify(value) ?? String(value)).toEqual(reference.errors ?? []);
  }
}
const DELETE = Symbol('delete');
type Path = (string | number)[];
function paths(value: unknown, prefix: Path = []): Path[] {
  if (!value || typeof value !== 'object') return [prefix];
  return [prefix, ...Object.entries(value).flatMap(([key, item]) => paths(item, [...prefix, Array.isArray(value) ? Number(key) : key]))];
}
function replaced(value: unknown, path: Path, replacement: unknown): unknown {
  if (path.length === 0) return replacement;
  const copy = structuredClone(value) as Record<string | number, unknown>;
  let parent = copy;
  for (const key of path.slice(0, -1)) parent = parent[key] as Record<string | number, unknown>;
  if (replacement === DELETE) delete parent[path.at(-1)!]; else parent[path.at(-1)!] = replacement;
  return copy;
}
// 每个位置逐一换成候选值（含删除与 undefined：react-hook-form 里清空的输入就是 undefined）。
const variants = (base: unknown, candidates: readonly unknown[]) => [base, ...paths(base).flatMap((path) => candidates.map((candidate) => replaced(base, path, candidate)))];
const STRUCTURE = [DELETE, undefined, null, 5, 1.5, 'x', '', true, [], {}, ['x'], { extra: 1 }];
const SCALARS = [DELETE, undefined, null, '', ' ', 'a', 'ab', 'abc', 'ABC', 'a b', '😀', '😀😀a', '\ud83d', String.fromCharCode(0x2028), 'a'.repeat(65), 'a'.repeat(201), 0, -0, -1, 1, 1.5, 25, 1e21, Number.NaN,
  Number.POSITIVE_INFINITY, true, false, {}, [], ['a'], { users: 1 }, '12', '-12', '007', '1.0', '2026-10-24', '2026-10-24T10:00:00Z', '2026-10-24t10:00:00.123+08:00',
  '2026-10-24 10:00:00z', '2026-10-24T10:00:00', '2024-02-29T23:59:60Z', '2023-02-29T10:00:00Z', '2026-13-01T00:00:00Z', '10:00:00Z', '123e4567-e89b-12d3-a456-426614174000',
  'urn:uuid:123E4567-E89B-12D3-A456-426614174000', '123e4567e89b12d3a456426614174000'];

// 仓库里交给 SchemaForm 的数据 Schema：fixture 直接引用，其余照原样抄录（租户运营三份是 OpenAPI 请求体解引用后的形状）。
const NAME = '^(?=[\\s\\S]*[^\\u0009-\\u000D\\u0020\\u0085\\u00A0\\u1680\\u2000-\\u200A\\u2028\\u2029\\u202F\\u205F\\u3000])[^\\u0000-\\u001F\\u007F-\\u009F]*$(?![\\s\\S])';
const INTEGER = '^-?(?:0|[1-9]\\d*)$';
const quotas = { type: ['object', 'null'], properties: { users: { type: ['string', 'integer', 'null'], pattern: INTEGER, default: null } }, additionalProperties: false };
const DATA: [string, unknown, Record<string, unknown>][] = [
  ['fixture 客户表单', schema.data, { code: 'a', name: '甲', enabled: true, phone: '1' }],
  ['fixture 账号密码', credentialSchema.data, { username: 'alice', password: 'example-password' }],
  ['可空嵌套对象与数字联合类型', { type: 'object', additionalProperties: false, properties: { quotas: { type: ['object', 'null'], properties: { users: { type: ['string', 'integer', 'null'], minimum: 0 } } } } }, { quotas: { users: 25 } }],
  ['可空布尔', { type: 'object', additionalProperties: false, properties: { enabled: { type: ['boolean', 'null'] } } }, { enabled: null }],
  ['文档示例客户表单', { type: 'object', additionalProperties: false, required: ['code', 'name'], properties: { code: { type: 'string', minLength: 1, maxLength: 16, pattern: '^[a-z0-9]+$' }, name: { type: 'string', minLength: 1, maxLength: 80 }, contactEnabled: { type: 'boolean', default: false }, phone: { type: 'string', minLength: 1, pattern: '^[0-9]*$' } } }, { code: 'sample', name: '示例客户', contactEnabled: true, phone: '13812345678' }],
  ['外壳改密', { type: 'object', additionalProperties: false, required: ['newPassword', 'confirmPassword'], properties: { newPassword: { type: 'string', minLength: 1 }, confirmPassword: { type: 'string', minLength: 1 } } }, { newPassword: 'a', confirmPassword: 'a' }],
  ['租户运营：新建', { type: 'object', additionalProperties: false, required: ['code', 'name', 'administrator'], properties: {
    code: { type: 'string', minLength: 1, maxLength: 64, pattern: '^[a-z0-9]+$(?![\\s\\S])' }, name: { type: 'string', minLength: 1, maxLength: 200, pattern: NAME }, isolation: { enum: ['row'], default: 'row' },
    administrator: { type: 'object', properties: { membershipId: { type: 'string', format: 'uuid' }, subjectId: { type: 'string' } }, required: ['membershipId', 'subjectId'], additionalProperties: false }, quotas,
  } }, { code: 'acme', name: '甲', isolation: 'row', administrator: { membershipId: '123e4567-e89b-12d3-a456-426614174000', subjectId: 's' }, quotas: { users: '10' } }],
  ['租户运营：编辑', { type: 'object', additionalProperties: false, required: ['name', 'expectedRevision'], properties: { name: { type: 'string', minLength: 1, maxLength: 200, pattern: NAME }, expectedRevision: { type: ['string', 'integer'], pattern: INTEGER }, quotas } }, { name: '甲', expectedRevision: 3, quotas: null }],
  ['租户运营：开通', { type: 'object', additionalProperties: false, properties: { trial: { type: 'boolean', default: false }, validUntil: { type: ['string', 'null'], format: 'date-time', default: null } } }, { trial: true, validUntil: '2026-10-24T10:00:00.000Z' }],
];

const layout = [{ field: 'code', labelKey: 'a', props: { readonlyMask: 'last4' } }, { field: 'group', children: [{ field: 'inner', widget: 'qian.textarea', children: [{ field: 'deep', descriptionKey: 'b' }] }] }];
const formDocument = { ...schema, ui: { layout, device: { h5: { overrides: [{ path: '/ui/layout/0', value: { hidden: true } }] } } }, binding: { entity: 'qian.test', fields: { code: 'code' }, systemColumns: ['id'] }, auth: { fields: { code: ['read', 'write'] } } };
const column = { field: 'name', titleKey: 'k', width: 120, fixed: 'left', align: 'center', summary: 's', resolver: 'r' };
const listDocument = { $schema: 'https://schema.qian.dev/list/v1', version: '1.0.0', id: 'qian.test.list', base: { dataSource: { kind: 'entity', ref: 'qian.test' }, layout: 'table', query: [{ field: 'name', children: [{ field: 'code' }] }], columns: [column], actions: {}, filter: {}, sort: [{ field: 'name', dir: 'asc' }] },
  overrides: { h5: { layout: 'card', columns: ['name'], query: [{ field: 'name' }], actions: {} }, pc: { columns: [column] } }, auth: { buttons: true, fields: true, columns: true, dataScope: true } };
const widgetDocument = { type: 'qian.input', version: '1.0.0', name: 'widget.input', category: 'basic', props: { type: 'object' }, valueType: 'string', events: ['change'], devices: ['pc'], capabilities: { bindsField: true, canBeColumn: true, canBeSearch: true, canBeGroup: false, canBeInSubtable: true, supportsReadonly: true, participatesInLinkage: true }, defaultOperators: ['eq'], authAware: true, sensitivityHint: 'pii', i18nKeys: ['widget.input'], a11y: { role: 'textbox', labelledBy: 'label' }, tokens: [] };

describe('解释型 JSON Schema 校验与 Ajv 逐条一致', () => {
  it.each(DATA)('表单数据 Schema：%s', (_name, data, valid) => { same(data, [{}, ...variants(valid, SCALARS)]); });

  it.each([['form', formSchema, formDocument], ['list', listSchema, listDocument], ['widget', widgetSchema, widgetDocument]] as const)('元数据 Schema：%s', (kind, metadata, document) => {
    expect(validateMetadata(kind, document)).toEqual({ valid: true, errors: [] });
    same(metadata, variants(document, STRUCTURE));
  });

  it('列表端覆盖的 columns 走 oneOf：零个、一个、两个分支通过', () => {
    same(listSchema, [[], ['a', { field: 'b' }], [{ field: 'b', width: 0 }], ['a', 5]].map((columns) => replaced(listDocument, ['overrides', 'h5', 'columns'], columns)));
  });

  it('求值顺序：不分类型的关键字、类型错误的位置与分组顺序', () => {
    same({ type: 'string', minLength: 1, enum: ['a'] }, [5, '', 'b', 'a']);
    same({ type: 'string', enum: ['a'] }, [5, 'b', 'a']);
    same({ type: 'number', format: 'date', enum: [1] }, ['x', 1, 2, '2026-10-24']);
    same({ type: 'object', required: [] }, [5, {}]);
    same({ type: 'object', properties: {} }, [5, []]);
    same({ maximum: 10, minimum: 0, exclusiveMaximum: 10, exclusiveMinimum: -1, type: 'integer' }, [-1.5, -1, 0, 10, 10.5, Number.NaN, Number.POSITIVE_INFINITY, Number.NEGATIVE_INFINITY, '5', null]);
    same({ type: ['number', 'null'], minimum: 0 }, [Number.NaN, null, -1, 'x']);
    same({ pattern: '^a', maxLength: 1, minLength: 3, format: 'uuid', type: 'string' }, ['b', 'ab', 5]);
    same({ items: { type: 'string' }, maxItems: 1, minItems: 3, type: 'array' }, [[1, 'a'], [], ['a', 'b', 'c', 'd'], 'x']);
    same({ properties: { a: { type: 'string' } }, additionalProperties: false, required: ['b', 'a'], type: 'object' }, [{ a: 1, c: 2, b: undefined }, { a: undefined }]);
  });

  it('类型、常量与枚举（JSON 深相等）', () => {
    same({ type: ['string', 'null'] }, [5, null, 'a', undefined]);
    for (const type of ['object', 'array', 'null', 'boolean', 'integer', 'number', 'string']) same({ type }, [{}, [], null, 0, 1.5, 'x', true, Number.NaN]);
    same({ const: { a: [1, { b: null }] } }, [{ a: [1, { b: null }] }, { a: [1, { b: 0 }] }, [1], null]);
    same({ const: 0 }, [0, -0, false, '0', null]);
    same({ enum: [{ a: 1 }, [1, 2], 'x', null, 1] }, [{ a: 1 }, { a: 2 }, [1, 2], [2, 1], 'x', null, 1, '1', true]);
  });

  it('字符串按码点计长，pattern 是 Unicode 正则，格式与 ajv-formats full 同义', () => {
    same({ type: 'string', minLength: 2, maxLength: 3 }, ['😀', '😀😀', '😀😀😀😀', 'ab', '\ud83d', '😀\ud83d']);
    same({ pattern: '^\\p{L}+$' }, ['abc', '中文', '123', 5]);
    same({ pattern: '^.$' }, ['😀', 'ab']);
    for (const format of ['date', 'time', 'date-time', 'uuid']) same({ format }, [...SCALARS.filter((value) => value !== DELETE), '2026-02-28', '2024-02-29', '1900-02-29', '2000-02-29', '2026-04-31', '2026-00-10',
      '23:59:60Z', '23:59:60+00:00', '15:59:60-08:00', '00:59:60+01:00', '23:59:61Z', '24:00:00Z', '10:00:00+24:00', '10:00:00+08:60', '10:00:00.5+0800', '10:00:00+08', '2026-10-24T10:00:00+08:00x']);
  });

  it('对象：属性存在按 Ajv 口径（取值不是 undefined），additionalProperties 的 Schema 形态与转义', () => {
    same({ type: 'object', properties: { a: {} }, additionalProperties: { type: 'string', minLength: 1 } }, [{ a: 1, b: '', c: undefined, 'x/y': 2, 'm~n': 3 }, {}]);
    same({ type: 'object', required: ['constructor', 'toString', 'a'], properties: { toString: { type: 'string' } } }, [{}, { a: undefined }, Object.create(null)]);
    same(JSON.parse('{"properties":{"__proto__":{"type":"string"}},"additionalProperties":false}'), [JSON.parse('{"__proto__":1}'), {}]);
    same({ properties: { 'a/b': { type: 'string' }, 'c d': { type: 'string' }, 'e~f': { type: 'string' } } }, [{ 'a/b': 1, 'c d': 2, 'e~f': 3 }]);
    same({ properties: { a: false } }, [{ a: 1 }, {}, { a: undefined }]);
    same(false, [1, null]);
    same(true, [1, null]);
  });

  it('$ref：就地展开、沿引用解析、递归与转义，schemaPath 同 Ajv', () => {
    same({ $defs: { 'a/b': { type: 'string' }, 'c d': { type: 'string' }, 'e~f': { type: 'integer' } }, properties: { x: { $ref: '#/$defs/a~1b' }, y: { $ref: '#/$defs/c%20d' }, z: { $ref: '#/$defs/e~0f' } } }, [{ x: 1, y: 2, z: 'q' }, {}]);
    same({ $defs: { S: { minLength: 3 } }, $ref: '#/$defs/S', type: 'string', maxLength: 1 }, ['ab', 5, 'abcd']);
    same({ $defs: { S: { minLength: 3 } }, $ref: '#/$defs/S', type: 'string', enum: ['ab'] }, [5, 'ab']);
    same({ $defs: { A: { $ref: '#/$defs/B' }, B: { type: 'string', minLength: 2 } }, properties: { a: { $ref: '#/$defs/A' } } }, [{ a: 1 }, { a: 'x' }, { a: 'xy' }]);
    same({ type: 'object', properties: { n: { $ref: '#' }, v: { type: 'integer' } }, additionalProperties: false }, [{ n: { n: { v: 'x' }, x: 1 } }, 5, { n: 5 }]);
    same({ $defs: { Node: { type: 'object', required: ['id'], properties: { id: { type: 'string' }, children: { type: 'array', items: { $ref: '#/$defs/Node' } } } } }, $ref: '#/$defs/Node' }, [{ id: 'a', children: [{ id: 1 }, { children: [{ id: 'c', children: 5 }] }] }, []]);
    same({ $defs: { S: { type: 'string', const: { $ref: 'x' } } }, properties: { a: { $ref: '#/$defs/S' } } }, [{ a: 1 }]);
    same({ $defs: { S: { type: 'string' } }, properties: { a: { $ref: '#/properties/b' }, b: { $ref: '#/$defs/S' } } }, [{ a: 1, b: 2 }]);
  });

  it('oneOf：分支错误保留，恰好一个通过时清空；找到第二个通过的分支即停', () => {
    same({ oneOf: [{ type: 'string' }, {}, {}, { type: 'boolean' }] }, [5, 'x', true]);
    same({ oneOf: [{}, true, { type: 'number' }] }, [5, 'x']);
    same({ oneOf: [{ type: 'string', minLength: 2 }, { type: 'number' }] }, ['a', 'ab', 1, null]);
    same({ oneOf: [false, false] }, [1]);
  });

  it('说明关键字与本 spec 的 x- 扩展不参与判定', () => {
    same({ $schema: 'https://json-schema.org/draft/2020-12/schema', $id: 'https://example.test/annotated', $comment: 'c', title: 't', description: 'd', default: 'x', examples: ['a'], deprecated: false, readOnly: true, writeOnly: false,
      type: 'string', 'x-sensitivity': 'pii', 'x-ref': 'qian.test', 'x-widget-hint': 'qian.input' }, ['a', 5]);
  });
});

describe('子集外拒绝，而不是静默放过', () => {
  const rejected = (schema: unknown, message: string, cause?: string) => expect(() => compileSchema(schema), JSON.stringify(schema)).toThrow(expect.objectContaining(cause ? { message, cause } : { message }));
  it('子集外的关键字、格式与引用：form.error.unsupportedKeyword', () => {
    for (const schema of [{ anyOf: [{}] }, { allOf: [{}] }, { not: {} }, { if: {}, then: {} }, { else: {} }, { patternProperties: {} }, { propertyNames: {} }, { prefixItems: [] }, { contains: {} }, { minContains: 1 },
      { uniqueItems: true }, { multipleOf: 2 }, { minProperties: 1 }, { maxProperties: 1 }, { dependentRequired: {} }, { dependentSchemas: {} }, { unevaluatedProperties: false }, { unevaluatedItems: false },
      { type: 'string', nullable: true }, { definitions: {} }, { $dynamicRef: '#x' }, { $dynamicAnchor: 'x' }, { $anchor: 'x' }, { $vocabulary: {} }, { formatMinimum: '2020-01-01' }, { contentMediaType: 'text/plain' },
      { format: 'email' }, { format: 'uri' }, { format: 'phone' }, { 'x-unknown': 1 }, { foo: 1 }, { $ref: 'https://example.test/s' }, { $ref: '#x' }, { $ref: 'other.json#/a' },
      { $schema: 'http://json-schema.org/draft-07/schema#' }, { properties: { a: { $id: 'https://example.test/a' } } }, { items: { $schema: 'https://json-schema.org/draft/2020-12/schema' } }]) rejected(schema, 'form.error.unsupportedKeyword');
    // 嵌套处、未被引用的 $defs 里的也拒绝；cause 指向出问题的位置。
    rejected({ properties: { a: { anyOf: [] } } }, 'form.error.unsupportedKeyword', '#/properties/a/anyOf');
    rejected({ $defs: { unused: { items: { not: {} } } } }, 'form.error.unsupportedKeyword', '#/$defs/unused/items/not');
    rejected({ oneOf: [{}, { additionalProperties: { format: 'email' } }] }, 'form.error.unsupportedKeyword', '#/oneOf/1/additionalProperties/format');
  });

  it('参数不合法、引用不可解析与过深嵌套：form.error.schema', () => {
    for (const schema of ['object', null, 5, [], { type: 'date' }, { type: [] }, { type: ['string', 'string'] }, { type: 1 }, { enum: [] }, { enum: 'a' }, { oneOf: [] }, { oneOf: {} }, { required: ['a', 'a'] }, { required: [1] },
      { required: 'a' }, { minLength: -1 }, { maxLength: 1.5 }, { minItems: '1' }, { maxItems: Number.POSITIVE_INFINITY }, { pattern: '(' }, { pattern: 1 }, { format: 1 }, { minimum: 'x' }, { exclusiveMinimum: Number.NaN },
      { items: [] }, { items: 1 }, { properties: [] }, { properties: { a: 1 } }, { additionalProperties: 'x' }, { $ref: '#/$defs/missing' }, { $ref: 1 }, { $ref: '#/%E0%A4%A' }, { title: 5 }, { examples: 'x' },
      { readOnly: 'yes' }, { $defs: [] }, { $id: 5 }, { $defs: { A: { $ref: '#/$defs/B' }, B: { $ref: '#/$defs/A' } }, $ref: '#/$defs/A' }]) rejected(schema, 'form.error.schema');
    let deep: unknown = { type: 'string' };
    for (let index = 0; index < 40; index++) deep = { type: 'object', properties: { next: deep } };
    rejected(deep, 'form.error.schema');
  });

  it('SchemaForm 渲染前就拒绝：assertFormSchema 编译数据 Schema，validateValues 的错误呈现不变', () => {
    const withField = (field: Record<string, unknown>): FormSchema => ({ ...schema, data: { ...schema.data, properties: { ...schema.data.properties, code: field } } });
    expect(() => assertFormSchema(withField({ type: 'string', anyOf: [{ minLength: 1 }] }))).toThrow('form.error.unsupportedKeyword');
    expect(() => assertFormSchema(withField({ type: 'string', format: 'email' }))).toThrow('form.error.unsupportedKeyword');
    expect(() => assertFormSchema(withField({ type: 'string', pattern: '(' }))).toThrow('form.error.schema');
    expect(() => assertFormSchema(schema)).not.toThrow();
    expect(validateValues(schema, { code: undefined, name: '甲', enabled: true })).toEqual([
      { instancePath: '', schemaPath: '#/required', keyword: 'required', params: { missingProperty: 'code' }, message: "must have required property 'code'" },
      { instancePath: '', schemaPath: '#/required', keyword: 'required', params: { missingProperty: 'phone' }, message: "must have required property 'phone'" },
    ]);
  });
});

// 外壳 meta CSP 不放行 'unsafe-eval'（S1-YUE-014）：yue-form 与它在运行期带进外壳源的工作区包都不能带 JSON Schema 编译器、不能在运行期生成代码。
describe('运行时路径不携带 JSON Schema 编译器、不在运行期生成代码', () => {
  const packages = new URL('../../', import.meta.url);
  const manifest = (name: string) => JSON.parse(readFileSync(new URL(`${name}/package.json`, packages), 'utf8')) as { dependencies?: Record<string, string>; peerDependencies?: Record<string, string> };
  const workspace = (dependencies: Record<string, string> = {}) => Object.keys(dependencies).filter((name) => name.startsWith('@idadawn/yue-')).map((name) => name.slice('@idadawn/yue-'.length));
  const visited = new Set<string>();
  for (const pending = ['form']; pending.length;) {
    const name = pending.pop()!;
    if (visited.has(name)) continue;
    visited.add(name);
    pending.push(...workspace(manifest(name).dependencies));
  }

  it('沿 dependencies 收集到 yue-form 及其工作区运行时依赖', () => {
    expect([...visited].sort()).toEqual(expect.arrayContaining(['contracts', 'core', 'form', 'sdk']));
  });

  it.each([...visited])('@idadawn/yue-%s：dependencies 不含 ajv / ajv-formats，源码不含 Ajv 导入与动态代码生成', (name) => {
    const { dependencies = {}, peerDependencies = {} } = manifest(name);
    expect([...Object.keys(dependencies), ...Object.keys(peerDependencies)].filter((dependency) => /^ajv(?:-|$)/.test(dependency))).toEqual([]);
    const source = new URL(`${name}/src/`, packages);
    const files = (readdirSync(source, { recursive: true }) as string[]).filter((file) => /\.(?:[cm]?js|tsx?)$/.test(file) && !/\.test\.tsx?$/.test(file) && file.split(/[\\/]/)[0] !== 'test');
    expect(files.length).toBeGreaterThan(0);
    for (const file of files) {
      const text = readFileSync(new URL(file, source), 'utf8');
      // 只认裸调用：私有方法 #require、obj.eval( 之类不算。
      expect(text, `${name}/src/${file}`).not.toMatch(/from ['"]ajv|import\(\s*['"]ajv|(?<![#.\w$])require\(|\bnew Function\b|(?<![#.\w$])Function\(|(?<![#.\w$])eval\(|\bset(?:Timeout|Interval)\(\s*['"`]/);
    }
  });
});
