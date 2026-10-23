import { createInstance } from '@module-federation/enhanced/runtime';
import * as React from 'react';
import { adaptFederation, type SharedDependencies } from '@idadawn/qian-plugin-sdk/federation';
import { SDK_VERSION } from '@idadawn/qian-plugin-sdk';

/** 宿主显式使用 MF2 runtime；延迟共享包工厂避免启动时加载完整 UI 命名空间。 */
export function createHostFederation() {
  const share = (version: string, load: () => Promise<unknown>) => ({ version, get: async () => { const module = await load(); return () => module; }, shareConfig: { singleton: true, requiredVersion: version, strictVersion: true } });
  const shared: SharedDependencies = {
    react: { version: React.version, lib: () => React, shareConfig: { singleton: true, requiredVersion: React.version, strictVersion: true } },
    'react-dom': share(React.version, () => import('react-dom')),
    'react-dom/client': share(React.version, () => import('react-dom/client')),
    'react/jsx-runtime': share(React.version, () => import('react/jsx-runtime')),
    'react/jsx-dev-runtime': share(React.version, () => import('react/jsx-dev-runtime')),
    '@idadawn/yue-core': share(__YUE_VERSION__, () => import('@idadawn/yue-core')),
    '@idadawn/yue-tokens': share(__YUE_VERSION__, () => import('@idadawn/yue-tokens')),
    '@idadawn/yue-contracts': share(__YUE_VERSION__, () => import('@idadawn/yue-contracts')),
    '@idadawn/yue-sdk': share(__YUE_VERSION__, () => import('@idadawn/yue-sdk')),
    '@idadawn/yue-form': share(__YUE_VERSION__, () => import('@idadawn/yue-form')),
    '@idadawn/yue-data': share(__YUE_VERSION__, () => import('@idadawn/yue-data')),
    '@idadawn/yue-shell': share(__YUE_VERSION__, () => import('@idadawn/yue-shell')),
    '@idadawn/yue-biz': share(__YUE_VERSION__, () => import('@idadawn/yue-biz')),
    '@idadawn/qian-plugin-sdk': share(SDK_VERSION, () => import('@idadawn/qian-plugin-sdk')),
  };
  const instance = createInstance({ name: 'qian_shell', shared, remotes: [] });
  return { transport: adaptFederation(instance), instance };
}
