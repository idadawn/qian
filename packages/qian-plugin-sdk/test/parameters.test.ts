import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import { compileParameters } from '../src/parameters';
import { validateSlot } from '../src/index';

// 客户端动作参数的可解释子集（S1-YUE-012）：不生成代码，严格 CSP 下可用；子集外的关键字拒绝登记，而不是静默放过。
describe('客户端动作参数 Schema（可解释子集）', () => {
  it('对象、必填、额外属性、枚举、常量、数组与字符串 / 数值约束', () => {
    const validate = compileParameters({
      type: 'object', additionalProperties: false, required: ['mode', 'tags'], title: '刷新', description: '说明关键字不参与判定',
      properties: {
        mode: { enum: ['soft', 'hard'] }, force: { type: 'boolean', default: false }, kind: { const: 'tenant' },
        tags: { type: 'array', items: { type: 'string', minLength: 1, maxLength: 3, pattern: '^[a-z😀]+$' }, minItems: 1, maxItems: 2, uniqueItems: true },
        limit: { type: 'integer', minimum: 1, maximum: 100, multipleOf: 5 }, ratio: { type: ['number', 'null'], exclusiveMinimum: 0, exclusiveMaximum: 1 },
      },
    });
    expect(validate({ mode: 'soft', tags: ['ab'] })).toBe(true);
    expect(validate({ mode: 'hard', tags: ['a😀c'], force: true, kind: 'tenant', limit: 10, ratio: null })).toBe(true);
    expect(validate({ mode: 'hard', tags: ['abcd'] })).toBe(false); // maxLength 按码点
    expect(validate({ mode: 'other', tags: ['a'] })).toBe(false);
    expect(validate({ mode: 'soft' })).toBe(false);
    expect(validate({ mode: 'soft', tags: ['a'], extra: 1 })).toBe(false);
    expect(validate({ mode: 'soft', tags: ['a', 'a'] })).toBe(false);
    expect(validate({ mode: 'soft', tags: [] })).toBe(false);
    expect(validate({ mode: 'soft', tags: ['A'] })).toBe(false);
    expect(validate({ mode: 'soft', tags: ['a'], kind: 'other' })).toBe(false);
    expect(validate({ mode: 'soft', tags: ['a'], limit: 7 })).toBe(false);
    expect(validate({ mode: 'soft', tags: ['a'], limit: 1.5 })).toBe(false);
    expect(validate({ mode: 'soft', tags: ['a'], ratio: 1 })).toBe(false);
    expect(validate({ mode: 'soft', tags: ['a'], force: 'yes' })).toBe(false);
    expect(validate(['soft'])).toBe(false);
    expect(compileParameters({ type: 'object', additionalProperties: { type: 'number' } })({ a: 1, b: 'x' })).toBe(false);
    expect(compileParameters({ type: 'object', minProperties: 1, maxProperties: 1 })({})).toBe(false);
    expect(compileParameters(true)(42)).toBe(true);
    expect(compileParameters({ properties: { a: false } })({ a: 1 })).toBe(false);
  });

  it('子集外的关键字、非法参数与过深嵌套拒绝登记（INPUT_INVALID，指针 /parameters）', () => {
    const rejected = [
      { $ref: '#/$defs/x' }, { oneOf: [{ type: 'string' }] }, { anyOf: [] }, { allOf: [] }, { not: {} }, { if: {}, then: {} }, { format: 'email' },
      { patternProperties: {} }, { prefixItems: [] }, { $id: 'https://example.test/x' }, { type: 'date' }, { type: [] }, { type: ['string', 'string'] },
      { enum: [] }, { required: ['a', 'a'] }, { required: [1] }, { minLength: -1 }, { maxItems: 1.5 }, { pattern: '(' }, { multipleOf: 0 }, { minimum: 'x' },
      { uniqueItems: 'yes' }, { properties: [] }, 'object', null,
    ];
    for (const schema of rejected) expect(() => compileParameters(schema), JSON.stringify(schema)).toThrow(expect.objectContaining({ code: 'INPUT_INVALID', pointer: '/parameters' }));
    let deep: unknown = { type: 'string' };
    for (let index = 0; index < 40; index++) deep = { type: 'object', properties: { next: deep } };
    expect(() => compileParameters(deep)).toThrow(expect.objectContaining({ code: 'INPUT_INVALID' }));
  });

  it('插槽登记沿用同一规则：子集外的参数 Schema 拒绝', () => {
    const action = { slot: 'qian.shell.client-action/v1', id: 'refresh', title: '刷新', description: '刷新列表', permission: 'tenant:read', expose: 'Page' };
    expect(() => validateSlot({ ...action, parameters: { type: 'object', properties: { force: { type: 'boolean' } } } })).not.toThrow();
    expect(() => validateSlot({ ...action, parameters: { type: 'object', properties: { force: { $ref: '#/x' } } } })).toThrow(expect.objectContaining({ pointer: '/parameters' }));
  });

  it('浏览器运行时的校验路径不携带 JSON Schema 编译器、不在运行期生成代码', () => {
    for (const file of ['validation.ts', 'parameters.ts', 'ui-validators.generated.ts', 'bridge.ts', 'host.ts']) {
      const source = readFileSync(new URL(`../src/${file}`, import.meta.url), 'utf8');
      // 只认裸调用：私有方法 #require 之类不算。
      expect(source, file).not.toMatch(/from 'ajv|(?<![#.\w$])require\(|\bnew Function\b|(?<![#.\w$])eval\(/);
    }
  });
});
