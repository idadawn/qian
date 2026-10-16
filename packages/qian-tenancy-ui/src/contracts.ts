import contract from './http-contract.generated.json';
import type { FormSchema, UiNode } from '@idadawn/yue-contracts';
import type { paths } from './api.generated';

export type Tenant = NonNullable<paths['/api/tenancy/tenants']['get']['responses'][200]['content']['application/json']['items']>[number];
export type StateMachine = paths['/api/tenancy/meta/state-machine']['get']['responses'][200]['content']['application/json'];
export type Quota = paths['/api/tenancy/tenants/{id}/quota']['get']['responses'][200]['content']['application/json'];
export type CreateRequest = paths['/api/tenancy/tenants']['post']['requestBody']['content']['application/json'];
export type UpdateRequest = paths['/api/tenancy/tenants/{id}']['put']['requestBody']['content']['application/json'];
export type ActivateRequest = paths['/api/tenancy/tenants/{id}:activate']['post']['requestBody']['content']['application/json'];
type ObjectNode = Record<string, unknown>;
function object(value: unknown): ObjectNode {
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new Error('tenant.contract.invalid');
  return value as ObjectNode;
}
function dereference(value: unknown, depth = 0): unknown {
  if (depth > 40) throw new Error('tenant.contract.depth');
  if (Array.isArray(value)) return value.map((item) => dereference(item, depth + 1));
  if (!value || typeof value !== 'object') return value;
  const node = object(value);
  if (typeof node.$ref === 'string') {
    if (!node.$ref.startsWith('#/')) throw new Error('tenant.contract.reference');
    let source: unknown = contract;
    for (const key of node.$ref.slice(2).split('/')) source = object(source)[key.replaceAll('~1', '/').replaceAll('~0', '~')];
    const { $ref: _ref, ...siblings } = node;
    return dereference({ ...object(source), ...siblings }, depth + 1);
  }
  return Object.fromEntries(Object.entries(node).map(([key, item]) => [key, dereference(item, depth + 1)]));
}
export function requestForm(path: keyof paths, method: 'post' | 'put', id: string, layout: UiNode[]): FormSchema {
  const operation = object(object(object(contract.paths)[path])[method]);
  const schema = object(object(object(operation.requestBody).content)['application/json']).schema;
  const data = object(dereference(schema));
  if (data.type !== 'object' || data.additionalProperties !== false) throw new Error('tenant.contract.request');
  return { $schema: 'https://schema.qian.dev/form/v1', version: '1.0.0', id, data: data as FormSchema['data'], ui: { layout }, rules: {}, binding: {}, auth: {} };
}
const quota: UiNode = { field: 'quotas', children: [{ field: 'users', labelKey: 'tenant.quota', descriptionKey: 'tenant.quotaHint' }] };
export const createSchema = requestForm('/api/tenancy/tenants', 'post', 'qian.tenancy.create', [
  { field: 'code', labelKey: 'tenant.code', descriptionKey: 'tenant.codeHint' }, { field: 'name', labelKey: 'tenant.name' },
  { field: 'administrator', children: [{ field: 'membershipId', labelKey: 'tenant.membership' }, { field: 'subjectId', labelKey: 'tenant.subject' }] }, quota,
]);
export const editSchema = requestForm('/api/tenancy/tenants/{id}', 'put', 'qian.tenancy.edit', [{ field: 'name', labelKey: 'tenant.name' }, quota]);
export const activateSchema = requestForm('/api/tenancy/tenants/{id}:activate', 'post', 'qian.tenancy.activate', [{ field: 'trial', labelKey: 'tenant.trial' }, { field: 'validUntil', labelKey: 'tenant.validUntil', descriptionKey: 'tenant.validUntilHint' }]);
export const detailSchema: FormSchema = { ...createSchema, id: 'qian.tenancy.detail', ui: { layout: [{ field: 'code', labelKey: 'tenant.code' }, { field: 'name', labelKey: 'tenant.name' }] } };

export type TenantAction = 'activate' | 'suspend' | 'resume';
export function legalActions(machine: StateMachine, status: Tenant['status']): TenantAction[] {
  const supported = new Set(['activate', 'suspend', 'resume']);
  return machine.transitions.filter((transition) => {
    const from = Array.isArray(transition.from) ? transition.from : [transition.from];
    const to = Array.isArray(transition.to) ? transition.to : [transition.to];
    return supported.has(transition.action) && (from.includes(status) || from.includes('any-s1')) && !to.every((next) => next === status)
      && machine.actions.some((action) => action.name === transition.action && action.available !== false);
  }).map((transition) => transition.action as TenantAction);
}
