import { ShellError } from './errors';
import { type Context, type Disposer } from '@idadawn/qian-runtime';
import type { JsonValue } from '@idadawn/yue-sdk';
import type { BridgeCaller, ClientActionDescriptor, DeepReadonly, RemoteBridge, RemoteRegistration, ShellContext, ShellHostOptions, SlotContribution, SlotId } from './contracts';
import { compileParameters, immutable, pageContributions, validateSlot } from './validation';

export type { BridgeCaller } from './contracts';
interface Session { registration: RemoteRegistration; context: Context; active: boolean }
interface InvokePaths {
  '/api/capabilities/{plugin}/{name}/invoke': { post: {
    parameters: { path: { plugin: string; name: string }; header?: { 'Idempotency-Key'?: string } };
    requestBody: { content: { 'application/json': JsonValue } };
    responses: { 200: { content: { 'application/json': unknown } } };
  } };
}
export const slotKey = (slot: SlotId) => `ShellSlot/${slot}`;

export class BridgeDispatcher {
  readonly #sessions = new WeakMap<BridgeCaller, Session>();
  readonly #options: ShellHostOptions;
  constructor(options: ShellHostOptions) { this.#options = options; }

  /** 宿主接线使用；不得把 Dispatcher 实例本身挂到 window。 */
  open(registration: RemoteRegistration, context: Context): { bridge: RemoteBridge; revoke: Disposer } {
    const caller = Object.freeze({ remoteId: registration.id, scope: registration.scope });
    const session = { registration, context, active: true };
    this.#sessions.set(caller, session);
    const bridge: RemoteBridge = Object.freeze<RemoteBridge>({
      caller,
      getContext: () => this.getContext(caller),
      navigate: (target) => this.navigate(caller, target),
      notify: (level, message) => this.notify(caller, level, message),
      invoke: <T>(id: string, input: JsonValue, options?: { idempotencyKey?: string; signal?: AbortSignal }) => this.invoke<T>(caller, id, input, options),
      registerClientAction: (descriptor, execute) => this.registerClientAction(caller, descriptor, execute),
    });
    return { bridge, revoke: () => { session.active = false; this.#sessions.delete(caller); } };
  }

  /** 只暴露调用入口，不暴露 open、session 或宿主回调。 */
  publicApi() {
    return Object.freeze({
      getContext: (caller: BridgeCaller) => this.getContext(caller),
      navigate: (caller: BridgeCaller, target: string) => this.navigate(caller, target),
      notify: (caller: BridgeCaller, level: Parameters<RemoteBridge['notify']>[0], message: string) => this.notify(caller, level, message),
      invoke: <T>(caller: BridgeCaller, id: string, input: JsonValue, options?: { idempotencyKey?: string; signal?: AbortSignal }) => this.invoke<T>(caller, id, input, options),
      registerClientAction: (caller: BridgeCaller, descriptor: ClientActionDescriptor, execute: (input: JsonValue) => unknown) => this.registerClientAction(caller, descriptor, execute),
    });
  }

  #require(caller: BridgeCaller, slot?: SlotId): Session {
    const session = caller && this.#sessions.get(caller);
    const current = this.#options.getContext();
    const currentScope = current.tenant ? `tenant:${current.tenant.id}` : 'global';
    if (!session?.active || !this.#options.isMounted(session.registration.id, session.registration.scope)
      || !this.#options.isEntitled(session.registration.id, session.registration.scope)
      || (session.registration.scope !== 'global' && session.registration.scope !== currentScope)) throw new ShellError('NOT_FOUND');
    if (slot && !session.registration.manifest.bridge.slots.includes(slot)) throw new ShellError('INSUFFICIENT_SCOPE');
    return session;
  }

  getContext(caller: BridgeCaller): DeepReadonly<ShellContext> { this.#require(caller); return immutable(this.#options.getContext()); }
  navigate(caller: BridgeCaller, target: string): void {
    const { registration } = this.#require(caller, 'qian.shell.nav/v1');
    if (typeof target !== 'string') throw new ShellError('INPUT_INVALID');
    let destination = target;
    if (target.startsWith('qian:')) {
      // 与 Qian.Capabilities NavigationContribution.Describe 的资源标识一致。
      const urn = /^qian:([^:]+):([a-z][a-z0-9.-]+):page:(.+)$/.exec(target);
      if (!urn || urn[1] !== this.#options.getContext().tenant?.id || urn[2] !== registration.id) throw new ShellError('NOT_FOUND');
      try { destination = decodeURIComponent(urn[3]!); } catch { throw new ShellError('INPUT_INVALID'); }
    }
    if (!registration.manifest.bridge.navigate.includes(destination)) throw new ShellError('INSUFFICIENT_SCOPE');
    const route = [...pageContributions(registration.manifest), ...registration.manifest.contributions ?? []].find((item) => item.slot === 'qian.shell.nav/v1' && item.route === destination);
    if (route && !this.#options.hasPermission(route.permission, registration.scope)) throw new ShellError('NOT_FOUND');
    this.#options.navigate(destination);
  }
  notify(caller: BridgeCaller, level: Parameters<RemoteBridge['notify']>[0], message: string): void {
    const { registration } = this.#require(caller);
    if (!registration.manifest.bridge.notify) throw new ShellError('INSUFFICIENT_SCOPE');
    if (!['info', 'success', 'warning', 'error'].includes(level) || typeof message !== 'string' || !message.trim() || message.length > 2048) throw new ShellError('INPUT_INVALID');
    this.#options.notify(level, message);
  }
  async invoke<T>(caller: BridgeCaller, id: string, input: JsonValue, options: { idempotencyKey?: string; signal?: AbortSignal } = {}): Promise<T> {
    const { registration } = this.#require(caller, 'qian.shell.command/v1');
    const capability = registration.manifest.bridge.capabilities.find((entry) => entry.id === id);
    if (!capability || !this.#options.hasPermission(capability.permission, registration.scope)) throw new ShellError('NOT_FOUND');
    const initialContext = this.#options.getContext();
    const [plugin, name] = id.split('/');
    if (!plugin || !name) throw new ShellError('INPUT_INVALID');
    let body = immutable(input);
    let idempotencyKey: string | undefined;
    if (capability.sideEffect === 'write') {
      if (!body || typeof body !== 'object' || Array.isArray(body)) throw new ShellError('INPUT_INVALID');
      const declaredKey = body.idempotencyKey;
      if (declaredKey !== undefined && typeof declaredKey !== 'string') throw new ShellError('INPUT_INVALID');
      if (declaredKey !== undefined && options.idempotencyKey !== undefined && declaredKey !== options.idempotencyKey) throw new ShellError('INPUT_INVALID');
      idempotencyKey = options.idempotencyKey ?? declaredKey ?? crypto.randomUUID();
      body = { ...body, idempotencyKey };
    }
    if (idempotencyKey && !/^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(idempotencyKey)) throw new ShellError('INPUT_INVALID');
    const result = await this.#options.client.api<InvokePaths>().POST('/api/capabilities/{plugin}/{name}/invoke', {
      params: { path: { plugin, name }, header: { 'Idempotency-Key': idempotencyKey } }, body, signal: options.signal,
    });
    this.#require(caller, 'qian.shell.command/v1');
    const current = this.#options.getContext();
    if (initialContext.tenant?.id !== current.tenant?.id || initialContext.membership?.id !== current.membership?.id) throw new ShellError('NOT_FOUND');
    return result.data as T;
  }
  registerClientAction(caller: BridgeCaller, descriptor: ClientActionDescriptor, execute: (input: JsonValue) => unknown): Disposer {
    const { registration, context } = this.#require(caller, 'qian.shell.client-action/v1');
    const declared = registration.manifest.contributions?.find((item) => item.slot === 'qian.shell.client-action/v1' && item.id === descriptor?.id);
    if (!declared || !this.#options.hasPermission(declared.permission, registration.scope)) throw new ShellError('NOT_FOUND');
    validateSlot({ ...descriptor, slot: 'qian.shell.client-action/v1', expose: declared.expose });
    if (descriptor.permission !== declared.permission || JSON.stringify(descriptor.parameters) !== JSON.stringify(declared.parameters) || typeof execute !== 'function') throw new ShellError('INPUT_INVALID');
    const validate = compileParameters(declared.parameters!);
    const implementation = (input: JsonValue) => {
      this.#require(caller, 'qian.shell.client-action/v1');
      if (!this.#options.hasPermission(declared.permission, registration.scope)) throw new ShellError('NOT_FOUND');
      if (!validate(input)) throw new ShellError('INPUT_INVALID');
      return execute(immutable(input));
    };
    // 动态动作仍然属于本次 Apply；ctx.provide 自动管理撤销。
    return context.provide<SlotContribution>(slotKey('qian.shell.client-action/v1'), descriptor.id, {
      descriptor: immutable({ ...declared, ...descriptor }), remoteId: registration.id, scope: registration.scope,
      implementation, bridge: this.openView(caller),
    });
  }
  private openView(caller: BridgeCaller): RemoteBridge {
    return Object.freeze<RemoteBridge>({ caller, getContext: () => this.getContext(caller), navigate: (target) => this.navigate(caller, target), notify: (level, message) => this.notify(caller, level, message),
      invoke: <T>(id: string, input: JsonValue, options?: { idempotencyKey?: string; signal?: AbortSignal }) => this.invoke<T>(caller, id, input, options),
      registerClientAction: (descriptor, execute) => this.registerClientAction(caller, descriptor, execute) });
  }
}
export type PublicBridge = ReturnType<BridgeDispatcher['publicApi']>;
