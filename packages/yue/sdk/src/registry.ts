import type { WidgetMeta } from '@idadawn/yue-contracts';
export type { WidgetMeta } from '@idadawn/yue-contracts';
export type JsonValue = null | boolean | number | string | JsonValue[] | { [key: string]: JsonValue };
export type JsonSchema = Record<string, JsonValue>;
export interface RegistrationMeta {
  id: string;
  title: string;
  description?: string;
  permission?: string;
  [key: string]: JsonValue | undefined;
}
export interface Registration<M = RegistrationMeta, T = unknown> { meta: M; implementation?: T }

function snapshot<T>(value: T): T {
  return JSON.parse(JSON.stringify(value, (_key, item: unknown) => {
    if (typeof item === 'function' || typeof item === 'symbol' || typeof item === 'bigint'
      || (typeof item === 'number' && !Number.isFinite(item))) throw new TypeError('sdk.error.registryMetadata');
    return item;
  })) as T;
}

/** 每个宿主可创建独立注册表；返回释放函数，便于插件卸载。 */
export class Registry<M = RegistrationMeta, T = unknown> {
  private readonly entries = new Map<string, Registration<M, T>>();
  private readonly key: (meta: M) => string;
  constructor(key: (meta: M) => string = (meta) => (meta as RegistrationMeta).id) { this.key = key; }
  register(definition: Registration<M, T>): () => void {
    const id = this.key(definition.meta);
    if (!id || this.entries.has(id)) throw new Error(`sdk.error.registryConflict: ${id}`);
    const entry = { ...definition, meta: snapshot(definition.meta) };
    this.entries.set(id, entry);
    return () => { if (this.entries.get(id) === entry) this.entries.delete(id); };
  }
  get(id: string): Registration<M, T> | undefined {
    const entry = this.entries.get(id);
    return entry && { ...entry, meta: snapshot(entry.meta) };
  }
  list(): M[] { return [...this.entries.values()].map(({ meta }) => snapshot(meta)); }
  toJSON(): M[] { return this.list(); }
}

export type Device = 'pc' | 'h5' | 'miniapp';
export type Presentation = 'edit' | 'readonly' | 'cell';
export interface WidgetDefinition<T = unknown> { meta: WidgetMeta; Runtime: T; Designer?: unknown }
export interface ClientActionMeta extends RegistrationMeta { parameters: JsonSchema; permission: string; description: string }

export function createRegistries() {
  const widgets = new Registry<WidgetMeta, WidgetDefinition>((meta) => meta.type);
  const registries = {
    navigation: new Registry(), settings: new Registry(), commands: new Registry(),
    shortcuts: new Registry(), previewers: new Registry(), menuTypes: new Registry(),
    clientActions: new Registry<ClientActionMeta, (parameters: JsonValue) => unknown>(), widgets,
  };
  return {
    ...registries,
    registerWidget(definition: WidgetDefinition) { return widgets.register({ meta: definition.meta, implementation: definition }); },
    listWidgets(filter?: { device?: Device; category?: string }) {
      return widgets.list().filter((meta) => (!filter?.device || meta.devices.includes(filter.device)) && (!filter?.category || meta.category === filter.category));
    },
    toJSON() { return Object.fromEntries(Object.entries(registries).map(([name, registry]) => [name, registry.toJSON()])); },
  };
}
export type PlatformRegistries = ReturnType<typeof createRegistries>;
export const platformRegistries = createRegistries();
export const registerWidget = platformRegistries.registerWidget;
export const listWidgets = platformRegistries.listWidgets;
