import { Button } from '@idadawn/yue-core';
import * as React from 'react';
import { PlatformClient, SDK_VERSION, type BridgeCaller, type JsonValue, type PublicBridge, type RemoteBridge } from '@idadawn/qian-plugin-sdk';
import { TenantPage } from './TenantPage';
import './remote.css';

export interface TenantRemoteProps { client: PlatformClient; bridge: RemoteBridge }
/** 通过真实 window 入口使用宿主在加载时交付的身份，不从字符串重建身份。 */
export function connectShell(caller: BridgeCaller) {
  const api = (window as Window & { __qian?: { bridge: PublicBridge } }).__qian?.bridge;
  if (!api) throw new Error('shell.bridge.unavailable');
  return Object.freeze({
    getContext: () => api.getContext(caller),
    navigate: (target: string) => api.navigate(caller, target),
    invoke: <T,>(id: string, input: JsonValue, options?: { idempotencyKey?: string; signal?: AbortSignal }) => api.invoke<T>(caller, id, input, options),
  });
}
export default function TenantRemote({ client, bridge }: TenantRemoteProps) {
  const context = connectShell(bridge.caller).getContext();
  return <section data-testid="qian-tenancy-remote" data-scope={context.tenant?.id ?? 'global'}><TenantPage client={client} embedded /></section>;
}
/** 消费者可验证 MF 共享引用；不导出令牌或宿主身份。 */
export const sharedIdentity = { Button, useState: React.useState, createElement: React.createElement, PlatformClient, sdkVersion: SDK_VERSION };
