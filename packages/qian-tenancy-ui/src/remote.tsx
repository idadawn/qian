import { Button } from '@idadawn/yue-core';
import * as React from 'react';
import { PlatformClient, SDK_VERSION, type RemoteBridge } from '@idadawn/qian-plugin-sdk';
import { TenantPage } from './TenantPage';
import './remote.css';

export interface TenantRemoteProps { client: PlatformClient; bridge: RemoteBridge }
export default function TenantRemote({ client, bridge }: TenantRemoteProps) {
  const context = bridge.getContext();
  return <section data-testid="qian-tenancy-remote" data-scope={context.tenant?.id ?? 'global'}><TenantPage client={client} embedded /></section>;
}
/** 消费者可验证 MF 共享引用；不导出令牌或宿主身份。 */
export const sharedIdentity = { Button, useState: React.useState, createElement: React.createElement, PlatformClient, sdkVersion: SDK_VERSION };
