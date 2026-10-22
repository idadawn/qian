import type { FormSchema } from '@idadawn/yue-contracts';
import { createI18n } from '@idadawn/yue-sdk';
export const i18n = createI18n();
i18n.addLanguagePack('zh-CN', { 'test.name': '名称', 'test.code': '编码', 'test.enabled': '启用联系', 'test.phone': '电话', 'test.username': '账号', 'test.password': '密码' });
export const schema: FormSchema = {
  $schema: 'https://schema.qian.dev/form/v1', version: '1.0.0', id: 'qian.test.form',
  data: { type: 'object', additionalProperties: false, required: ['code', 'name'], properties: { code: { type: 'string', pattern: '^[a-z0-9]+$' }, name: { type: 'string', minLength: 1 }, enabled: { type: 'boolean' }, phone: { type: 'string', minLength: 1 } } },
  ui: { layout: [{ field: 'code', labelKey: 'test.code' }, { field: 'name', labelKey: 'test.name' }, { field: 'enabled', labelKey: 'test.enabled' }, { field: 'phone', labelKey: 'test.phone', props: { readonlyMask: 'last4' } }], device: { h5: { overrides: [] } } },
  rules: { dependencies: [{ source: 'enabled', equals: true, target: 'phone', effect: 'required', value: true }] }, binding: {}, auth: {},
};

export const credentialSchema: FormSchema = {
  $schema: 'https://schema.qian.dev/form/v1', version: '1.0.0', id: 'qian.test.credentials',
  data: { type: 'object', additionalProperties: false, required: ['username', 'password'], properties: { username: { type: 'string', minLength: 1 }, password: { type: 'string', minLength: 8 } } },
  ui: { layout: [{ field: 'username', labelKey: 'test.username', props: { autoComplete: 'username' } }, { field: 'password', labelKey: 'test.password', props: { type: 'password', autoComplete: 'current-password', readonlyMask: 'none' } }] },
  rules: {}, binding: {}, auth: {},
};
