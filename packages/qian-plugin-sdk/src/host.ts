import { ShellError } from './errors';
import { PluginLoader, type Context } from '@idadawn/qian-runtime';
import { satisfies, valid, validRange } from 'semver';
import { BridgeDispatcher, slotKey, type PublicBridge } from './bridge';
import type { RemoteModule, RemoteRegistration, ShellHostOptions, ShellScope, SlotContribution, SlotDescriptor, SlotId, SlotOutputs } from './contracts';
import { immutable, pageContributions, validateManifest } from './validation';

interface Entry { registration: RemoteRegistration; alias: string; descriptors: SlotDescriptor[]; loaded: Set<string> }
const identity = (id: string, scope: ShellScope) => `${scope}|${id}`;
const aliasFor = (entry: RemoteRegistration) => 'qian_' + Array.from(`${entry.id}@${entry.version}:${entry.scope}`, (char) => char.codePointAt(0)!.toString(16)).join('_');

/** qian.shell 与每个已加载远程都是实际 PluginLoader 条目。 */
export class ShellHost {
  readonly #runtime = new PluginLoader();
  readonly #options: ShellHostOptions;
  readonly #dispatcher: BridgeDispatcher;
  readonly #entries = new Map<string, Entry>();
  readonly #registered = new Set<string>();
  #root: Context | undefined;
  #tail: Promise<unknown> = Promise.resolve();
  #disposed = false;
  #revision = 0;
  readonly #listeners = new Set<() => void>();
  readonly bridge: PublicBridge;
  constructor(options: ShellHostOptions) {
    if (!valid(options.yueVersion)) throw new ShellError('INPUT_INVALID', {}, '/yueVersion');
    this.#options = options;
    this.#dispatcher = new BridgeDispatcher({ ...options, isMounted: (id, scope) => !this.#disposed && this.#entries.has(identity(id, scope)) && options.isMounted(id, scope) });
    this.bridge = this.#dispatcher.publicApi();
  }
  async start(): Promise<void> {
    if (this.#root || this.#disposed) throw new ShellError('CONFLICT');
    await this.#runtime.mount([{ entry: { id: 'shell', plugin: 'qian.shell', version: '1.0.0', scope: 'global', layer: 0, config: {} }, provides: ['Shell'],
      plugin: { name: 'qian.shell', apply: (context) => { context.provide('Shell', this.bridge); this.#root = context; context.effect(() => { this.#root = undefined; }); } } }]);
  }
  registerRemote(input: RemoteRegistration): void {
    if (!this.#root || this.#disposed) throw new ShellError('NOT_FOUND');
    if (!input || !/^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)+$/.test(input.id) || !valid(input.version)
      || (input.scope !== 'global' && !/^tenant:[A-Za-z0-9_-]+$/.test(input.scope))) throw new ShellError('INPUT_INVALID');
    validateManifest(input.manifest);
    if (!validRange(input.manifest.engines.yue)) throw new ShellError('INPUT_INVALID', {}, '/engines/yue');
    const registration = immutable(input);
    const url = new URL(registration.manifest.federation.manifest, this.#options.baseUrl);
    const allowed = new Set([new URL(this.#options.baseUrl).origin, ...this.#options.trustedOrigins ?? []]);
    if (!['https:', 'http:'].includes(url.protocol) || !allowed.has(url.origin) || url.username || url.password || url.hash || url.search) throw new ShellError('INPUT_INVALID', {}, '/federation/manifest');
    const key = identity(input.id, input.scope);
    if (this.#entries.has(key)) throw new ShellError('CONFLICT');
    this.#entries.set(key, { registration, alias: aliasFor(registration), descriptors: [...pageContributions(registration.manifest), ...registration.manifest.contributions ?? []], loaded: new Set() });
    this.refreshContext();
  }
  #currentScope(): ShellScope { const tenant = this.#options.getContext().tenant; return tenant ? `tenant:${tenant.id}` : 'global'; }
  #allowed(entry: Entry, slot: SlotId): SlotDescriptor[] {
    const { registration: remote } = entry;
    if ((remote.scope !== 'global' && remote.scope !== this.#currentScope()) || !this.#options.isMounted(remote.id, remote.scope) || !this.#options.isEntitled(remote.id, remote.scope)) return [];
    return entry.descriptors.filter((item) => item.slot === slot && this.#options.hasPermission(item.permission, remote.scope));
  }
  async #version(entry: Entry): Promise<void> {
    if (satisfies(this.#options.yueVersion, entry.registration.manifest.engines.yue)) return;
    const error = new ShellError('INPUT_INVALID', {}, '/engines/yue');
    const event = { name: 'shell.remote.rejected' as const, remoteId: entry.registration.id, scope: entry.registration.scope, reason: 'engines.yue' as const, code: error.code,
      hostVersion: this.#options.yueVersion, requiredVersion: entry.registration.manifest.engines.yue };
    console.error('qian.shell.remote.rejected', event);
    await this.#options.audit(immutable(event));
    throw error;
  }
  #register(entry: Entry): void {
    if (this.#registered.has(entry.alias)) return;
    this.#options.federation.registerRemotes([{ name: entry.alias, entry: new URL(entry.registration.manifest.federation.manifest, this.#options.baseUrl).href }]);
    this.#registered.add(entry.alias);
  }
  #serial<T>(action: () => Promise<T>): Promise<T> {
    const next = this.#tail.then(() => { if (this.#disposed) throw new ShellError('NOT_FOUND'); return action(); });
    this.#tail = next.catch(() => {}); return next;
  }
  /** 页面需要哪个插槽才请求其 exposes；未挂载、无权益或无权限的远程完全不请求。 */
  loadSlot(slot: SlotId, id?: string): Promise<readonly SlotContribution[]> {
    return this.#serial(async () => {
      const selected = this.catalog(slot).filter((item) => id === undefined || item.descriptor.id === id);
      for (const entry of this.#entries.values()) {
        const allowed = this.#allowed(entry, slot).filter((item) => selected.some((choice) => choice.remoteId === entry.registration.id && choice.scope === entry.registration.scope && choice.descriptor.id === item.id));
        if (!allowed.length) continue;
        await this.#version(entry); this.#register(entry);
        for (const descriptor of allowed) {
          if (entry.loaded.has(descriptor.expose)) continue;
          const module = await this.#options.federation.loadRemote<RemoteModule>(`${entry.alias}/${descriptor.expose}`);
          // 网络等待期间租户/权益可能变化，下载完成不能直接变成挂载授权。
          if (this.#disposed || !this.#allowed(entry, slot).some((item) => item.id === descriptor.id)) continue;
          if (!module || !('default' in module)) throw new ShellError('INPUT_INVALID', {}, '/expose');
          const related = entry.descriptors.filter((item) => item.expose === descriptor.expose);
          const entryId = `${entry.registration.id}/${descriptor.expose}`;
          await this.#runtime.mount([{ entry: { id: entryId, plugin: entry.registration.id, version: entry.registration.version, scope: entry.registration.scope, layer: 1, config: {} }, provides: [],
            plugin: { name: entry.registration.id, inject: ['Shell'], apply: async (context) => {
              const { bridge, revoke } = this.#dispatcher.open(entry.registration, context);
              context.effect(revoke);
              for (const item of related.filter((item) => item.slot !== 'qian.shell.client-action/v1')) context.provide<SlotContribution>(slotKey(item.slot), item.id, {
                descriptor: immutable(item), remoteId: entry.registration.id, scope: entry.registration.scope, implementation: module.default, bridge,
              });
              const dispose = await module.activate?.(bridge);
              if (dispose !== undefined) { if (typeof dispose !== 'function') throw new ShellError('INPUT_INVALID'); context.effect(dispose); }
            } } }]);
          if (this.#runtime.getState(entryId, entry.registration.scope) !== 'Ready') {
            const failure = this.#runtime.getFailure(entryId, entry.registration.scope);
            await this.#runtime.unmount(entryId, entry.registration.scope);
            throw failure;
          }
          entry.loaded.add(descriptor.expose);
        }
      }
      return this.list(slot);
    });
  }
  /** 静态导航无需请求任何远程 manifest；目录与执行使用同一授权和遮蔽规则。 */
  catalog(slot: SlotId): readonly { descriptor: Readonly<SlotDescriptor>; remoteId: string; scope: ShellScope }[] {
    if (this.#disposed) return [];
    const result = new Map<string, { descriptor: SlotDescriptor; remoteId: string; scope: ShellScope }>();
    const entries = [...this.#entries.values()].sort((a, b) => Number(a.registration.scope !== 'global') - Number(b.registration.scope !== 'global'));
    // 与 Runtime 一致：先按当前 scope 选本地同名项，再应用权限/挂载过滤。
    // 被禁止的本地贡献不能借由退回全局同名项绕过过滤。
    for (const entry of entries) {
      if (entry.registration.scope !== 'global' && entry.registration.scope !== this.#currentScope()) continue;
      for (const descriptor of entry.descriptors.filter((item) => item.slot === slot)) result.set(descriptor.id, { descriptor, remoteId: entry.registration.id, scope: entry.registration.scope });
    }
    return [...result.values()].filter((item) => {
      const entry = this.#entries.get(identity(item.remoteId, item.scope))!;
      return this.#allowed(entry, slot).some((descriptor) => descriptor.id === item.descriptor.id);
    }).map((item) => immutable(item));
  }
  preloadSlot(slot: SlotId): Promise<void> {
    return this.#serial(async () => {
      const selected = this.catalog(slot);
      for (const entry of this.#entries.values()) {
        const allowed = this.#allowed(entry, slot).filter((item) => selected.some((choice) => choice.remoteId === entry.registration.id && choice.scope === entry.registration.scope && choice.descriptor.id === item.id));
        if (!allowed.length) continue;
        await this.#version(entry); this.#register(entry);
        await this.#options.federation.preloadRemote([{ nameOrAlias: entry.alias, exposes: [...new Set(allowed.map((item) => item.expose))] }]);
      }
    });
  }
  list<T = unknown>(slot: SlotId): readonly SlotContribution<T>[] {
    if (!this.#root || this.#disposed) return [];
    const selected = this.catalog(slot);
    return this.#root.scoped(this.#currentScope()).list<SlotContribution<T>>(slotKey(slot)).map(({ value }) => value)
      .filter((value) => selected.some((item) => item.remoteId === value.remoteId && item.scope === value.scope && item.descriptor.id === value.descriptor.id))
      .map((value) => Object.freeze({ ...value }));
  }
  exportClientActions() { return this.list('qian.shell.client-action/v1').map(({ descriptor }) => immutable({ id: descriptor.id, title: descriptor.title, description: descriptor.description, permission: descriptor.permission, parameters: descriptor.parameters })); }
  listSlot<K extends SlotId>(slot: K): readonly SlotContribution<SlotOutputs[K]>[] { return this.list<SlotOutputs[K]>(slot); }
  subscribe = (listener: () => void) => { this.#listeners.add(listener); return () => { this.#listeners.delete(listener); }; };
  getRevision = () => this.#revision;
  /** 身份/租户/权限由宿主刷新后调用；旧 bridge 每次调用都会重新检查。 */
  refreshContext(): void { this.#revision++; for (const listener of this.#listeners) listener(); }
  dumpConfig() { return this.#runtime.dumpConfig(); }
  unloadRemote(id: string, scope: ShellScope): Promise<void> {
    return this.#serial(async () => {
      const key = identity(id, scope); const entry = this.#entries.get(key);
      if (!entry) return;
      // 先撤销访问，再等待异步卸载，关闭悬空桥接调用窗口。
      this.#entries.delete(key);
      this.refreshContext();
      for (const expose of [...entry.loaded].reverse()) await this.#runtime.unmount(`${id}/${expose}`, scope);
      this.#options.federation.removeRemote?.(entry.alias); this.#registered.delete(entry.alias);
    });
  }
  async dispose(): Promise<void> {
    if (this.#disposed) return;
    this.#disposed = true;
    await this.#tail;
    await this.#runtime.dispose();
    for (const alias of this.#registered) this.#options.federation.removeRemote?.(alias);
    this.#registered.clear(); this.#entries.clear();
    this.refreshContext(); this.#listeners.clear();
  }
}
