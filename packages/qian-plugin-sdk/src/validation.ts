import { ShellError } from './errors';
import { Ajv2020, type ValidateFunction } from 'ajv/dist/2020.js';
import { SLOT_IDS, type SlotDescriptor, type UiManifest } from './contracts';

export function immutable<T>(value: T): T {
  const cloned = structuredClone(value);
  const freeze = (item: unknown): void => { if (item && typeof item === 'object') { Object.values(item).forEach(freeze); Object.freeze(item); } };
  freeze(cloned);
  return cloned;
}
const text = { type: 'string', minLength: 1, maxLength: 256 };
const id = { ...text, pattern: '^(?:[A-Za-z0-9][A-Za-z0-9._/-]*|/(?!/)[A-Za-z0-9._/-]*)$' };
const expose = { ...text, pattern: '^[A-Za-z][A-Za-z0-9_-]*$' };
const route = { ...text, pattern: '^/(?!/)[^\\\\?#]*$' };
const parameterSchema = { type: 'object' };
const ajv = new Ajv2020({ strict: true, allErrors: true });

/** 每个版本化插槽有独立 $id、输入 Schema 与 TS 输出；目录可直接供工具索引。 */
export const slotContracts = immutable(Object.fromEntries(SLOT_IDS.map((slot) => [slot, {
  $schema: 'https://json-schema.org/draft/2020-12/schema',
  $id: `https://qian.dev/schemas/${slot}`,
  type: 'object', additionalProperties: false,
  required: ['slot', 'id', 'title', 'description', 'permission', 'expose', ...(slot === 'qian.shell.nav/v1' ? ['route'] : []), ...(slot === 'qian.shell.client-action/v1' ? ['parameters'] : [])],
  properties: { slot: { const: slot }, id, title: text, description: text, permission: text, expose, route, parameters: parameterSchema },
}])));
const validators = new Map<string, ValidateFunction>(Object.entries(slotContracts).map(([key, schema]) => [key, ajv.compile(schema)]));
export function validateSlot(value: unknown): asserts value is SlotDescriptor {
  const slot = value && typeof value === 'object' ? Reflect.get(value, 'slot') : undefined;
  const validate = typeof slot === 'string' && validators.get(slot);
  if (!validate || !validate(value)) throw new ShellError('INPUT_INVALID', {}, '/contributions');
  if ((value as SlotDescriptor).route) validateRoute((value as SlotDescriptor).route!);
  if ((value as SlotDescriptor).parameters) compileParameters((value as SlotDescriptor).parameters!);
}
const manifestValidator = ajv.compile({
  type: 'object', additionalProperties: false, required: ['$schema', 'federation', 'engines', 'bridge'],
  properties: {
    $schema: { const: 'https://qian.dev/schemas/ui/v2' },
    engines: { type: 'object', additionalProperties: false, required: ['yue'], properties: { yue: text } },
    federation: { type: 'object', additionalProperties: false, required: ['manifest', 'exposes'], properties: { manifest: { type: 'string', minLength: 1, maxLength: 2048 }, exposes: { type: 'array', minItems: 1, uniqueItems: true, items: expose } } },
    pages: { type: 'array', items: { type: 'object', additionalProperties: false, required: ['route', 'expose', 'permission', 'nav'], properties: { route, expose, permission: text, nav: { type: 'object', additionalProperties: false, required: ['title'], properties: { title: text, icon: text, order: { type: 'integer' } } } } } },
    contributions: { type: 'array', items: { type: 'object' } },
    bridge: { type: 'object', additionalProperties: false, required: ['slots', 'navigate', 'notify', 'capabilities'], properties: {
      slots: { type: 'array', uniqueItems: true, items: { enum: SLOT_IDS } }, navigate: { type: 'array', uniqueItems: true, items: { type: 'string', minLength: 1, maxLength: 256, pattern: '^/(?!/)[^\\\\?#]*$' } }, notify: { type: 'boolean' },
      capabilities: { type: 'array', items: { type: 'object', additionalProperties: false, required: ['id', 'permission', 'sideEffect'], properties: { id: { ...text, pattern: '^[a-z][a-z0-9.]+/[A-Za-z0-9._-]+$' }, permission: text, sideEffect: { enum: ['read', 'write'] } } } },
    } },
  },
});
export function validateManifest(value: unknown): asserts value is UiManifest {
  if (!manifestValidator(value)) throw new ShellError('INPUT_INVALID', {}, '/ui');
  const manifest = value as UiManifest;
  for (const target of manifest.bridge.navigate) validateRoute(target);
  const slots = [...manifest.contributions ?? [], ...pageContributions(manifest)];
  const names = new Set<string>();
  for (const contribution of slots) {
    validateSlot(contribution);
    const key = `${contribution.slot}:${contribution.id}`;
    if (names.has(key) || !manifest.federation.exposes.includes(contribution.expose) || !manifest.bridge.slots.includes(contribution.slot)) throw new ShellError('INPUT_INVALID', {}, '/contributions');
    names.add(key);
  }
  if (new Set(manifest.bridge.capabilities.map(({ id: name }) => name)).size !== manifest.bridge.capabilities.length) throw new ShellError('INPUT_INVALID', {}, '/bridge/capabilities');
}
function validateRoute(route: string): void {
  if ([...route].some((character) => character.charCodeAt(0) <= 32 || '\\?#'.includes(character)) || !route.startsWith('/') || route.startsWith('//') || route.split('/').some((segment) => segment === '.' || segment === '..')) throw new ShellError('INPUT_INVALID', {}, '/route');
}
export function pageContributions(manifest: UiManifest): SlotDescriptor[] {
  return (manifest.pages ?? []).map((page) => ({ slot: 'qian.shell.nav/v1', id: page.route, title: page.nav.title, description: page.nav.title, route: page.route, permission: page.permission, expose: page.expose }));
}
export function compileParameters(schema: object): ValidateFunction {
  try { return ajv.compile(schema); } catch { throw new ShellError('INPUT_INVALID', {}, '/parameters'); }
}
