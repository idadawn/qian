import { useState } from 'react';
import { Button } from '@idadawn/yue-core';
import { createI18n, createMockTransport, PlatformClient } from '@idadawn/yue-sdk';
import { FeedbackProvider, SchemaForm, useConfirm, useDrawer, useMessage, useModal, usePopup, type FormSchema, type FormValues } from '@idadawn/yue-form';

const schema: FormSchema = {
  $schema: 'https://schema.qian.dev/form/v1', version: '1.0.0', id: 'qian.demo.customer',
  data: { type: 'object', additionalProperties: false, required: ['code', 'name'], properties: {
    code: { type: 'string', minLength: 1, maxLength: 16, pattern: '^[a-z0-9]+$' },
    name: { type: 'string', minLength: 1, maxLength: 80 },
    contactEnabled: { type: 'boolean', default: false },
    phone: { type: 'string', minLength: 1, pattern: '^[0-9]*$' },
  } },
  ui: { layout: [{ field: 'code', labelKey: 'demo.code' }, { field: 'name', labelKey: 'demo.name' }, { field: 'contactEnabled', labelKey: 'demo.contact', descriptionKey: 'demo.contactHelp' }, { field: 'phone', labelKey: 'demo.phone', props: { readonlyMask: 'last4' } }], device: { h5: { overrides: [] } } },
  rules: { dependencies: [{ source: 'contactEnabled', equals: true, target: 'phone', effect: 'required', value: true }] }, binding: {}, auth: {},
};
interface Paths { '/customers': { post: { requestBody: { content: { 'application/json': FormValues } }; responses: { 200: { content: { 'application/json': FormValues } } } } } }
const i18n = createI18n();
i18n.addLanguagePack('zh-CN', { 'demo.code': '客户号', 'demo.name': '客户名称', 'demo.contact': '填写联系方式', 'demo.contactHelp': '勾选后，手机号码为必填项。', 'demo.phone': '手机号码', 'demo.saved': '保存成功' });

function DemoContent({ readonly = false, invalid = false }: { readonly?: boolean; invalid?: boolean }) {
  const [record, setRecord] = useState<FormValues>({ code: 'sample', name: '示例客户', phone: '13812345678' });
  const [result, setResult] = useState('');
  const [client] = useState(() => new PlatformClient({ baseUrl: 'https://yue.mock', transport: createMockTransport([{ method: 'POST', path: '/customers', respond: async (request) => {
    const body = await request.json() as FormValues;
    if (body.name === '服务端错误' || invalid) return Response.json({ type: 'https://qian.dev/problems/protocol/INPUT_INVALID', title: '输入无效', status: 400, code: 'INPUT_INVALID', message: '名称已被保留', trace_id: 'a'.repeat(32), pointer: '#/name', errors: [{ code: 'INPUT_INVALID', message: '名称已被保留', pointer: '#/name' }] }, { status: 400 });
    return Response.json(body);
  } }]) }));
  const modal = useModal(i18n);
  const drawer = useDrawer(i18n);
  const message = useMessage();
  const confirm = useConfirm();
  const detail = <SchemaForm schema={schema} i18n={i18n} initialValues={record} mode="readonly" />;
  const popup = usePopup(i18n, <Button>浮层详情</Button>, { title: '只读详情', content: detail });
  const form = (values: FormValues, close: () => void) => <SchemaForm schema={schema} i18n={i18n} initialValues={values} onSubmit={async (data) => {
    const saved = await client.api<Paths>().POST('/customers', { body: data });
    setRecord(saved.data!); setResult('已保存：' + String(saved.data!.name)); message.success('demo.saved'); close();
  }} />;
  if (readonly) return detail;
  return <section aria-label="SchemaForm 示例" className="space-y-4">
    <div className="flex flex-wrap gap-2">
      <Button onClick={() => modal.open({ title: '新建客户', description: '客户号只接受小写字母和数字', content: form({}, modal.close) })}>新建客户</Button>
      <Button onClick={() => drawer.open({ title: '编辑客户', content: form(record, drawer.close) })}>编辑客户</Button>
      <Button onClick={() => modal.open({ title: '只读详情', content: detail })}>只读详情</Button>
      <Button onClick={() => { void confirm({ title: '确认操作', description: '确认后显示统一消息' }).then((ok) => { if (ok) message.info('demo.saved'); }); }}>确认操作</Button>
    </div>
    <p role="status">{result || '通过统一表单与 mock 请求链验证新增、编辑和详情。'}</p>
    {modal.holder}{drawer.holder}{popup.holder}
  </section>;
}

export default function SchemaFormDemo(props: { readonly?: boolean; invalid?: boolean }) {
  return <FeedbackProvider i18n={i18n}><DemoContent {...props} /></FeedbackProvider>;
}
