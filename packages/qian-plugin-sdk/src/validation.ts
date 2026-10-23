import { ShellError } from './errors';
import { uiSchema } from './ui-schema.generated';
import { Ajv2020, type ValidateFunction } from 'ajv/dist/2020.js';
import { SLOT_IDS, type SlotDescriptor, type UiManifest } from './contracts';

export function immutable<T>(value: T): T {
  const cloned = structuredClone(value);
  const freeze = (item: unknown): void => { if (item && typeof item === 'object') { Object.values(item).forEach(freeze); Object.freeze(item); } };
  freeze(cloned);
  return cloned;
}
const ajv = new Ajv2020({ strict: true, allErrors: true });

/** 每个版本化插槽有独立 $id、输入 Schema 与 TS 输出；目录可直接供工具索引。 */
export const slotContracts = immutable(Object.fromEntries(SLOT_IDS.map((slot) => [slot, {
  $schema: 'https://json-schema.org/draft/2020-12/schema', $id: `https://qian.dev/schemas/${slot}`,
  ...uiSchema.$defs.contribution, $defs: uiSchema.$defs,
  properties: { ...uiSchema.$defs.contribution.properties, slot: { const: slot } },
}])));
const validators = new Map<string, ValidateFunction>(Object.entries(slotContracts).map(([key, schema]) => [key, ajv.compile(schema)]));
export function validateSlot(value: unknown): asserts value is SlotDescriptor {
  const slot = value && typeof value === 'object' ? Reflect.get(value, 'slot') : undefined;
  const validate = typeof slot === 'string' && validators.get(slot);
  if (!validate || !validate(value)) throw new ShellError('INPUT_INVALID', {}, '/contributions');
  if ((value as SlotDescriptor).route) validateRoute((value as SlotDescriptor).route!);
  if ((value as SlotDescriptor).parameters) compileParameters((value as SlotDescriptor).parameters!);
}
const manifestValidator = ajv.compile(uiSchema);
export function validateManifest(value: unknown): asserts value is UiManifest {
  if (!manifestValidator(value)) throw new ShellError('INPUT_INVALID', {}, '/ui');
  // 公共 Schema 保留静态索引词表；当前 MF2 加载只实现显式 bridge、pages.nav 与六类版本插槽。
  const parsed = value as { bridge?: unknown; widgets?: unknown[]; settingsCards?: unknown[] };
  if (!parsed.bridge) throw new ShellError('INPUT_INVALID', {}, '/bridge');
  if (parsed.widgets?.length) throw new ShellError('INPUT_INVALID', {}, '/widgets');
  if (parsed.settingsCards?.length) throw new ShellError('INPUT_INVALID', {}, '/settingsCards');
  const manifest = value as UiManifest;
  for (const target of manifest.bridge.navigate) validateRoute(target);
  const slots = [...manifest.contributions ?? [], ...pageContributions(manifest)];
  const names = new Set<string>();
  const routes = new Set<string>();
  for (const contribution of slots) {
    validateSlot(contribution);
    const key = `${contribution.slot}:${contribution.id}`;
    if (names.has(key) || !manifest.federation.exposes.includes(contribution.expose) || !manifest.bridge.slots.includes(contribution.slot)) throw new ShellError('INPUT_INVALID', {}, '/contributions');
    names.add(key);
    if (contribution.slot === 'qian.shell.nav/v1' && contribution.route) {
      if (routes.has(contribution.route)) throw new ShellError('INPUT_INVALID', {}, '/contributions');
      routes.add(contribution.route);
    }
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
