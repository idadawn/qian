import { Ajv2020 } from 'ajv/dist/2020';
import { expect, it } from 'vitest';
import { errorDetailSchema, sseEventSchema, type SseEvent } from './index';

const ajv = new Ajv2020({ strict: false });
ajv.addSchema(errorDetailSchema, new URL('./error-detail.schema.json', sseEventSchema.$id).href);
const validate = ajv.compile(sseEventSchema);

it('九类 SSE 事件以 Schema 验证，未知事件与携带令牌的信封被拒绝', () => {
  const payloads = {
    text: { text: '内容' }, recommend: { items: [{ title: '建议', value: 'value' }] },
    toolCall: { callId: '1', name: 'read', arguments: {} }, toolResult: { callId: '1', result: {} },
    artifact: { id: '1', title: '文件', uri: '/artifact/1' }, clientAction: { name: 'open', parameters: {} },
    progress: { value: 1, messageKey: 'progress.done' }, end: { reason: 'done' },
    error: { type: 'https://qian.dev/problems/protocol/INPUT_INVALID', code: 'INPUT_INVALID', title: '输入无效', message: '输入无效', status: 400, trace_id: 'a'.repeat(32) },
  };
  for (const [type, payload] of Object.entries(payloads)) expect(validate({ version: 'v1', id: '1', type, payload }), JSON.stringify(validate.errors)).toBe(true);
  expect(validate({ version: 'v1', id: '1', type: 'unknown', payload: {} })).toBe(false);
  expect(validate({ version: 'v1', id: '1', type: 'text', payload: { text: 'ok' }, accessToken: 'secret' })).toBe(false);
});

it('生成类型可按事件 type 缩窄，错误事件保留 ErrorDetail 字段', () => {
  const event: SseEvent = { version: 'v1', id: '1', type: 'clientAction', payload: { name: 'open', parameters: { id: 'x' } } };
  expect(event.payload.name).toBe('open');
  expect(validate({ version: 'v1', id: '1', type: 'error', payload: { code: 'INVALID' } })).toBe(false);
});
