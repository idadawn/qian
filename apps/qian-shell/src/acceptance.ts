import { Button } from '@idadawn/yue-core/shell';
import * as React from 'react';
import { PlatformClient, SDK_VERSION, type BridgeCaller, type RemoteBridge, type RemoteRegistration, type ShellScope } from '@idadawn/qian-plugin-sdk';
import type { ShellRuntime } from './runtime';

/** 只在 --mode acceptance 产物中引用；生产构建经 scripts/verify-dist.mjs 断言没有此控制面。 */
export function installAcceptance(runtime: ShellRuntime) {
  const { host, federation: { instance } } = runtime;
  let retainedWindowBridge: Pick<RemoteBridge, 'getContext' | 'navigate' | 'invoke'> | undefined;
  const control = {
    async windowCalls() {
      const remote = instance.options.remotes[0]!;
      const module = await instance.loadRemote<{ connectShell: (caller: BridgeCaller) => Pick<RemoteBridge, 'getContext' | 'navigate' | 'invoke'> }>(`${remote.name}/TenantPage`);
      retainedWindowBridge = module!.connectShell(host.list('qian.shell.nav/v1')[0]!.bridge.caller);
      const context = retainedWindowBridge.getContext();
      retainedWindowBridge.navigate('/tenants');
      const result = await retainedWindowBridge.invoke('qian.acceptance/ping', { message: 'window-bridge' }, { idempotencyKey: '11111111-1111-4111-8111-111111111111' });
      return { tenant: context.tenant?.id, frozen: Object.isFrozen(context.membership?.permissions), pathname: location.pathname, result };
    },
    windowCallerRejected() { try { retainedWindowBridge!.getContext(); return false; } catch { return true; } },
    async identity() {
      const remote = instance.options.remotes[0];
      if (!remote) throw new Error('尚未加载远程');
      const module = await instance.loadRemote<{ sharedIdentity: { Button: unknown; useState: unknown; createElement: unknown; PlatformClient: unknown; sdkVersion: string } }>(`${remote.name}/TenantPage`);
      return { button: module?.sharedIdentity.Button === Button, react: module?.sharedIdentity.useState === React.useState && module.sharedIdentity.createElement === React.createElement,
        client: module?.sharedIdentity.PlatformClient === PlatformClient, sdk: module?.sharedIdentity.sdkVersion === SDK_VERSION };
    },
    catalog: () => host.catalog('qian.shell.nav/v1'),
    dump: () => host.dumpConfig(),
    preload: () => host.preloadSlot('qian.shell.nav/v1'),
    unload: (id: string, scope: ShellScope) => host.unloadRemote(id, scope),
    register: (remote: RemoteRegistration) => host.registerRemote(remote),
    setTenant: (id: string) => { const bootstrap = runtime.getState().bootstrap; if (!bootstrap) throw new Error('尚未取得 bootstrap'); bootstrap.context.tenant = { id, name: id }; host.refreshContext(); },
    bridgeGuards() {
      const item = host.list('qian.shell.nav/v1')[0]!;
      let unknownRejected = false, scopeRejected = false;
      try { host.bridge.getContext({ remoteId: item.remoteId, scope: item.scope }); } catch { unknownRejected = true; }
      try { item.bridge.navigate('/not-declared'); } catch { scopeRejected = true; }
      const context = item.bridge.getContext();
      return { unknownRejected, scopeRejected, frozen: Object.isFrozen(context) && Object.isFrozen(context.tenant) && Object.isFrozen(context.membership?.permissions) };
    },
  };
  Object.defineProperty(window, '__qianAcceptance', { configurable: true, value: control });
}
