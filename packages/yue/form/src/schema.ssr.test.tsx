import { renderToString } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { listSchema, type ListSchema } from '@idadawn/yue-contracts';
import { SchemaForm } from './schema-form';
import { assertFormSchema, effectiveSchema, exportRuleCatalog, maskValue, validateMetadata, validateValues } from './schema';
import { credentialSchema, i18n, schema } from './test/fixture';

describe('表单与元数据契约', () => {
  it('密码输入支持 SSR，自动填充与输入类型只接受白名单，密码只读强制遮蔽', () => {
    const edit = renderToString(<SchemaForm schema={credentialSchema} i18n={i18n} />);
    expect(edit).toContain('type="password"');
    expect(edit).toContain('autoComplete="current-password"');
    const readonly = renderToString(<SchemaForm schema={credentialSchema} i18n={i18n} initialValues={{ username: 'alice', password: 'example-password' }} mode="readonly" />);
    expect(readonly).toContain('••••');
    expect(readonly).not.toContain('example-password');
    const unsupported: typeof credentialSchema = { ...credentialSchema, ui: { layout: [{ field: 'username', props: { type: 'hidden', autoComplete: 'unsupported-autocomplete', formAction: 'https://example.invalid/collect' } }] } };
    const rendered = renderToString(<SchemaForm schema={unsupported} i18n={i18n} />);
    expect(rendered).toContain('type="text"');
    expect(rendered).not.toContain('type="hidden"');
    expect(rendered).not.toContain('unsupported-autocomplete');
    expect(rendered).not.toContain('example.invalid');
  });

  it('同一引擎 SSR 支持编辑和只读，数据不需要浏览器全局对象', () => {
    const props = { schema, i18n, initialValues: { code: 'a', name: '甲', phone: '13812345678' } };
    expect(renderToString(<SchemaForm {...props} />)).toContain('input');
    const readonly = renderToString(<SchemaForm {...props} mode="readonly" />);
    expect(readonly).toContain('schema-field-readonly');
    expect(readonly).toContain('••••5678');
    expect(readonly).not.toContain('<input');
  });
  it('FormSchema / ListSchema 带 device、auth，拒绝未知版本和缺失字段', () => {
    expect(validateMetadata('form', schema).valid).toBe(true);
    expect(validateMetadata('form', { ...schema, version: '2.0.0' }).valid).toBe(false);
    const list: ListSchema = { $schema: 'https://schema.qian.dev/list/v1', version: '1.0.0', id: 'qian.test.list', base: { dataSource: { kind: 'entity', ref: 'qian.test' }, layout: 'table', columns: [{ field: 'name' }] }, overrides: { h5: { layout: 'card', columns: ['name'] } }, auth: { buttons: true, fields: true, columns: true, dataScope: true } };
    expect(validateMetadata('list', list).valid).toBe(true);
    expect(validateMetadata('list', { ...list, auth: undefined }).valid).toBe(false);
    expect(listSchema.properties.overrides.properties.h5).toBeDefined();
    expect(() => assertFormSchema({ ...schema, data: { ...schema.data, 'x-unknown': true } })).toThrow('form.error.extension');
  });
  it('控件 manifest 通过自身 Schema 并要求 device 与只读能力', () => {
    const widget = { type: 'qian.input', version: '1.0.0', name: 'widget.input', category: 'basic', props: { type: 'object' }, valueType: 'string', events: ['change'], devices: ['pc'], capabilities: { bindsField: true, canBeColumn: true, canBeSearch: true, canBeGroup: false, canBeInSubtable: true, supportsReadonly: true, participatesInLinkage: true }, defaultOperators: ['eq'], authAware: true, i18nKeys: ['widget.input'], a11y: { role: 'textbox', labelledBy: 'label' }, tokens: [] };
    expect(validateMetadata('widget', widget).valid).toBe(true);
    expect(validateMetadata('widget', { ...widget, devices: [] }).valid).toBe(false);
  });
  it('联动仅作用于当前值，不修改原始 Schema；取消条件恢复原约束', () => {
    expect(effectiveSchema(schema, { enabled: true }).data.required).toContain('phone');
    expect(effectiveSchema(schema, { enabled: false }).data.required).not.toContain('phone');
    expect(schema.data.required).toEqual(['code', 'name']);
    expect(validateValues(schema, { code: 'a', name: '甲', enabled: true }).some((error) => error.params.missingProperty === 'phone')).toBe(true);
  });
  it('校验与脱敏目录可导出 JSON，不泄漏实现；短值也不会被 last4 原样显示', () => {
    const catalog = exportRuleCatalog();
    expect(JSON.parse(JSON.stringify(catalog)).validation.map((rule: { id: string }) => rule.id)).toEqual(['required', 'length', 'pattern']);
    expect(catalog.masking.every((rule) => rule.parameters.type === 'object')).toBe(true);
    expect(maskValue('123', 'last4')).toBe('•••');
    expect(maskValue('123456', 'full')).toBe('••••');
  });
});
