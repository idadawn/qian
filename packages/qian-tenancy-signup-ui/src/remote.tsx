import { Button } from '@idadawn/yue-core';
import * as React from 'react';
import { PlatformClient, SDK_VERSION, type BridgeCaller, type PublicBridge, type RemoteBridge } from '@idadawn/qian-plugin-sdk';
import { ApplicationsPage } from './operator/ApplicationsPage';
import './remote.css';

export interface ApplicationsRemoteProps { client: PlatformClient; bridge: RemoteBridge }
/** 通过真实 window 入口使用宿主在加载时交付的上下文，不从字符串重建身份。 */
function shellContext(caller: BridgeCaller) {
  const api = (window as Window & { __qian?: { bridge: PublicBridge } }).__qian?.bridge;
  if (!api) throw new Error('shell.bridge.unavailable');
  return api.getContext(caller);
}
/** 开通申请页只在平台控制台的全局 scope 挂载；请求一律经宿主交付的 PlatformClient（网关会话），页面不持有令牌。 */
export default function ApplicationsRemote({ client, bridge }: ApplicationsRemoteProps) {
  const context = shellContext(bridge.caller);
  return <section data-testid="qian-tenancy-signup-remote" data-scope={context.tenant?.id ?? 'global'}><ApplicationsPage client={client} embedded locale={context.locale} /></section>;
}
/** 消费者可验证 MF 共享引用；不导出令牌或宿主身份。 */
export const sharedIdentity = { Button, useState: React.useState, createElement: React.createElement, PlatformClient, sdkVersion: SDK_VERSION };
