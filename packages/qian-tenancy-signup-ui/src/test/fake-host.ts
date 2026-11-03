import type { RequestTransport } from '@idadawn/yue-sdk';
import type { ApplicationDetail, ApplicationRow, MyApplication } from '../contracts';

/** 组件测试用的替身宿主：按契约应答开通申请接口，状态在内存里；真实宿主上的流程在 apps/qian-shell 的 e2e-auth。 */
export interface FakeHost { transport: RequestTransport; calls: { method: string; path: string; body?: unknown }[]; applications: MyApplication[]; session: boolean }
const json = (status: number, body: unknown) => new Response(JSON.stringify(body), { status, headers: { 'content-type': status >= 400 ? 'application/problem+json' : 'application/json' } });
function problem(status: number, code: string, message: string, pointer?: string) {
  const kind = status === 409 || status === 422 ? 'business' : 'protocol';
  return json(status, { type: `https://qian.dev/problems/${kind}/${code}`, title: message, status, code, message, trace_id: '0123456789abcdef0123456789abcdef', ...(pointer ? { pointer, errors: [{ code, pointer, message }] } : {}) });
}
export const machine = { states: ['submitted', 'provisioning', 'approved', 'failed', 'rejected', 'withdrawn'], terminal: ['approved', 'rejected', 'withdrawn'],
  transitions: [{ from: ['submitted'], action: 'approve', to: 'provisioning' }, { from: ['submitted'], action: 'reject', to: 'rejected' }, { from: ['failed'], action: 'retry', to: 'provisioning' }],
  actions: ['approve', 'reject', 'retry'].map((name) => ({ name, actor: 'operator', sideEffect: 'write', agent: { expose: false, requiresConfirmation: name !== 'retry' } })) };
export function application(overrides: Partial<MyApplication> = {}): MyApplication {
  return { id: '0193a1b2-0000-7000-8000-000000000001', number: 'AP-20261103-0001', status: 'submitted', companyName: '星河科技有限公司', code: 'xinghe', size: '21-100', contactName: '李雷',
    phone: '138****6021', submittedAt: '2026-11-03T06:32:00Z', industry: 'software', description: '用于项目管理', email: 'lilei@example.com', completedAt: null, plan: null, validUntil: null, rejectReason: null, ...overrides };
}
export function row(item: MyApplication, check: ApplicationRow['check'] = { kind: 'code', level: 'pass' }): ApplicationRow {
  return { id: item.id, number: item.number, status: item.status, companyName: item.companyName, code: item.code, size: item.size, contactName: item.contactName, phone: item.phone, submittedAt: item.submittedAt, industry: item.industry, check };
}
export function detail(item: MyApplication): ApplicationDetail {
  return { id: item.id, number: item.number, status: item.status, company: { name: item.companyName, code: item.code, size: item.size, industry: item.industry, description: item.description },
    applicant: { subjectId: '0193a1b2-0000-7000-8000-0000000000aa', contactName: item.contactName, phone: item.phone, existingAccount: false, email: item.email },
    checks: [{ kind: 'code', level: 'pass' }, { kind: 'duplicateName', level: 'warning', count: 1 }, { kind: 'existingMemberships', level: 'pass', count: 0 }],
    defaults: { trialDays: 30, users: 20 }, submittedAt: item.submittedAt, completedAt: null };
}

export function fakeHost(options: { existing?: string; session?: boolean; applications?: MyApplication[]; taken?: string[] } = {}): FakeHost {
  const host: FakeHost = { calls: [], applications: [...options.applications ?? []], session: options.session ?? false, transport: async () => new Response(null, { status: 500 }) };
  const applicant = () => ({ phone: '138****6021', existingAccount: !!options.existing, displayName: options.existing ?? null });
  host.transport = async (request) => {
    const url = new URL(request.url);
    const text = request.method === 'GET' || request.method === 'DELETE' ? '' : await request.text();
    const body = text ? JSON.parse(text) as Record<string, unknown> : undefined;
    host.calls.push({ method: request.method, path: url.pathname, body });
    const route = `${request.method} ${url.pathname}`;
    if (route === 'POST /api/signup/verifications') return json(202, { id: '0193a1b2-0000-7000-8000-0000000000c1', expiresAt: new Date(Date.now() + 300_000).toISOString(), resendAfter: new Date(Date.now() + 60_000).toISOString() });
    if (route === 'POST /api/signup/session') {
      if (body?.code !== '246810') return problem(400, 'SIGNUP_CODE_INVALID', '验证码不正确，还可以再试 4 次。', '#/code');
      host.session = true; return json(200, applicant());
    }
    if (route === 'GET /api/signup/session') return host.session ? json(200, applicant()) : problem(401, 'UNAUTHENTICATED', '需要登录。');
    if (route === 'DELETE /api/signup/session') { host.session = false; return new Response(null, { status: 204 }); }
    if (route === 'GET /api/signup/code-availability') {
      const code = url.searchParams.get('code') ?? '';
      if (!/^[a-z0-9]{3,32}$/.test(code)) return json(200, { code, available: false, reason: 'invalid' });
      return json(200, options.taken?.includes(code) ? { code, available: false, reason: 'taken' } : { code, available: true });
    }
    if (route === 'GET /api/signup/me/applications') return host.session ? json(200, { items: host.applications }) : problem(401, 'UNAUTHENTICATED', '需要登录。');
    if (route === 'POST /api/signup/me/applications') {
      if (options.taken?.includes(String(body?.code))) return problem(409, 'SIGNUP_CODE_TAKEN', '租户号已被占用，请换一个。', '#/code');
      const created = application({ id: '0193a1b2-0000-7000-8000-000000000002', number: 'AP-20261103-0002', companyName: String(body?.companyName), code: String(body?.code), size: String(body?.size) });
      host.applications.unshift(created); return json(201, created);
    }
    const withdraw = /^\/api\/signup\/me\/applications\/([^/:]+):withdraw$/.exec(url.pathname);
    if (request.method === 'POST' && withdraw) {
      if (!request.headers.get('content-type')?.startsWith('application/json')) return problem(415, 'UNSUPPORTED_MEDIA_TYPE', '只接受 application/json。');
      const item = host.applications.find((entry) => entry.id === withdraw[1]);
      if (!item || item.status !== 'submitted') return problem(409, 'SIGNUP_STATE_CONFLICT', '当前申请状态不允许该操作。');
      item.status = 'withdrawn'; return json(200, item);
    }
    if (route === 'GET /api/signup/meta/state-machine') return json(200, machine);
    if (route === 'GET /api/signup/applications') {
      const status = url.searchParams.get('status');
      const items = host.applications.filter((item) => !status || item.status === status);
      const counts = Object.fromEntries(['submitted', 'provisioning', 'approved', 'failed', 'rejected', 'withdrawn'].map((key) => [key, host.applications.filter((item) => item.status === key).length]));
      return json(200, { items: items.map((item) => row(item)), total: items.length, counts: { all: host.applications.length, ...counts } });
    }
    const operator = /^\/api\/signup\/applications\/([^/:]+)(?::(approve|reject|retry))?$/.exec(url.pathname);
    if (operator) {
      const item = host.applications.find((entry) => entry.id === operator[1]);
      if (!item) return problem(404, 'NOT_FOUND', '申请不存在。');
      if (!operator[2]) return json(200, detail(item));
      item.status = operator[2] === 'reject' ? 'rejected' : 'approved';
      if (operator[2] === 'reject') item.rejectReason = String(body?.reason);
      return json(200, { ...detail(item), review: { reviewer: '王敏', reviewedAt: new Date().toISOString(), plan: body?.plan ?? null, rejectReason: body?.reason ?? null, note: body?.note ?? null } });
    }
    return problem(404, 'NOT_FOUND', '未找到。');
  };
  return host;
}
