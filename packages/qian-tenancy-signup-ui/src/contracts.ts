import contract from './http-contract.generated.json';
import type { FormSchema, UiNode } from '@idadawn/yue-contracts';
import type { paths } from './api.generated';

type Json<P extends keyof paths, M extends 'get' | 'post', C extends number> = paths[P][M] extends { responses: Record<C, { content: { 'application/json': infer T } }> } ? T : never;
export type Applicant = Json<'/api/signup/session', 'get', 200>;
export type Verification = Json<'/api/signup/verifications', 'post', 202>;
export type Availability = Json<'/api/signup/code-availability', 'get', 200>;
export type MyApplication = Json<'/api/signup/me/applications', 'get', 200>['items'][number];
export type ApplicationStatus = MyApplication['status'];
export type ApplicationRow = Json<'/api/signup/applications', 'get', 200>['items'][number];
export type ApplicationCounts = Json<'/api/signup/applications', 'get', 200>['counts'];
export type ApplicationDetail = Json<'/api/signup/applications/{id}', 'get', 200>;
export type Check = ApplicationDetail['checks'][number];
export type StateMachine = Json<'/api/signup/meta/state-machine', 'get', 200>;
export type SubmitRequest = paths['/api/signup/me/applications']['post']['requestBody']['content']['application/json'];
export type ApproveRequest = paths['/api/signup/applications/{id}:approve']['post']['requestBody']['content']['application/json'];
export type RejectRequest = paths['/api/signup/applications/{id}:reject']['post']['requestBody']['content']['application/json'];

/** 申请状态：进行中的三种占用租户号与申请人名额（规格 §3）；申请人一侧把开通失败也显示为开通中，不暴露内部原因。 */
export const statuses = ['submitted', 'provisioning', 'approved', 'failed', 'rejected', 'withdrawn'] as const satisfies readonly ApplicationStatus[];
export const openStatuses: readonly ApplicationStatus[] = ['submitted', 'provisioning', 'failed'];
export const sizes = ['1-20', '21-100', '101-500', '501-1000', '1000+'] as const;
// 行业是契约里的自由文本（最长 64）；页面给出一组稳定代码，文案按语言包显示，运营页遇到未知值原样显示。
export const industries = ['software', 'manufacturing', 'retail', 'education', 'finance', 'healthcare', 'other'] as const;

type ObjectNode = Record<string, unknown>;
function object(value: unknown): ObjectNode {
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new Error('signup.contract.invalid');
  return value as ObjectNode;
}
function dereference(value: unknown, depth = 0): unknown {
  if (depth > 40) throw new Error('signup.contract.depth');
  if (Array.isArray(value)) return value.map((item) => dereference(item, depth + 1));
  if (!value || typeof value !== 'object') return value;
  const node = object(value);
  if (typeof node.$ref === 'string') {
    if (!node.$ref.startsWith('#/')) throw new Error('signup.contract.reference');
    let source: unknown = contract;
    for (const key of node.$ref.slice(2).split('/')) source = object(source)[key.replaceAll('~1', '/').replaceAll('~0', '~')];
    const { $ref: _ref, ...siblings } = node;
    return dereference({ ...object(source), ...siblings }, depth + 1);
  }
  return Object.fromEntries(Object.entries(node).map(([key, item]) => [key, dereference(item, depth + 1)]));
}
/** 契约里某个 POST 接口的请求体 Schema（已解引用）。 */
function requestBody(path: keyof paths): ObjectNode {
  const operation = object(object(object(contract.paths)[path]).post);
  return object(dereference(object(object(object(operation.requestBody).content)['application/json']).schema));
}
/** 表单的数据 Schema 取自契约的请求体（与 qian-tenancy-ui 同一做法），页面只给布局与少量本地约束。 */
function requestForm(path: keyof paths, id: string, layout: UiNode[]): FormSchema {
  const data = requestBody(path);
  if (data.type !== 'object' || data.additionalProperties !== false) throw new Error('signup.contract.request');
  return { $schema: 'https://schema.qian.dev/form/v1', version: '1.0.0', id, data: data as FormSchema['data'], ui: { layout }, rules: {}, binding: {}, auth: {} };
}
type Properties = FormSchema['data']['properties'];
function reshape(form: FormSchema, change: { omit?: string[]; require?: string[]; properties?: Properties }): FormSchema {
  const properties = Object.fromEntries(Object.entries({ ...form.data.properties, ...change.properties }).filter(([key]) => !change.omit?.includes(key)));
  const required = [...new Set([...(Array.isArray(form.data.required) ? form.data.required as string[] : []), ...(change.require ?? [])])].filter((key) => key in properties);
  return { ...form, data: { ...form.data, properties, required } };
}
const property = (path: keyof paths, key: string) => object(object(requestBody(path).properties)[key]);

/** 短信验证码的约束取自核验接口的请求体；表单字段叫 smsCode，避免与企业的租户号 code 同名。 */
const smsCode: Properties = { smsCode: property('/api/signup/session', 'code') };
/** 进度页的核验表单：手机号与验证码同在一张表单，验证码由验证码右侧的按钮发送；挑战编号由页面在提交时补上。 */
export const verifySchema: FormSchema = { ...reshape(requestForm('/api/signup/verifications', 'qian.tenancy.signup.verify', []), { require: ['smsCode'], properties: smsCode }),
  ui: { layout: [{ field: 'phone', labelKey: 'signup.phone', props: { autoComplete: 'tel' } }, { field: 'smsCode', labelKey: 'signup.code', props: { autoComplete: 'one-time-code' } }] } };

const secret = { type: 'password', autoComplete: 'new-password' };
function company(marker: number): UiNode {
  return { labelKey: 'signup.companySection', descriptionKey: 'signup.companySectionHint', props: { marker }, children: [
    { field: 'companyName', labelKey: 'signup.companyName', props: { autoComplete: 'organization' } },
    { field: 'code', labelKey: 'signup.tenantCode', descriptionKey: 'signup.tenantCodeHint' },
    { field: 'industry', labelKey: 'signup.industry', props: { optionLabelKey: 'signup.industries' } },
    // 企业规模是契约的五档（原型画了四档），半栏放不下五个等分项，独占一行。
    { field: 'size', labelKey: 'signup.size', widget: 'qian.segmented', props: { optionLabelKey: 'signup.sizes', span: 'full' } },
    { field: 'description', labelKey: 'signup.description', widget: 'qian.textarea' },
  ] };
}
const submit = requestForm('/api/signup/me/applications', 'qian.tenancy.signup.apply', []);
// 行业在页面上限定为一组代码；同意条款是表单外的勾选框（与操作按钮同一行），提交时由页面补上。
const industry: Properties = { industry: { type: 'string', enum: [...industries] } };
/**
 * 没有已有账号会话：账号区填姓名、邮箱、手机号与短信验证码、密码与确认密码（新账号必填姓名与密码，规格 §5.1）。
 * 手机号、验证码与确认密码只在页面使用：提交时先用验证码换申请人会话，再提交申请，三者都不进申请请求体。
 * verified 不渲染，只驱动联动：核验通过后手机号只读、验证码收起（会话建立后验证码已作废）。
 */
export const accountSchema: FormSchema = {
  ...reshape(submit, { omit: ['acceptTerms'], require: ['displayName', 'phone', 'smsCode', 'password', 'passwordConfirm'],
    properties: { ...industry, phone: property('/api/signup/verifications', 'phone'), ...smsCode, passwordConfirm: { type: 'string', minLength: 1, maxLength: 1024 }, verified: { type: 'boolean' } } }),
  ui: { layout: [
    { labelKey: 'signup.accountSection', descriptionKey: 'signup.accountSectionHint', props: { marker: 1 }, children: [
      { field: 'displayName', labelKey: 'signup.displayName', props: { autoComplete: 'name' } },
      { field: 'email', labelKey: 'signup.email', descriptionKey: 'signup.emailHint', props: { autoComplete: 'email' } },
      { field: 'phone', labelKey: 'signup.phone', props: { autoComplete: 'tel' } },
      { field: 'smsCode', labelKey: 'signup.code', props: { autoComplete: 'one-time-code' } },
      { field: 'password', labelKey: 'signup.password', descriptionKey: 'signup.passwordHint', props: secret },
      { field: 'passwordConfirm', labelKey: 'signup.passwordConfirm', props: secret },
    ] },
    company(2),
  ] },
  rules: { dependencies: [
    { source: 'verified', equals: true, target: 'phone', effect: 'disabled', value: true },
    { source: 'verified', equals: true, target: 'smsCode', effect: 'visible', value: false },
    { source: 'verified', equals: true, target: 'smsCode', effect: 'required', value: false },
  ] },
};
/** 已有账号会话：不建新账号、不改密码（契约要求不得带姓名与密码），账号区整节不显示，企业信息是第 1 节。 */
export const existingAccountSchema: FormSchema = { ...reshape(submit, { omit: ['displayName', 'password', 'email', 'acceptTerms'], properties: industry }), ui: { layout: [company(1)] } };
/** 企业信息的字段：已有账号核验后换成已有账号表单时，只把这些值带过去。 */
export const companyFields = ['companyName', 'code', 'industry', 'size', 'description'] as const;

/** 审核抽屉的开通设置。到期日的标签与说明随开通方式变化，由页面在翻译时按当前方式取文案。 */
export const approveSchema = requestForm('/api/signup/applications/{id}:approve', 'qian.tenancy.signup.approve', [
  { field: 'code', labelKey: 'applications.codeField', descriptionKey: 'applications.codeHint' },
  { field: 'plan', labelKey: 'applications.plan', widget: 'qian.segmented', props: { optionLabelKey: 'applications.plans' } },
  { field: 'validUntil', labelKey: 'applications.validUntil', descriptionKey: 'applications.validUntilHint' },
  { field: 'users', labelKey: 'applications.users', descriptionKey: 'applications.usersHint' },
  { field: 'note', labelKey: 'applications.note', descriptionKey: 'applications.noteHint', widget: 'qian.textarea' },
]);
/** 驳回原因的长度约束取自驳回接口的请求体（原因必填，对申请人可见）。 */
export const reasonLimit = { min: Number(property('/api/signup/applications/{id}:reject', 'reason').minLength ?? 1), max: Number(property('/api/signup/applications/{id}:reject', 'reason').maxLength ?? 500) };

/** 状态机元数据给出的确认要求；元数据缺席时一律确认（通过、驳回在契约里标 humanOnly + confirm）。 */
export function needsConfirmation(machine: StateMachine | undefined, action: string): boolean {
  return machine?.actions.find((item) => item.name === action)?.agent.requiresConfirmation ?? true;
}
/** 契约把整数写成「字符串或整数」；页面只接受安全的非负整数，其余按缺席处理。 */
export function count(value: unknown): number | null {
  const parsed = typeof value === 'string' ? Number(value) : value;
  return typeof parsed === 'number' && Number.isSafeInteger(parsed) && parsed >= 0 ? parsed : null;
}
