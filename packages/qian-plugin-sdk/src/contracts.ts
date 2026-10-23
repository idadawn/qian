import type { Disposer, ScopeKey } from '@idadawn/qian-runtime';
import type { JsonSchema, JsonValue, PlatformClient } from '@idadawn/yue-sdk';
import metadata from '../package.json';

export const SDK_VERSION: string = metadata.version;
export const SLOT_IDS = ['qian.shell.nav/v1', 'qian.shell.settings/v1', 'qian.shell.user-menu/v1', 'qian.shell.command/v1', 'qian.shell.preview/v1', 'qian.shell.client-action/v1'] as const;
export type SlotId = typeof SLOT_IDS[number];
export type ShellScope = Extract<ScopeKey, 'global' | `tenant:${string}`>;
export interface ShellContext {
  tenant: { id: string; name: string } | null;
  membership: { id: string; permissions: string[] } | null;
  app: { id: string; name: string } | null;
  locale: string;
  theme: 'light' | 'dark';
}
export type DeepReadonly<T> = T extends object ? { readonly [P in keyof T]: DeepReadonly<T[P]> } : T;
export interface SlotDescriptor {
  slot: SlotId;
  id: string;
  title: string;
  description: string;
  permission: string;
  expose: string;
  route?: string;
  parameters?: JsonSchema;
}
export interface UiManifest {
  $schema: 'https://qian.dev/schemas/ui/v2';
  federation: { manifest: string; exposes: string[] };
  engines: { yue: string };
  pages?: { route: string; expose: string; permission: string; nav: { title: string; icon?: string; order?: number } }[];
  /** 与 pages 同属 ui/v2 表面的六种版本化插槽；nav 不得与 pages 重复。 */
  contributions?: SlotDescriptor[];
  bridge: {
    slots: SlotId[];
    navigate: string[];
    notify: boolean;
    capabilities: { id: string; permission: string; sideEffect: 'read' | 'write' }[];
  };
}
/** 由可信宿主提供，不能由远程模块自行声称已挂载、已购买或拥有权限。 */
export interface RemoteRegistration {
  id: string;
  version: string;
  scope: ShellScope;
  manifest: UiManifest;
}
export interface ClientActionDescriptor {
  id: string;
  title: string;
  description: string;
  permission: string;
  parameters: JsonSchema;
}
/** 只能使用受信任加载过程交付的原对象；同形对象、JSON 副本都没有 WeakMap 身份。 */
export interface BridgeCaller { readonly remoteId: string; readonly scope: ShellScope }
export interface RemoteBridge {
  /** 提供给 window.__qian.bridge 的调用方句柄；卸载时立即撤销其身份。 */
  readonly caller: BridgeCaller;
  getContext(): DeepReadonly<ShellContext>;
  navigate(target: string): void;
  notify(level: 'info' | 'success' | 'warning' | 'error', message: string): void;
  invoke<T = unknown>(capabilityId: string, input: JsonValue, options?: { idempotencyKey?: string; signal?: AbortSignal }): Promise<T>;
  registerClientAction(descriptor: ClientActionDescriptor, execute: (input: JsonValue) => unknown | Promise<unknown>): Disposer;
}
/** 暴露模块提供组件或动作；可选 activate 的清理由真实 ctx.effect 管理。 */
export interface RemoteModule<T = unknown> {
  default: T;
  activate?: (bridge: RemoteBridge) => void | Disposer | Promise<void | Disposer>;
}
export interface SlotContribution<T = unknown> {
  descriptor: Readonly<SlotDescriptor>;
  remoteId: string;
  scope: ShellScope;
  implementation: T;
  bridge: RemoteBridge;
}
/** 插槽输出与渲染输入的编译期契约；组件返回值由消费 UI 框架解释。 */
export interface RemoteViewProps { client: PlatformClient; bridge: RemoteBridge }
export interface SlotOutputs {
  'qian.shell.nav/v1': (props: RemoteViewProps) => unknown;
  'qian.shell.settings/v1': (props: RemoteViewProps & { section: string }) => unknown;
  'qian.shell.user-menu/v1': (props: { bridge: RemoteBridge; close: () => void }) => unknown;
  'qian.shell.command/v1': (input: JsonValue, bridge: RemoteBridge) => unknown | Promise<unknown>;
  'qian.shell.preview/v1': (props: RemoteViewProps & { urn: string }) => unknown;
  'qian.shell.client-action/v1': (input: JsonValue) => unknown | Promise<unknown>;
}
export interface FederationTransport {
  registerRemotes(remotes: readonly { name: string; entry: string }[]): void;
  loadRemote<T>(id: string): Promise<T | null>;
  preloadRemote(remotes: readonly { nameOrAlias: string; exposes: string[] }[]): Promise<unknown>;
  removeRemote?(name: string): void;
}
export interface ShellAuditEvent {
  name: 'shell.remote.rejected';
  remoteId: string;
  scope: ShellScope;
  reason: 'engines.yue' | 'load';
  code: string;
  hostVersion: string;
  requiredVersion: string;
}
export interface ShellHostOptions {
  yueVersion: string;
  baseUrl: string;
  /** 默认仅同源；跨源远程必须由宿主显式列入可信源。 */
  trustedOrigins?: readonly string[];
  federation: FederationTransport;
  client: PlatformClient;
  getContext: () => ShellContext;
  isMounted: (id: string, scope: ShellScope) => boolean;
  isEntitled: (id: string, scope: ShellScope) => boolean;
  hasPermission: (permission: string, scope: ShellScope) => boolean;
  navigate: (target: string) => void;
  notify: (level: 'info' | 'success' | 'warning' | 'error', message: string) => void;
  /** 宿主必须提供可观察、可持久化的审计出口；错误不能被静默吞掉。 */
  audit: (event: ShellAuditEvent) => void | Promise<void>;
}
