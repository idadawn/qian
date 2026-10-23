import { createInstance } from '@module-federation/enhanced/runtime';
import type { FederationTransport } from './contracts';
export type SharedDependencies = NonNullable<Parameters<typeof createInstance>[0]['shared']>;
/** 无构建插件的宿主可显式提供共享依赖实例；使用 Vite 插件时传其既有实例，禁止创建第二套 share scope。 */
export function createFederationTransport(name: string, shared: SharedDependencies): FederationTransport {
  return adaptFederation(createInstance({ name, shared, remotes: [] }));
}
export function adaptFederation(instance: Pick<ReturnType<typeof createInstance>, 'registerRemotes' | 'loadRemote' | 'preloadRemote'>): FederationTransport {
  return {
    registerRemotes: (remotes) => instance.registerRemotes(remotes.map((remote) => ({ ...remote, type: 'module' }))),
    loadRemote: <T>(id: string) => instance.loadRemote<T>(id),
    preloadRemote: (remotes) => instance.preloadRemote(remotes.map((remote) => ({ ...remote, exposes: [...remote.exposes], resourceCategory: 'all' }))),
  };
}
